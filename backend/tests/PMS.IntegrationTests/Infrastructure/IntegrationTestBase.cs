using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Features.Auth;
using PMS.Application.Features.Projects;
using PMS.Application.Features.Sprints;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace PMS.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase
{
    protected readonly PmsWebApplicationFactory Factory;
    protected IntegrationTestBase(PmsWebApplicationFactory factory) => Factory = factory;

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string? email = null)
    {
        var client = Factory.CreateClient();
        email ??= $"user-{Guid.NewGuid():N}@pms.test";

        var res = await client.PostAsJsonAsync("/api/v1/Auth/register", new RegisterRequest(
            Name: "Test User", Email: email,
            Password: "Test@1234", ConfirmPassword: "Test@1234"));

        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    protected record TestUser(HttpClient Client, Guid EmployeeId, string Email);

    protected async Task<TestUser> CreateUserAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@pms.test";
        var client = await CreateAuthenticatedClientAsync(email);

        var employeeId = await WithDbAsync(db => db.Employees
            .AsNoTracking()
            .Where(e => e.Email == email)
            .Select(e => e.Id)
            .SingleAsync());

        return new TestUser(client, employeeId, email);
    }

    /// <summary>
    /// Tạo user rồi nâng lên SystemAdmin thẳng trong DB. Không có API tự phong quyền —
    /// đó là chủ ý chống privilege escalation, nên test buộc phải "mồi" ở tầng DB.
    /// </summary>
    protected async Task<TestUser> CreateSystemAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@pms.test";
        var client = Factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/Auth/register", new RegisterRequest(
            "Admin", email, "Test@1234", "Test@1234"));

        var id = await WithDbAsync(async db =>
        {
            var e = await db.Employees.SingleAsync(x => x.Email == email);
            e.ChangeSystemRole(SystemRole.SystemAdmin);
            await db.SaveChangesAsync();
            return e.Id;
        });

        // Đăng nhập LẠI để token mang claim SystemAdmin — token cũ vẫn mang role User.
        // Chính hiện tượng này là lý do phải thu hồi refresh token khi đổi SystemRole.
        var login = await client.PostAsJsonAsync("/api/v1/Auth/login",
            new LoginRequest(email, "Test@1234"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return new TestUser(client, id, email);
    }

    protected async Task<T> WithDbAsync<T>(Func<PmsDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<PmsDbContext>());
    }

    protected async Task WithDbAsync(Func<PmsDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<PmsDbContext>());
    }

    /// <summary>
    /// Chèn task trực tiếp vào DB. TaskItem.Status là private set + có state machine,
    /// nên phải đi đúng đường ToDo -> InProgress -> Review -> Done, không set tắt được.
    /// </summary>
    protected async Task<Guid> SeedTaskAsync(Guid projectId, Guid reporterId, Status status)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Name = "Task test",
            ProjectId = projectId, ReporterId = reporterId
        };

        Status[] path = status switch
        {
            Status.ToDo       => [],
            Status.InProgress => [Status.InProgress],
            Status.Review     => [Status.InProgress, Status.Review],
            Status.Done       => [Status.InProgress, Status.Review, Status.Done],
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        foreach (var step in path) task.ChangeStatus(step);

        await WithDbAsync(async db => { db.Tasks.Add(task); await db.SaveChangesAsync(); });
        return task.Id;
    }

    /// <summary>
    /// Mời + chấp nhận qua API thật. Dùng cho mọi trường hợp cần thành viên Accepted —
    /// đi đúng luồng nghiệp vụ nên test cũng gián tiếp bảo vệ luồng mời.
    /// </summary>
    protected static async Task InviteAndAcceptAsync(
        HttpClient pmClient, TestUser invitee, Guid projectId, RoleInProject role)
    {
        var invite = await pmClient.PostAsJsonAsync(
            $"/api/v1/Projects/{projectId}/members",
            new InviteMemberRequest(invitee.Email, role));
        invite.StatusCode.ShouldBe(HttpStatusCode.Created);

        var accept = await invitee.Client.PostAsync(
            $"/api/v1/Projects/{projectId}/members/me/accept", null);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Chèn thành viên thẳng vào DB nhưng ĐI QUA domain method để invariant vẫn được tôn trọng.
    /// Chỉ dùng cho trạng thái mà API không dựng trực tiếp được (Declined), hoặc khi test
    /// cần cô lập hẳn khỏi luồng mời. Trường hợp thông thường dùng InviteAndAcceptAsync.
    /// </summary>
    protected Task SeedMemberAsync(
        Guid projectId, Guid employeeId, RoleInProject role, InvitationStatus status)
        => WithDbAsync(async db =>
        {
            var project = await db.Projects
                .Include(p => p.Members)
                .SingleAsync(p => p.Id == projectId);
            var employee = await db.Employees.SingleAsync(e => e.Id == employeeId);

            var member = project.Invite(employee, role);
            if (status == InvitationStatus.Accepted) member.Accept();
            if (status == InvitationStatus.Declined) member.Decline();

            await db.SaveChangesAsync();
        });

    /// <summary>Tạo project qua API (đi đúng luồng thật) và trả về Id.</summar
    protected static async Task<Guid> CreateProjectAsync(HttpClient client, string name = "PMS")
    {
        var res = await client.PostAsJsonAsync("/api/v1/Projects",
            new CreateProjectRequest(name, "Mô tả", DateTime.UtcNow.AddDays(30)));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<ProjectSummaryResponse>(TestJson.Options);
        return body!.Id;
    }

    /// <summary>
    /// Tạo task qua API thật. Khác SeedTaskAsync (chèn thẳng DB): đi qua đúng luồng
    /// nghiệp vụ nên cũng gián tiếp bảo vệ endpoint tạo task.
    /// </summary>
    protected static async Task<Guid> CreateTaskAsync(
        HttpClient client, Guid projectId, string name = "Task",
        Guid? sprintId = null, Guid? parentTaskId = null,
        Priority priority = Priority.Medium, DateTime? dueDate = null)
    {
        var res = await client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest(name, projectId, sprintId, parentTaskId, dueDate, priority));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);
        return body!.Id;
    }

    protected static async Task<Guid> CreateSprintAsync(
        HttpClient client, Guid projectId, string name = "Sprint 1",
        int startOffset = 0, int endOffset = 14)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/sprints",
            new CreateSprintRequest(name, "Mục tiêu sprint",
                DateTime.UtcNow.AddDays(startOffset), DateTime.UtcNow.AddDays(endOffset)));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<SprintResponse>(TestJson.Options);
        return body!.Id;
    }

    /// <summary>Đưa task tới đúng trạng thái mong muốn qua API, đi từng bước của state machine.</summary>
    protected static async Task AdvanceStatusAsync(HttpClient client, Guid taskId, Status target)
    {
        Status[] path = target switch
        {
            Status.ToDo       => [],
            Status.InProgress => [Status.InProgress],
            Status.Review     => [Status.InProgress, Status.Review],
            Status.Done       => [Status.InProgress, Status.Review, Status.Done],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };

        foreach (var step in path)
        {
            var res = await client.PatchAsJsonAsync(
                $"/api/v1/tasks/{taskId}/status", new ChangeTaskStatusRequest(step));
            res.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    protected Task<int> CountActivityLogsAsync(Guid entityId)
        => WithDbAsync(db => db.ActivityLogs.CountAsync(l => l.EntityId == entityId));

    protected Task<int> CountNotificationsAsync(Guid recipientId)
        => WithDbAsync(db => db.Notifications.CountAsync(n => n.EmployeeId == recipientId));
}

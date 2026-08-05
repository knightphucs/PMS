using Microsoft.EntityFrameworkCore;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence;

public static class DbSeeder
{
    private const string DefaultPassword = "Password123!";

    public static async Task SeedAsync(PmsDbContext context, CancellationToken ct = default)
    {
        if (await context.Employees.AnyAsync(ct)) return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);

        // ---------- Employees ----------
        var admin = NewEmployee("System Admin", "admin@pms.local", passwordHash, SystemRole.SystemAdmin);
        var an    = NewEmployee("Nguyen Van An",  "an@pms.local",   passwordHash);
        var binh  = NewEmployee("Tran Thi Binh",  "binh@pms.local", passwordHash);
        var cuong = NewEmployee("Le Van Cuong",   "cuong@pms.local", passwordHash);
        var dung  = NewEmployee("Pham Thi Dung",  "dung@pms.local", passwordHash);
        var em    = NewEmployee("Hoang Van Em",   "em@pms.local",   passwordHash);

        var employees = new[] { admin, an, binh, cuong, dung, em };
        await context.Employees.AddRangeAsync(employees, ct);

        // ---------- Labels ----------
        var lblBug      = new Label { Id = Guid.NewGuid(), Name = "bug",      Color = "#DC2626" };
        var lblFeature  = new Label { Id = Guid.NewGuid(), Name = "feature",  Color = "#16A34A" };
        var lblUrgent   = new Label { Id = Guid.NewGuid(), Name = "urgent",   Color = "#EA580C" };
        var lblBackend  = new Label { Id = Guid.NewGuid(), Name = "backend",  Color = "#2563EB" };
        var lblFrontend = new Label { Id = Guid.NewGuid(), Name = "frontend", Color = "#9333EA" };
        await context.Labels.AddRangeAsync(
            new[] { lblBug, lblFeature, lblUrgent, lblBackend, lblFrontend }, ct);

        // ---------- Project 1: đầy đủ Sprint + Task nhiều trạng thái ----------
        var project1 = NewProject("Hệ thống quản lý dự án", key: "PMS",
            "Xây dựng PMS nội bộ tương tự Jira thu nhỏ", daysFromNow: 90);

        AddMember(project1, an,    RoleInProject.ProjectManager, accepted: true);
        AddMember(project1, binh,  RoleInProject.Member,         accepted: true);
        AddMember(project1, cuong, RoleInProject.Member,         accepted: true);
        AddMember(project1, dung,  RoleInProject.Viewer,         accepted: true);
        AddMember(project1, em,    RoleInProject.Member,         accepted: false);  // demo lời mời Pending

        var sprint1 = NewSprint(project1, "Sprint 1 - Nền tảng",
            "Hoàn thiện Domain và Infrastructure", startOffset: -14, endOffset: 0);
        var sprint2 = NewSprint(project1, "Sprint 2 - API",
            "Xây dựng REST API cho Project/Task", startOffset: 1, endOffset: 15);

        // Task đã Done — đi qua đúng state machine, không gán thẳng Status
        var t1 = NewTask(project1, sprint1, an, "Thiết kế Class Diagram và ERD",
            Priority.High, dueOffset: -10);
        Advance(t1, project1, Status.Done);
        t1.Labels.Add(lblBackend);

        var t2 = NewTask(project1, sprint1, an, "Cài đặt tầng Domain",
            Priority.Highest, dueOffset: -5);
        Advance(t2, project1, Status.Done);
        t2.Labels.Add(lblBackend);

        var t3 = NewTask(project1, sprint1, binh, "Cấu hình EF Core và migration",
            Priority.High, dueOffset: 2);
        Advance(t3, project1, Status.Review);
        t3.Labels.Add(lblBackend);

        // Task QUÁ HẠN — để demo Notification/IsOverdue
        var t4 = NewTask(project1, sprint1, an, "Viết tài liệu API",
            Priority.Medium, dueOffset: -3);
        Advance(t4, project1, Status.InProgress);
        t4.Labels.Add(lblUrgent);

        var t5 = NewTask(project1, sprint2, binh, "Xây dựng ProjectController",
            Priority.High, dueOffset: 10);
        t5.Labels.Add(lblFeature);

        // Task có Subtask — demo progress bar
        var t6 = NewTask(project1, sprint2, an, "Xây dựng TaskController",
            Priority.Highest, dueOffset: 12);
        var sub1 = NewTask(project1, null, an, "Endpoint tạo Task", Priority.High, dueOffset: 8);
        var sub2 = NewTask(project1, null, an, "Endpoint gán nhân sự", Priority.High, dueOffset: 9);
        var sub3 = NewTask(project1, null, an, "Endpoint đổi trạng thái", Priority.Medium, dueOffset: 11);
        t6.AddSubtask(sub1);
        t6.AddSubtask(sub2);
        t6.AddSubtask(sub3);
        Advance(sub1, project1, Status.Done);      // 1/3 = 33.33%

        // Task ở Backlog (SprintId = null)
        var t7 = NewTask(project1, null, an, "Tích hợp SignalR real-time",
            Priority.Low, dueOffset: 45);
        var t8 = NewTask(project1, null, an, "Sửa lỗi hiển thị ngày tháng",
            Priority.Medium, dueOffset: 30);
        t8.Labels.Add(lblBug);

        // TaskLink: t5 bị chặn bởi t3 (t3 --Blocks--> t5)
        t3.LinkTo(t5, LinkType.Blocks);

        // Gán người
        t1.AddAssignee(an, RoleInTask.Owner);
        t2.AddAssignee(binh, RoleInTask.Owner);
        t2.AddAssignee(cuong, RoleInTask.Contributor);   // demo nhiều người / 1 task
        t3.AddAssignee(binh, RoleInTask.Owner);
        t4.AddAssignee(cuong, RoleInTask.Owner);
        t5.AddAssignee(binh, RoleInTask.Owner);
        t6.AddAssignee(cuong, RoleInTask.Owner);
        sub1.AddAssignee(cuong, RoleInTask.Owner);
        sub2.AddAssignee(binh, RoleInTask.Owner);

        // Watcher: theo dõi mà không được gán
        t4.Watchers.Add(new Watcher { TaskId = t4.Id, EmployeeId = an.Id,   CreatedAt = DateTime.UtcNow });
        t5.Watchers.Add(new Watcher { TaskId = t5.Id, EmployeeId = dung.Id, CreatedAt = DateTime.UtcNow });

        // Comment
        t3.Comments.Add(NewComment(t3, binh, "Đã xong migration đầu tiên, nhờ anh review giúp."));
        t3.Comments.Add(NewComment(t3, an,   "OK, để mình kiểm tra lại phần cascade delete."));
        t4.Comments.Add(NewComment(t4, cuong, "Task này bị trễ do chờ chốt format response."));

        // ---------- Project 2 ----------
        var project2 = NewProject("Website giới thiệu công ty", key: "WEB",
            "Landing page và trang tin tức", daysFromNow: 45);
        AddMember(project2, binh, RoleInProject.ProjectManager, accepted: true);
        AddMember(project2, an,   RoleInProject.Member,         accepted: true);

        var sprint3 = NewSprint(project2, "Sprint 1 - Giao diện",
            "Dựng layout chính", startOffset: -7, endOffset: 7);

        var t9 = NewTask(project2, sprint3, binh, "Thiết kế trang chủ", Priority.High, dueOffset: 3);
        Advance(t9, project2, Status.InProgress);
        t9.Labels.Add(lblFrontend);

        var t10 = NewTask(project2, sprint3, binh, "Tối ưu SEO", Priority.Low, dueOffset: 6);
        t9.AddAssignee(an, RoleInTask.Owner);

        // ---------- Project 3: đã hoàn thành ----------
        var project3 = NewProject("Nâng cấp hạ tầng máy chủ", key: "HT",
            "Chuyển sang môi trường container", daysFromNow: -10);
        project3.Complete();
        AddMember(project3, cuong, RoleInProject.ProjectManager, accepted: true);
        AddMember(project3, an,    RoleInProject.Member,         accepted: true);

        var t11 = NewTask(project3, null, cuong, "Dựng Docker Compose", Priority.High, dueOffset: -15);
        Advance(t11, project3, Status.Done);

        await context.Projects.AddRangeAsync(new[] { project1, project2, project3 }, ct);
        await context.Sprints.AddRangeAsync(new[] { sprint1, sprint2, sprint3 }, ct);
        await context.Tasks.AddRangeAsync(
            new[] { t1, t2, t3, t4, t5, t6, sub1, sub2, sub3, t7, t8, t9, t10, t11 }, ct);

        // Bộ đếm phải khớp số task cao nhất đã seed, nếu không task tạo đầu tiên sau khi
        // seed sẽ đụng unique index (ProjectId, Number) — ADR-033.
        await context.ProjectTaskCounters.AddRangeAsync(
            new[] { project1, project2, project3 }.Select(p => new ProjectTaskCounter
            {
                ProjectId = p.Id,
                NextNumber = SeedTaskNumbers.GetValueOrDefault(p.Id)
            }), ct);

        // ---------- ActivityLog + Notification ----------
        await context.ActivityLogs.AddRangeAsync(new[]
        {
            NewLog("Project", project1.Id, an.Id, ActivityAction.Created,       "Tạo project 'Hệ thống quản lý dự án'"),
            NewLog("Task",    t1.Id,       an.Id, ActivityAction.StatusChanged, "Chuyển trạng thái: Review -> Done"),
            NewLog("Task",    t2.Id,       an.Id, ActivityAction.Assigned,      "Gán Tran Thi Binh làm Owner"),
            NewLog("Task",    t4.Id,    cuong.Id, ActivityAction.StatusChanged, "Chuyển trạng thái: ToDo -> InProgress"),
        }, ct);

        await context.Notifications.AddRangeAsync(new[]
        {
            NewNotification(binh.Id, NotificationType.TaskAssigned,
                "Bạn được gán vào task 'Cài đặt tầng Domain'", t2.Id, isRead: true),
            NewNotification(cuong.Id, NotificationType.DueSoon,
                "Task 'Viết tài liệu API' đã quá hạn", t4.Id),
            NewNotification(an.Id, NotificationType.CommentAdded,
                "Tran Thi Binh đã bình luận trong task bạn theo dõi", t3.Id),
            NewNotification(em.Id, NotificationType.InvitedToProject,
                "Bạn được mời tham gia project 'Hệ thống quản lý dự án'", project1.Id),
        }, ct);

        await context.SaveChangesAsync(ct);
    }

    // ==================== Helpers ====================

    private static Employee NewEmployee(
        string name, string email, string hash, SystemRole role = SystemRole.User)
    {
        var employee = Employee.Register(name, email, hash);
        employee.ChangeSystemRole(role);
        return employee;
    }

    private static Project NewProject(string name, string key, string description, int daysFromNow)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(), Name = name, Key = key, Description = description,
            ExpectedCompletionDate = DateTime.UtcNow.AddDays(daysFromNow)
        };

        // ⚠️ Hàm này dựng Project bằng object initializer chứ KHÔNG qua `Project.Create`
        // (nó cần đặt sẵn Id và Key cho dữ liệu mẫu), nên phải tự cấp bốn cột mặc định —
        // `Create` làm việc đó hộ, đường này thì không. Thiếu bước này thì mọi task seed
        // không có cột nào để đứng và `Advance` ném ngay ở project đầu tiên.
        foreach (var column in BoardColumn.CreateDefaults(project.Id))
            project.BoardColumns.Add(column);

        return project;
    }

    private static void AddMember(Project project, Employee employee, RoleInProject role, bool accepted)
    {
        var member = project.Invite(employee, role);
        if (accepted) member.Accept();
    }

    private static Sprint NewSprint(
        Project project, string name, string goal, int startOffset, int endOffset)
        => new()
        {
            Id = Guid.NewGuid(), Name = name, Goal = goal,
            ProjectId = project.Id,
            StartDate = DateTime.UtcNow.AddDays(startOffset),
            EndDate = DateTime.UtcNow.AddDays(endOffset)
        };

    /// <summary>
    /// Bộ đếm số task theo project, chỉ dùng trong lúc seed — soi gương đúng thứ
    /// <c>ProjectTaskCounters</c> làm ở runtime, để dữ liệu seed cũng có mã task hợp lệ
    /// và không trùng trong phạm vi một project (ADR-033).
    /// </summary>
    private static readonly Dictionary<Guid, int> SeedTaskNumbers = [];

    private static TaskItem NewTask(
        Project project, Sprint? sprint, Employee reporter,
        string name, Priority priority, int dueOffset)
    {
        SeedTaskNumbers.TryGetValue(project.Id, out var last);
        SeedTaskNumbers[project.Id] = last + 1;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(), Name = name, Priority = priority,
            ProjectId = project.Id, SprintId = sprint?.Id, ReporterId = reporter.Id,
            DueDate = DateTime.UtcNow.AddDays(dueOffset)
        };
        task.AssignNumber(last + 1);
        return task;
    }

    /// <summary>
    /// Đưa task tới cột tương ứng với một trạng thái mặc định (ADR-052).
    ///
    /// <para>
    /// Trước đây hàm này đi từng bước qua state machine <c>ToDo→InProgress→Review→Done</c>
    /// vì setter của <c>Status</c> là private. Nay không còn state machine — cột do người
    /// dùng định nghĩa thì không có "bước kề" nào để đi — nên nó chỉ việc tìm đúng cột trong
    /// bốn cột mặc định của project rồi gọi <c>MoveTo</c>.
    /// </para>
    /// <para>
    /// ⚠️ Vẫn đi qua <c>MoveTo</c> chứ không gán thẳng hai trường: đó là người ghi DUY NHẤT
    /// giữ <c>BoardColumnId</c> và <c>Category</c> khớp nhau. Seed data lệch bản sao đó sẽ
    /// tạo ra một database dev mà mọi phép kiểm "xong chưa" trả lời sai — và vì nó là dữ
    /// liệu mẫu nên sai đó sẽ theo vào mọi ảnh chụp màn hình lẫn mọi lần thử tay.
    /// </para>
    /// </summary>
    private static void Advance(TaskItem task, Project project, Status target)
    {
        // Bốn cột mặc định của `Project.Create` được cấp đúng theo thứ tự enum cũ, nên chỉ
        // số của enum chính là `Order` của cột. Chỉ đúng với project vừa tạo qua
        // `Project.Create` — seeder thì luôn như vậy.
        task.MoveTo(project.BoardColumns.Single(c => c.Order == (int)target));
    }

    private static Comment NewComment(TaskItem task, Employee author, string content)
        => new()
        {
            Id = Guid.NewGuid(), TaskId = task.Id, EmployeeId = author.Id,
            Content = content, CreatedAt = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 72))
        };

    private static ActivityLog NewLog(
        string entityType, Guid entityId, Guid employeeId, ActivityAction action, string detail)
        => new()
        {
            Id = Guid.NewGuid(), EntityType = entityType, EntityId = entityId,
            EmployeeId = employeeId, Action = action, Detail = detail,
        };

    private static Notification NewNotification(
        Guid employeeId, NotificationType type, string content,
        Guid relatedEntityId, bool isRead = false)
        => new()
        {
            Id = Guid.NewGuid(), EmployeeId = employeeId, Type = type,
            Content = content, RelatedEntityId = relatedEntityId, IsRead = isRead,
            CreatedAt = DateTime.UtcNow.AddHours(-Random.Shared.Next(1, 48))
        };
}

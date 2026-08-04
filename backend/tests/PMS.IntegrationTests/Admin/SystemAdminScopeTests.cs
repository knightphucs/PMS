using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Admin;

/// <summary>
/// 🔴 <b>Hợp đồng kiến trúc của ADR-042:</b> <c>SystemAdmin</c> là vai trò QUẢN TRỊ HỆ THỐNG
/// thuần túy và <b>không có bất kỳ đặc quyền nghiệp vụ nào</b> — không đọc, không ghi, không
/// "read-only để hỗ trợ".
/// <para>
/// Trước 2026-08-03 §10 mô tả một ngoại lệ "SystemAdmin xem được read-only toàn bộ project"
/// mà <b>chưa từng có dòng code nào hiện thực</b>. Tài liệu đã được sửa cho khớp code thay
/// vì ngược lại; file này là thứ giữ cho nó không trôi lại.
/// </para>
/// <para>
/// Viết dạng <c>[Theory]</c> trên danh sách route để việc thêm một endpoint project-scoped
/// mà quên bổ sung vào đây là một thiếu sót <b>nhìn thấy được</b> khi review, chứ không phải
/// một khoảng trống im lặng.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SystemAdminScopeTests : IntegrationTestBase
{
    public SystemAdminScopeTests(PmsWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<string> ProjectScopedRoutes => new()
    {
        "/api/v1/projects/{projectId}",
        "/api/v1/projects/{projectId}/tasks",
        "/api/v1/projects/{projectId}/board",
        "/api/v1/projects/{projectId}/backlog",
        "/api/v1/projects/{projectId}/members",
        "/api/v1/projects/{projectId}/sprints",
        "/api/v1/projects/{projectId}/statistics",
        "/api/v1/projects/{projectId}/activity",
        "/api/v1/projects/{projectId}/attachments",
        "/api/v1/tasks/{taskId}",
        "/api/v1/tasks/{taskId}/comments",
        "/api/v1/tasks/{taskId}/activity",
        "/api/v1/tasks/{taskId}/attachments",
        "/api/v1/tasks/{taskId}/watchers",
        "/api/v1/tasks/{taskId}/links",
        "/api/v1/tasks/{taskId}/assignees"
    };

    [Theory]
    [MemberData(nameof(ProjectScopedRoutes))]
    public async Task SystemAdmin_khong_phai_thanh_vien_nhan_404_tren_moi_route_project_scoped(
        string routeTemplate)
    {
        var pm = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();

        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var url = routeTemplate
            .Replace("{projectId}", projectId.ToString())
            .Replace("{taskId}", taskId.ToString());

        // 404 chứ không 403: 403 xác nhận cho người ngoài rằng id đó có thật (ADR-006/019).
        (await admin.Client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SystemAdmin_khong_ghi_duoc_vao_project_minh_khong_tham_gia()
    {
        var pm = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await admin.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Task do admin tạo", projectId, null, null, null, Priority.Medium));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Positive_control_SystemAdmin_LA_thanh_vien_thi_doc_duoc_binh_thuong()
    {
        // Không có test này thì một đường authz hỏng toàn cục (mọi thứ đều 404) vẫn làm
        // theory phía trên xanh — tức là nó không bảo vệ được gì.
        var pm = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await InviteAndAcceptAsync(pm.Client, admin, projectId, RoleInProject.Member);

        (await admin.Client.GetAsync($"/api/v1/projects/{projectId}")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    // ==================== Audit log cấp hệ thống (ADR-042) ====================

    [Fact]
    public async Task Audit_log_he_thong_chi_SystemAdmin_doc_duoc()
    {
        var user = await CreateUserAsync();
        var admin = await CreateSystemAdminAsync();

        (await user.Client.GetAsync("/api/v1/admin/audit-logs")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await admin.Client.GetAsync("/api/v1/admin/audit-logs")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Audit_log_he_thong_KHONG_lo_hoat_dong_cua_project_hay_task()
    {
        // 🔴 Đây là ranh giới quan trọng nhất của ADR-042. Nếu endpoint này nhận
        // `entityType` làm query param — hoặc ai đó thêm Project/TaskItem vào danh sách cố
        // định trong ActivityLogService — thì nó trở thành đúng cái "god mode" mà quyết định
        // tách vai trò vừa đóng lại.
        var admin = await CreateSystemAdminAsync();

        // Chính admin tạo project và task -> chắc chắn có ActivityLog cho hai entity đó
        var projectId = await CreateProjectAsync(admin.Client);
        var taskId = await CreateTaskAsync(admin.Client, projectId);

        var log = await admin.Client.GetFromJsonAsync<PagedResult<SystemAuditLogResponse>>(
            "/api/v1/admin/audit-logs?pageSize=100", TestJson.Options);

        log!.Items.ShouldNotContain(x => x.EntityId == projectId);
        log.Items.ShouldNotContain(x => x.EntityId == taskId);
        log.Items.ShouldAllBe(x => x.EntityType == "Employee" || x.EntityType == "Label");
    }

    [Fact]
    public async Task Audit_log_he_thong_CO_ghi_lai_viec_khoa_tai_khoan()
    {
        // Đối trọng của test trên: bỏ hết đặc quyền nghiệp vụ thì SystemAdmin vẫn phải có
        // trách nhiệm giải trình cho những việc họ THẬT SỰ làm.
        var admin = await CreateSystemAdminAsync();
        var victim = await CreateUserAsync();

        var lockRes = await admin.Client.PostAsJsonAsync(
            $"/api/v1/admin/employees/{victim.EmployeeId}/lock",
            new { Reason = "Kiểm thử audit log" });
        lockRes.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var log = await admin.Client.GetFromJsonAsync<PagedResult<SystemAuditLogResponse>>(
            "/api/v1/admin/audit-logs?pageSize=100", TestJson.Options);

        log!.Items.ShouldContain(x =>
            x.EntityId == victim.EmployeeId && x.Action == ActivityAction.AccountLocked);
    }
}

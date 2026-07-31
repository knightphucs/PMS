using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Notifications;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Notifications;

[Collection(IntegrationTestCollection.Name)]
public class NotificationsTests : IntegrationTestBase
{
    public NotificationsTests(PmsWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// Lý do tồn tại của cả module này: trước phiên 2026-07-30 (tiếp), Notification được sinh
    /// ra ở MỌI luồng Project/Task nhưng không có đường nào đọc — dữ liệu chỉ ghi vào rồi nằm
    /// đó. Test này đi hết vòng: sinh ra ở luồng nghiệp vụ thật rồi đọc lại qua API.
    /// </summary>
    [Fact]
    public async Task Thong_bao_sinh_ra_o_luong_nghiep_vu_doc_lai_duoc_qua_API()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Dựng API");

        // Member tự nhận task -> TaskAssignmentService báo cho PM
        (await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var inbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);

        // PM nhận hai thông báo từ hai luồng khác nhau — chính đây là điều trước phiên này
        // không ai kiểm chứng được: cả hai đều đã được ghi từ lâu mà không có đường nào đọc.
        inbox!.Items.Count.ShouldBe(2);

        var taskNoti = inbox.Items.Single(n => n.Type == NotificationType.TaskAssigned);
        taskNoti.IsRead.ShouldBeFalse();
        taskNoti.RelatedEntityId.ShouldBe(taskId);
        taskNoti.RelatedEntityKind.ShouldBe(RelatedEntityKind.Task);      // ADR-025

        var inviteNoti = inbox.Items.Single(n => n.Type == NotificationType.InvitationAccepted);
        inviteNoti.RelatedEntityId.ShouldBe(projectId);
        inviteNoti.RelatedEntityKind.ShouldBe(RelatedEntityKind.Project); // ADR-025
    }

    // ---------- ADR-023: chỉ đọc được thông báo của chính mình ----------

    [Fact]
    public async Task Khong_thay_thong_bao_cua_nguoi_khac_trong_danh_sach_cua_minh()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        // Hai người đều có thông báo, nhưng KHÁC nhau: PM nhận "đã chấp nhận lời mời",
        // member nhận "bạn được mời". Hộp của mỗi người phải rời nhau hoàn toàn.
        var pmInbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);
        var memberInbox = await member.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);

        pmInbox!.Items.ShouldNotBeEmpty();
        memberInbox!.Items.ShouldNotBeEmpty();

        var pmIds = pmInbox.Items.Select(n => n.Id).ToHashSet();
        memberInbox.Items.ShouldAllBe(n => !pmIds.Contains(n.Id));

        pmInbox.Items.ShouldAllBe(n => n.Type == NotificationType.InvitationAccepted);
        memberInbox.Items.ShouldAllBe(n => n.Type == NotificationType.InvitedToProject);

        // Và tổng số của mỗi người khớp đúng số dòng của chính họ trong DB — không rò rỉ,
        // không thiếu.
        pmInbox.TotalCount.ShouldBe(await CountNotificationsAsync(pm.EmployeeId));
        memberInbox.TotalCount.ShouldBe(await CountNotificationsAsync(member.EmployeeId));
    }

    [Fact]
    public async Task Danh_dau_thong_bao_cua_nguoi_khac_nhan_404_chu_khong_phai_403()
    {
        // Cùng lý do ADR-006 chọn 404 thay 403: 403 xác nhận cho người ngoài rằng id đó tồn
        // tại thật, đủ để dò sự tồn tại của bản ghi (OWASP API1:2023).
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        var pmInbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);
        var notiCuaPm = pmInbox!.Items.First().Id;

        var res = await member.Client.PatchAsync($"/api/v1/notifications/{notiCuaPm}/read", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // và thông báo của PM vẫn chưa đọc
        var stillUnread = await WithDbAsync(db => db.Notifications
            .AsNoTracking().Where(n => n.Id == notiCuaPm).Select(n => n.IsRead).SingleAsync());
        stillUnread.ShouldBeFalse();
    }

    [Fact]
    public async Task Thong_bao_khong_ton_tai_cung_nhan_404()
        => (await (await CreateUserAsync()).Client
                .PatchAsync($"/api/v1/notifications/{Guid.NewGuid()}/read", null))
           .StatusCode.ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public async Task Danh_dau_da_doc_hai_lan_van_200_khong_phai_409()
    {
        var (pm, notiId) = await SeedInboxAsync();

        var first = await pm.Client.PatchAsync($"/api/v1/notifications/{notiId}/read", null);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<NotificationResponse>(TestJson.Options))!
            .IsRead.ShouldBeTrue();

        var second = await pm.Client.PatchAsync($"/api/v1/notifications/{notiId}/read", null);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<NotificationResponse>(TestJson.Options))!
            .IsRead.ShouldBeTrue();
    }

    // ---------- đếm chưa đọc + lọc ----------

    [Fact]
    public async Task Dem_chua_doc_giam_sau_khi_danh_dau_va_ve_0_sau_khi_danh_dau_tat_ca()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        var before = await GetUnreadCountAsync(pm);
        before.ShouldBeGreaterThanOrEqualTo(2);   // accept lời mời + tự nhận task

        var inbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);
        await pm.Client.PatchAsync($"/api/v1/notifications/{inbox!.Items.First().Id}/read", null);

        (await GetUnreadCountAsync(pm)).ShouldBe(before - 1);

        var markAll = await pm.Client.PatchAsync("/api/v1/notifications/read-all", null);
        markAll.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await markAll.Content.ReadFromJsonAsync<MarkAllReadResponse>(TestJson.Options))!
            .MarkedCount.ShouldBe(before - 1);

        (await GetUnreadCountAsync(pm)).ShouldBe(0);
    }

    [Fact]
    public async Task Loc_isRead_false_chi_tra_ve_thong_bao_chua_doc()
    {
        var (pm, notiId) = await SeedInboxAsync();
        await pm.Client.PatchAsync($"/api/v1/notifications/{notiId}/read", null);

        var unread = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications?isRead=false", TestJson.Options);
        var read = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications?isRead=true", TestJson.Options);

        unread!.Items.ShouldNotContain(n => n.Id == notiId);
        read!.Items.ShouldContain(n => n.Id == notiId);
    }

    [Fact]
    public async Task Danh_sach_tra_ve_moi_nhat_truoc()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        var inbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);

        inbox!.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        inbox.Items.ShouldBeInOrder(SortDirection.Descending, new CreatedAtComparer());
    }

    // ---------- ADR-024 ----------

    [Fact]
    public async Task MarkAllAsRead_dong_dau_UpdatedAt_vi_khong_dung_ExecuteUpdate()
    {
        // Bulk update (ExecuteUpdateAsync) đi thẳng xuống SQL, bỏ qua ApplyAuditFields() nên
        // UpdatedAt sẽ là null trong khi IsRead vẫn đúng — lỗi âm thầm mà chỉ test ở tầng DB
        // bắt được. Cùng lý do ADR-008 chọn Option A cho soft delete.
        var (pm, notiId) = await SeedInboxAsync();

        (await pm.Client.PatchAsync("/api/v1/notifications/read-all", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = await WithDbAsync(db => db.Notifications
            .AsNoTracking()
            .Where(n => n.Id == notiId)
            .Select(n => new { n.IsRead, n.UpdatedAt })
            .SingleAsync());

        row.IsRead.ShouldBeTrue();
        row.UpdatedAt.ShouldNotBeNull();
    }

    // ---------- helpers ----------

    private async Task<(TestUser Pm, Guid NotificationId)> SeedInboxAsync()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        var inbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications", TestJson.Options);

        return (pm, inbox!.Items.First().Id);
    }

    private static async Task<int> GetUnreadCountAsync(TestUser user)
        => (await user.Client.GetFromJsonAsync<UnreadCountResponse>(
                "/api/v1/notifications/unread-count", TestJson.Options))!.UnreadCount;

    private class CreatedAtComparer : IComparer<NotificationResponse>
    {
        public int Compare(NotificationResponse? x, NotificationResponse? y)
            => x!.CreatedAt.CompareTo(y!.CreatedAt);
    }
}

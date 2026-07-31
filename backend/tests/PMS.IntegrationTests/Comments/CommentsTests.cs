using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Comments;
using PMS.Application.Features.Notifications;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Comments;

[Collection(IntegrationTestCollection.Name)]
public class CommentsTests : IntegrationTestBase
{
    public CommentsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Tao_doc_sua_xoa_comment_theo_dung_luong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Dựng API");

        var created = await CreateCommentAsync(pm.Client, taskId, "Bình luận đầu tiên");
        created.AuthorId.ShouldBe(pm.EmployeeId);
        created.TaskId.ShouldBe(taskId);
        created.Content.ShouldBe("Bình luận đầu tiên");
        created.UpdatedAt.ShouldBeNull();   // chưa sửa

        var list = await GetCommentsAsync(pm.Client, taskId);
        list.Items.ShouldHaveSingleItem().Id.ShouldBe(created.Id);

        var updated = await pm.Client.PutAsJsonAsync($"/api/v1/comments/{created.Id}",
            new UpdateCommentRequest("Bình luận đã sửa"));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await updated.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options);
        body!.Content.ShouldBe("Bình luận đã sửa");
        body.UpdatedAt.ShouldNotBeNull();   // ApplyAuditFields đóng dấu (ADR-014)

        (await pm.Client.DeleteAsync($"/api/v1/comments/{created.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await GetCommentsAsync(pm.Client, taskId)).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Comment_bi_xoa_cung_khong_con_dong_nao_trong_DB()
    {
        // ADR-026: xóa CỨNG, nhất quán ADR-012 (gỡ member cũng xóa cứng) — khác Project/Task.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(pm.Client, taskId, "Sẽ bị xóa");

        await pm.Client.DeleteAsync($"/api/v1/comments/{comment.Id}");

        var stillInDb = await WithDbAsync(db =>
            db.Comments.IgnoreQueryFilters().AnyAsync(c => c.Id == comment.Id));
        stillInDb.ShouldBeFalse();
    }

    // ---------- §10 + ADR-019: ma trận quyền ----------

    [Fact]
    public async Task Member_viet_duoc_comment()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await member.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new CreateCommentRequest("Member cũng thảo luận được"));

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Viewer_khong_viet_duoc_comment_nhung_van_doc_duoc()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await CreateCommentAsync(pm.Client, taskId, "PM nói gì đó");

        var write = await viewer.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new CreateCommentRequest("Viewer thử viết"));
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // nhưng đọc thì được: Viewer là stakeholder theo dõi tiến độ (§10)
        (await GetCommentsAsync(viewer.Client, taskId)).Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_khi_doc_comment_cua_task()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await outsider.Client.GetAsync($"/api/v1/tasks/{taskId}/comments")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_khi_xoa_comment()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(pm.Client, taskId, "Bình luận nội bộ");

        var res = await outsider.Client.DeleteAsync($"/api/v1/comments/{comment.Id}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await res.Content.ReadAsStringAsync();
        problem.ShouldNotContain(projectId.ToString());   // không lộ project (ADR-019)
    }

    // ---------- ADR-026: sửa chỉ tác giả, xóa tác giả hoặc PM ----------

    [Fact]
    public async Task Thanh_vien_khac_khong_sua_duoc_comment_cua_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(pm.Client, taskId, "Lời của PM");

        var res = await member.Client.PutAsJsonAsync($"/api/v1/comments/{comment.Id}",
            new UpdateCommentRequest("Member sửa lén"));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProjectManager_khong_sua_duoc_nhung_xoa_duoc_comment_cua_Member()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(member.Client, taskId, "Lời của Member");

        var sua = await pm.Client.PutAsJsonAsync($"/api/v1/comments/{comment.Id}",
            new UpdateCommentRequest("PM viết lại lời Member"));
        sua.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var xoa = await pm.Client.DeleteAsync($"/api/v1/comments/{comment.Id}");
        xoa.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Member_khong_xoa_duoc_comment_cua_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(pm.Client, taskId, "Lời của PM");

        (await member.Client.DeleteAsync($"/api/v1/comments/{comment.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- bảo đảm bằng cấu trúc: query filter theo Task.IsDeleted ----------

    [Fact]
    public async Task Comment_cua_task_da_xoa_mem_tu_bien_mat_khoi_moi_query()
    {
        // CommentConfiguration khai HasQueryFilter(c => !c.Task.IsDeleted) nên không service
        // nào phải nhớ lọc — nếu ai đó bỏ query filter đó đi, test này đỏ (ADR-026).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var comment = await CreateCommentAsync(pm.Client, taskId, "Bình luận trên task sắp bị xóa");

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // dòng vẫn còn trong DB (task chỉ xóa mềm, comment không bị xóa theo)...
        var conTrongDb = await WithDbAsync(db =>
            db.Comments.IgnoreQueryFilters().AnyAsync(c => c.Id == comment.Id));
        conTrongDb.ShouldBeTrue();

        // ...nhưng query bình thường không thấy nữa
        var thayQuaQueryThuong = await WithDbAsync(db =>
            db.Comments.AnyAsync(c => c.Id == comment.Id));
        thayQuaQueryThuong.ShouldBeFalse();
    }

    // ---------- nối với module Notification của cùng phiên ----------

    [Fact]
    public async Task Comment_moi_sinh_thong_bao_doc_duoc_qua_API_notifications()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Dựng API");

        // PM là Reporter của task -> nằm trong InterestedEmployeeIds; member là người viết
        // nên NotifyMany tự loại chính họ.
        await CreateCommentAsync(member.Client, taskId, "Em làm xong phần này rồi ạ");

        var inbox = await pm.Client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications?isRead=false", TestJson.Options);

        // Hộp của PM cũng có thông báo "đã chấp nhận lời mời" từ bước dựng dữ liệu, nên lọc
        // theo type thay vì giả định chỉ có một dòng.
        var noti = inbox!.Items
            .Where(n => n.Type == NotificationType.CommentAdded)
            .ShouldHaveSingleItem();

        noti.RelatedEntityId.ShouldBe(taskId);
        noti.RelatedEntityKind.ShouldBe(RelatedEntityKind.Task);
        noti.Content.ShouldContain("Dựng API");
    }

    [Fact]
    public async Task Nguoi_viet_khong_tu_nhan_thong_bao_ve_comment_cua_minh()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var truoc = await CountNotificationsAsync(pm.EmployeeId);

        await CreateCommentAsync(pm.Client, taskId, "PM tự bình luận trên task của mình");

        // PM vừa là reporter vừa là người viết -> NotifyMany loại người thực hiện
        (await CountNotificationsAsync(pm.EmployeeId)).ShouldBe(truoc);
    }

    // ---------- validator ----------

    [Fact]
    public async Task Comment_rong_bi_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new CreateCommentRequest("   "));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Comment_qua_2000_ky_tu_bi_chan_400_thay_vi_500_tu_SQL()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new CreateCommentRequest(new string('x', 2001)));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Comment_tren_task_khong_ton_tai_nhan_404()
        => (await (await CreateUserAsync()).Client.PostAsJsonAsync(
                $"/api/v1/tasks/{Guid.NewGuid()}/comments",
                new CreateCommentRequest("Nội dung"))).StatusCode
           .ShouldBe(HttpStatusCode.NotFound);

    [Fact]
    public async Task Comment_ghi_ActivityLog_cho_ca_ba_hanh_dong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var truoc = await CountActivityLogsAsync(taskId);

        var comment = await CreateCommentAsync(pm.Client, taskId, "Bình luận");
        await pm.Client.PutAsJsonAsync($"/api/v1/comments/{comment.Id}",
            new UpdateCommentRequest("Bình luận sửa"));
        await pm.Client.DeleteAsync($"/api/v1/comments/{comment.Id}");

        (await CountActivityLogsAsync(taskId)).ShouldBe(truoc + 3);
    }

    // ---------- helpers ----------

    private static async Task<CommentResponse> CreateCommentAsync(
        HttpClient client, Guid taskId, string content)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new CreateCommentRequest(content));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options))!;
    }

    private static async Task<PagedResult<CommentResponse>> GetCommentsAsync(
        HttpClient client, Guid taskId)
        => (await client.GetFromJsonAsync<PagedResult<CommentResponse>>(
                $"/api/v1/tasks/{taskId}/comments", TestJson.Options))!;
}

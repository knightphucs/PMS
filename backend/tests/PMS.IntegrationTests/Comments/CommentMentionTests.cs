using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.Notifications;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Comments;

/// <summary>
/// @mention trong bình luận — thêm 2026-08-04.
///
/// <para>
/// Server <b>không parse</b> <c>@tên</c> từ nội dung: tên hiển thị không phải định danh.
/// Client gửi <c>mentionedEmployeeIds</c> vì nó vốn đã biết id từ ô gợi ý người dùng chọn.
/// </para>
/// <para>
/// 🔴 Nhưng chính vì id do client gửi, <b>server bắt buộc phải lọc</b> — và đó là khẳng định
/// quan trọng nhất trong file này.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CommentMentionTests : IntegrationTestBase
{
    public CommentMentionTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Nhac_ten_mot_thanh_vien_thi_ho_nhan_thong_bao_Mentioned()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var member = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments", new
        {
            Content = "Nhờ bạn xem giúp phần này nhé",
            MentionedEmployeeIds = new[] { member.EmployeeId }
        });
        res.EnsureSuccessStatusCode();

        var got = await NotificationsOfAsync(member.Client);
        got.Count(n => n.Type == NotificationType.Mentioned).ShouldBe(1);

        // Không nhận HAI thông báo cho cùng một hành động: cái cụ thể hơn ("được nhắc tên")
        // thắng, và người đó bị loại khỏi lượt CommentAdded.
        got.Count(n => n.Type == NotificationType.CommentAdded && n.RelatedEntityId == taskId)
           .ShouldBe(0);
    }

    /// <summary>
    /// 🔴 Khẳng định quan trọng nhất. Không lọc thì bất kỳ ai cũng bắn được thông báo tới bất
    /// kỳ ai bằng cách nhét id lạ vào body — và người nhận sẽ thấy tên một task thuộc dự án
    /// họ không có quyền mở. Vừa là rò rỉ thông tin, vừa là một kênh quấy rối.
    /// </summary>
    [Fact]
    public async Task Nhac_ten_nguoi_NGOAI_du_an_thi_ho_KHONG_nhan_duoc_gi()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var outsider = await CreateUserAsync();   // không hề được mời vào dự án

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments", new
        {
            Content = "Thử nhắc tên người ngoài dự án",
            MentionedEmployeeIds = new[] { outsider.EmployeeId }
        });

        // Request vẫn thành công — bình luận là hợp lệ, chỉ phần nhắc tên bị lọc bỏ. Trả 400
        // ở đây sẽ tiết lộ "id này có tồn tại nhưng không thuộc dự án", tức lại là rò rỉ.
        res.EnsureSuccessStatusCode();

        var got = await NotificationsOfAsync(outsider.Client);
        got.ShouldBeEmpty();
    }

    [Fact]
    public async Task Nhac_ten_nguoi_moi_CHUA_chap_nhan_thi_cung_khong_nhan_duoc()
    {
        // Chỉ thành viên `Accepted` mới là thành viên thật — cùng một luật với
        // `GetRoleInProjectAsync` và `useMyProjectRole` ở frontend.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var invited = await CreateUserAsync();
        await SeedMemberAsync(projectId, invited.EmployeeId, RoleInProject.Member,
            InvitationStatus.Pending);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments", new
        {
            Content = "Nhắc tên người mới được mời",
            MentionedEmployeeIds = new[] { invited.EmployeeId }
        });

        var got = await NotificationsOfAsync(invited.Client);
        got.ShouldNotContain(n => n.Type == NotificationType.Mentioned);
    }

    [Fact]
    public async Task Tu_nhac_ten_minh_thi_khong_tu_bao_cho_minh()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments", new
        {
            Content = "Ghi chú cho chính mình",
            MentionedEmployeeIds = new[] { pm.EmployeeId }
        });

        var got = await NotificationsOfAsync(pm.Client);
        got.ShouldNotContain(n => n.Type == NotificationType.Mentioned);
    }

    [Fact]
    public async Task Khong_gui_mentionedEmployeeIds_thi_hanh_vi_giu_nguyen_nhu_cu()
    {
        // Trường mới là tùy chọn — client cũ không gửi vẫn phải chạy đúng như trước.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/comments",
            new { Content = "Bình luận không nhắc ai" });

        res.EnsureSuccessStatusCode();
    }

    // ---------- helper ----------

    private static async Task<List<NotificationResponse>> NotificationsOfAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<NotificationResponse>>(
            "/api/v1/notifications?pageSize=100", TestJson.Options);

        return page!.Items.ToList();
    }
}

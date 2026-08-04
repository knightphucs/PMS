using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PMS.Application.Features.Attachments;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Attachments;

/// <summary>
/// File đính kèm (ADR-035). Trọng tâm là <b>whitelist</b>: mỗi bước kiểm tra có ít nhất một
/// test giữ, vì đây là loại code mà "chạy được" và "an toàn" là hai chuyện khác hẳn nhau.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AttachmentsTests : IntegrationTestBase
{
    public AttachmentsTests(PmsWebApplicationFactory factory) : base(factory) { }

    // Chữ ký thật của từng định dạng — dùng để dựng file hợp lệ tối thiểu.
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];
    private static readonly byte[] WindowsExeHeader = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    // ==================== Luồng thuận ====================

    [Fact]
    public async Task Tai_len_file_hop_le_vao_task_roi_doc_lai_duoc_trong_danh_sach()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var uploaded = await UploadToTaskAsync(pm.Client, taskId, "so-do.png", "image/png", PngHeader);

        uploaded.FileName.ShouldBe("so-do.png");
        uploaded.TaskId.ShouldBe(taskId);
        uploaded.ProjectId.ShouldBeNull();          // CHECK constraint: đúng MỘT chủ sở hữu
        uploaded.UploaderId.ShouldBe(pm.EmployeeId);
        uploaded.UploaderName.ShouldNotBeNullOrEmpty();
        uploaded.SizeBytes.ShouldBe(PngHeader.Length);

        var list = await pm.Client.GetFromJsonAsync<List<AttachmentResponse>>(
            $"/api/v1/tasks/{taskId}/attachments", TestJson.Options);
        list!.ShouldContain(a => a.Id == uploaded.Id);
    }

    [Fact]
    public async Task Tai_len_file_vao_project_thi_ProjectId_co_gia_tri_con_TaskId_null()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsync($"/api/v1/projects/{projectId}/attachments",
            BuildMultipart("hop-dong.pdf", "application/pdf", PdfHeader));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);

        var uploaded = await res.Content.ReadFromJsonAsync<AttachmentResponse>(TestJson.Options);
        uploaded!.ProjectId.ShouldBe(projectId);
        uploaded.TaskId.ShouldBeNull();

        // 🔴 Query filter của Attachment có nhánh `TaskId == null || !Task.IsDeleted`.
        // Thiếu nhánh đó thì attachment của PROJECT bị lọc mất im lặng khỏi mọi query,
        // vì `!NULL` trong SQL là NULL chứ không phải TRUE.
        var list = await pm.Client.GetFromJsonAsync<List<AttachmentResponse>>(
            $"/api/v1/projects/{projectId}/attachments", TestJson.Options);
        list!.ShouldContain(a => a.Id == uploaded.Id);
    }

    [Fact]
    public async Task Tai_ve_tra_dung_noi_dung_va_ba_header_bao_ve()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var uploaded = await UploadToTaskAsync(pm.Client, taskId, "anh.png", "image/png", PngHeader);

        var res = await pm.Client.GetAsync($"/api/v1/attachments/{uploaded.Id}/download");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Ba thứ này đi CÙNG NHAU để triệt đường render inline một payload HTML/SVG
        res.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        res.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        res.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");

        // Nội dung phải NGUYÊN VẸN: bước kiểm chữ ký đọc mất 8 byte đầu, không tua lại
        // stream thì file lưu xuống bị thiếu đầu — hỏng im lặng, chỉ lộ khi mở file ra.
        (await res.Content.ReadAsByteArrayAsync()).ShouldBe(PngHeader);
    }

    // ==================== Whitelist: từng bước kiểm tra một test ====================

    [Fact]
    public async Task Doi_duoi_exe_thanh_png_bi_chan_o_buoc_kiem_CHU_KY()
    {
        // Đây là bài kiểm tra quan trọng nhất của cả tính năng: cả phần mở rộng lẫn
        // content-type đều do CLIENT tự khai, nên không có bước đọc nội dung thì đổi tên
        // evil.exe -> evil.png và khai "image/png" là qua sạch mọi kiểm tra khác.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("evil.png", "image/png", WindowsExeHeader));

        // 400 chứ không 415: file NÓI DỐI về định dạng, khác với định dạng chưa hỗ trợ.
        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await AssertNoAttachmentAsync(pm.Client, taskId);
    }

    [Fact]
    public async Task Duoi_khong_nam_trong_whitelist_bi_chan_415()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("script.exe", "application/octet-stream", WindowsExeHeader));

        res.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Duoi_kep_bi_chan_ke_ca_khi_duoi_CUOI_hop_le()
    {
        // a.php.png có đuôi cuối là .png (hợp lệ) nhưng chứa .php ở giữa. Một số máy chủ
        // web chọn handler theo phần mở rộng BẤT KỲ mà chúng nhận ra, không chỉ phần cuối.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("shell.php.png", "image/png", PngHeader));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await AssertNoAttachmentAsync(pm.Client, taskId);
    }

    [Theory]
    [InlineData("../../etc/passwd.png")]
    [InlineData("..\\windows\\system32.png")]
    [InlineData(".hidden.png")]
    public async Task Ten_file_co_y_do_duong_dan_bi_chan_400(string fileName)
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart(fileName, "image/png", PngHeader));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Content_type_khong_khop_whitelist_bi_chan_415()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("anh.png", "text/html", PngHeader));

        res.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task File_vuot_gioi_han_bi_chan_413()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        // Giới hạn trong môi trường test là 1 MB (xem PmsWebApplicationFactory)
        var tooBig = new byte[2 * 1024 * 1024];
        PngHeader.CopyTo(tooBig, 0);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("qua-lon.png", "image/png", tooBig));

        res.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task File_rong_bi_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("rong.png", "image/png", []));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ==================== Phân quyền ====================

    [Fact]
    public async Task Viewer_khong_tai_len_duoc_nhung_van_tai_ve_duoc()
    {
        // Soi gương ma trận quyền của comment (ADR-026): đọc = View (kể cả Viewer),
        // ghi = action riêng chỉ PM/Member.
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        var uploaded = await UploadToTaskAsync(pm.Client, taskId, "tai-lieu.pdf", "application/pdf", PdfHeader);

        (await viewer.Client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart("cua-toi.png", "image/png", PngHeader))).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await viewer.Client.GetAsync($"/api/v1/attachments/{uploaded.Id}/download")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Xoa_file_la_nguoi_tai_len_HOAC_PM_chu_khong_phai_member_khac()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var other = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, other, projectId, RoleInProject.Member);

        var uploaded = await UploadToTaskAsync(member.Client, taskId, "cua-member.png", "image/png", PngHeader);

        // Member khác: không phải người tải lên, cũng không phải PM -> 403
        (await other.Client.DeleteAsync($"/api/v1/attachments/{uploaded.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        // PM xóa được file của người khác (kiểm duyệt — cùng lý do ADR-026 cho comment)
        (await pm.Client.DeleteAsync($"/api/v1/attachments/{uploaded.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        await AssertNoAttachmentAsync(pm.Client, taskId);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var uploaded = await UploadToTaskAsync(pm.Client, taskId, "rieng-tu.pdf", "application/pdf", PdfHeader);

        // 404 ở CẢ hai, nếu không người ngoài phân biệt được "không tồn tại" với
        // "tồn tại nhưng không phải của tôi" — đủ để dò (ADR-006/019).
        (await outsider.Client.GetAsync($"/api/v1/attachments/{uploaded.Id}/download")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.DeleteAsync($"/api/v1/attachments/{uploaded.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/v1/tasks/{taskId}/attachments")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private static MultipartFormDataContent BuildMultipart(
        string fileName, string contentType, byte[] content)
    {
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // Tên trường phải là "file" — khớp tham số IFormFile file của controller.
        return new MultipartFormDataContent { { fileContent, "file", fileName } };
    }

    private static async Task<AttachmentResponse> UploadToTaskAsync(
        HttpClient client, Guid taskId, string fileName, string contentType, byte[] content)
    {
        var res = await client.PostAsync($"/api/v1/tasks/{taskId}/attachments",
            BuildMultipart(fileName, contentType, content));
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<AttachmentResponse>(TestJson.Options))!;
    }

    private static async Task AssertNoAttachmentAsync(HttpClient client, Guid taskId)
    {
        var list = await client.GetFromJsonAsync<List<AttachmentResponse>>(
            $"/api/v1/tasks/{taskId}/attachments", TestJson.Options);
        list!.ShouldBeEmpty();
    }
}

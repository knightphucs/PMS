using System.Net.Http.Json;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Giữ ADR-022. Đọc RAW JSON chứ không deserialize: nếu deserialize thì
/// <see cref="TestJson"/> đọc được cả hai dạng nên test sẽ xanh kể cả khi converter bị
/// tháo khỏi <c>Program.cs</c> — tức là không bảo vệ được quyết định nào.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class EnumSerializationTests : IntegrationTestBase
{
    public EnumSerializationTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Response_tra_enum_duoi_dang_ten_chu_khong_phai_so()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId, priority: Priority.Highest);

        var json = await pm.Client.GetStringAsync($"/api/v1/tasks/{taskId}");

        // ADR-052: `status` nay là một OBJECT (cột board) chứ không còn là chuỗi enum. Enum
        // duy nhất còn lại trong đó là `category` — và nó vẫn phải ra dạng TÊN.
        json.ShouldContain("\"category\":\"ToDo\"");
        json.ShouldContain("\"priority\":\"Highest\"");
        json.ShouldNotContain("\"category\":0");
        json.ShouldNotContain("\"priority\":0");
    }

    [Fact]
    public async Task Request_van_nhan_enum_dang_so_de_client_cu_khong_vo()
    {
        // Tính tương thích ngược mà ADR-022 dựa vào để không phải sửa 18 request Postman
        // ngay lập tức: JsonStringEnumConverter cho phép cả số ở chiều đọc.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks", new
        {
            name = "Task gửi priority bằng số",
            projectId,
            sprintId = (Guid?)null,
            parentTaskId = (Guid?)null,
            dueDate = (DateTime?)null,
            priority = 0                     // Highest
        });

        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);
        created!.Priority.ShouldBe(Priority.Highest);
    }

    [Fact]
    public async Task Request_nhan_enum_dang_ten()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks", new
        {
            name = "Task gửi priority bằng tên",
            projectId,
            sprintId = (Guid?)null,
            parentTaskId = (Guid?)null,
            dueDate = (DateTime?)null,
            priority = "Low"
        });

        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);
        created!.Priority.ShouldBe(Priority.Low);
    }
}

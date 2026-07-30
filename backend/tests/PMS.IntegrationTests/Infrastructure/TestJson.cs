using System.Text.Json;
using System.Text.Json.Serialization;

namespace PMS.IntegrationTests.Infrastructure;

/// <summary>
/// Tùy chọn JSON dùng chung cho MỌI lần đọc response trong integration test.
/// <para>
/// ADR-022 bật <see cref="JsonStringEnumConverter"/> ở API nên response trả enum dưới dạng
/// tên ("ToDo"), còn <c>System.Text.Json</c> mặc định chỉ đọc được số — không truyền options
/// này thì <c>ReadFromJsonAsync</c>/<c>GetFromJsonAsync</c> ném JsonException.
/// </para>
/// <para>
/// Chiều GHI (<c>PostAsJsonAsync</c>/<c>PutAsJsonAsync</c>) KHÔNG cần options: nó serialize
/// enum thành số và converter ở phía server vẫn nhận số bình thường. Giữ nguyên để thấy rõ
/// tính tương thích ngược mà ADR-022 dựa vào.
/// </para>
/// <para>
/// <see cref="JsonSerializerDefaults.Web"/> để khớp mặc định của ASP.NET Core (camelCase,
/// so tên không phân biệt hoa thường) — đúng thứ mà các overload không-tham-số đang dùng.
/// </para>
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

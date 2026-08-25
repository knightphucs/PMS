using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Filtering;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Filtering;

/// <summary>
/// Khoá danh mục bộ lọc (ADR-061, mở rộng ở ADR-063).
///
/// <para>
/// 🔴 <b>File này tồn tại vì một comment trong mã nguồn đã nói sai về chính nó.</b>
/// <c>TaskFilterCatalog.KindOf</c> từng mang chú thích <i>"không dùng `_ =>`: một thành viên
/// enum mới mà quên khai kiểu sẽ là lỗi BIÊN DỊCH"</i> — nhưng nhánh <c>_ =></c> nằm ngay
/// dưới đó, nên thực tế là một ngoại lệ <b>lúc chạy</b> (→ 500). Kiểm chứng ở phiên
/// ADR-063: thêm <c>TaskField.ApprovalState</c> mà chưa khai kiểu vẫn <c>dotnet build</c>
/// sạch.
/// </para>
/// <para>
/// Lời hứa đó nay được mua bằng thứ mua được nó thật: một test vét cạn danh mục, đúng khuôn
/// <c>SystemPermissionsCatalogTests</c> (ADR-045). Quên khai kiểu = test ĐỎ, không phải một
/// endpoint 500 mà chỉ người dùng đầu tiên gặp phải mới biết.
/// </para>
/// </summary>
public class TaskFilterCatalogTests
{
    [Fact]
    public void Moi_truong_dung_san_deu_phai_khai_kieu_gia_tri()
    {
        foreach (var field in Enum.GetValues<TaskField>())
            Should.NotThrow(() => TaskFilterCatalog.KindOf(field),
                $"TaskField.{field} chưa có nhánh trong KindOf — thêm nó vào TaskFilterCatalog, " +
                "và nhớ cả nhánh dịch LINQ ở TaskRepository.ApplyBuiltIn.");
    }

    [Fact]
    public void Moi_kieu_truong_tuy_bien_deu_phai_khai_kieu_gia_tri()
    {
        foreach (var type in Enum.GetValues<FieldType>())
            Should.NotThrow(() => TaskFilterCatalog.KindOf(type),
                $"FieldType.{type} chưa có nhánh trong KindOf.");
    }

    [Fact]
    public void Moi_truong_kieu_Enum_deu_phai_khai_danh_sach_gia_tri_hop_le()
    {
        // 🔴 Phép kiểm này bắt một lỗi CÓ THẬT đã tồn tại trước ADR-063: danh sách "Nhận:"
        // trong thông điệp 400 từng là một ternary HAI VẾ (Priority ? ... : StatusCategory),
        // nên trường Enum thứ ba được báo sai là nhận "ToDo, InProgress, Done".
        //
        // Nó không làm hỏng chức năng nào — chỉ nói dối người dùng về cách sửa lỗi của họ,
        // đúng loại sai im lặng mà "build sạch, test xanh" không bao giờ bắt được.
        var enumFields = Enum.GetValues<TaskField>()
            .Where(f => TaskFilterCatalog.KindOf(f) == FilterValueKind.Enum)
            .ToList();

        enumFields.ShouldNotBeEmpty();

        foreach (var field in enumFields)
        {
            var ex = Should.Throw<BusinessRuleException>(() => TaskFilterCatalog.Resolve(
                field, null, null, FilterOperator.Equals, "gia-tri-khong-ton-tai", field.ToString()));

            var expected = field switch
            {
                TaskField.Priority      => Enum.GetNames<Priority>(),
                TaskField.Category      => Enum.GetNames<StatusCategory>(),
                TaskField.ApprovalState => Enum.GetNames<TaskApprovalState>(),
                _ => throw new Xunit.Sdk.XunitException(
                    $"TaskField.{field} là kiểu Enum nhưng test này chưa biết danh sách của nó. " +
                    "Thêm nhánh ở đây VÀ ở TaskFilterCatalog.ParseEnumByName.")
            };

            foreach (var name in expected)
                ex.Message.Contains(name).ShouldBeTrue(
                    $"Thông điệp lỗi của TaskField.{field} không liệt kê '{name}'. " +
                    $"Thông điệp thật: {ex.Message}");
        }
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Pending")]
    [InlineData("approved")]   // khớp KHÔNG phân biệt hoa thường
    [InlineData("REJECTED")]
    public void ApprovalState_nhan_dung_bon_gia_tri(string raw)
    {
        var resolved = TaskFilterCatalog.Resolve(
            TaskField.ApprovalState, null, null, FilterOperator.Equals, raw, "Trạng thái duyệt");

        resolved.Kind.ShouldBe(FilterValueKind.Enum);
        resolved.EnumValue.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+2")]
    public void MOI_truong_Enum_deu_tu_choi_gia_tri_dang_SO(string numeric)
    {
        // 🪤 Test này bắt một lỗi CÓ SẴN từ ADR-061, không phải một lỗi của ADR-063.
        //
        // `ParseEnumByName` tự nhận là "khớp theo TÊN, không theo số", nhưng
        // `Enum.TryParse` của .NET CŨNG nhận chuỗi số: TryParse<Priority>("1") trả về
        // `High` với IsDefined == true. Đã kiểm chứng bằng chương trình dò, không suy đoán.
        //
        // Hậu quả cụ thể: một SavedView lưu điều kiện `Priority = "1"` vẫn chạy hôm nay, và
        // sẽ âm thầm đổi nghĩa nếu ai đó CHÈN một thành viên vào giữa enum — đúng cái bẫy
        // remap mà ADR-052 đã trả giá, và đúng thứ XML doc ở đó tuyên bố là đã chặn.
        //
        // Vì vậy test chạy trên MỌI trường Enum, không riêng trường mới: lỗ hổng nằm ở
        // Priority và Category trước, ApprovalState chỉ thừa hưởng nó.
        var enumFields = Enum.GetValues<TaskField>()
            .Where(f => TaskFilterCatalog.KindOf(f) == FilterValueKind.Enum);

        foreach (var field in enumFields)
            Should.Throw<BusinessRuleException>(
                () => TaskFilterCatalog.Resolve(
                    field, null, null, FilterOperator.Equals, numeric, field.ToString()),
                $"TaskField.{field} nhận '{numeric}' như một giá trị hợp lệ — " +
                "bộ lọc đã lưu sẽ âm thầm đổi nghĩa khi enum thêm thành viên.");
    }

    [Fact]
    public void ApprovalState_KHONG_so_sanh_co_thu_tu_duoc()
    {
        // "Lớn hơn Pending" không có nghĩa nào người dùng đọc được — §0 "không ship một
        // toán tử nói dối". Cùng lý lẽ đã áp cho Priority và Category.
        foreach (var op in new[] { FilterOperator.GreaterThan, FilterOperator.LessThan,
                                   FilterOperator.GreaterThanOrEqual, FilterOperator.LessThanOrEqual })
            TaskFilterCatalog.Supports(FilterValueKind.Enum, op).ShouldBeFalse();
    }
}

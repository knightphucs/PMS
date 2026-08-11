using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IFieldDefinitionRepository : IRepository<FieldDefinition>
{
    /// <summary>
    /// Mọi trường của project kèm <c>Options</c>, sắp theo <c>Order</c> rồi tie-break bằng
    /// <c>Id</c> — cùng lý do đã ghi ở <see cref="IBoardColumnRepository.ListByProjectAsync"/>:
    /// <c>Order</c> không unique nên thiếu tie-break thì thứ tự nhảy giữa hai lần gọi.
    /// </summary>
    Task<IReadOnlyList<FieldDefinition>> ListByProjectAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Như <see cref="ListByProjectAsync"/> nhưng CÓ tracking.
    ///
    /// <para>
    /// 🔴 Tồn tại vì một cái bẫy đã nổ thật. <see cref="ListByProjectAsync"/> đọc
    /// <c>AsNoTracking</c>, nên các <c>FieldOption</c> nó trả về là entity RỜI. Gán một
    /// entity rời vào navigation của một entity đang được theo dõi
    /// (<c>value.SelectedOptions.Add(option)</c>) thì EF coi nó là hàng MỚI và sinh INSERT
    /// — dẫn tới <i>"Violation of PRIMARY KEY constraint 'PK_FieldOptions'"</i>, tức 500 ở
    /// mọi lần ghi giá trị Select.
    /// </para>
    /// <para>
    /// ⚠️ Đừng "tối ưu" bằng cách quay lại no-tracking ở đường GHI: nhanh hơn không đáng
    /// kể, và lỗi quay lại y nguyên.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<FieldDefinition>> ListByProjectWithTrackingAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>Một trường kèm Options, có tracking (để sửa/xoá).</summary>
    Task<FieldDefinition?> GetWithOptionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Số task đang có giá trị cho từng trường — nuôi cảnh báo trước khi xoá.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountValuesByFieldAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Giá trị trường tuỳ biến của MỘT task, kèm định nghĩa và các lựa chọn đang chọn.
    /// Có tracking: đây là tập mà <c>SetValuesAsync</c> sửa tại chỗ.
    /// </summary>
    Task<IReadOnlyList<FieldValue>> ListValuesForTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Xoá MỌI giá trị của một trường bằng một lệnh DELETE, trả về số hàng đã xoá.
    ///
    /// <para>
    /// 🔴 Phải gọi TRƯỚC khi xoá <c>FieldDefinition</c>: FK từ <c>FieldValues</c> là
    /// <c>Restrict</c> chứ không phải <c>Cascade</c> — không phải vì nghiệp vụ muốn vậy mà
    /// vì Cascade ở đó tạo hai đường cascade xuống bảng nối và SQL Server từ chối tạo FK.
    /// Chi tiết ở <c>FieldValueConfiguration</c>.
    /// </para>
    /// <para>
    /// Hàng ở bảng nối <c>FieldValueOptions</c> biến mất theo (FK cascade từ FieldValues).
    /// </para>
    /// </summary>
    Task<int> DeleteValuesOfFieldAsync(Guid fieldDefinitionId, CancellationToken ct = default);
}

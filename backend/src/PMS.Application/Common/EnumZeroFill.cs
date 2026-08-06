namespace PMS.Application.Common;

/// <summary>
/// Bù đủ mọi giá trị enum, kể cả giá trị không có bản ghi nào — biểu đồ ở frontend không
/// nên phải tự bịa ra hạng mục còn thiếu, và nếu nó tự bịa thì bảng đó lệch dần khỏi backend
/// mỗi lần thêm một giá trị enum mới. Thứ tự trả về theo thứ tự khai báo enum, ổn định giữa
/// các lần gọi.
///
/// <para>
/// Tách ra khỏi <c>StatisticsService</c> (nơi sinh ra logic này) khi <c>ReportsService</c>
/// cần đúng công thức đó lần thứ hai cho backlog insight theo priority — chép lại là dựng
/// hai bản có thể trôi khỏi nhau, đúng lớp lỗi ADR-034 đã đặt tên.
/// </para>
/// </summary>
public static class EnumZeroFill
{
    public static IReadOnlyList<TResult> Fill<TTally, TEnum, TResult>(
        IReadOnlyList<TTally> tallies,
        Func<TTally, TEnum> keyOf,
        Func<TTally, int> countOf,
        Func<TEnum, int, TResult> build) where TEnum : struct, Enum
    {
        var lookup = tallies.ToDictionary(keyOf, countOf);
        return Enum.GetValues<TEnum>()
            .Select(value => build(value, lookup.GetValueOrDefault(value)))
            .ToList();
    }
}

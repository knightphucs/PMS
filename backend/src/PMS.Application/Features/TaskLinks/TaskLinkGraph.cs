using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;

namespace PMS.Application.Features.TaskLinks;

/// <summary>
/// Hai luật thuần hàm của TaskLink, tách khỏi service để unit test được mà không cần DB
/// (ADR-038).
/// </summary>
public static class TaskLinkGraph
{
    /// <summary>
    /// 🔴 <b>Chuẩn hóa lúc ghi.</b> Unique index <c>(SourceTaskId, TargetTaskId, LinkType)</c>
    /// <b>không</b> bắt được trùng ngữ nghĩa: <c>Blocks(A,B)</c> và <c>IsBlockedBy(B,A)</c>
    /// là CÙNG một sự thật với giá trị cột khác nhau, index lưu cả hai vui vẻ.
    /// <para>
    /// Cách sửa bằng cấu trúc: quy mọi liên kết về một dạng duy nhất trước khi ghi.
    /// <list type="bullet">
    /// <item><c>IsBlockedBy(A,B)</c> → <c>Blocks(B,A)</c> — đảo chiều, đổi loại.</item>
    /// <item><c>RelatesTo</c>/<c>Duplicates</c> đối xứng → sắp cặp theo thứ tự Guid, để
    /// <c>RelatesTo(A,B)</c> và <c>RelatesTo(B,A)</c> ra cùng một hàng.</item>
    /// </list>
    /// Hệ quả phải nhớ khi đọc DB: <see cref="LinkType.IsBlockedBy"/> là giá trị
    /// <b>chỉ dùng ở đầu vào, không bao giờ được lưu</b>.
    /// </para>
    /// </summary>
    public static (Guid Source, Guid Target, LinkType Type) Canonicalize(
        Guid source, Guid target, LinkType type) => type switch
    {
        LinkType.IsBlockedBy => (target, source, LinkType.Blocks),
        LinkType.Blocks      => (source, target, LinkType.Blocks),

        // Đối xứng: chỉ cần một thứ tự ổn định, chọn thứ tự Guid cho tất định.
        _ => source.CompareTo(target) <= 0
                ? (source, target, type)
                : (target, source, type)
    };

    /// <summary>
    /// Từ góc nhìn của <paramref name="viewerTaskId"/>, một hàng <c>Blocks(S,T)</c> đọc là
    /// "tôi chặn T" khi tôi là S, và "tôi bị T chặn" khi tôi là T. Loại đối xứng thì hiện
    /// nguyên trạng ở cả hai phía.
    /// </summary>
    public static (LinkType Displayed, Guid RelatedTaskId) ViewFrom(
        Guid viewerTaskId, Guid sourceTaskId, Guid targetTaskId, LinkType stored)
    {
        var iAmSource = viewerTaskId == sourceTaskId;
        var relatedId = iAmSource ? targetTaskId : sourceTaskId;

        var displayed = stored == LinkType.Blocks && !iAmSource
            ? LinkType.IsBlockedBy
            : stored;

        return (displayed, relatedId);
    }

    /// <summary>
    /// Có đường đi theo cạnh <c>Blocks</c> từ <paramref name="from"/> tới
    /// <paramref name="to"/> không — BFS trong bộ nhớ trên toàn bộ cạnh của một project.
    /// <para>
    /// Dùng để chặn <c>A Blocks B</c> khi B đã (gián tiếp) chặn A. Gọi đúng tên hiện tượng:
    /// đó <b>không</b> phải vòng lặp vô hạn trong code — <c>GetUnfinishedBlockersAsync</c>
    /// không đệ quy — mà là <b>livelock nghiệp vụ</b>: cả hai task vĩnh viễn không vào được
    /// <c>InProgress</c> vì mỗi cái chờ cái kia <c>Done</c>.
    /// </para>
    /// </summary>
    public static bool HasPath(IReadOnlyList<BlockingEdge> edges, Guid from, Guid to)
    {
        var adjacency = edges
            .GroupBy(e => e.SourceTaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetTaskId).ToList());

        var visited = new HashSet<Guid> { from };
        var queue = new Queue<Guid>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to) return true;

            if (!adjacency.TryGetValue(current, out var neighbours)) continue;

            foreach (var next in neighbours)
                if (visited.Add(next)) queue.Enqueue(next);
        }

        return false;
    }
}

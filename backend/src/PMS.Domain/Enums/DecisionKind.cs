namespace PMS.Domain.Enums;

/// <summary>
/// Một lá phiếu trên một yêu cầu duyệt (ADR-062). Danh mục ĐÓNG, và cố ý chỉ có HAI giá trị.
///
/// <para>
/// 📌 Không có <c>Abstain</c>: một phiếu trắng không đổi gì trong phép đếm quorum, nên nó là
/// một giá trị không chặn được gì (luật 4 của Doctrine §0). Người không muốn quyết định thì
/// đơn giản là chưa quyết định — trạng thái đó đã có tên rồi, là
/// <see cref="ApprovalStatus.Pending"/>.
/// </para>
/// </summary>
public enum DecisionKind
{
    Approve,
    Reject
}

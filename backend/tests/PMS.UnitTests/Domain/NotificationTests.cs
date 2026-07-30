using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Domain;

public class NotificationTests
{
    // ---------- ADR-023: đánh dấu đã đọc là idempotent ----------

    [Fact]
    public void MarkAsRead_lan_dau_tra_true_va_doi_trang_thai()
    {
        var notification = New(NotificationType.TaskAssigned);

        notification.MarkAsRead().ShouldBeTrue();
        notification.IsRead.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsRead_lan_hai_tra_false_chu_khong_nem_DomainException()
    {
        // Khác ProjectMember.Accept(): bấm chuông thông báo hai lần không phải vi phạm
        // nghiệp vụ. Trả false để service biết không cần SaveChanges.
        var notification = New(NotificationType.TaskAssigned);
        notification.MarkAsRead();

        notification.MarkAsRead().ShouldBeFalse();
        notification.IsRead.ShouldBeTrue();
    }

    // ---------- ADR-025: RelatedEntityKind suy ra từ Type, không lưu cột ----------

    [Theory]
    [InlineData(NotificationType.InvitedToProject)]
    [InlineData(NotificationType.InvitationAccepted)]
    [InlineData(NotificationType.InvitationDeclined)]
    [InlineData(NotificationType.RoleChanged)]
    [InlineData(NotificationType.RemovedFromProject)]
    [InlineData(NotificationType.MemberLeftProject)]
    public void Thong_bao_ve_thanh_vien_tro_toi_Project(NotificationType type)
        => New(type).RelatedEntityKind.ShouldBe(RelatedEntityKind.Project);

    [Theory]
    [InlineData(NotificationType.TaskAssigned)]
    [InlineData(NotificationType.TaskUnassigned)]
    [InlineData(NotificationType.DueSoon)]
    [InlineData(NotificationType.CommentAdded)]
    [InlineData(NotificationType.StatusChanged)]
    public void Thong_bao_ve_cong_viec_tro_toi_Task(NotificationType type)
        => New(type).RelatedEntityKind.ShouldBe(RelatedEntityKind.Task);

    /// <summary>
    /// Hợp đồng kiến trúc cùng loại với <c>SoftDeletableContractTests</c> và
    /// <c>ProjectPermissionsTests.Moi_gia_tri_ProjectAction_phai_duoc_khai_bao_tuong_minh</c>:
    /// thêm <c>NotificationType</c> mới mà quên khai báo nó trỏ tới đâu thì đỏ ngay ở tầng
    /// thấp nhất, thay vì để frontend nhận <c>None</c> và không điều hướng được (ADR-025).
    /// </summary>
    [Fact]
    public void Moi_gia_tri_NotificationType_phai_duoc_khai_bao_tro_toi_Project_hoac_Task()
    {
        var chuaKhaiBao = Enum.GetValues<NotificationType>()
            .Where(type => New(type).RelatedEntityKind == RelatedEntityKind.None)
            .ToList();

        chuaKhaiBao.ShouldBeEmpty(
            "NotificationType chưa được khai báo trong Notification.RelatedEntityKind: "
            + string.Join(", ", chuaKhaiBao));
    }

    private static Notification New(NotificationType type) => new()
    {
        Id = Guid.NewGuid(),
        EmployeeId = Guid.NewGuid(),
        Type = type,
        Content = "Nội dung thông báo",
        RelatedEntityId = Guid.NewGuid()
    };
}

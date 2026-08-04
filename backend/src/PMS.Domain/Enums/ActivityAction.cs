namespace PMS.Domain.Enums;

public enum ActivityAction
{
    Created,
    Updated,
    Deleted,
    StatusChanged,
    MemberInvited,
    MemberJoined,
    MemberDeclined,
    MemberRoleChanged,
    MemberRemoved,
    Assigned,
    Unassigned,
    Commented,
    CommentUpdated,
    CommentDeleted,
    AccountLocked,
    AccountUnlocked,
    SystemRoleChanged,

    /// <summary>Đổi tập quyền của một vai trò hệ thống (ADR-045).</summary>
    PermissionsChanged
}
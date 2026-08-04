/**
 * Soi gương `PMS.Domain/Enums/*`.
 *
 * Enum được serialize ra JSON dưới dạng TÊN, không phải số (ADR-022) — nên định nghĩa
 * bằng string union chứ không phải numeric enum. Chiều gửi lên backend vẫn nhận cả số
 * nhưng đừng dựa vào đó.
 */

/** Dùng chung cho cả `Project.Status` lẫn `TaskItem.Status`. */
export type Status = 'ToDo' | 'InProgress' | 'Review' | 'Done';

export type Priority = 'Highest' | 'High' | 'Medium' | 'Low' | 'Lowest';

export type RoleInProject = 'ProjectManager' | 'Member' | 'Viewer';

export type RoleInTask = 'Owner' | 'Contributor';

export type SystemRole = 'User' | 'SystemAdmin';

export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined';

/** ADR-025 — dùng cặp (kind, id) để điều hướng khi bấm vào thông báo. */
export type RelatedEntityKind = 'None' | 'Project' | 'Task';

/**
 * Soi gương `PMS.Domain/Enums/NotificationType.cs`.
 *
 * ⚠️ Đừng dựng bảng `NotificationType → route` từ danh sách này. `RelatedEntityKind` là
 * giá trị SUY RA từ chính `Type` ở phía backend (ADR-025) — dựng bảng thứ hai ở frontend
 * là tạo ra một bản sao chắc chắn có lúc lệch. Điều hướng bằng cặp
 * `(relatedEntityKind, relatedEntityId)`; enum này chỉ dùng để chọn icon và nhãn.
 */
export type NotificationType =
  | 'TaskAssigned'
  | 'TaskUnassigned'
  | 'DueSoon'
  | 'CommentAdded'
  | 'StatusChanged'
  | 'InvitedToProject'
  | 'InvitationAccepted'
  | 'InvitationDeclined'
  | 'RoleChanged'
  | 'RemovedFromProject'
  | 'MemberLeftProject';

/**
 * Loại liên kết giữa hai task.
 *
 * ⚠️ `IsBlockedBy` **không bao giờ tồn tại trong DB** — backend chuẩn hóa nó thành `Blocks`
 * đảo chiều lúc ghi (ADR-038). Nhưng nó VẪN xuất hiện ở hai nơi hợp lệ: khi client gửi lên
 * (tiện cho UI "task này bị chặn bởi..."), và trong `TaskLinkResponse.linkType` khi xem từ
 * đầu bị chặn.
 */
export type LinkType = 'Blocks' | 'IsBlockedBy' | 'RelatesTo' | 'Duplicates';

/** Soi gương `PMS.Domain/Enums/ActivityAction.cs`. */
export type ActivityAction =
  | 'Created'
  | 'Updated'
  | 'Deleted'
  | 'StatusChanged'
  | 'MemberInvited'
  | 'MemberJoined'
  | 'MemberDeclined'
  | 'MemberRoleChanged'
  | 'MemberRemoved'
  | 'Assigned'
  | 'Unassigned'
  | 'Commented'
  | 'CommentUpdated'
  | 'CommentDeleted'
  | 'AccountLocked'
  | 'AccountUnlocked'
  | 'SystemRoleChanged'
  /** Đổi tập quyền của một vai trò hệ thống (ADR-045). `EntityType = "RolePermission"`. */
  | 'PermissionsChanged';

/**
 * Thứ tự hiển thị của Priority.
 *
 * Không suy ra được từ chuỗi, và thứ tự số phía backend thì NGƯỢC trực giác
 * (`Highest` = 0). Giữ bảng này ở một chỗ duy nhất thay vì rải `sort` ở từng màn hình.
 */
export const PRIORITY_ORDER: readonly Priority[] = [
  'Highest',
  'High',
  'Medium',
  'Low',
  'Lowest',
];

/** Nhãn tiếng Việt cho Status — backend trả tên tiếng Anh, chỉ đổi ở lớp hiển thị. */
export const STATUS_LABEL: Record<Status, string> = {
  ToDo: 'Cần làm',
  InProgress: 'Đang làm',
  Review: 'Đang duyệt',
  Done: 'Hoàn thành',
};

export const ROLE_IN_PROJECT_LABEL: Record<RoleInProject, string> = {
  ProjectManager: 'Quản lý dự án',
  Member: 'Thành viên',
  Viewer: 'Người xem',
};

export const SYSTEM_ROLE_LABEL: Record<SystemRole, string> = {
  User: 'Người dùng',
  SystemAdmin: 'Quản trị hệ thống',
};

export const PRIORITY_LABEL: Record<Priority, string> = {
  Highest: 'Cao nhất',
  High: 'Cao',
  Medium: 'Trung bình',
  Low: 'Thấp',
  Lowest: 'Thấp nhất',
};

export const ROLE_IN_TASK_LABEL: Record<RoleInTask, string> = {
  Owner: 'Phụ trách',
  Contributor: 'Tham gia',
};

export const INVITATION_STATUS_LABEL: Record<InvitationStatus, string> = {
  Pending: 'Chờ phản hồi',
  Accepted: 'Đã tham gia',
  Declined: 'Đã từ chối',
};

/**
 * Nhãn đọc từ phía TASK ĐANG MỞ — `Blocks` nghĩa là "task này chặn task kia".
 * `Record<LinkType, string>` chứ không phải object thường: thêm một `LinkType` mới mà quên
 * cập nhật thì đỏ ngay lúc biên dịch (ADR-029).
 */
export const LINK_TYPE_LABEL: Record<LinkType, string> = {
  Blocks: 'Đang chặn',
  IsBlockedBy: 'Bị chặn bởi',
  RelatesTo: 'Liên quan tới',
  Duplicates: 'Trùng với',
};

/**
 * Nhãn ngắn cho thông báo — dùng làm tiêu đề phụ, KHÔNG thay cho `content`.
 * `content` do backend soạn sẵn (có tên người, tên task) và luôn là thứ hiển thị chính.
 */
export const NOTIFICATION_TYPE_LABEL: Record<NotificationType, string> = {
  TaskAssigned: 'Được giao việc',
  TaskUnassigned: 'Gỡ khỏi việc',
  DueSoon: 'Sắp tới hạn',
  CommentAdded: 'Bình luận mới',
  StatusChanged: 'Đổi trạng thái',
  InvitedToProject: 'Lời mời dự án',
  InvitationAccepted: 'Chấp nhận lời mời',
  InvitationDeclined: 'Từ chối lời mời',
  RoleChanged: 'Đổi vai trò',
  RemovedFromProject: 'Bị gỡ khỏi dự án',
  MemberLeftProject: 'Thành viên rời dự án',
};

export const ACTIVITY_ACTION_LABEL: Record<ActivityAction, string> = {
  Created: 'Tạo mới',
  Updated: 'Cập nhật',
  Deleted: 'Xóa',
  StatusChanged: 'Đổi trạng thái',
  MemberInvited: 'Mời thành viên',
  MemberJoined: 'Tham gia dự án',
  MemberDeclined: 'Từ chối lời mời',
  MemberRoleChanged: 'Đổi vai trò',
  MemberRemoved: 'Gỡ thành viên',
  Assigned: 'Giao việc',
  Unassigned: 'Gỡ khỏi việc',
  Commented: 'Bình luận',
  CommentUpdated: 'Sửa bình luận',
  CommentDeleted: 'Xóa bình luận',
  AccountLocked: 'Khóa tài khoản',
  AccountUnlocked: 'Mở khóa tài khoản',
  SystemRoleChanged: 'Đổi quyền hệ thống',
  PermissionsChanged: 'Đổi quyền của vai trò',
};

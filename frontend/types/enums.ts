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

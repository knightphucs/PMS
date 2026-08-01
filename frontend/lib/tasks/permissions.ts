import type { RoleInProject } from '@/types/enums';

/**
 * Soi gương `PMS.Application/Common/Authorization/ProjectPermissions.cs`.
 *
 * 🔴 Luật nền: ẩn/hiện nút theo VAI TRÒ, tuyệt đối không đoán từ mã lỗi.
 * Người ngoài project nhận **404** một cách CỐ Ý để không lộ sự tồn tại của project
 * (ADR-006/019) — nên 404 không nói gì về quyền. **403** chỉ xảy ra khi ĐÃ là thành
 * viên nhưng vai trò không đủ.
 *
 * `role === null` nghĩa là chưa biết (đang tải) HOẶC không phải thành viên đã chấp nhận
 * lời mời. Cả hai đều không được cấp quyền gì.
 */

export const canManageProject = (role: RoleInProject | null) => role === 'ProjectManager';
export const canManageMembers = (role: RoleInProject | null) => role === 'ProjectManager';
export const canManageSprints = (role: RoleInProject | null) => role === 'ProjectManager';

/** Tạo / sửa / xóa task và chuyển task giữa sprint — chỉ PM. Member KHÔNG có. */
export const canManageTasks = (role: RoleInProject | null) => role === 'ProjectManager';

/** Tự nhận / tự rút khỏi task. Viewer không có. */
export const canSelfAssign = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

export const canComment = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

/**
 * Đổi trạng thái task — ADR-017, luật per-row KHÔNG nằm trong ma trận `ProjectPermissions`.
 *
 * Là `Assignee` của CHÍNH task đó, HOẶC là `ProjectManager` của project (PM override được
 * cả task không do mình giao). `Viewer` không bao giờ được, kể cả nếu bằng cách nào đó
 * lọt vào danh sách assignee.
 */
export const canChangeTaskStatus = (role: RoleInProject | null, isAssignee: boolean) =>
  role === 'ProjectManager' || (role === 'Member' && isAssignee);

/**
 * Sửa comment = CHỈ tác giả (ADR-026). PM cũng không sửa được comment của người khác —
 * đây là chỗ dễ nhầm nhất vì mọi quyền khác PM đều override được.
 */
export const canEditComment = (isAuthor: boolean) => isAuthor;

/** Xóa comment = tác giả HOẶC ProjectManager (ADR-026). */
export const canDeleteComment = (role: RoleInProject | null, isAuthor: boolean) =>
  isAuthor || role === 'ProjectManager';

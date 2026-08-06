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

/** PM tạo được mọi subtask; Member chỉ được tạo dưới task đang được giao cho mình. */
export const canCreateSubtask = (role: RoleInProject | null, isAssignee: boolean) =>
  role === 'ProjectManager' || (role === 'Member' && isAssignee);

/** Tự nhận / tự rút khỏi task. Viewer không có. */
export const canSelfAssign = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

export const canComment = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

/**
 * Ba action "cộng tác trên nội dung công việc" — `ManageTaskLabels` (ADR-037),
 * `ManageTaskLinks` (ADR-038), `UploadAttachment` (ADR-035).
 *
 * Backend gom cả ba vào **cùng một nhánh** với `CreateComment` trong
 * `ProjectPermissions.cs`: PM + Member ghi được, `Viewer` chỉ đọc. Vẫn tách thành ba hàm
 * chứ không dùng chung một `canCollaborate`: chúng chỉ TÌNH CỜ bằng nhau hôm nay, và gộp
 * lại thì lần đầu backend tách một trong ba ra sẽ phải sửa mọi nơi gọi.
 *
 * ⚠️ ĐỌC cả ba đều đi qua `ProjectAction.View` — `Viewer` vẫn xem nhãn, xem liên kết và
 * **tải được** file đính kèm. Đừng ẩn cả khối, chỉ ẩn nút ghi.
 */
export const canManageLabels = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

export const canManageTaskLinks = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

export const canUploadAttachment = (role: RoleInProject | null) =>
  role === 'ProjectManager' || role === 'Member';

/**
 * Theo dõi task — `ProjectAction.Watch` trả `true` cho **cả ba** vai trò.
 *
 * 🔴 `Viewer` theo dõi được. Đây là thao tác GHI duy nhất mà `Viewer` làm được (ADR-036),
 * và hợp lý vì nó chỉ ảnh hưởng hộp thông báo của chính họ. Đừng ẩn nút bằng một phép kiểm
 * `role !== 'Viewer'` chung chung — đó là lối tắt sai ở đúng chỗ này.
 *
 * `null` vẫn là không: chưa tải xong vai trò, hoặc chưa chấp nhận lời mời.
 */
export const canWatch = (role: RoleInProject | null) => role !== null;

/**
 * Xóa file đính kèm — luật **per-row**, không có `ProjectAction` riêng: người tải lên
 * HOẶC ProjectManager (`AttachmentService.DeleteAsync`), đúng khuôn xóa comment của ADR-026.
 */
export const canDeleteAttachment = (role: RoleInProject | null, isUploader: boolean) =>
  isUploader || role === 'ProjectManager';

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

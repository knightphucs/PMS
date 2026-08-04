import type { NotificationResponse } from '@/types/notification';

/**
 * Bấm vào thông báo thì đi đâu.
 *
 * 🔑 Đích đến quyết định bằng cặp `(relatedEntityKind, relatedEntityId)` — ADR-025.
 * `relatedEntityKind` là giá trị **suy ra** từ `type` ở phía backend, nên dựng một bảng
 * `type → route` đầy đủ ở đây là tạo bản sao thứ hai của cùng một luật: thêm một
 * `NotificationType` mới mà quên cập nhật bảng đó thì thông báo lặng lẽ không bấm được.
 *
 * ⚠️ Nhánh `Task` KHÔNG trỏ thẳng tới `/projects/{projectId}/tasks/{taskId}`: DTO thông báo
 * chỉ có `taskId`, không có `projectId`. `/tasks/{id}` là trang phân giải — nó hỏi API lấy
 * `projectId` rồi `replace` sang URL đầy đủ.
 */
export function notificationHref(notification: NotificationResponse): string | null {
  if (notification.relatedEntityId === null) return null;

  // ── Hai ngoại lệ, và chúng KHÔNG mâu thuẫn với ADR-025 ─────────────────────────────
  // Cặp (kind, id) nói *thông báo nói về cái gì*, không nói *người nhận có vào được không*.
  // Đúng hai loại trỏ tới một project mà người nhận CHẮC CHẮN không phải thành viên đã
  // chấp nhận, nên đi thẳng vào đó chỉ nhận 404 cố ý (ADR-006/019) — trông như lỗi.
  switch (notification.type) {
    case 'InvitedToProject':
      // Chưa chấp nhận thì chưa là thành viên. Chỗ hành động đúng là trang lời mời.
      return '/invitations';
    case 'RemovedFromProject':
      // Đã bị gỡ — không còn gì để mở. Thông báo vẫn đọc được, chỉ là không dẫn đi đâu.
      return null;
    default:
      break;
  }

  switch (notification.relatedEntityKind) {
    case 'Project':
      return `/projects/${notification.relatedEntityId}`;
    case 'Task':
      return `/tasks/${notification.relatedEntityId}`;
    case 'None':
      return null;
  }
}

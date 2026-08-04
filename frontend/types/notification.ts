import type { NotificationType, RelatedEntityKind } from './enums';

/**
 * Soi gương `PMS.Application/Features/Notifications/NotificationDtos.cs`.
 *
 * ⚠️ Cố ý KHÔNG có `employeeId`: endpoint chỉ bao giờ trả thông báo của chính người gọi,
 * nên trường đó không mang thông tin gì. Thông báo của người khác trả **404**, không phải
 * 403 — cùng nguyên tắc "không lộ sự tồn tại" của ADR-006/019.
 */
export interface NotificationResponse {
  id: string;
  type: NotificationType;
  /** Câu tiếng Việt do backend soạn sẵn — hiển thị nguyên văn, đừng ghép lại ở client. */
  content: string;
  isRead: boolean;
  relatedEntityId: string | null;
  /**
   * Suy ra từ `type` ở phía backend, không phải cột trong DB (ADR-025). Đây là **nguồn duy
   * nhất** để quyết định bấm vào thì đi đâu — `'None'` nghĩa là thông báo không dẫn đi đâu.
   */
  relatedEntityKind: RelatedEntityKind;
  createdAt: string;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

/**
 * Số dòng THỰC SỰ đổi. Gọi lần thứ hai trả `0` chứ không lỗi — thao tác idempotent
 * (ADR-024), nên `0` không phải tín hiệu thất bại.
 */
export interface MarkAllReadResponse {
  markedCount: number;
}

/** Bộ lọc của danh sách thông báo. `isRead` bỏ trống = lấy cả hai. */
export interface NotificationListRequest {
  page?: number;
  pageSize?: number;
  isRead?: boolean;
}

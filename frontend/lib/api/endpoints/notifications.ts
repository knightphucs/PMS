import type { PagedResult } from '@/types/common';
import type {
  MarkAllReadResponse,
  NotificationListRequest,
  NotificationResponse,
  UnreadCountResponse,
} from '@/types/notification';

import { apiFetch } from '../http';

/**
 * Thông báo là **ngoại lệ hợp lệ** của phân quyền project-scoped (ADR-023): không có
 * `projectId` ở bất kỳ đâu trong nhóm endpoint này, quyền được quyết bằng "người nhận là
 * chính người gọi" ngay trong repository.
 *
 * Mặc định backend sắp xếp `CreatedAt` giảm dần. `sortBy` chỉ nhận `createdAt`/`isRead`,
 * nên đừng bày ô sắp xếp tự do trên UI.
 */
export function listNotifications(request: NotificationListRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<NotificationResponse>>('/notifications', {
    // `isRead: undefined` bị `buildUrl` loại bỏ — đúng ý "không lọc", không phải "lọc false".
    query: { ...request },
    signal,
  });
}

export function getUnreadCount(signal?: AbortSignal) {
  return apiFetch<UnreadCountResponse>('/notifications/unread-count', { signal });
}

/** Đánh dấu một thông báo đã đọc. Gọi lại trên thông báo đã đọc vẫn **200**, không lỗi. */
export function markNotificationRead(id: string) {
  return apiFetch<NotificationResponse>(`/notifications/${id}/read`, { method: 'PATCH' });
}

/** Trả `markedCount` = số dòng thực sự đổi; lần gọi thứ hai ra `0` chứ không phải lỗi. */
export function markAllNotificationsRead() {
  return apiFetch<MarkAllReadResponse>('/notifications/read-all', { method: 'PATCH' });
}

import type { CreateTaskLinkRequest, TaskLinkResponse } from '@/types/task-link';

import { apiFetch } from '../http';

export function getTaskLinks(taskId: string, signal?: AbortSignal) {
  return apiFetch<TaskLinkResponse[]>(`/tasks/${taskId}/links`, { signal });
}

/**
 * Tạo liên kết. Bốn nhánh lỗi cần xử lý riêng ở UI:
 * - **400** — tự liên kết với chính nó, hoặc hai task khác project
 * - **409 "đã có liên kết"** — trùng, kể cả trùng NGỮ NGHĨA (`Blocks(A,B)` vs
 *   `IsBlockedBy(B,A)` là cùng một thứ sau chuẩn hóa — ADR-038)
 * - **409 "tạo ra vòng chặn"** — A chặn B mà B đã (gián tiếp) chặn A; cả hai sẽ không bao
 *   giờ vào được `InProgress`
 * - **404** — task đích không tồn tại, hoặc người gọi ngoài project
 */
export function createTaskLink(taskId: string, body: CreateTaskLinkRequest) {
  return apiFetch<TaskLinkResponse>(`/tasks/${taskId}/links`, { method: 'POST', body });
}

/** Route theo id của LIÊN KẾT, không lồng dưới task — một liên kết thuộc về cả hai task. */
export function deleteTaskLink(linkId: string) {
  return apiFetch<void>(`/task-links/${linkId}`, { method: 'DELETE' });
}

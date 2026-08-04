import type { CreateLabelRequest, LabelResponse, UpdateLabelRequest } from '@/types/label';

import { apiFetch } from '../http';

/** Nhãn toàn cục — dùng chung giữa mọi project. */
export function listLabels(signal?: AbortSignal) {
  return apiFetch<LabelResponse[]>('/labels', { signal });
}

/** Mọi user đã đăng nhập đều tạo được. Trùng tên → **409**. */
export function createLabel(body: CreateLabelRequest) {
  return apiFetch<LabelResponse>('/labels', { method: 'POST', body });
}

/** ⚠️ Chỉ `SystemAdmin` — người khác nhận **403** (ADR-037). */
export function updateLabel(id: string, body: UpdateLabelRequest) {
  return apiFetch<LabelResponse>(`/labels/${id}`, { method: 'PUT', body });
}

/** ⚠️ Chỉ `SystemAdmin`. Gỡ nhãn khỏi **mọi** task đang gắn nó, không chỉ project hiện tại. */
export function deleteLabel(id: string) {
  return apiFetch<void>(`/labels/${id}`, { method: 'DELETE' });
}

/**
 * Gắn nhãn vào task. **Idempotent** — gắn lại nhãn đã có trả 200 chứ không 409.
 * Trả về danh sách nhãn MỚI của task, dùng luôn để cập nhật cache.
 */
export function attachLabel(taskId: string, labelId: string) {
  return apiFetch<LabelResponse[]>(`/tasks/${taskId}/labels/${labelId}`, { method: 'POST' });
}

/** Gỡ nhãn khỏi task. Cũng idempotent. */
export function detachLabel(taskId: string, labelId: string) {
  return apiFetch<LabelResponse[]>(`/tasks/${taskId}/labels/${labelId}`, { method: 'DELETE' });
}

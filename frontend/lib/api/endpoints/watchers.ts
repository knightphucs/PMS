import type { WatcherResponse, WatchStateResponse } from '@/types/watcher';

import { apiFetch } from '../http';

export function getWatchers(taskId: string, signal?: AbortSignal) {
  return apiFetch<WatcherResponse[]>(`/tasks/${taskId}/watchers`, { signal });
}

/**
 * Tự theo dõi. Route là `/me` — **không nhận employeeId**, nên không ai ép người khác theo
 * dõi được. Ràng buộc nằm ở hình dạng route chứ không ở một dòng kiểm tra.
 *
 * Idempotent: gọi khi đã theo dõi vẫn trả 200 với trạng thái hiện tại.
 * `Viewer` cũng gọi được — đây là thao tác ghi duy nhất của vai trò đó (ADR-036).
 */
export function watchTask(taskId: string) {
  return apiFetch<WatchStateResponse>(`/tasks/${taskId}/watchers/me`, { method: 'POST' });
}

export function unwatchTask(taskId: string) {
  return apiFetch<WatchStateResponse>(`/tasks/${taskId}/watchers/me`, { method: 'DELETE' });
}

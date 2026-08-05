import type {
  CompleteSprintRequest,
  CreateSprintRequest,
  SprintCompletionPreview,
  SprintResponse,
  UpdateSprintRequest,
} from '@/types/sprint';

import { apiFetch } from '../http';

/** Mảng trần, KHÔNG phân trang — một project hiếm khi có quá vài chục sprint. */
export function listSprints(projectId: string, signal?: AbortSignal) {
  return apiFetch<SprintResponse[]>(`/projects/${projectId}/sprints`, { signal });
}

export function getSprint(id: string, signal?: AbortSignal) {
  return apiFetch<SprintResponse>(`/sprints/${id}`, { signal });
}

export function createSprint(projectId: string, body: CreateSprintRequest) {
  return apiFetch<SprintResponse>(`/projects/${projectId}/sprints`, { method: 'POST', body });
}

/** ⚠️ KHÔNG có `rowVersion` — sửa đồng thời là last-write-wins, không có tín hiệu nào. */
export function updateSprint(id: string, body: UpdateSprintRequest) {
  return apiFetch<SprintResponse>(`/sprints/${id}`, { method: 'PUT', body });
}

/**
 * Xóa mềm. Task của sprint được đẩy về **Backlog** chứ không bị xóa theo (ADR-020),
 * nên endpoint này **không bao giờ trả 409** — khác hẳn xóa project.
 */
export function deleteSprint(id: string) {
  return apiFetch<void>(`/sprints/${id}`, { method: 'DELETE' });
}

/**
 * Bắt đầu sprint (ADR-050). Idempotent — gọi lại trên sprint đang chạy trả 200.
 *
 * **409** khi project đã có sprint KHÁC đang chạy (tối đa một), hoặc sprint này đã đóng.
 */
export function startSprint(id: string) {
  return apiFetch<SprintResponse>(`/sprints/${id}/start`, { method: 'POST' });
}

/**
 * Xem trước việc đóng sprint — số task chưa xong và danh sách sprint đích hợp lệ.
 *
 * Gọi TRƯỚC khi mở dialog đóng: hỏi "task chưa xong đi đâu" mà không nói có bao nhiêu task
 * và đi được sang đâu thì người dùng không có cơ sở nào để chọn.
 */
export function previewSprintCompletion(id: string, signal?: AbortSignal) {
  return apiFetch<SprintCompletionPreview>(`/sprints/${id}/completion-preview`, { signal });
}

/**
 * Đóng sprint (ADR-050).
 *
 * **409** khi sprint chưa bắt đầu hoặc đã đóng · **400** khi sprint đích đã đóng hoặc trùng
 * chính nó · **404** khi sprint đích thuộc project khác.
 */
export function completeSprint(id: string, body: CompleteSprintRequest) {
  return apiFetch<SprintResponse>(`/sprints/${id}/complete`, { method: 'POST', body });
}

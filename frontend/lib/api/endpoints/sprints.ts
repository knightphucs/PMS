import type { CreateSprintRequest, SprintResponse, UpdateSprintRequest } from '@/types/sprint';

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

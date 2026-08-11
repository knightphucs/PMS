import type {
  CreateWorkItemTypeRequest,
  DeleteWorkItemTypeRequest,
  ReorderWorkItemTypesRequest,
  UpdateWorkItemTypeRequest,
  WorkItemTypeResponse,
} from '@/types/work-item-type';

import { apiFetch } from '../http';

/**
 * Loại công việc theo project (ADR-060).
 * Đọc mở cho mọi thành viên; mọi thao tác GHI đều PM-only.
 */
export function listWorkItemTypes(projectId: string, signal?: AbortSignal) {
  return apiFetch<WorkItemTypeResponse[]>(`/projects/${projectId}/work-item-types`, { signal });
}

/** **409** trùng tên loại · **404** gắn trường của project khác. */
export function createWorkItemType(projectId: string, body: CreateWorkItemTypeRequest) {
  return apiFetch<WorkItemTypeResponse>(`/projects/${projectId}/work-item-types`, {
    method: 'POST',
    body,
  });
}

export function updateWorkItemType(typeId: string, body: UpdateWorkItemTypeRequest) {
  return apiFetch<WorkItemTypeResponse>(`/work-item-types/${typeId}`, { method: 'PUT', body });
}

/**
 * Xoá loại.
 *
 * ⚠️ **DELETE có thân request** — khác thường nhưng cố ý, y hệt `DELETE /columns/{id}`:
 * loại còn task thì bắt buộc kèm `targetTypeId`.
 *
 * **400** còn task mà không chọn đích (thông điệp kèm số task) · **409** loại cuối cùng.
 */
export function deleteWorkItemType(typeId: string, body: DeleteWorkItemTypeRequest) {
  return apiFetch<void>(`/work-item-types/${typeId}`, { method: 'DELETE', body });
}

/** Gửi TRỌN danh sách theo thứ tự mới. */
export function reorderWorkItemTypes(projectId: string, body: ReorderWorkItemTypesRequest) {
  return apiFetch<void>(`/projects/${projectId}/work-item-types/order`, {
    method: 'PUT',
    body,
  });
}

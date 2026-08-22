import type { PagedResult } from '@/types/common';
import type {
  CreateSavedViewRequest,
  ReorderSavedViewsRequest,
  SavedViewResponse,
  TaskListItemResponse,
  TaskQueryRequest,
  UpdateSavedViewRequest,
} from '@/types/saved-view';

import { apiFetch } from '../http';

/**
 * View lưu được (ADR-061).
 *
 * Route chia hai nhóm giống `custom-fields.ts`: `/projects/{id}/views*` là KHAI BÁO view,
 * `/projects/{id}/tasks/query` là CHẠY một bộ lọc.
 */

/** View chia sẻ của dự án + view riêng của chính người gọi. Người ngoài project nhận 404. */
export function listSavedViews(projectId: string, signal?: AbortSignal) {
  return apiFetch<SavedViewResponse[]>(`/projects/${projectId}/views`, { signal });
}

/** **403** khi tạo view chia sẻ mà không phải PM · **409** trùng tên view của chính mình. */
export function createSavedView(projectId: string, body: CreateSavedViewRequest) {
  return apiFetch<SavedViewResponse>(`/projects/${projectId}/views`, { method: 'POST', body });
}

/**
 * ⚠️ **404** — không phải 403 — khi sửa view RIÊNG của người khác. Cố ý: 403 sẽ xác nhận
 * "view này có thật", đúng thứ danh sách đã cố ý không trả về.
 */
export function updateSavedView(viewId: string, body: UpdateSavedViewRequest) {
  return apiFetch<SavedViewResponse>(`/views/${viewId}`, { method: 'PUT', body });
}

export function deleteSavedView(viewId: string) {
  return apiFetch<void>(`/views/${viewId}`, { method: 'DELETE' });
}

/** Gửi TRỌN danh sách view đang thấy theo thứ tự mới — server từ chối danh sách thiếu. */
export function reorderSavedViews(projectId: string, body: ReorderSavedViewsRequest) {
  return apiFetch<void>(`/projects/${projectId}/views/order`, { method: 'PUT', body });
}

/**
 * Chạy một bộ lọc trên danh sách task.
 *
 * 🔴 **POST cho một thao tác ĐỌC — có chủ đích, không phải nhầm.** Bộ lọc là một danh sách
 * đối tượng (trường, toán tử, giá trị) × tối đa 20 dòng; nhồi vào query string sẽ cần một
 * cú pháp mã hoá tự chế mà cả hai đầu phải cùng hiểu.
 *
 * ⚠️ Hệ quả cần biết khi dùng: response của POST **không được trình duyệt cache**, và
 * `queryKey` phải chứa trọn `body` thì TanStack Query mới phân biệt được hai lượt lọc khác
 * nhau — xem `taskQueryKeys`.
 */
export function queryProjectTasks(
  projectId: string,
  body: TaskQueryRequest,
  signal?: AbortSignal,
) {
  return apiFetch<PagedResult<TaskListItemResponse>>(`/projects/${projectId}/tasks/query`, {
    method: 'POST',
    body,
    signal,
  });
}

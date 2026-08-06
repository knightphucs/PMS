import type { PagedRequest, PagedResult } from '@/types/common';
import type { MyWorkResponse } from '@/types/my-work';
import type {
  AssignTaskRequest,
  BoardResponse,
  CreateTaskRequest,
  TaskAssigneeResponse,
  TaskDetailResponse,
  TaskSummaryResponse,
  UpdateTaskRequest,
} from '@/types/task';

import { apiFetch } from '../http';

/**
 * Board và Backlog nằm trên `TasksController` (`[Route("api/v1")]`) chứ KHÔNG phải
 * `ProjectsController`, dù đường dẫn bắt đầu bằng `/projects/`.
 */

/**
 * Board Kanban.
 *
 * ⚠️ Bỏ `sprintId` KHÔNG cho ra "board của backlog": backend rơi xuống
 * `GetRootTasksByProjectAsync(projectId)`, tức **tất cả** task gốc kể cả task đang
 * thuộc sprint khác. Nhãn đúng cho lựa chọn đó là "Tất cả task".
 *
 * `sprintId` của project khác → **400**.
 */
export function getBoard(projectId: string, sprintId: string | null, signal?: AbortSignal) {
  return apiFetch<BoardResponse>(`/projects/${projectId}/board`, {
    // `undefined` bị `buildUrl` loại bỏ, nên không gửi `sprintId=` rỗng.
    query: { sprintId: sprintId ?? undefined },
    signal,
  });
}

/** Mảng trần, KHÔNG phân trang. `sprintId == null && parentTaskId == null`, xếp theo Priority. */
export function getBacklog(projectId: string, signal?: AbortSignal) {
  return apiFetch<TaskSummaryResponse[]>(`/projects/${projectId}/backlog`, { signal });
}

/** Chỉ trả task GỐC (`parentTaskId == null`). sortBy nhận: name | priority | status. */
export function listProjectTasks(projectId: string, params: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<TaskSummaryResponse>>(`/projects/${projectId}/tasks`, {
    query: { ...params },
    signal,
  });
}

export function getTask(id: string, signal?: AbortSignal) {
  return apiFetch<TaskDetailResponse>(`/tasks/${id}`, { signal });
}

/** Task mới LUÔN rơi vào cột TRÁI NHẤT của project — request không có trường cột (ADR-052). */
export function createTask(body: CreateTaskRequest) {
  return apiFetch<TaskSummaryResponse>('/tasks', { method: 'POST', body });
}

/** ⚠️ Endpoint DUY NHẤT của Task cần `rowVersion`. 409 = người khác vừa sửa. */
export function updateTask(id: string, body: UpdateTaskRequest) {
  return apiFetch<TaskDetailResponse>(`/tasks/${id}`, { method: 'PUT', body });
}

/** Xóa mềm. **409** nếu còn subtask chưa `Done`. */
export function deleteTask(id: string) {
  return apiFetch<void>(`/tasks/${id}`, { method: 'DELETE' });
}

/**
 * Chuyển task sang cột khác. **KHÔNG** cần `rowVersion` (ADR-021).
 *
 * 🔄 Đổi theo ADR-052 — hai điều từng đúng nay không còn:
 * - Không còn "bước chuyển không hợp lệ": mọi cột đều tới thẳng được.
 * - Gửi đúng cột task đang đứng nay trả **200** (no-op), không còn 409.
 *
 * Còn lại:
 * - **409** khi task đang bị `TaskLink` chặn và cột đích thuộc **nhóm** `InProgress` —
 *   trường hợp duy nhất client không đoán trước được (xem `mayFailUnpredictably`).
 * - **404** khi cột đích thuộc project khác (ADR-019: không xác nhận nó có tồn tại).
 * - **403** khi người gọi không phải assignee và cũng không phải PM (ADR-017).
 */
export function changeTaskStatus(id: string, targetColumnId: string) {
  return apiFetch<TaskSummaryResponse>(`/tasks/${id}/status`, {
    method: 'PATCH',
    body: { targetColumnId },
  });
}

/**
 * Ghim/gỡ ghim — task ghim luôn đứng đầu cột trên board, cho MỌI người xem project.
 * Cùng quyền với sửa task (PM). KHÔNG cần `rowVersion`.
 */
export function pinTask(id: string, pinned: boolean) {
  return apiFetch<TaskSummaryResponse>(`/tasks/${id}/pin`, {
    method: 'PATCH',
    body: { pinned },
  });
}

/** Chuyển task sang sprint khác, `null` = về Backlog. KHÔNG cần `rowVersion`. */
export function moveTaskToSprint(id: string, sprintId: string | null) {
  return apiFetch<TaskSummaryResponse>(`/tasks/${id}/sprint`, {
    method: 'PUT',
    // Gửi `null` THẬT, không phải chuỗi "null" hay bỏ trống trường.
    body: { sprintId },
  });
}

export function getTaskAssignees(id: string, signal?: AbortSignal) {
  return apiFetch<TaskAssigneeResponse[]>(`/tasks/${id}/assignees`, { signal });
}

/** Gán NGƯỜI KHÁC — chỉ ProjectManager. Trả **200**, không phải 201. */
export function assignTask(id: string, body: AssignTaskRequest) {
  return apiFetch<TaskAssigneeResponse>(`/tasks/${id}/assignees`, { method: 'POST', body });
}

/** Tự nhận việc. Không body. Chỉ được khi task đang ở `ToDo` và chưa ai nhận. */
export function selfAssignTask(id: string) {
  return apiFetch<TaskAssigneeResponse>(`/tasks/${id}/assignees/me`, { method: 'POST' });
}

/** Gỡ người khỏi task. Tự rút thì không cần PM duyệt. */
export function unassignTask(id: string, employeeId: string) {
  return apiFetch<void>(`/tasks/${id}/assignees/${employeeId}`, { method: 'DELETE' });
}

/**
 * Việc của CHÍNH người đang đăng nhập, xuyên mọi dự án, gom sẵn theo dự án (ADR-053).
 *
 * ⚠️ Endpoint này **không nhận `employeeId` ở đâu cả** — kể cả query string. Nhận vào là
 * biến nó thành đường xem lịch làm việc của người khác.
 *
 * Server lọc: được gán cho tôi · chưa thuộc cột nhóm `Done` · có hạn ≤ hôm nay (gồm quá hạn).
 */
export function getMyWork(signal?: AbortSignal) {
  return apiFetch<MyWorkResponse>('/tasks/my', { signal });
}

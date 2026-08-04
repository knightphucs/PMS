'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  assignTask,
  createTask,
  deleteTask,
  getBacklog,
  getTask,
  listProjectTasks,
  moveTaskToSprint,
  selfAssignTask,
  unassignTask,
  updateTask,
} from '@/lib/api/endpoints/tasks';
import { backlogKeys, projectDataKeys, taskKeys } from '@/lib/hooks/keys';
import type { AssignTaskRequest, CreateTaskRequest, UpdateTaskRequest } from '@/types/task';

export function useBacklog(projectId: string) {
  return useQuery({
    queryKey: backlogKeys.all(projectId),
    queryFn: ({ signal }) => getBacklog(projectId, signal),
  });
}

/**
 * Chi tiết task — nguồn DUY NHẤT của `rowVersion`.
 *
 * `staleTime: 0` / `gcTime: 0` giống hệt `useProject`: mỗi lần mở form sửa là một lượt
 * đọc mới. `rowVersion` cũ nằm lại trong cache chính là thứ sinh ra 409.
 */
export function useTask(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskKeys.detail(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getTask(taskId!, signal),
    enabled: taskId !== null,
    staleTime: 0,
    gcTime: 0,
  });
}

/** Số task tối đa nạp cho ô chọn — trùng trần `pageSize` mà backend kẹp lại (100). */
export const TASK_OPTIONS_LIMIT = 100;

/**
 * Danh sách task để CHỌN (ô "liên kết tới task nào").
 *
 * ⚠️ Lọc ở phía CLIENT, không gửi `search` lên. `PagedRequest.Search` được model binder
 * nhận nhưng `TaskRepository` **không dùng tới** — gửi lên chỉ tạo cảm giác đang lọc phía
 * server trong khi thực ra không. Đổi lại phải chấp nhận trần 100 task; project vượt mức
 * đó thì đây là chỗ cần một endpoint tìm kiếm thật (§B "Search toàn cục" vẫn còn ⬜).
 *
 * Khóa cache riêng, KHÔNG dùng `taskKeys.detail`: dữ liệu ở đây là `TaskSummaryResponse`
 * (không có `rowVersion`) nên không có gì để gieo nhầm cho form sửa.
 */
export function useProjectTaskOptions(projectId: string, enabled: boolean) {
  return useQuery({
    queryKey: [...taskKeys.all(projectId), 'options'],
    queryFn: ({ signal }) =>
      listProjectTasks(projectId, { page: 1, pageSize: TASK_OPTIONS_LIMIT }, signal),
    enabled,
    staleTime: 30_000,
  });
}

/**
 * Đọc GHÉ chi tiết task đang có trong cache — cho breadcrumb, nơi cần `code` chứ không
 * cần một lượt tải riêng.
 *
 * `enabled: false` nên hook này KHÔNG BAO GIỜ tự fetch: nó chỉ quan sát khóa mà
 * `TaskDetailContent` đã mount. Không có dữ liệu thì breadcrumb hiện nhãn chung.
 *
 * 🔴 `staleTime`/`gcTime: 0` PHẢI lặp lại ở đây. Trong TanStack v5, `gcTime` hiệu lực của
 * một query là giá trị LỚN NHẤT trong các observer đang mount. Quên dòng đó là mục cache
 * sống thêm 5 phút sau khi đóng dialog — dựng lại đúng bug `rowVersion` cũ → 409 vĩnh
 * viễn mà `useTask` ở trên đang chặn.
 */
export function useTaskCached(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskKeys.detail(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getTask(taskId!, signal),
    enabled: false,
    staleTime: 0,
    gcTime: 0,
  });
}

export function useCreateTask(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTaskRequest) => createTask(body),
    onSuccess: () => {
      // Task mới luôn ở `ToDo`, nhưng nó rơi vào backlog hay vào board của sprint là tùy
      // `sprintId` — làm mới cả nhánh thay vì đoán.
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

export function useUpdateTask(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: UpdateTaskRequest) => updateTask(taskId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
    onError: () => {
      // Kể cả khi hỏng vẫn bỏ chi tiết đang cache. Ca 409 quan trọng nhất: giữ lại nghĩa
      // là lần thử sau gửi lại đúng `rowVersion` hỏng đó và 409 vĩnh viễn.
      void queryClient.invalidateQueries({ queryKey: taskKeys.detail(projectId, taskId) });
    },
  });
}

/** Xóa mềm. **409** nếu còn subtask chưa `Done`. */
export function useDeleteTask(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (taskId: string) => deleteTask(taskId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

/**
 * Chuyển task giữa Backlog và Sprint.
 *
 * **Cố ý KHÔNG cập nhật lạc quan**, khác hẳn `useChangeTaskStatus`. Lạc quan đáng giá khi
 * mắt người dùng đang bám theo một vật thể di chuyển (thẻ Kanban). Một dòng biến mất khỏi
 * bảng sau cú bấm menu không tạo ra kỳ vọng đó, trong khi bề mặt rollback lại gấp ba số
 * cache (backlog + hai board + taskCount của hai sprint). Dùng pending state theo dòng.
 */
export function useMoveTaskToSprint(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, sprintId }: { taskId: string; sprintId: string | null }) =>
      moveTaskToSprint(taskId, sprintId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

export function useAssignTask(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, ...body }: AssignTaskRequest & { taskId: string }) =>
      assignTask(taskId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

export function useSelfAssignTask(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (taskId: string) => selfAssignTask(taskId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

export function useUnassignTask(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, employeeId }: { taskId: string; employeeId: string }) =>
      unassignTask(taskId, employeeId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

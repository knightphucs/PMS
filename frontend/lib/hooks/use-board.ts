'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { changeTaskStatus, getBoard, pinTask } from '@/lib/api/endpoints/tasks';
import { boardKeys, projectDataKeys } from '@/lib/hooks/keys';
import { moveTaskInBoard, patchTaskInBoard, pinTaskInBoard } from '@/lib/tasks/board-cache';
import type { BoardResponse } from '@/types/task';

/**
 * Board của project, lọc theo sprint.
 *
 * ⚠️ `sprintId = null` KHÔNG phải "không nạp" — nó là board **"Tất cả task"** của project.
 * Muốn hoãn việc nạp (ví dụ danh sách sprint chỉ nạp task cho sprint đang mở) thì dùng
 * `enabled`, đừng truyền `null`: hai thứ đó nghĩa hoàn toàn khác nhau và nhầm chúng sẽ nạp
 * cả project trong khi ta chỉ muốn một sprint.
 */
export function useBoard(
  projectId: string,
  sprintId: string | null,
  options?: { enabled?: boolean },
) {
  return useQuery({
    queryKey: boardKeys.detail(projectId, sprintId),
    queryFn: ({ signal }) => getBoard(projectId, sprintId, signal),
    enabled: options?.enabled ?? true,
  });
}

/**
 * Đổi trạng thái task kèm **cập nhật lạc quan**.
 *
 * Thẻ phải nhảy cột NGAY rồi mới gọi API; hỏng thì trả về chỗ cũ. Chờ round-trip mới di
 * chuyển là cảm giác chậm chạp điển hình của app sinh viên.
 *
 * Hook KHÔNG toast (giữ quy ước của `use-projects.ts`) — component toast sau
 * `mutateAsync`, vì chỉ nó mới biết nên nói gì trong ngữ cảnh của mình.
 */
export function useChangeTaskStatus(projectId: string, sprintId: string | null) {
  const queryClient = useQueryClient();
  const key = boardKeys.detail(projectId, sprintId);

  return useMutation({
    mutationFn: ({ taskId, targetColumnId }: { taskId: string; targetColumnId: string }) =>
      changeTaskStatus(taskId, targetColumnId),

    onMutate: async ({ taskId, targetColumnId }) => {
      // 🔴 BẮT BUỘC. Một lượt GET board phát đi TRƯỚC lúc kéo mà về SAU `setQueryData` sẽ
      // ghi đè bản lạc quan: thẻ nhảy sang cột mới rồi tự quay về chỗ cũ vài trăm ms sau,
      // không có lỗi nào hiện ra. Đây là loại bug chỉ tái hiện được khi mạng chậm.
      await queryClient.cancelQueries({ queryKey: key });

      const previous = queryClient.getQueryData<BoardResponse>(key);
      queryClient.setQueryData<BoardResponse>(key, (old) =>
        old ? moveTaskInBoard(old, taskId, targetColumnId) : old,
      );

      return { previous };
    },

    onSuccess: (summary) => {
      // Server trả về `TaskSummaryResponse` đầy đủ — vá tại chỗ thay vì refetch, để board
      // không nháy sau mỗi lần kéo.
      queryClient.setQueryData<BoardResponse>(key, (old) =>
        old ? patchTaskInBoard(old, summary) : old,
      );
    },

    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(key, context.previous);
    },

    onSettled: () => {
      // Cùng một task xuất hiện trên cả board "tất cả task" LẪN board của sprint chứa nó,
      // nên làm mới cả nhánh chứ không chỉ khóa đang xem.
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

/**
 * Ghim/gỡ ghim, cùng khuôn cập nhật lạc quan với {@link useChangeTaskStatus}.
 *
 * Khác drag–drop ở một điểm: `pinTaskInBoard` không chỉ vá trường `isPinned` mà còn SẮP
 * LẠI cột đó ngay lập tức — cả điểm của việc ghim là thấy thẻ nhảy lên đầu tức thì, không
 * phải đợi round-trip rồi mới re-sort.
 */
export function usePinTask(projectId: string, sprintId: string | null) {
  const queryClient = useQueryClient();
  const key = boardKeys.detail(projectId, sprintId);

  return useMutation({
    mutationFn: ({ taskId, pinned }: { taskId: string; pinned: boolean }) =>
      pinTask(taskId, pinned),

    onMutate: async ({ taskId, pinned }) => {
      await queryClient.cancelQueries({ queryKey: key });

      const previous = queryClient.getQueryData<BoardResponse>(key);
      queryClient.setQueryData<BoardResponse>(key, (old) =>
        old ? pinTaskInBoard(old, taskId, pinned) : old,
      );

      return { previous };
    },

    onSuccess: (summary) => {
      // Vị trí đã đúng từ `onMutate` — chỉ cần vá lại các trường tính sẵn từ server,
      // KHÔNG gọi `pinTaskInBoard` lần hai (sẽ sort thừa một lượt vô hại nhưng thừa).
      queryClient.setQueryData<BoardResponse>(key, (old) =>
        old ? patchTaskInBoard(old, summary) : old,
      );
    },

    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(key, context.previous);
    },

    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

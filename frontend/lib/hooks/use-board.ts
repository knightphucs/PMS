'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { changeTaskStatus, getBoard } from '@/lib/api/endpoints/tasks';
import { boardKeys, projectDataKeys } from '@/lib/hooks/keys';
import { moveTaskInBoard, patchTaskInBoard } from '@/lib/tasks/board-cache';
import type { Status } from '@/types/enums';
import type { BoardResponse } from '@/types/task';

export function useBoard(projectId: string, sprintId: string | null) {
  return useQuery({
    queryKey: boardKeys.detail(projectId, sprintId),
    queryFn: ({ signal }) => getBoard(projectId, sprintId, signal),
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
    mutationFn: ({ taskId, target }: { taskId: string; target: Status }) =>
      changeTaskStatus(taskId, target),

    onMutate: async ({ taskId, target }) => {
      // 🔴 BẮT BUỘC. Một lượt GET board phát đi TRƯỚC lúc kéo mà về SAU `setQueryData` sẽ
      // ghi đè bản lạc quan: thẻ nhảy sang cột mới rồi tự quay về chỗ cũ vài trăm ms sau,
      // không có lỗi nào hiện ra. Đây là loại bug chỉ tái hiện được khi mạng chậm.
      await queryClient.cancelQueries({ queryKey: key });

      const previous = queryClient.getQueryData<BoardResponse>(key);
      queryClient.setQueryData<BoardResponse>(key, (old) =>
        old ? moveTaskInBoard(old, taskId, target) : old,
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

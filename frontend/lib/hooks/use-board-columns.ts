'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createBoardColumn,
  deleteBoardColumn,
  listBoardColumns,
  reorderBoardColumns,
  updateBoardColumn,
} from '@/lib/api/endpoints/board-columns';
import { boardColumnKeys, projectDataKeys } from '@/lib/hooks/keys';
import type {
  CreateBoardColumnRequest,
  DeleteBoardColumnRequest,
  ReorderBoardColumnsRequest,
  UpdateBoardColumnRequest,
} from '@/types/task';

/**
 * Danh sách cột của project (ADR-052).
 *
 * `staleTime` dài: cấu hình board gần như không đổi trong một phiên làm việc, mà hook này
 * được mount ở nhiều chỗ cùng lúc (ô chọn trạng thái ở chi tiết task, dialog quản lý cột,
 * bộ lọc backlog). Không có nó thì mỗi lần mở một dialog là một request thừa.
 */
export function useBoardColumns(projectId: string | null) {
  return useQuery({
    queryKey: boardColumnKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listBoardColumns(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

/**
 * Mọi mutation về cột đều làm mới `projectDataKeys.all`, KHÔNG chỉ khóa danh sách cột.
 *
 * 🔴 Lý do: đổi cấu hình cột chạm tới board (số cột, tên, màu), backlog (chip trạng thái),
 * chi tiết task (ô chọn), và thống kê (trục biểu đồ). Làm mới hẹp sẽ để lại một board vẽ
 * theo cột cũ bên cạnh một ô chọn theo cột mới — và người dùng không có cách nào biết cái
 * nào đúng.
 */
function useColumnInvalidation(projectId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: boardColumnKeys.all(projectId) });
    void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
  };
}

export function useCreateBoardColumn(projectId: string) {
  const invalidate = useColumnInvalidation(projectId);

  return useMutation({
    mutationFn: (body: CreateBoardColumnRequest) => createBoardColumn(projectId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateBoardColumn(projectId: string) {
  const invalidate = useColumnInvalidation(projectId);

  return useMutation({
    mutationFn: ({ columnId, body }: { columnId: string; body: UpdateBoardColumnRequest }) =>
      updateBoardColumn(columnId, body),
    onSuccess: invalidate,
  });
}

export function useDeleteBoardColumn(projectId: string) {
  const invalidate = useColumnInvalidation(projectId);

  return useMutation({
    mutationFn: ({ columnId, body }: { columnId: string; body: DeleteBoardColumnRequest }) =>
      deleteBoardColumn(columnId, body),
    // `onSettled` chứ không `onSuccess`: ca 400 "còn N task, hãy chọn cột đích" là lúc số
    // task trong cột đang được hiển thị, và con số đó phải đúng ở lần thử kế tiếp.
    onSettled: invalidate,
  });
}

export function useReorderBoardColumns(projectId: string) {
  const invalidate = useColumnInvalidation(projectId);

  return useMutation({
    mutationFn: (body: ReorderBoardColumnsRequest) => reorderBoardColumns(projectId, body),
    onSuccess: invalidate,
  });
}

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createWorkItemType,
  deleteWorkItemType,
  listWorkItemTypes,
  reorderWorkItemTypes,
  updateWorkItemType,
} from '@/lib/api/endpoints/work-item-types';
import { projectDataKeys, workItemTypeKeys } from '@/lib/hooks/keys';
import type {
  CreateWorkItemTypeRequest,
  DeleteWorkItemTypeRequest,
  ReorderWorkItemTypesRequest,
  UpdateWorkItemTypeRequest,
} from '@/types/work-item-type';

/**
 * Loại công việc của project (ADR-060).
 *
 * `staleTime` dài cùng lý do `useBoardColumns`/`useFieldDefinitions`: danh sách loại gần như
 * không đổi trong một phiên, mà hook mount ở nhiều chỗ (ô chọn loại ở form task, dialog quản
 * lý, chip trên thẻ).
 */
export function useWorkItemTypes(projectId: string | null) {
  return useQuery({
    queryKey: workItemTypeKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listWorkItemTypes(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

/**
 * 🔴 Đổi loại chạm tới nhiều thứ hơn vẻ ngoài: chip trên thẻ board/backlog, chi tiết task,
 * VÀ tập trường tuỳ biến hiện ra trên từng task (ADR-060 lọc field-values theo loại).
 * Dọn hẹp sẽ để lại một khối trường vẽ theo loại cũ bên cạnh một chip đã đổi.
 */
function useTypeInvalidation(projectId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: workItemTypeKeys.all(projectId) });
    void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
  };
}

export function useCreateWorkItemType(projectId: string) {
  const invalidate = useTypeInvalidation(projectId);
  return useMutation({
    mutationFn: (body: CreateWorkItemTypeRequest) => createWorkItemType(projectId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateWorkItemType(projectId: string) {
  const invalidate = useTypeInvalidation(projectId);
  return useMutation({
    mutationFn: ({ typeId, body }: { typeId: string; body: UpdateWorkItemTypeRequest }) =>
      updateWorkItemType(typeId, body),
    onSuccess: invalidate,
  });
}

export function useDeleteWorkItemType(projectId: string) {
  const invalidate = useTypeInvalidation(projectId);
  return useMutation({
    mutationFn: ({ typeId, body }: { typeId: string; body: DeleteWorkItemTypeRequest }) =>
      deleteWorkItemType(typeId, body),
    // `onSettled` chứ không `onSuccess`: ca 400 "còn N task, hãy chọn loại đích" là lúc số
    // task đang được hiển thị, và con số đó phải đúng ở lần thử kế tiếp.
    onSettled: invalidate,
  });
}

export function useReorderWorkItemTypes(projectId: string) {
  const invalidate = useTypeInvalidation(projectId);
  return useMutation({
    mutationFn: (body: ReorderWorkItemTypesRequest) => reorderWorkItemTypes(projectId, body),
    onSuccess: invalidate,
  });
}

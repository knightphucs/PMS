'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createFieldDefinition,
  deleteFieldDefinition,
  listFieldDefinitions,
  listFieldValues,
  reorderFieldDefinitions,
  setFieldValues,
  updateFieldDefinition,
} from '@/lib/api/endpoints/custom-fields';
import { fieldDefinitionKeys, taskDetailKeys } from '@/lib/hooks/keys';
import type {
  CreateFieldDefinitionRequest,
  ReorderFieldDefinitionsRequest,
  SetFieldValuesRequest,
  UpdateFieldDefinitionRequest,
} from '@/types/custom-field';

/**
 * Lược đồ trường tuỳ biến của project (ADR-059).
 *
 * `staleTime` dài cùng lý do `useBoardColumns`: lược đồ gần như không đổi trong một phiên
 * làm việc, mà hook này mount ở nhiều chỗ (dialog quản lý, khối trường trên mỗi task đang
 * mở). Không có nó thì mỗi lần mở một task là một request thừa.
 */
export function useFieldDefinitions(projectId: string | null) {
  return useQuery({
    queryKey: fieldDefinitionKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listFieldDefinitions(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

/**
 * 🔴 Mọi mutation về LƯỢC ĐỒ phải dọn cả giá trị trên các task đang mở, không chỉ khoá
 * lược đồ.
 *
 * Xoá một trường làm mọi `field-values` đang cache mang theo một trường không còn tồn tại;
 * đổi tên trường thì cache cũ hiển thị tên cũ. Dọn hẹp sẽ để lại một dialog quản lý đã cập
 * nhật bên cạnh một khối trường trên task vẫn vẽ theo lược đồ cũ — và người dùng không có
 * cách nào biết cái nào đúng. Cùng lý lẽ `useColumnInvalidation` của ADR-052.
 */
function useFieldSchemaInvalidation(projectId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: fieldDefinitionKeys.all(projectId) });
    // Khớp tiền tố: dọn field-values của MỌI task trong project đang nằm trong cache.
    void queryClient.invalidateQueries({
      predicate: (query) =>
        query.queryKey.includes(projectId) && query.queryKey.includes('field-values'),
    });
  };
}

export function useCreateFieldDefinition(projectId: string) {
  const invalidate = useFieldSchemaInvalidation(projectId);

  return useMutation({
    mutationFn: (body: CreateFieldDefinitionRequest) => createFieldDefinition(projectId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateFieldDefinition(projectId: string) {
  const invalidate = useFieldSchemaInvalidation(projectId);

  return useMutation({
    mutationFn: ({ fieldId, body }: { fieldId: string; body: UpdateFieldDefinitionRequest }) =>
      updateFieldDefinition(fieldId, body),
    onSuccess: invalidate,
  });
}

export function useDeleteFieldDefinition(projectId: string) {
  const invalidate = useFieldSchemaInvalidation(projectId);

  return useMutation({
    mutationFn: (fieldId: string) => deleteFieldDefinition(fieldId),
    onSettled: invalidate,
  });
}

export function useReorderFieldDefinitions(projectId: string) {
  const invalidate = useFieldSchemaInvalidation(projectId);

  return useMutation({
    mutationFn: (body: ReorderFieldDefinitionsRequest) =>
      reorderFieldDefinitions(projectId, body),
    onSuccess: invalidate,
  });
}

/** Giá trị trên MỘT task — trả về mọi trường của project, kể cả trường chưa điền. */
export function useFieldValues(projectId: string, taskId: string) {
  return useQuery({
    queryKey: taskDetailKeys.fieldValues(projectId, taskId),
    queryFn: ({ signal }) => listFieldValues(taskId, signal),
  });
}

export function useSetFieldValues(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: SetFieldValuesRequest) => setFieldValues(taskId, body),
    // Ghi thẳng phản hồi vào cache thay vì invalidate: server đã trả về trạng thái đầy đủ
    // sau khi ghi, nên một request nữa chỉ để đọc lại đúng thứ vừa nhận là thừa — và khoảng
    // trống giữa hai request là lúc ô nhập nhấp nháy về giá trị cũ.
    onSuccess: (data) =>
      queryClient.setQueryData(taskDetailKeys.fieldValues(projectId, taskId), data),
  });
}

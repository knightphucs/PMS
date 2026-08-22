'use client';

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createSavedView,
  deleteSavedView,
  listSavedViews,
  queryProjectTasks,
  reorderSavedViews,
  updateSavedView,
} from '@/lib/api/endpoints/saved-views';
import { savedViewKeys, taskQueryKeys } from '@/lib/hooks/keys';
import type {
  CreateSavedViewRequest,
  ReorderSavedViewsRequest,
  TaskQueryRequest,
  UpdateSavedViewRequest,
} from '@/types/saved-view';

/**
 * Danh sách view của project (ADR-061).
 *
 * `staleTime` dài cùng lý do `useFieldDefinitions`/`useBoardColumns`: danh sách view gần
 * như không đổi trong một phiên, mà hook này mount ở thanh view của màn danh sách.
 */
export function useSavedViews(projectId: string | null) {
  return useQuery({
    queryKey: savedViewKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listSavedViews(projectId!, signal),
    enabled: projectId !== null,
    staleTime: 5 * 60_000,
  });
}

function useSavedViewInvalidation(projectId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: savedViewKeys.all(projectId) });
    // Đổi lược đồ view làm cũ mọi kết quả đã chạy: sửa bộ lọc của view đang mở mà không
    // dọn thì bảng vẫn hiện kết quả của bộ lọc cũ bên dưới một cái tên đã đổi.
    void queryClient.invalidateQueries({ queryKey: taskQueryKeys.all(projectId) });
  };
}

export function useCreateSavedView(projectId: string) {
  const invalidate = useSavedViewInvalidation(projectId);

  return useMutation({
    mutationFn: (body: CreateSavedViewRequest) => createSavedView(projectId, body),
    onSuccess: invalidate,
  });
}

export function useUpdateSavedView(projectId: string) {
  const invalidate = useSavedViewInvalidation(projectId);

  return useMutation({
    mutationFn: ({ viewId, body }: { viewId: string; body: UpdateSavedViewRequest }) =>
      updateSavedView(viewId, body),
    onSuccess: invalidate,
  });
}

export function useDeleteSavedView(projectId: string) {
  const invalidate = useSavedViewInvalidation(projectId);

  return useMutation({
    mutationFn: (viewId: string) => deleteSavedView(viewId),
    // `onSettled` chứ không `onSuccess`: xoá hỏng (409/404) cũng cần đọc lại danh sách để
    // biết trạng thái thật — cùng khuôn `useDeleteFieldDefinition`.
    onSettled: invalidate,
  });
}

export function useReorderSavedViews(projectId: string) {
  const invalidate = useSavedViewInvalidation(projectId);

  return useMutation({
    mutationFn: (body: ReorderSavedViewsRequest) => reorderSavedViews(projectId, body),
    onSuccess: invalidate,
  });
}

/**
 * Chạy một bộ lọc.
 *
 * `placeholderData: keepPreviousData` — đổi trang hoặc sửa bộ lọc thì bảng cũ ở nguyên và
 * mờ đi thay vì nháy về skeleton. Không có nó, mỗi lần gõ vào ô tìm kiếm là một lần bảng
 * biến mất rồi hiện lại.
 */
export function useTaskQuery(projectId: string, request: TaskQueryRequest, enabled = true) {
  return useQuery({
    queryKey: taskQueryKeys.run(projectId, request),
    queryFn: ({ signal }) => queryProjectTasks(projectId, request, signal),
    enabled,
    placeholderData: keepPreviousData,
  });
}

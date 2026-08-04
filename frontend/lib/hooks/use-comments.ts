'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createComment,
  deleteComment,
  getTaskComments,
  updateComment,
} from '@/lib/api/endpoints/comments';
import { taskDetailKeys } from '@/lib/hooks/keys';
import type { CreateCommentRequest, UpdateCommentRequest } from '@/types/comment';
import type { PagedRequest } from '@/types/common';

export function useTaskComments(projectId: string, taskId: string | null, request: PagedRequest) {
  return useQuery({
    queryKey: [...taskDetailKeys.comments(projectId, taskId ?? ''), request],
    queryFn: ({ signal }) => getTaskComments(taskId!, request, signal),
    enabled: taskId !== null,
  });
}

export function useCreateComment(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateCommentRequest) => createComment(taskId, body),
    onSuccess: () => invalidate(queryClient, projectId, taskId),
  });
}

/** ⚠️ Chỉ tác giả gọi được — PM cũng 403. Gác nút bằng `canEditComment(isAuthor)` (ADR-026). */
export function useUpdateComment(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCommentRequest }) =>
      updateComment(id, body),
    onSuccess: () => invalidate(queryClient, projectId, taskId),
  });
}

/** Tác giả HOẶC ProjectManager. Xóa CỨNG — dùng `ConfirmDialog`, không khôi phục được. */
export function useDeleteComment(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteComment(id),
    onSuccess: () => invalidate(queryClient, projectId, taskId),
  });
}

/**
 * Comment sinh `ActivityLog` (`Commented`/`CommentUpdated`/`CommentDeleted` — ADR-026), nên
 * mọi thao tác comment cũng làm cũ tab Lịch sử. Gom vào một chỗ để ba mutation không lệch nhau.
 */
function invalidate(
  queryClient: ReturnType<typeof useQueryClient>,
  projectId: string,
  taskId: string,
) {
  void queryClient.invalidateQueries({
    queryKey: taskDetailKeys.comments(projectId, taskId),
  });
  void queryClient.invalidateQueries({
    queryKey: taskDetailKeys.activity(projectId, taskId),
  });
}

import type {
  CommentResponse,
  CreateCommentRequest,
  UpdateCommentRequest,
} from '@/types/comment';
import type { PagedRequest, PagedResult } from '@/types/common';

import { apiFetch } from '../http';

export function getTaskComments(taskId: string, request: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<CommentResponse>>(`/tasks/${taskId}/comments`, {
    query: { ...request },
    signal,
  });
}

/** Viết comment: `ProjectManager` + `Member`. `Viewer` chỉ đọc → 403 (ADR-026). */
export function createComment(taskId: string, body: CreateCommentRequest) {
  return apiFetch<CommentResponse>(`/tasks/${taskId}/comments`, { method: 'POST', body });
}

/**
 * ⚠️ Sửa = **CHỈ tác giả**, PM cũng không. Dùng `canEditComment(isAuthor)` để ẩn/hiện nút,
 * đừng đoán từ vai trò trong project.
 */
export function updateComment(id: string, body: UpdateCommentRequest) {
  return apiFetch<CommentResponse>(`/comments/${id}`, { method: 'PUT', body });
}

/** Xóa = tác giả **HOẶC** ProjectManager. Xóa CỨNG — không khôi phục được. */
export function deleteComment(id: string) {
  return apiFetch<void>(`/comments/${id}`, { method: 'DELETE' });
}

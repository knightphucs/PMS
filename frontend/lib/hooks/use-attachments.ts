'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  deleteAttachment,
  getProjectAttachments,
  getTaskAttachments,
  uploadProjectAttachment,
  uploadTaskAttachment,
} from '@/lib/api/endpoints/attachments';
import { projectAttachmentKeys, taskDetailKeys } from '@/lib/hooks/keys';

export function useTaskAttachments(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskDetailKeys.attachments(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getTaskAttachments(taskId!, signal),
    enabled: taskId !== null,
  });
}

export function useProjectAttachments(projectId: string) {
  return useQuery({
    queryKey: projectAttachmentKeys.all(projectId),
    queryFn: ({ signal }) => getProjectAttachments(projectId, signal),
  });
}

/**
 * Tải file lên task.
 *
 * ⚠️ **Không** cập nhật lạc quan: khác kéo–thả Kanban, ở đây phần lớn phép kiểm tra chỉ
 * backend làm được (magic number của nội dung file). Hiện file trong danh sách trước rồi
 * gỡ ra khi 400 là hứa một thứ thường xuyên không đúng.
 *
 * Bốn mã lỗi cần thông điệp riêng: **413** quá lớn · **415** định dạng không hỗ trợ ·
 * **400** tên file/nội dung sai lệch · **403** Viewer không được tải lên.
 */
export function useUploadTaskAttachment(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => uploadTaskAttachment(taskId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: taskDetailKeys.attachments(projectId, taskId),
      });
    },
  });
}

export function useUploadProjectAttachment(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => uploadProjectAttachment(projectId, file),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectAttachmentKeys.all(projectId) });
    },
  });
}

/**
 * Xóa file đính kèm — người tải lên HOẶC ProjectManager.
 *
 * Không biết trước file thuộc task hay project nên làm mới cả hai nhánh; danh sách đính
 * kèm luôn nhỏ nên đây là đánh đổi đúng (cùng lý do `projectDataKeys.all` hơi rộng).
 */
export function useDeleteAttachment(projectId: string, taskId?: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (attachmentId: string) => deleteAttachment(attachmentId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectAttachmentKeys.all(projectId) });
      if (taskId) {
        void queryClient.invalidateQueries({
          queryKey: taskDetailKeys.attachments(projectId, taskId),
        });
      }
    },
  });
}

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { createTaskLink, deleteTaskLink, getTaskLinks } from '@/lib/api/endpoints/task-links';
import { taskDetailKeys, taskKeys } from '@/lib/hooks/keys';
import type { CreateTaskLinkRequest } from '@/types/task-link';

export function useTaskLinks(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskDetailKeys.links(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getTaskLinks(taskId!, signal),
    enabled: taskId !== null,
  });
}

/**
 * Tạo liên kết.
 *
 * ⚠️ Làm mới cả nhánh project, không chỉ task hiện tại: liên kết luôn có **hai đầu**, nên
 * chi tiết của task kia cũng vừa bị làm cũ.
 *
 * 409 có **hai nghĩa khác nhau** — "đã có liên kết cùng loại" và "tạo ra vòng chặn". Hiển
 * thị nguyên văn `message` từ `ApiError` thay vì tự viết một câu chung chung, vì hướng xử
 * lý của người dùng khác hẳn nhau.
 */
export function useCreateTaskLink(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateTaskLinkRequest) => createTaskLink(taskId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.all(projectId) });
    },
  });
}

export function useDeleteTaskLink(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (linkId: string) => deleteTaskLink(linkId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.all(projectId) });
    },
  });
}

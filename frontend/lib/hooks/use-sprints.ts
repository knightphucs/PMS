'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  createSprint,
  deleteSprint,
  listSprints,
  updateSprint,
} from '@/lib/api/endpoints/sprints';
import { projectDataKeys, sprintKeys } from '@/lib/hooks/keys';
import type { CreateSprintRequest, UpdateSprintRequest } from '@/types/sprint';

export function useSprints(projectId: string | null) {
  return useQuery({
    queryKey: sprintKeys.all(projectId ?? ''),
    queryFn: ({ signal }) => listSprints(projectId!, signal),
    enabled: projectId !== null,
  });
}

export function useCreateSprint(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateSprintRequest) => createSprint(projectId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: sprintKeys.all(projectId) });
    },
  });
}

/**
 * ⚠️ Không có `rowVersion` nên KHÔNG có luồng 409 "dữ liệu đã cũ" như project/task.
 * Đừng dựng UI cảnh báo stale ở đây — không có gì để phát hiện.
 */
export function useUpdateSprint(projectId: string, sprintId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: UpdateSprintRequest) => updateSprint(sprintId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: sprintKeys.all(projectId) });
    },
  });
}

export function useDeleteSprint(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sprintId: string) => deleteSprint(sprintId),
    onSuccess: () => {
      // Task của sprint bị đẩy về Backlog (ADR-020), nên backlog và mọi board đều cũ —
      // không chỉ danh sách sprint.
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

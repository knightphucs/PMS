'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  completeSprint,
  createSprint,
  deleteSprint,
  listSprints,
  previewSprintCompletion,
  startSprint,
  updateSprint,
} from '@/lib/api/endpoints/sprints';
import { projectDataKeys, sprintKeys } from '@/lib/hooks/keys';
import type {
  CompleteSprintRequest,
  CreateSprintRequest,
  UpdateSprintRequest,
} from '@/types/sprint';

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

export function useStartSprint(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (sprintId: string) => startSprint(sprintId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: sprintKeys.all(projectId) });
    },
  });
}

/**
 * Xem trước việc đóng sprint (ADR-050).
 *
 * `enabled` chỉ bật khi dialog mở: preview đọc số task chưa xong ngay tại thời điểm hỏi,
 * nạp sẵn từ trước thì con số đã cũ khi người dùng thật sự bấm.
 */
export function useSprintCompletionPreview(projectId: string, sprintId: string | null) {
  return useQuery({
    queryKey: [...sprintKeys.detail(projectId, sprintId ?? ''), 'completion-preview'],
    queryFn: ({ signal }) => previewSprintCompletion(sprintId!, signal),
    enabled: sprintId !== null,
    // `staleTime: 0` là bắt buộc: preview đọc số task chưa xong NGAY tại thời điểm hỏi.
    // Một con số cũ vài phút ở đây là hỏi người dùng dựa trên dữ kiện sai.
    staleTime: 0,
  });
}

export function useCompleteSprint(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ sprintId, body }: { sprintId: string; body: CompleteSprintRequest }) =>
      completeSprint(sprintId, body),
    onSuccess: () => {
      // Đóng sprint đẩy task chưa xong đi nơi khác (Backlog hoặc sprint đích), nên board,
      // backlog và thống kê đều cũ — không chỉ danh sách sprint.
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
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

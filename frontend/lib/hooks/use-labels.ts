'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  attachLabel,
  createLabel,
  deleteLabel,
  detachLabel,
  listLabels,
  updateLabel,
} from '@/lib/api/endpoints/labels';
import { labelKeys, projectDataKeys } from '@/lib/hooks/keys';
import type { CreateLabelRequest, UpdateLabelRequest } from '@/types/label';

/** Danh sách nhãn toàn cục — dùng cho ô chọn nhãn ở mọi project. */
export function useLabels() {
  return useQuery({
    queryKey: labelKeys.all,
    queryFn: ({ signal }) => listLabels(signal),
    // Nhãn đổi rất hiếm và dùng ở nhiều màn: giữ lâu hơn mặc định để không refetch mỗi lần
    // mở dropdown.
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreateLabel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreateLabelRequest) => createLabel(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: labelKeys.all });
    },
  });
}

/** ⚠️ Chỉ `SystemAdmin`. Ẩn nút theo `systemRole`, đừng để người khác bấm rồi ăn 403. */
export function useUpdateLabel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateLabelRequest }) => updateLabel(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: labelKeys.all });
      // Nhãn hiển thị lồng trong task (chip trên thẻ) nên đổi tên/màu làm cũ luôn dữ liệu
      // project. Không biết project nào đang mở nên làm mới tất cả.
      void queryClient.invalidateQueries({ queryKey: ['project-data'] });
    },
  });
}

export function useDeleteLabel() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteLabel(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: labelKeys.all });
      void queryClient.invalidateQueries({ queryKey: ['project-data'] });
    },
  });
}

/**
 * Gắn/gỡ nhãn trên một task.
 *
 * Cả hai đều idempotent nên UI không cần dò trạng thái trước. Làm mới cả nhánh project vì
 * nhãn hiện ở **hai chỗ**: chip trên thẻ Kanban/backlog và danh sách ở chi tiết task.
 */
export function useAttachLabel(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, labelId }: { taskId: string; labelId: string }) =>
      attachLabel(taskId, labelId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

export function useDetachLabel(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ taskId, labelId }: { taskId: string; labelId: string }) =>
      detachLabel(taskId, labelId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: projectDataKeys.all(projectId) });
    },
  });
}

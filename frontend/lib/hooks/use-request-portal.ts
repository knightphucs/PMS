'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  getMyRequest,
  getRequestForm,
  listMyRequests,
  listRequestPortals,
  submitRequest,
} from '@/lib/api/endpoints/request-portal';
import type { SubmitRequestRequest } from '@/types/request-portal';

/**
 * Cổng yêu cầu (ADR-063).
 *
 * 📌 Khoá PHẲNG và **không mang `employeeId`** — cùng lý lẽ với `myWorkKeys` (ADR-053):
 * mọi endpoint ở đây luôn trả dữ liệu của chính người gọi, và cache bị xoá sạch khi đăng
 * xuất (`lib/providers.tsx`). Nhét id vào khoá chỉ gợi ý rằng có thể tra yêu cầu của người
 * khác — thứ backend cố ý không cho (guard G3).
 *
 * ⚠️ Khoá này nằm NGOÀI `projectDataKeys`: một yêu cầu thuộc về một CON NGƯỜI, và người đó
 * thường không phải thành viên project. Gộp chung sẽ khiến một lần invalidate ở màn dự án
 * kéo theo cache của một người không có quyền đọc dự án đó.
 */
export const requestPortalKeys = {
  all: ['request-portal'] as const,
  portals: () => [...requestPortalKeys.all, 'portals'] as const,
  form: (projectId: string) => [...requestPortalKeys.all, 'form', projectId] as const,
  myRequests: (page: number, pageSize: number) =>
    [...requestPortalKeys.all, 'my-requests', page, pageSize] as const,
  myRequest: (taskId: string) => [...requestPortalKeys.all, 'my-request', taskId] as const,
};

/**
 * Các project đang mở cổng.
 *
 * `staleTime` dài: danh mục cổng đổi khi PM bật/tắt một cờ cấu hình — chuyện hiếm, và
 * hook này chạy trên MỌI lần vẽ sidebar của MỌI người trong công ty.
 */
export function useRequestPortals(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: requestPortalKeys.portals(),
    queryFn: ({ signal }) => listRequestPortals(signal),
    staleTime: 5 * 60_000,
    enabled: options?.enabled ?? true,
  });
}

export function useRequestForm(projectId: string | null) {
  return useQuery({
    queryKey: requestPortalKeys.form(projectId ?? ''),
    queryFn: ({ signal }) => getRequestForm(projectId!, signal),
    enabled: Boolean(projectId),
    staleTime: 60_000,
  });
}

export function useMyRequests(page: number, pageSize = 20) {
  return useQuery({
    queryKey: requestPortalKeys.myRequests(page, pageSize),
    queryFn: ({ signal }) => listMyRequests(page, pageSize, signal),
    staleTime: 30_000,
  });
}

export function useMyRequest(taskId: string) {
  return useQuery({
    queryKey: requestPortalKeys.myRequest(taskId),
    queryFn: ({ signal }) => getMyRequest(taskId, signal),
    staleTime: 30_000,
  });
}

export function useSubmitRequest(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: SubmitRequestRequest) => submitRequest(projectId, body),
    onSuccess: () => {
      // Chỉ làm mới nhánh của cổng. KHÔNG đụng `projectDataKeys`: người gửi có thể không
      // có quyền đọc project đó, nên invalidate rộng sẽ bắn ra một loạt request trả 404.
      void queryClient.invalidateQueries({ queryKey: requestPortalKeys.all });
    },
  });
}

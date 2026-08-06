'use client';

import { useQuery } from '@tanstack/react-query';

import { getMyWork } from '@/lib/api/endpoints/tasks';

/**
 * Việc của người đang đăng nhập, xuyên mọi dự án (ADR-053).
 *
 * 📌 Khóa PHẲNG và không mang `employeeId`: endpoint luôn trả việc của chính người gọi, và
 * cache của TanStack vốn đã bị xóa sạch khi đăng xuất (`lib/providers.tsx`). Nhét id vào
 * khóa chỉ gợi ý rằng có thể tra việc của người khác — thứ endpoint cố ý không cho.
 *
 * `staleTime` ngắn: đây là màn hình người ta mở ra để biết "sáng nay cần làm gì", nên một
 * task vừa được giao phải xuất hiện nhanh.
 */
export const myWorkKeys = {
  all: ['my-work'] as const,
};

export function useMyWork() {
  return useQuery({
    queryKey: myWorkKeys.all,
    queryFn: ({ signal }) => getMyWork(signal),
    staleTime: 30_000,
  });
}

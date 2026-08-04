'use client';

import { useQuery } from '@tanstack/react-query';

import { getProjectStatistics } from '@/lib/api/endpoints/statistics';
import { statisticsKeys } from '@/lib/hooks/keys';

/**
 * Thống kê project cho màn Dashboard.
 *
 * `byStatus` và `byPriority` LUÔN đủ mọi giá trị enum (kể cả count = 0), nên biểu đồ vẽ
 * thẳng được, không cần zero-fill ở client.
 */
export function useProjectStatistics(projectId: string) {
  return useQuery({
    queryKey: statisticsKeys.all(projectId),
    queryFn: ({ signal }) => getProjectStatistics(projectId, signal),
    // Thống kê là dữ liệu tổng hợp, không cần tươi từng giây; tránh gọi lại mỗi lần
    // chuyển tab qua lại.
    staleTime: 60 * 1000,
  });
}

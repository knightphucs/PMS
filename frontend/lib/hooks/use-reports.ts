'use client';

import { useQuery } from '@tanstack/react-query';

import { getBacklogInsight, getTimeline, getVelocity } from '@/lib/api/endpoints/reports';
import { reportKeys } from '@/lib/hooks/keys';

/** Backlog insight — cùng `staleTime` với `useProjectStatistics`, cùng lý do: dữ liệu tổng hợp. */
export function useBacklogInsight(projectId: string, dueSoonHorizonDays?: number) {
  return useQuery({
    queryKey: reportKeys.backlogInsight(projectId),
    queryFn: ({ signal }) => getBacklogInsight(projectId, dueSoonHorizonDays, signal),
    staleTime: 60 * 1000,
  });
}

export function useVelocity(projectId: string) {
  return useQuery({
    queryKey: reportKeys.velocity(projectId),
    queryFn: ({ signal }) => getVelocity(projectId, signal),
    staleTime: 60 * 1000,
  });
}

/** Timeline — mọi sprint, cùng `staleTime` với hai báo cáo còn lại. */
export function useTimeline(projectId: string) {
  return useQuery({
    queryKey: reportKeys.timeline(projectId),
    queryFn: ({ signal }) => getTimeline(projectId, signal),
    staleTime: 60 * 1000,
  });
}

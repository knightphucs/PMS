import type { BacklogInsightResponse, TimelineResponse, VelocityResponse } from '@/types/report';

import { apiFetch } from '../http';

/**
 * Backlog insight — nhóm báo cáo kiểu Jira. Cùng quyền với Thống kê (cả ba vai trò, ADR-039);
 * người ngoài project nhận 404, không phải 403.
 *
 * `dueSoonHorizonDays` mặc định 7 ở SERVER khi không truyền — không tự đặt mặc định ở đây để
 * tránh hai nơi định nghĩa "sắp đến hạn" là bao lâu.
 */
export function getBacklogInsight(
  projectId: string,
  dueSoonHorizonDays?: number,
  signal?: AbortSignal,
) {
  const query = dueSoonHorizonDays ? `?dueSoonHorizonDays=${dueSoonHorizonDays}` : '';
  return apiFetch<BacklogInsightResponse>(
    `/projects/${projectId}/reports/backlog-insight${query}`,
    { signal },
  );
}

export function getVelocity(projectId: string, signal?: AbortSignal) {
  return apiFetch<VelocityResponse>(`/projects/${projectId}/reports/velocity`, { signal });
}

/** Mọi sprint (Planned/Active/Completed) — roadmap kiểu Jira. */
export function getTimeline(projectId: string, signal?: AbortSignal) {
  return apiFetch<TimelineResponse>(`/projects/${projectId}/reports/timeline`, { signal });
}

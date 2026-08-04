import type { ProjectStatisticsResponse } from '@/types/statistics';

import { apiFetch } from '../http';

/**
 * Thống kê một project — nguồn dữ liệu cho màn Dashboard (Recharts).
 *
 * Cả ba vai trò đều gọi được kể từ ADR-039 (`Member` được bổ sung 2026-08-03). Người ngoài
 * project vẫn nhận **404**, không phải 403.
 */
export function getProjectStatistics(projectId: string, signal?: AbortSignal) {
  return apiFetch<ProjectStatisticsResponse>(`/projects/${projectId}/statistics`, { signal });
}

import type { PagedRequest, PagedResult } from '@/types/common';
import type {
  CreateProjectRequest,
  ProjectDetailResponse,
  ProjectSummaryResponse,
} from '@/types/project';

import { apiFetch } from '../http';

/**
 * `GET /projects` — chỉ trả project mà người gọi là thành viên.
 *
 * Tên query param khớp `PagedRequest` phía backend. Binding của ASP.NET không phân biệt
 * hoa thường nên camelCase an toàn.
 */
export function listProjects(params: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<ProjectSummaryResponse>>('/projects', {
    query: { ...params },
    signal,
  });
}

export function getProject(id: string, signal?: AbortSignal) {
  return apiFetch<ProjectDetailResponse>(`/projects/${id}`, { signal });
}

/**
 * Người tạo tự động trở thành `ProjectManager` của project đó — không phải gọi thêm
 * endpoint nào để tự thêm mình vào.
 */
export function createProject(body: CreateProjectRequest) {
  return apiFetch<ProjectSummaryResponse>('/projects', { method: 'POST', body });
}

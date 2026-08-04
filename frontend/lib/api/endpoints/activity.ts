import type { ActivityLogResponse, SystemAuditLogResponse } from '@/types/activity';
import type { PagedRequest, PagedResult } from '@/types/common';

import { apiFetch } from '../http';

/** Lịch sử của một task. Mọi thành viên đọc được, kể cả `Viewer`. */
export function getTaskActivity(taskId: string, request: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<ActivityLogResponse>>(`/tasks/${taskId}/activity`, {
    query: { ...request },
    signal,
  });
}

/**
 * Lịch sử của một project.
 *
 * 📌 Bao gồm **cả hoạt động sprint**: `SprintService` ghi log với `EntityType = Project`.
 * Nghĩa là "lịch sử project" ≠ "những gì làm lên đúng hàng Project" — đó là kết quả mong
 * muốn, nhưng đừng ngạc nhiên khi thấy "Tạo sprint ..." trong feed này.
 */
export function getProjectActivity(
  projectId: string,
  request: PagedRequest,
  signal?: AbortSignal,
) {
  return apiFetch<PagedResult<ActivityLogResponse>>(`/projects/${projectId}/activity`, {
    query: { ...request },
    signal,
  });
}

/**
 * Nhật ký CẤP HỆ THỐNG — chỉ `SystemAdmin` (403 với người khác).
 *
 * ⚠️ Chỉ trả `EntityType` là `Employee` và `Label`. Nó **không phải** cửa để xem hoạt động
 * của project/task, và cố tình không nhận tham số lọc loại đối tượng (ADR-042).
 */
export function getSystemAuditLogs(request: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<SystemAuditLogResponse>>('/admin/audit-logs', {
    query: { ...request },
    signal,
  });
}

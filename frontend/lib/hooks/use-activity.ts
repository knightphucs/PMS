'use client';

import { useQuery } from '@tanstack/react-query';

import {
  getProjectActivity,
  getSystemAuditLogs,
  getTaskActivity,
} from '@/lib/api/endpoints/activity';
import { projectActivityKeys, systemAuditKeys, taskDetailKeys } from '@/lib/hooks/keys';
import type { PagedRequest } from '@/types/common';

export function useTaskActivity(projectId: string, taskId: string | null, request: PagedRequest) {
  return useQuery({
    queryKey: [...taskDetailKeys.activity(projectId, taskId ?? ''), request],
    queryFn: ({ signal }) => getTaskActivity(taskId!, request, signal),
    enabled: taskId !== null,
  });
}

/** 📌 Bao gồm cả hoạt động sprint — `SprintService` ghi log dưới `EntityType = Project`. */
export function useProjectActivity(projectId: string, request: PagedRequest) {
  return useQuery({
    queryKey: [...projectActivityKeys.all(projectId), request],
    queryFn: ({ signal }) => getProjectActivity(projectId, request, signal),
  });
}

/** ⚠️ Chỉ `SystemAdmin` (403 với người khác). Chỉ gồm `Employee` và `Label` (ADR-042). */
export function useSystemAuditLogs(request: PagedRequest) {
  return useQuery({
    queryKey: [...systemAuditKeys.all, request],
    queryFn: ({ signal }) => getSystemAuditLogs(request, signal),
  });
}

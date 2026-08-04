/** Soi gương `PMS.Application/Features/ActivityLogs/ActivityLogDtos.cs`. */

import type { ActivityAction } from './enums';

export interface ActivityLogResponse {
  id: string;
  action: ActivityAction;
  /** Mô tả tiếng Việt do backend sinh — hiển thị nguyên văn, không cần dịch lại. */
  detail: string;
  actorId: string;
  actorName: string;
  createdAt: string;
}

/**
 * Nhật ký CẤP HỆ THỐNG — chỉ `SystemAdmin` đọc được, và chỉ gồm `EntityType` là
 * `Employee` hoặc `Label` (ADR-042).
 *
 * ⚠️ Endpoint này **cố tình không** cho lọc theo `entityType`: nó không phải cửa để xem hoạt
 * động của project/task. Đừng thêm tham số đó vào client rồi tưởng backend sẽ nhận.
 */
export interface SystemAuditLogResponse {
  id: string;
  entityType: string;
  entityId: string;
  action: ActivityAction;
  detail: string;
  actorId: string;
  actorName: string;
  createdAt: string;
}

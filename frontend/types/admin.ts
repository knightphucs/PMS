/** Soi gương `PMS.Application/Features/Admin/AdminDtos.cs`. */

import type { SystemRole } from './enums';

/**
 * Một dòng của danh sách nhân sự ở màn quản trị.
 *
 * ⚠️ Khác `EmployeeDto` của auth: có thêm trạng thái khóa và `createdAt`, nhưng KHÔNG có
 * `permissions` — quyền gắn với VAI TRÒ chứ không với từng người (ADR-045), nên muốn biết
 * người này có quyền gì thì tra `systemRole` trong ma trận ở `/admin/roles`.
 */
export interface EmployeeAdminResponse {
  id: string;
  name: string;
  email: string;
  systemRole: SystemRole;
  isLocked: boolean;
  /** ISO 8601 UTC, `null` khi chưa từng bị khóa. */
  lockedAt: string | null;
  lockReason: string | null;
  createdAt: string;
}

/** `reason` là BẮT BUỘC, tối đa 256 ký tự — backend trả 400 nếu rỗng. */
export interface LockAccountRequest {
  reason: string;
}

export interface ChangeSystemRoleRequest {
  role: SystemRole;
}

// ---------- Phân quyền vai trò (ADR-045) ----------

export interface PermissionResponse {
  /** Dạng `resource:action`, ví dụ `employees:manage`. */
  code: string;
  /** Mô tả tiếng Việt do backend cấp — hiện cạnh ô tích. */
  description: string;
}

export interface RolePermissionsResponse {
  role: SystemRole;
  permissions: string[];
}

/**
 * GHI ĐÈ TOÀN PHẦN, không phải delta — mã nào không gửi là bị gỡ.
 *
 * ⚠️ Backend từ chối CẢ LÔ (400) nếu có mã ngoài danh mục, và trả 409 nếu gỡ
 * `roles:manage` khỏi `SystemAdmin`.
 */
export interface UpdateRolePermissionsRequest {
  permissions: string[];
}

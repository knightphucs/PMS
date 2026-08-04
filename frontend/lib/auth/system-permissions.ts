/**
 * Quyền cấp HỆ THỐNG (tầng 1) — soi gương
 * `PMS.Application/Common/Authorization/SystemPermissions.cs` (ADR-045).
 *
 * 🔴 **Đây KHÔNG phải bản sao thứ hai của `lib/tasks/permissions.ts`.** Hai file, hai tầng,
 * không chồng lấn:
 *
 * | | Tầng 1 — file này | Tầng 2 — `lib/tasks/permissions.ts` |
 * |---|---|---|
 * | Phạm vi | Toàn hệ thống | Theo từng project |
 * | Nguồn | `EmployeeDto.permissions` (từ DB, qua claim) | `RoleInProject` của chính tôi trong project |
 * | Soi gương | `SystemPermissions.cs` | `ProjectPermissions.cs` |
 * | Đổi bằng | Màn `/admin/roles` (dữ liệu) | Đổi vai trò thành viên |
 *
 * Quyền project-scoped **không bao giờ** xuất hiện ở đây — đó là ranh giới ADR-042 dựng lên
 * và backend có test khóa (`SystemPermissionsCatalogTests`).
 */

import type { EmployeeDto } from '@/types/auth';

/**
 * Danh mục ĐÓNG. Phải khớp từng ký tự với `SystemPermissions.All` phía backend — sai một
 * chữ thì nút biến mất im lặng chứ không báo lỗi.
 */
export const SYSTEM_PERMISSIONS = {
  employeesManage: 'employees:manage',
  auditRead: 'audit:read',
  labelsManage: 'labels:manage',
  projectsCreate: 'projects:create',
  rolesManage: 'roles:manage',
} as const;

export type SystemPermission = (typeof SYSTEM_PERMISSIONS)[keyof typeof SYSTEM_PERMISSIONS];

/** Nhãn tiếng Việt cho từng quyền — dùng khi backend chưa trả `description`. */
export const SYSTEM_PERMISSION_LABEL: Record<SystemPermission, string> = {
  'employees:manage': 'Quản lý nhân sự',
  'audit:read': 'Đọc nhật ký hệ thống',
  'labels:manage': 'Quản lý nhãn toàn cục',
  'projects:create': 'Tạo dự án',
  'roles:manage': 'Phân quyền vai trò',
};

/**
 * Người dùng có quyền này không?
 *
 * 🔴 Fail-closed ở MỌI đường: `user` là `null`/`undefined` (chưa tải xong), hoặc
 * `permissions` không tồn tại → trả `false`.
 *
 * Nhánh `permissions` thiếu KHÔNG phải phòng xa lý thuyết: một tab đang mở lúc backend
 * được deploy vẫn giữ `employee` cũ trong store cho tới lần refresh token kế tiếp (tối đa
 * 15 phút), và bản cũ đó không có trường này. Đọc thành mảng rỗng khiến UI ẩn nút admin
 * trong 15 phút đó — đúng, vì token của họ cũng chưa có claim nên bấm vào chỉ nhận 403.
 */
export function hasPermission(
  user: EmployeeDto | null | undefined,
  permission: SystemPermission,
): boolean {
  return user?.permissions?.includes(permission) ?? false;
}

/** Có ÍT NHẤT MỘT trong các quyền — dùng để quyết định có hiện mục "Quản trị" hay không. */
export function hasAnyPermission(
  user: EmployeeDto | null | undefined,
  permissions: readonly SystemPermission[],
): boolean {
  return permissions.some((p) => hasPermission(user, p));
}

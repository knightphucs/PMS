import type {
  ChangeSystemRoleRequest,
  EmployeeAdminResponse,
  LockAccountRequest,
  PermissionResponse,
  RolePermissionsResponse,
  UpdateRolePermissionsRequest,
} from '@/types/admin';
import type { PagedRequest, PagedResult } from '@/types/common';
import type { SystemRole } from '@/types/enums';

import { apiFetch } from '../http';

// ---------- Nhân sự ----------

/**
 * Danh sách nhân sự — cần quyền `employees:manage` (403 với người khác).
 *
 * ✅ `search` ở endpoint này **THỰC SỰ CHẠY** (khớp `Name` HOẶC `Email`) — nó là một trong
 * hai chỗ hiếm hoi như vậy, cùng với `/notifications`. Ở project/task/sprint/comment thì
 * `?search=` bị nhận rồi bỏ qua im lặng, đừng đem khuôn này áp sang đó.
 *
 * `sortBy` nhận: `name` · `email` · `role` · `locked`.
 */
export function listAdminEmployees(request: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<EmployeeAdminResponse>>('/admin/employees', {
    query: { ...request },
    signal,
  });
}

/**
 * Khóa tài khoản. Trả **204**.
 *
 * Bốn mã lỗi cần thông điệp RIÊNG, đừng gộp thành "Thao tác thất bại":
 * `409` đây là SystemAdmin hoạt động cuối cùng · `409` tài khoản đã bị khóa sẵn ·
 * `400` tự khóa chính mình · `404` không tìm thấy.
 */
export function lockEmployee(id: string, body: LockAccountRequest) {
  return apiFetch<void>(`/admin/employees/${id}/lock`, { method: 'POST', body });
}

/** Mở khóa. Trả **204**. `409` nếu tài khoản vốn không bị khóa. */
export function unlockEmployee(id: string) {
  return apiFetch<void>(`/admin/employees/${id}/unlock`, { method: 'POST' });
}

/**
 * Đổi vai trò hệ thống. Trả **204**, và **thu hồi toàn bộ refresh token** của người đó
 * (ADR-015) — họ sẽ phải đăng nhập lại.
 *
 * `400` tự đổi vai trò của chính mình · `409` hạ vai trò SystemAdmin hoạt động cuối cùng.
 */
export function changeSystemRole(id: string, body: ChangeSystemRoleRequest) {
  return apiFetch<void>(`/admin/employees/${id}/system-role`, { method: 'PUT', body });
}

// ---------- Phân quyền vai trò (ADR-045) ----------

/** Danh mục quyền (mã + mô tả). Cần `roles:manage`. */
export function listPermissions(signal?: AbortSignal) {
  return apiFetch<PermissionResponse[]>('/admin/permissions', { signal });
}

/** Ma trận đầy đủ: mọi vai trò kèm tập quyền hiện tại. */
export function listRolePermissions(signal?: AbortSignal) {
  return apiFetch<RolePermissionsResponse[]>('/admin/roles/permissions', { signal });
}

/**
 * Ghi đè TOÀN BỘ tập quyền của một vai trò. Trả **204**.
 *
 * ⚠️ Có hiệu lực với một người chỉ khi họ lấy access token mới — tối đa 15 phút, vì lệnh
 * này thu hồi refresh token của mọi người mang vai trò đó. UI phải nói rõ cửa sổ này chứ
 * không được ngụ ý là tức thì.
 *
 * `400` mã ngoài danh mục (từ chối cả lô, không ghi một phần) ·
 * `409` gỡ `roles:manage` khỏi `SystemAdmin`.
 */
export function updateRolePermissions(role: SystemRole, body: UpdateRolePermissionsRequest) {
  return apiFetch<void>(`/admin/roles/${role}/permissions`, { method: 'PUT', body });
}

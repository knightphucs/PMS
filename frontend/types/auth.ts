/** Soi gương `PMS.Application/Features/Auth/AuthDtos.cs`. */

import type { SystemRole } from './enums';

export interface RegisterRequest {
  name: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface EmployeeDto {
  id: string;
  name: string;
  email: string;
  systemRole: SystemRole;
  /**
   * Quyền cấp HỆ THỐNG (tầng 1) — ADR-045. Cùng tập với claim `permission` trong JWT, cùng
   * một nguồn là bảng `RolePermissions` phía backend.
   *
   * 🔴 Có ở đây để frontend gác nút/menu mà **không phải giải mã JWT**. Access token nằm
   * trong bộ nhớ Zustand và client chưa từng có dòng nào đọc nội dung nó (ADR-027); thêm
   * một bộ phân tích token ở client là thêm một chỗ nữa để lệch. Cưỡng chế thật vẫn 100%
   * ở server — đây chỉ để UI khỏi hiện nút chắc chắn nhận 403.
   *
   * ⚠️ Gác quyền qua `lib/auth/system-permissions.ts`, đừng đọc mảng này trực tiếp: phiên
   * đã tải trước lúc deploy giữ `employee` trong cache **không có** trường này cho tới lần
   * refresh kế, và hàm ở đó đọc `undefined` thành "không có quyền nào".
   *
   * 📌 Đây là quyền tầng 1. Quyền theo từng project (tầng 2) KHÔNG nằm ở đây và không bao
   * giờ được nằm ở đây — xem `lib/tasks/permissions.ts`.
   */
  permissions: string[];
}

/**
 * Thân phản hồi của `/auth/register`, `/auth/login`, `/auth/refresh`.
 *
 * CỐ Ý không có `refreshToken` (ADR-027): nó đi bằng cookie httpOnly mà JavaScript không
 * đọc được. Nếu một ngày trường này xuất hiện lại trong JSON thì cookie httpOnly mất
 * sạch tác dụng — có `AuthCookieTests.Than_phan_hoi_khong_duoc_chua_refresh_token` giữ.
 */
export interface AuthenticatedResponse {
  accessToken: string;
  /** ISO 8601 UTC. Dùng để refresh CHỦ ĐỘNG thay vì đợi 401. */
  accessTokenExpiresAt: string;
  employee: EmployeeDto;
}

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

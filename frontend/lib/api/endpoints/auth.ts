import type {
  AuthenticatedResponse,
  EmployeeDto,
  LoginRequest,
  RegisterRequest,
} from '@/types/auth';

import { AUTH_PATH } from '../config';
import { apiFetch } from '../http';

/**
 * Không cần lưu refresh token ở đâu cả: phản hồi kèm `Set-Cookie` httpOnly và trình
 * duyệt tự giữ (ADR-027).
 */
export function register(body: RegisterRequest) {
  return apiFetch<AuthenticatedResponse>(AUTH_PATH.register, {
    method: 'POST',
    body,
    anonymous: true,
  });
}

export function login(body: LoginRequest) {
  return apiFetch<AuthenticatedResponse>(AUTH_PATH.login, {
    method: 'POST',
    body,
    anonymous: true,
  });
}

/** Backend đọc refresh token từ cookie và thu hồi nó, rồi xóa cookie. Trả 204. */
export function logout() {
  return apiFetch<void>(AUTH_PATH.logout, { method: 'POST' });
}

export function getCurrentUser() {
  return apiFetch<EmployeeDto>(AUTH_PATH.me);
}

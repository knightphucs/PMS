import type {
  AuthenticatedResponse,
  EmployeeDto,
  LoginRequest,
  RegisterRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  UpdateProfileRequest,
  ChangePasswordRequest,
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

/**
 * Yêu cầu đặt lại mật khẩu.
 *
 * 🔴 **LUÔN trả 204**, kể cả khi email không tồn tại (ADR-041) — đó là chủ đích, không phải
 * lỗi. UI vì vậy phải hiện đúng MỘT thông điệp cho mọi trường hợp ("Nếu email tồn tại,
 * chúng tôi đã gửi hướng dẫn"). Hiện "email không tồn tại" là dựng lại đúng kênh dò tài
 * khoản mà backend vừa bịt.
 *
 * Rate limit 3 lần/phút theo IP → có thể nhận **429**.
 */
export function forgotPassword(body: ForgotPasswordRequest) {
  return apiFetch<void>(AUTH_PATH.forgotPassword, {
    method: 'POST',
    body,
    anonymous: true,
  });
}

/**
 * Đặt lại mật khẩu bằng token từ email.
 *
 * Token sai / hết hạn / đã dùng đều trả **cùng một 400 với cùng một thông điệp** — đừng cố
 * suy ra nguyên nhân cụ thể để hiện thông báo "thông minh" hơn, thông tin đó cố tình không
 * có ở đây.
 *
 * Thành công thì **mọi phiên đã bị thu hồi** (kể cả trên thiết bị khác) → luôn điều hướng
 * về `/login`, đừng giả định người dùng vẫn đang đăng nhập.
 */
export function resetPassword(body: ResetPasswordRequest) {
  return apiFetch<void>(AUTH_PATH.resetPassword, {
    method: 'POST',
    body,
    anonymous: true,
  });
}

/**
 * Đổi tên hồ sơ (ADR-049). Trả về `AuthenticatedResponse` MỚI — `name` nằm trong JWT
 * claim, nên gọi xong bắt buộc phải `setSession(...)` lại với response này, không phải chỉ
 * cập nhật `user` trong store bằng tay. Xem `useAuth().updateProfile`.
 */
export function updateProfile(body: UpdateProfileRequest) {
  return apiFetch<AuthenticatedResponse>(AUTH_PATH.updateProfile, {
    method: 'PUT',
    body,
  });
}

/**
 * Đổi mật khẩu khi đang đăng nhập. Thu hồi mọi phiên KHÁC nhưng vẫn phát lại token cho
 * chính tab đang gọi — cũng phải `setSession(...)` lại với response, cùng lý do như trên.
 */
export function changePassword(body: ChangePasswordRequest) {
  return apiFetch<AuthenticatedResponse>(AUTH_PATH.changePassword, {
    method: 'POST',
    body,
  });
}

'use client';

import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useRef } from 'react';

import * as authApi from '@/lib/api/endpoints/auth';
import { refreshAccessToken, resetRefreshState } from '@/lib/api/refresh';
import { useAuthStore } from '@/store/auth-store';
import type {
  ChangePasswordRequest,
  LoginRequest,
  RegisterRequest,
  UpdateProfileRequest,
} from '@/types/auth';

/**
 * Khôi phục phiên sau khi tải lại trang.
 *
 * Access token chỉ nằm trong bộ nhớ nên F5 là mất (ADR-027) — nhưng cookie refresh
 * httpOnly vẫn còn, nên gọi /auth/refresh đúng một lần là lấy lại được phiên.
 *
 * Lợi ích phụ đáng kể: mỗi lần F5 đều chạy qua đúng đường single-flight, nên nếu cơ chế
 * đó hỏng thì lộ ra ngay trong lúc phát triển thay vì đợi tới khi token hết hạn sau 15
 * phút mới biết.
 */
export function useSessionBootstrap() {
  const status = useAuthStore((s) => s.status);
  const markAnonymous = useAuthStore((s) => s.markAnonymous);
  const started = useRef(false);

  useEffect(() => {
    if (status !== 'unknown' || started.current) return;
    started.current = true;

    refreshAccessToken().catch(() => {
      // 401 ở đây là chuyện BÌNH THƯỜNG: nghĩa là chưa từng đăng nhập hoặc cookie đã hết
      // hạn. Không phải lỗi để báo cho người dùng.
      markAnonymous();
    });
  }, [status, markAnonymous]);

  return status;
}

export function useAuth() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const status = useAuthStore((s) => s.status);
  const setSession = useAuthStore((s) => s.setSession);
  const clearSession = useAuthStore((s) => s.clearSession);

  const login = useCallback(
    async (body: LoginRequest) => {
      const auth = await authApi.login(body);
      setSession(auth.accessToken, auth.accessTokenExpiresAt, auth.employee);
      return auth;
    },
    [setSession],
  );

  const register = useCallback(
    async (body: RegisterRequest) => {
      const auth = await authApi.register(body);
      setSession(auth.accessToken, auth.accessTokenExpiresAt, auth.employee);
      return auth;
    },
    [setSession],
  );

  /**
   * Đổi tên hồ sơ (ADR-049). `authApi.updateProfile` trả `AuthenticatedResponse` MỚI —
   * `setSession` ngay với nó, không chỉ ghi `user` bằng tay, vì `name` nằm trong JWT claim
   * và tab này cần access token mang tên mới để `/auth/me` không nói dối ở lần gọi kế.
   */
  const updateProfile = useCallback(
    async (body: UpdateProfileRequest) => {
      const auth = await authApi.updateProfile(body);
      setSession(auth.accessToken, auth.accessTokenExpiresAt, auth.employee);
      return auth;
    },
    [setSession],
  );

  /**
   * Đổi mật khẩu khi đang đăng nhập. Cùng lý do `setSession` lại như `updateProfile`: phiên
   * hiện tại vẫn sống tiếp bằng token MỚI mà server vừa phát, các phiên khác đã bị thu hồi.
   */
  const changePassword = useCallback(
    async (body: ChangePasswordRequest) => {
      const auth = await authApi.changePassword(body);
      setSession(auth.accessToken, auth.accessTokenExpiresAt, auth.employee);
      return auth;
    },
    [setSession],
  );

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      // Đăng xuất phía client phải thành công kể cả khi lời gọi mạng hỏng — nếu không
      // người dùng bị kẹt trong trạng thái "đã đăng nhập" mà không thoát ra được.
      // Cookie có Expires nên dù sao cũng tự chết.
    } finally {
      resetRefreshState();
      clearSession();
      // Xóa cache TanStack Query: dữ liệu project của người vừa đăng xuất không được
      // hiện lại cho người đăng nhập kế tiếp trên cùng máy.
      queryClient.clear();
      router.replace('/login');
    }
  }, [clearSession, queryClient, router]);

  return {
    user,
    status,
    isAuthenticated: status === 'authenticated',
    login,
    register,
    logout,
    updateProfile,
    changePassword,
  };
}

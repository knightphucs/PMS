import { create } from 'zustand';

import type { EmployeeDto } from '@/types/auth';

/**
 * `unknown` là trạng thái KHỞI ĐỘNG, không phải "chưa đăng nhập".
 *
 * Phân biệt hai cái này là điều kiện để route guard không đá người dùng ra ngoài trong
 * lúc đang khôi phục phiên: sau F5 access token đã mất (nó chỉ nằm trong bộ nhớ), phải
 * gọi /auth/refresh một lần rồi mới biết được. Gộp `unknown` với `anonymous` thì mỗi lần
 * F5 người dùng sẽ bị văng về /login rồi mới quay lại — nháy và sai.
 */
export type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

interface AuthState {
  /**
   * CHỈ nằm trong bộ nhớ, không persist (ADR-027).
   *
   * Đây là nửa còn lại của quyết định "refresh token trong cookie httpOnly": nếu access
   * token được ghi xuống localStorage thì XSS vẫn có 15 phút thao túng dưới danh nghĩa
   * người dùng, và nó tồn tại qua cả khi đóng tab.
   */
  accessToken: string | null;
  /** Epoch ms. Dùng để refresh chủ động trước khi token hết hạn. */
  accessTokenExpiresAt: number | null;
  user: EmployeeDto | null;
  status: AuthStatus;

  setSession: (accessToken: string, expiresAtIso: string, user: EmployeeDto) => void;
  clearSession: () => void;
  markAnonymous: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  accessTokenExpiresAt: null,
  user: null,
  status: 'unknown',

  setSession: (accessToken, expiresAtIso, user) =>
    set({
      accessToken,
      accessTokenExpiresAt: new Date(expiresAtIso).getTime(),
      user,
      status: 'authenticated',
    }),

  clearSession: () =>
    set({
      accessToken: null,
      accessTokenExpiresAt: null,
      user: null,
      status: 'anonymous',
    }),

  markAnonymous: () => set({ status: 'anonymous' }),
}));

/**
 * Đọc state ngoài React.
 *
 * Tầng API client không phải component nên không dùng hook được, mà nó vẫn cần token
 * mới nhất tại đúng thời điểm gửi request — không phải giá trị bị đóng băng trong
 * closure từ lần render trước.
 */
export const authStore = {
  get: useAuthStore.getState,
  set: useAuthStore.setState,
};

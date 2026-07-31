import type { AuthenticatedResponse } from '@/types/auth';
import { authStore } from '@/store/auth-store';

import { API_BASE_URL, AUTH_PATH } from './config';
import { NetworkError, toApiError } from './problem';

/**
 * 🔴 SINGLE-FLIGHT REFRESH — đọc kỹ trước khi sửa file này.
 *
 * `AuthService.RefreshAsync` phía backend dùng rotation kèm REUSE DETECTION: token cũ bị
 * thu hồi ngay khi đổi, và dùng lại một token đã thu hồi bị coi là token BỊ ĐÁNH CẮP nên
 * backend gọi `RevokeAllAsync` — thu hồi TOÀN BỘ phiên của người dùng đó.
 *
 * Hệ quả nếu để nhiều lời gọi /refresh chạy song song: request đầu đổi token thành công,
 * request thứ hai gửi đúng token vừa bị thu hồi, backend kết luận bị tấn công và đá người
 * dùng ra khỏi mọi thiết bị. Triệu chứng ngoài đời là "thỉnh thoảng tự đăng xuất" — cực
 * khó chẩn đoán vì nó chỉ xuất hiện khi có từ hai request cùng hết hạn một lúc.
 *
 * Nên chỉ được có ĐÚNG MỘT lời gọi /refresh tại một thời điểm. Mọi request khác bám vào
 * cùng một promise rồi retry sau khi nó xong.
 *
 * Chốt chặn duy nhất cho quy tắc này là biến `inFlight` bên dưới. Đừng thay bằng cách
 * gọi trực tiếp `performRefresh()` ở bất cứ đâu.
 */
let inFlight: Promise<string> | null = null;

async function performRefresh(): Promise<string> {
  let response: Response;

  try {
    response = await fetch(`${API_BASE_URL}${AUTH_PATH.refresh}`, {
      method: 'POST',
      // Không có body: refresh token đi bằng cookie httpOnly (ADR-027).
      // `credentials: 'include'` là thứ khiến trình duyệt đính cookie vào request
      // cross-origin — thiếu nó thì cookie không bao giờ được gửi và luôn nhận 401.
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
  } catch (cause) {
    throw new NetworkError(cause);
  }

  if (!response.ok) {
    // 401 ở đây là điểm cuối: có thể refresh token đã hết hạn, đã bị thu hồi do đổi
    // SystemRole / khóa tài khoản (ADR-015), hoặc reuse detection đã kích hoạt. Không
    // trường hợp nào retry được — thử lại chỉ làm tình hình tệ hơn.
    authStore.get().clearSession();
    throw await toApiError(response);
  }

  const auth = (await response.json()) as AuthenticatedResponse;
  authStore.get().setSession(auth.accessToken, auth.accessTokenExpiresAt, auth.employee);
  return auth.accessToken;
}

/**
 * Trả về access token còn hiệu lực, gọi /refresh nếu cần.
 *
 * An toàn khi gọi đồng thời từ nhiều nơi: lời gọi thứ hai trở đi nhận lại đúng promise
 * đang chạy chứ không tạo request mới.
 */
export function refreshAccessToken(): Promise<string> {
  inFlight ??= (async () => {
    try {
      return await performRefresh();
    } finally {
      // Xóa ngay khi settle — kể cả khi thất bại — để lần đăng nhập kế tiếp không bị
      // dính lại promise hỏng của phiên cũ.
      inFlight = null;
    }
  })();

  return inFlight;
}

/** Cho test tay và cho luồng đăng xuất: bỏ promise đang chờ. */
export function resetRefreshState(): void {
  inFlight = null;
}

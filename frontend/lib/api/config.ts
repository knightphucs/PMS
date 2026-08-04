/**
 * ⚠️ Đường dẫn viết THƯỜNG chữ "auth".
 *
 * Routing của ASP.NET không phân biệt hoa thường nhưng `Path` của cookie thì CÓ. Cookie
 * refresh được đặt với `Path=/api/v1/auth`; gọi `/api/v1/Auth/refresh` vẫn trúng route
 * nhưng trình duyệt sẽ không đính cookie vào — và hỏng theo kiểu tệ nhất: 401 mà không
 * có lỗi nào chỉ ra nguyên nhân.
 */
export const AUTH_PATH = {
  register: '/auth/register',
  login: '/auth/login',
  refresh: '/auth/refresh',
  logout: '/auth/logout',
  me: '/auth/me',
  forgotPassword: '/auth/forgot-password',
  resetPassword: '/auth/reset-password',
} as const;

const rawBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;

if (!rawBaseUrl) {
  throw new Error(
    'Thiếu NEXT_PUBLIC_API_BASE_URL. Sao chép frontend/.env.example thành .env.local.',
  );
}

/** Không có dấu `/` ở cuối, để ghép với path bắt đầu bằng `/` là ra URL đúng. */
export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, '');

/**
 * Refresh chủ động khi access token còn dưới ngần này thì hết hạn.
 *
 * Access token sống 15 phút và `AuthenticatedResponse.accessTokenExpiresAt` cho biết
 * chính xác lúc nào — nên đổi token trước còn hơn để request thất bại rồi mới retry.
 * 60s đủ rộng để trùm lệch đồng hồ (backend cho ClockSkew 30s) và độ trễ mạng.
 */
export const REFRESH_SKEW_MS = 60_000;

import { authStore } from '@/store/auth-store';

import { API_BASE_URL, AUTH_PATH, REFRESH_SKEW_MS } from './config';
import { NetworkError, toApiError } from './problem';
import { refreshAccessToken } from './refresh';

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  /**
   * Sẽ được `JSON.stringify`. Bỏ trống nếu endpoint không nhận body.
   *
   * ⚠️ **Ngoại lệ: `FormData`.** Truyền `FormData` thì nó được gửi NGUYÊN TRẠNG và header
   * `Content-Type` **cố tình không được đặt** — xem `buildInit`.
   */
  body?: unknown;
  /** Query string. Giá trị `undefined`, `null` hoặc chuỗi rỗng bị bỏ qua. */
  query?: Record<string, string | number | boolean | undefined | null>;
  /** Bỏ qua bước gắn access token — dùng cho login/register. */
  anonymous?: boolean;
  signal?: AbortSignal;
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  // `API_BASE_URL` có thể tuyệt đối (dev: https://localhost:7264/api/v1) hoặc tương đối
  // (deploy: /api/v1, để đi qua rewrite same-origin trong next.config.ts — xem ADR-027).
  // `new URL()` không tự suy ra origin cho chuỗi tương đối, phải truyền `base` tường
  // minh; khi URL đã tuyệt đối thì `base` bị bỏ qua, không ảnh hưởng gì.
  const base = typeof window !== 'undefined' ? window.location.origin : undefined;
  const url = new URL(`${API_BASE_URL}${path}`, base);

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue;
    url.searchParams.set(key, String(value));
  }

  return url.toString();
}

/** Token còn hiệu lực để gắn vào request, tự refresh trước khi hết hạn. */
async function resolveAccessToken(): Promise<string | null> {
  const { accessToken, accessTokenExpiresAt } = authStore.get();

  if (!accessToken) return null;

  const expiringSoon =
    accessTokenExpiresAt !== null && accessTokenExpiresAt - Date.now() < REFRESH_SKEW_MS;

  // Refresh CHỦ ĐỘNG. Đi qua cùng một single-flight nên nhiều request cùng phát hiện
  // token sắp hết hạn cũng chỉ tạo ra một lời gọi /refresh.
  if (expiringSoon) {
    try {
      return await refreshAccessToken();
    } catch {
      // Refresh hỏng: cứ gửi token cũ đi. Nếu nó thật sự hết hạn thì nhận 401 và đi
      // tiếp theo đường xử lý 401 bên dưới — không nuốt lỗi ở đây.
      return accessToken;
    }
  }

  return accessToken;
}

async function send(url: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(url, init);
  } catch (cause) {
    // `fetch` chỉ ném khi mạng hỏng / backend không chạy / chứng chỉ dev chưa được tin
    // tưởng. Lỗi HTTP (4xx, 5xx) KHÔNG ném — nó về như một response bình thường.
    throw new NetworkError(cause);
  }
}

/**
 * Một lời gọi API đã xử lý sẵn: JWT, tự refresh khi 401, và map lỗi thành `ApiError`.
 *
 * Trả `undefined` cho 204 No Content.
 */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, anonymous = false, signal } = options;
  const url = buildUrl(path, query);

  // Upload file đi bằng multipart/form-data, không phải JSON.
  const isFormData = typeof FormData !== 'undefined' && body instanceof FormData;

  const buildInit = (token: string | null): RequestInit => {
    const headers: Record<string, string> = { Accept: 'application/json' };

    // 🔴 KHÔNG đặt Content-Type cho FormData. multipart cần một `boundary=...` mà chỉ
    // trình duyệt sinh được; tự đặt `multipart/form-data` (không boundary) thì server
    // không tách được các phần và trả 400 với thông điệp chẳng liên quan gì tới nguyên
    // nhân thật. Bỏ trống thì `fetch` tự điền cả kiểu lẫn boundary.
    if (body !== undefined && !isFormData) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;

    return {
      method,
      headers,
      body:
        body === undefined ? undefined
        : isFormData ? (body as FormData)
        : JSON.stringify(body),
      // Bắt buộc để cookie refresh đi kèm. Nhờ cookie có `Path=/api/v1/auth` nên nó chỉ
      // thực sự được gửi tới các endpoint auth, không rò ra mọi request nghiệp vụ.
      credentials: 'include',
      signal,
    };
  };

  const token = anonymous ? null : await resolveAccessToken();
  let response = await send(url, buildInit(token));

  // Retry ĐÚNG MỘT LẦN sau khi refresh. Không lặp: nếu lần thứ hai vẫn 401 thì phiên đã
  // thật sự chết và thử tiếp chỉ tạo thêm lời gọi /refresh vô ích.
  const shouldRetry =
    response.status === 401 && !anonymous && token !== null && path !== AUTH_PATH.refresh;

  if (shouldRetry) {
    let refreshed: string;
    try {
      refreshed = await refreshAccessToken();
    } catch {
      // `performRefresh` đã clearSession() rồi; AuthGuard sẽ thấy status 'anonymous' và
      // điều hướng về /login. Tầng API không tự đụng vào router.
      throw await toApiError(response);
    }

    response = await send(url, buildInit(refreshed));
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
}

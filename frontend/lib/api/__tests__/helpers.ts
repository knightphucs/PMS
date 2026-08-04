import type { AuthenticatedResponse, EmployeeDto } from '@/types/auth';

/**
 * Dựng `Response` THẬT, không phải object tự chế.
 *
 * `problem.ts` đọc `headers.get('content-type')` và `http.ts` đọc `content-length` —
 * chỉ `Response` thật mới tính đúng những header đó. Một object `{ status, json() }`
 * sẽ khiến test xanh trong khi code thật đi nhánh khác.
 */
export const jsonResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json; charset=utf-8' },
  });

/** Lỗi từ `ExceptionHandlingMiddleware` và `ValidationFilter` — content-type riêng. */
export const problemResponse = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/problem+json; charset=utf-8' },
  });

/**
 * 401/403 do middleware xác thực chặn trước controller, và 429 do rate limiter — cả ba
 * KHÔNG có body và KHÔNG có content-type json.
 *
 * ⚠️ `new Response('', { status: 204 })` NÉM. Body phải là `null` cho 204/205/304.
 */
export const emptyResponse = (status: number) => new Response(null, { status });

export const EMPLOYEE: EmployeeDto = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Nguyễn Văn A',
  email: 'a@pms.test',
  systemRole: 'User',
  // Đúng tập quyền mà backend seed cho vai trò `User` (ADR-045).
  permissions: ['projects:create'],
};

/** Body của `/auth/refresh` khi thành công. */
export const authResponse = (
  accessToken: string,
  expiresInMs = 15 * 60_000,
): AuthenticatedResponse => ({
  accessToken,
  accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
  employee: EMPLOYEE,
});

/** `fetch` đã bị stub ở setup.ts — ép kiểu về mock để đọc `mock.calls`. */
export const fetchMock = () => globalThis.fetch as unknown as import('vitest').Mock;

/**
 * Trả cùng một BODY cho mọi lời gọi, nhưng dựng `Response` MỚI mỗi lần.
 *
 * ⚠️ `mockResolvedValue(jsonResponse(...))` trả về đúng một đối tượng `Response` dùng
 * lại cho mọi lời gọi. Body của `Response` chỉ đọc được MỘT lần, nên lời gọi thứ hai
 * chết với "Body is unusable" — lỗi trông như bug của code chứ không phải của test.
 */
export function alwaysJson(status: number, body: unknown): void {
  fetchMock().mockImplementation(() => Promise.resolve(jsonResponse(status, body)));
}

/** Đối số của lần gọi `fetch` thứ `index` (0-based). */
export function fetchCall(index: number): { url: string; init: RequestInit } {
  const call = fetchMock().mock.calls[index];
  if (!call) throw new Error(`Không có lời gọi fetch thứ ${index}`);
  return { url: String(call[0]), init: (call[1] ?? {}) as RequestInit };
}

export function authHeaderOf(index: number): string | undefined {
  const headers = fetchCall(index).init.headers as Record<string, string> | undefined;
  return headers?.Authorization;
}

import { describe, expect, it } from 'vitest';

import { ApiError, NetworkError } from '@/lib/api/problem';
import { refreshAccessToken } from '@/lib/api/refresh';
import { useAuthStore } from '@/store/auth-store';

import { authResponse, emptyResponse, fetchCall, fetchMock, jsonResponse } from './helpers';

/**
 * 🔴 Đây là bộ test đắt giá nhất của tầng API.
 *
 * Backend dùng rotation kèm reuse detection: hai lời gọi /refresh song song với cùng một
 * token khiến backend kết luận token bị đánh cắp và thu hồi TOÀN BỘ phiên. Triệu chứng
 * ngoài đời là "thỉnh thoảng tự đăng xuất" — không log, không lỗi, cực khó chẩn đoán.
 *
 * Phiên trước phải kiểm điều này bằng một harness biên dịch tay rồi xóa đi. Nay nó ở lại.
 */
describe('refreshAccessToken — single-flight', () => {
  it('hai lời gọi ĐỒNG THỜI chỉ tạo ĐÚNG MỘT request /refresh', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, authResponse('token-moi')));

    const [a, b] = await Promise.all([refreshAccessToken(), refreshAccessToken()]);

    expect(fetchMock()).toHaveBeenCalledTimes(1);
    expect(a).toBe('token-moi');
    expect(b).toBe('token-moi');
  });

  it('ba lời gọi đồng thời cũng vậy — tất cả bám vào cùng một promise', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, authResponse('token-moi')));

    const ket_qua = await Promise.all([
      refreshAccessToken(),
      refreshAccessToken(),
      refreshAccessToken(),
    ]);

    expect(fetchMock()).toHaveBeenCalledTimes(1);
    expect(ket_qua).toEqual(['token-moi', 'token-moi', 'token-moi']);
  });

  it('gọi LẠI sau khi lần trước đã xong thì tạo request mới (inFlight được xóa)', async () => {
    fetchMock()
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-1')))
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-2')));

    expect(await refreshAccessToken()).toBe('token-1');
    expect(await refreshAccessToken()).toBe('token-2');
    expect(fetchMock()).toHaveBeenCalledTimes(2);
  });

  it('refresh THẤT BẠI cũng phải xóa inFlight, không để lại promise hỏng', async () => {
    fetchMock()
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-sau-khi-dang-nhap-lai')));

    await expect(refreshAccessToken()).rejects.toBeInstanceOf(ApiError);

    // Nếu inFlight còn giữ promise đã reject thì lần đăng nhập kế tiếp sẽ nhận lại
    // đúng lỗi cũ mà không hề gửi request nào.
    expect(await refreshAccessToken()).toBe('token-sau-khi-dang-nhap-lai');
    expect(fetchMock()).toHaveBeenCalledTimes(2);
  });
});

describe('refreshAccessToken — hình dạng request', () => {
  it('gửi POST kèm credentials: "include" và KHÔNG có body', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, authResponse('token-moi')));

    await refreshAccessToken();
    const { url, init } = fetchCall(0);

    // Refresh token đi bằng cookie httpOnly (ADR-027). Thiếu `credentials: 'include'`
    // thì trình duyệt không đính cookie vào request cross-origin và luồng refresh hỏng
    // IM LẶNG — luôn 401 mà không có gì chỉ ra nguyên nhân.
    expect(init.credentials).toBe('include');
    expect(init.method).toBe('POST');
    expect(init.body).toBeUndefined();
    // Chữ `auth` viết thường là bắt buộc: cookie có Path=/api/v1/auth, phân biệt hoa thường.
    expect(url).toBe('https://localhost:7264/api/v1/auth/refresh');
  });
});

describe('refreshAccessToken — tác động lên phiên', () => {
  it('thành công thì ghi phiên mới vào store', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, authResponse('token-moi')));

    await refreshAccessToken();
    const state = useAuthStore.getState();

    expect(state.status).toBe('authenticated');
    expect(state.accessToken).toBe('token-moi');
    expect(state.user?.email).toBe('a@pms.test');
    // Epoch ms, không phải chuỗi ISO — resolveAccessToken làm phép trừ trên nó.
    expect(typeof state.accessTokenExpiresAt).toBe('number');
  });

  it('401 thì xóa phiên để AuthGuard đưa về /login', async () => {
    useAuthStore.setState({ accessToken: 'token-cu', status: 'authenticated' });
    fetchMock().mockResolvedValue(emptyResponse(401));

    await expect(refreshAccessToken()).rejects.toBeInstanceOf(ApiError);

    expect(useAuthStore.getState().status).toBe('anonymous');
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('mạng hỏng thì ném NetworkError, KHÔNG phải TypeError trần', async () => {
    fetchMock().mockRejectedValue(new TypeError('fetch failed'));

    await expect(refreshAccessToken()).rejects.toBeInstanceOf(NetworkError);
  });
});

import { beforeEach, describe, expect, it } from 'vitest';

import { apiFetch } from '@/lib/api/http';
import { ApiError, NetworkError } from '@/lib/api/problem';
import { useAuthStore } from '@/store/auth-store';

import {
  EMPLOYEE,
  alwaysJson,
  authHeaderOf,
  authResponse,
  emptyResponse,
  fetchCall,
  fetchMock,
  jsonResponse,
  problemResponse,
} from './helpers';

/** Phiên đang đăng nhập, token còn hạn lâu — không kích hoạt refresh chủ động. */
function dangDangNhap(token = 'token-cu') {
  useAuthStore.setState({
    accessToken: token,
    accessTokenExpiresAt: Date.now() + 15 * 60_000,
    user: EMPLOYEE,
    status: 'authenticated',
  });
}

describe('apiFetch — dựng query string', () => {
  beforeEach(() => dangDangNhap());

  it('bỏ undefined / null / chuỗi rỗng nhưng GIỮ số 0 và false', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, { items: [] }));

    await apiFetch('/projects', {
      query: {
        page: 0,
        isRead: false,
        search: '',
        sortBy: undefined,
        sortDirection: null,
        pageSize: 20,
      },
    });

    // ⚠️ Điều kiện lọc là `value === ''`, KHÔNG phải falsy. Một lần "dọn dẹp" thành
    // `if (!value) continue` sẽ lặng lẽ nuốt mất page=0 và isRead=false — đúng loại
    // lỗi mà bộ lọc "chưa đọc" của chuông thông báo sẽ dính.
    const url = new URL(fetchCall(0).url);
    expect(url.searchParams.get('page')).toBe('0');
    expect(url.searchParams.get('isRead')).toBe('false');
    expect(url.searchParams.get('pageSize')).toBe('20');
    expect(url.searchParams.has('search')).toBe(false);
    expect(url.searchParams.has('sortBy')).toBe(false);
    expect(url.searchParams.has('sortDirection')).toBe(false);
  });

  it('mã hóa ký tự đặc biệt trong giá trị query', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, {}));

    await apiFetch('/projects', { query: { search: 'kho & bãi' } });

    expect(new URL(fetchCall(0).url).searchParams.get('search')).toBe('kho & bãi');
  });

  it('nối path vào base URL không sinh dấu gạch chéo đôi', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, {}));

    await apiFetch('/projects');

    expect(fetchCall(0).url).toBe('https://localhost:7264/api/v1/projects');
  });
});

describe('apiFetch — xử lý 401 và retry', () => {
  it('401 -> refresh -> gửi lại ĐÚNG MỘT LẦN, lần gửi lại mang token MỚI', async () => {
    dangDangNhap('token-cu');
    fetchMock()
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-moi')))
      .mockResolvedValueOnce(jsonResponse(200, { id: 'p1' }));

    const ket_qua = await apiFetch<{ id: string }>('/projects/p1');

    expect(ket_qua).toEqual({ id: 'p1' });
    expect(fetchMock()).toHaveBeenCalledTimes(3);
    expect(authHeaderOf(0)).toBe('Bearer token-cu');
    expect(fetchCall(1).url).toContain('/auth/refresh');
    expect(authHeaderOf(2)).toBe('Bearer token-moi');
  });

  it('401 lần thứ hai thì DỪNG, không lặp vô hạn', async () => {
    dangDangNhap('token-cu');
    fetchMock()
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-moi')))
      .mockResolvedValueOnce(emptyResponse(401));

    await expect(apiFetch('/projects/p1')).rejects.toMatchObject({ status: 401 });

    // Đúng 3, không phải 4 hay 5: phiên đã thật sự chết, thử tiếp chỉ tạo thêm lời gọi
    // /refresh vô ích và làm reuse detection nổ.
    expect(fetchMock()).toHaveBeenCalledTimes(3);
  });

  it('refresh hỏng thì ném lỗi 401 GỐC, không nuốt mã lỗi', async () => {
    dangDangNhap('token-cu');
    fetchMock()
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(emptyResponse(401));

    await expect(apiFetch('/projects/p1')).rejects.toMatchObject({ status: 401 });
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('request `anonymous` (đăng nhập) nhận 401 thì KHÔNG kích hoạt refresh', async () => {
    fetchMock().mockResolvedValue(
      problemResponse(401, { title: 'Email hoặc mật khẩu không đúng.', status: 401 }),
    );

    await expect(
      apiFetch('/auth/login', { method: 'POST', body: {}, anonymous: true }),
    ).rejects.toMatchObject({ status: 401 });

    // Sai mật khẩu mà đi gọi /refresh là vô nghĩa, và tệ hơn là nó ăn hạn mức
    // rate limit 10 lần/phút của endpoint đó.
    expect(fetchMock()).toHaveBeenCalledTimes(1);
  });

  it('chưa đăng nhập (không có token) nhận 401 thì cũng không refresh', async () => {
    fetchMock().mockResolvedValue(emptyResponse(401));

    await expect(apiFetch('/projects')).rejects.toBeInstanceOf(ApiError);
    expect(fetchMock()).toHaveBeenCalledTimes(1);
  });
});

describe('apiFetch — refresh chủ động', () => {
  it('token sắp hết hạn thì refresh TRƯỚC khi gửi, request mang token mới', async () => {
    // 10 giây < REFRESH_SKEW_MS (60 giây).
    useAuthStore.setState({
      accessToken: 'token-sap-het-han',
      accessTokenExpiresAt: Date.now() + 10_000,
      user: EMPLOYEE,
      status: 'authenticated',
    });
    fetchMock()
      .mockResolvedValueOnce(jsonResponse(200, authResponse('token-moi')))
      .mockResolvedValueOnce(jsonResponse(200, { id: 'p1' }));

    await apiFetch('/projects/p1');

    expect(fetchMock()).toHaveBeenCalledTimes(2);
    expect(fetchCall(0).url).toContain('/auth/refresh');
    expect(authHeaderOf(1)).toBe('Bearer token-moi');
  });

  it('refresh chủ động hỏng thì vẫn gửi token cũ đi, không nuốt lỗi tại chỗ', async () => {
    useAuthStore.setState({
      accessToken: 'token-sap-het-han',
      accessTokenExpiresAt: Date.now() + 10_000,
      user: EMPLOYEE,
      status: 'authenticated',
    });
    fetchMock()
      .mockResolvedValueOnce(emptyResponse(401)) // /refresh hỏng
      .mockResolvedValueOnce(jsonResponse(200, { id: 'p1' })); // token cũ hóa ra vẫn dùng được

    await expect(apiFetch('/projects/p1')).resolves.toEqual({ id: 'p1' });
    expect(authHeaderOf(1)).toBe('Bearer token-sap-het-han');
  });
});

describe('apiFetch — hình dạng request và response', () => {
  beforeEach(() => dangDangNhap());

  it('có body thì đặt Content-Type và JSON.stringify; không body thì bỏ cả hai', async () => {
    // `alwaysJson` chứ không phải `mockResolvedValue`: test này gọi apiFetch hai lần và
    // body của một Response chỉ đọc được một lần.
    alwaysJson(200, {});
    await apiFetch('/projects', { method: 'POST', body: { name: 'Kho' } });

    const { init } = fetchCall(0);
    expect(init.body).toBe('{"name":"Kho"}');
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json');

    fetchMock().mockClear();
    await apiFetch('/projects');
    const khongBody = fetchCall(0).init;
    expect((khongBody.headers as Record<string, string>)['Content-Type']).toBeUndefined();
    expect(khongBody.body).toBeUndefined();
  });

  it('luôn gửi credentials: "include"', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, {}));

    await apiFetch('/projects');

    // Cookie có Path=/api/v1/auth nên nó chỉ thật sự đi tới 4 endpoint auth — bật cờ
    // này ở mọi request không làm rò cookie ra request nghiệp vụ.
    expect(fetchCall(0).init.credentials).toBe('include');
  });

  it('204 No Content trả về undefined, không cố parse JSON', async () => {
    fetchMock().mockResolvedValue(emptyResponse(204));

    await expect(apiFetch<void>('/projects/p1', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('fetch ném (backend chưa chạy / chứng chỉ dev chưa tin) -> NetworkError', async () => {
    fetchMock().mockRejectedValue(new TypeError('fetch failed'));

    await expect(apiFetch('/projects')).rejects.toBeInstanceOf(NetworkError);
  });

  it('lỗi nghiệp vụ 409 giữ nguyên thông điệp tiếng Việt của backend', async () => {
    fetchMock().mockResolvedValue(
      problemResponse(409, { title: 'Dự án còn task chưa hoàn thành.', status: 409 }),
    );

    await expect(apiFetch('/projects/p1', { method: 'DELETE' })).rejects.toMatchObject({
      status: 409,
      message: 'Dự án còn task chưa hoàn thành.',
    });
  });
});

describe('apiFetch — body dạng FormData (upload file đính kèm)', () => {
  beforeEach(() => dangDangNhap());

  it('KHÔNG đặt Content-Type để trình duyệt tự sinh boundary', async () => {
    fetchMock().mockResolvedValue(jsonResponse(201, { id: 'a1' }));

    const form = new FormData();
    form.append('file', new Blob([new Uint8Array([1, 2, 3])]), 'anh.png');

    await apiFetch('/tasks/t1/attachments', { method: 'POST', body: form });

    const headers = fetchCall(0).init.headers as Record<string, string>;

    // Tự đặt 'multipart/form-data' (không boundary) thì server không tách được các phần
    // và trả 400 với thông điệp chẳng liên quan gì tới nguyên nhân thật.
    expect(headers['Content-Type']).toBeUndefined();
    // Vẫn phải giữ Authorization — upload là endpoint cần đăng nhập.
    expect(headers.Authorization).toBe('Bearer token-cu');
  });

  it('gửi FormData NGUYÊN TRẠNG, không JSON.stringify', async () => {
    fetchMock().mockResolvedValue(jsonResponse(201, { id: 'a1' }));

    const form = new FormData();
    form.append('file', new Blob(['noi dung']), 'tai-lieu.pdf');

    await apiFetch('/tasks/t1/attachments', { method: 'POST', body: form });

    // JSON.stringify(FormData) cho ra chuỗi '{}' — file biến mất im lặng và server nhận
    // một request rỗng. Đây chính là hành vi của apiFetch TRƯỚC 2026-08-03.
    expect(fetchCall(0).init.body).toBe(form);
  });

  it('vẫn JSON.stringify body thường — nhánh FormData không làm hỏng đường cũ', async () => {
    fetchMock().mockResolvedValue(jsonResponse(200, {}));

    await apiFetch('/labels', { method: 'POST', body: { name: 'bug' } });

    const { init } = fetchCall(0);
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json');
    expect(init.body).toBe('{"name":"bug"}');
  });
});

import { describe, expect, it } from 'vitest';

import { ApiError, NetworkError, errorMessage, toApiError } from '@/lib/api/problem';

import { emptyResponse, jsonResponse, problemResponse } from './helpers';

/**
 * Bốn hình dạng lỗi mà backend thật sự phát ra (khối chú thích đầu `problem.ts`).
 * Mỗi hình dạng có một cách hỏng riêng, nên phải test riêng từng cái.
 */
describe('toApiError — hình dạng 1: ProblemDetails từ ExceptionHandlingMiddleware', () => {
  it('lấy thông điệp từ `title`, không phải `detail`', async () => {
    const error = await toApiError(
      problemResponse(409, {
        title: 'Không thể xóa dự án còn task chưa hoàn thành.',
        status: 409,
        detail: null,
        traceId: '00-abc-123',
      }),
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error.message).toBe('Không thể xóa dự án còn task chưa hoàn thành.');
    expect(error.status).toBe(409);
    expect(error.traceId).toBe('00-abc-123');
    expect(error.isConflict).toBe(true);
    expect(error.fieldErrors).toBeUndefined();
    expect(error.isValidation).toBe(false);
  });

  it('404 KHÔNG được diễn giải thành "không có quyền" (ADR-006/019)', async () => {
    const error = await toApiError(emptyResponse(404));

    expect(error.isNotFound).toBe(true);
    expect(error.isForbidden).toBe(false);
    expect(error.message).not.toMatch(/quyền/i);
  });
});

describe('toApiError — hình dạng 2: ValidationProblemDetails từ ValidationFilter', () => {
  it('chuẩn hóa key PascalCase sang camelCase để khớp tên field của react-hook-form', async () => {
    const error = await toApiError(
      problemResponse(400, {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          Name: ['Tên dự án không được để trống.'],
          RowVersion: ['RowVersion là bắt buộc.'],
        },
      }),
    );

    expect(Object.keys(error.fieldErrors!)).toEqual(['name', 'rowVersion']);
    expect(error.isValidation).toBe(true);
  });

  it('lấy thông điệp tiếng Việt đầu tiên làm message, KHÔNG lấy `title` tiếng Anh', async () => {
    const error = await toApiError(
      problemResponse(400, {
        title: 'One or more validation errors occurred.',
        errors: { Name: ['Tên dự án không được để trống.'] },
      }),
    );

    // Hiện `title` ra là đưa câu tiếng Anh mặc định của framework cho người dùng cuối.
    expect(error.message).toBe('Tên dự án không được để trống.');
    expect(error.message).not.toContain('validation errors occurred');
  });
});

describe('toApiError — hình dạng 3: ValidationProblemDetails từ [ApiController] binder', () => {
  it('giữ NGUYÊN VĂN key đường dẫn binder, kể cả dấu `$`', async () => {
    // Đây là test bắt được đúng thứ mà một lần "dọn dẹp" toCamelCase trông vô hại sẽ
    // làm hỏng: hạ chữ cái đầu của '$.priority' cho ra '$.priority' (không đổi), nhưng
    // bỏ nhánh `startsWith('$')` đi thì key rỗng hoặc key lạ sẽ vỡ.
    const error = await toApiError(
      problemResponse(400, {
        errors: {
          '$.priority': ['The JSON value could not be converted to Priority.'],
          request: ['The request field is required.'],
        },
      }),
    );

    expect(Object.keys(error.fieldErrors!)).toEqual(['$.priority', 'request']);
  });
});

describe('toApiError — hình dạng 4: phản hồi KHÔNG có body', () => {
  it.each([
    [401, 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.'],
    [403, 'Bạn không có quyền thực hiện thao tác này.'],
    [429, 'Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút.'],
  ])('%i không body -> dùng thông điệp mặc định, không ném SyntaxError', async (status, expected) => {
    const error = await toApiError(emptyResponse(status));

    expect(error.status).toBe(status);
    expect(error.message).toBe(expected);
  });

  it('content-type nói JSON nhưng body hỏng -> vẫn giữ được mã lỗi', async () => {
    const broken = new Response('{ khong-phai-json', {
      status: 500,
      headers: { 'content-type': 'application/json' },
    });

    const error = await toApiError(broken);

    expect(error.status).toBe(500);
    expect(error.message).toBe('Đã có lỗi xảy ra phía máy chủ.');
  });

  it('body là HTML (trang lỗi của web server) -> không cố parse', async () => {
    const html = new Response('<!doctype html><h1>500</h1>', {
      status: 500,
      headers: { 'content-type': 'text/html' },
    });

    expect((await toApiError(html)).message).toBe('Đã có lỗi xảy ra phía máy chủ.');
  });

  it('mã lỗi không có trong bảng mặc định vẫn cho ra thông điệp đọc được', async () => {
    expect((await toApiError(emptyResponse(418))).message).toBe('Yêu cầu thất bại (HTTP 418).');
  });

  it('`title` rỗng hoặc toàn khoảng trắng thì rơi về thông điệp mặc định', async () => {
    const error = await toApiError(jsonResponse(403, { title: '   ', status: 403 }));

    expect(error.message).toBe('Bạn không có quyền thực hiện thao tác này.');
  });
});

describe('errorMessage', () => {
  it('đọc được mọi loại lỗi, kể cả thứ không phải Error', () => {
    expect(errorMessage(new ApiError(409, 'Xung đột dữ liệu.'))).toBe('Xung đột dữ liệu.');
    expect(errorMessage(new NetworkError(new TypeError('failed')))).toMatch(/không kết nối được/i);
    expect(errorMessage(new Error('lỗi thường'))).toBe('lỗi thường');
    expect(errorMessage('một chuỗi trần')).toBe('Đã có lỗi không xác định xảy ra.');
    expect(errorMessage(undefined)).toBe('Đã có lỗi không xác định xảy ra.');
  });
});

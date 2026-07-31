/**
 * Chuyển phản hồi lỗi của backend thành một loại lỗi duy nhất mà UI hiểu được.
 *
 * Backend phát ra BỐN hình dạng lỗi khác nhau, không phải một:
 *
 * 1. `ProblemDetails` (RFC 7807) từ `ExceptionHandlingMiddleware` — `{ title, status,
 *    traceId }`. Thông điệp tiếng Việt nằm ở `title`, KHÔNG phải `detail` (`detail` luôn
 *    null ở đường này).
 * 2. `ValidationProblemDetails` từ `ValidationFilter` — có thêm `errors`, `title` là câu
 *    tiếng Anh mặc định của framework còn thông điệp tiếng Việt nằm trong từng mảng.
 * 3. `ValidationProblemDetails` từ `[ApiController]` khi JSON hỏng hoặc route param sai
 *    kiểu — cùng mã 400 nhưng key của `errors` là đường dẫn binder (`$.priority`).
 * 4. KHÔNG CÓ BODY: 401/403 do middleware xác thực chặn trước khi vào controller, và 429
 *    do rate limiter. Gọi `res.json()` ở đây sẽ ném `SyntaxError` và nuốt mất mã lỗi thật.
 */

/** Thông điệp mặc định cho các phản hồi không kèm body. */
const FALLBACK_MESSAGE: Record<number, string> = {
  400: 'Dữ liệu gửi lên không hợp lệ.',
  401: 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.',
  403: 'Bạn không có quyền thực hiện thao tác này.',
  // ⚠️ 404 KHÔNG được nói "không có quyền". Người ngoài project nhận 404 một cách CỐ Ý
  // để không lộ sự tồn tại của project/task/comment (ADR-006/019, OWASP API1:2023).
  // Hiện "bạn không có quyền" ở đây là làm hỏng đúng cái mà quyết định đó bảo vệ.
  404: 'Không tìm thấy dữ liệu bạn yêu cầu.',
  409: 'Dữ liệu đã thay đổi hoặc thao tác không hợp lệ ở trạng thái hiện tại.',
  429: 'Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút.',
  500: 'Đã có lỗi xảy ra phía máy chủ.',
};

export class ApiError extends Error {
  readonly status: number;
  readonly traceId?: string;
  /** Map field -> danh sách thông điệp. Chỉ có ở lỗi validate (400). */
  readonly fieldErrors?: Record<string, string[]>;

  constructor(
    status: number,
    message: string,
    options?: { traceId?: string; fieldErrors?: Record<string, string[]> },
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.traceId = options?.traceId;
    this.fieldErrors = options?.fieldErrors;
  }

  get isValidation() {
    return this.status === 400 && this.fieldErrors !== undefined;
  }

  /** 404 nghĩa là "không tìm thấy HOẶC không được phép biết" — xem chú thích phía trên. */
  get isNotFound() {
    return this.status === 404;
  }

  /** 403 chỉ xảy ra khi ĐÃ là thành viên project nhưng role không đủ. */
  get isForbidden() {
    return this.status === 403;
  }

  get isConflict() {
    return this.status === 409;
  }
}

/** Lỗi mạng / backend không chạy — `fetch` ném TypeError chứ không trả response. */
export class NetworkError extends Error {
  constructor(cause: unknown) {
    super('Không kết nối được tới máy chủ. Kiểm tra backend đã chạy chưa.');
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

/**
 * `errors` của `ValidationProblemDetails` có key PascalCase (`Name`, `RowVersion`) trong
 * khi mọi thứ khác trong API đều camelCase — vì `DictionaryKeyPolicy` không được cấu
 * hình ở backend. react-hook-form đăng ký field theo tên camelCase, nên không chuẩn hóa
 * thì `setError('Name')` trỏ vào một field không tồn tại và lỗi biến mất không dấu vết.
 */
function toCamelCase(key: string): string {
  // Đường dẫn binder của [ApiController] (`$.priority`, `request`) giữ nguyên.
  if (key.startsWith('$') || key.length === 0) return key;
  return key.charAt(0).toLowerCase() + key.slice(1);
}

function normalizeFieldErrors(errors: Record<string, string[]>): Record<string, string[]> {
  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => [toCamelCase(key), messages]),
  );
}

interface ProblemDetailsShape {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** Đọc phản hồi lỗi một cách an toàn trước cả bốn hình dạng ở đầu file. */
export async function toApiError(response: Response): Promise<ApiError> {
  const status = response.status;
  const fallback = FALLBACK_MESSAGE[status] ?? `Yêu cầu thất bại (HTTP ${status}).`;

  // Kiểm tra content-type TRƯỚC khi parse. 401/403/429 không có body.
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('json')) {
    return new ApiError(status, fallback);
  }

  let problem: ProblemDetailsShape;
  try {
    problem = (await response.json()) as ProblemDetailsShape;
  } catch {
    // Content-Type nói JSON nhưng body rỗng hoặc hỏng — vẫn phải giữ được mã lỗi.
    return new ApiError(status, fallback);
  }

  if (problem.errors && Object.keys(problem.errors).length > 0) {
    const fieldErrors = normalizeFieldErrors(problem.errors);
    // `title` ở đường validate là câu tiếng Anh mặc định của framework, không đưa ra cho
    // người dùng. Lấy thông điệp tiếng Việt đầu tiên trong `errors` làm tóm tắt.
    const firstMessage = Object.values(fieldErrors)[0]?.[0];
    return new ApiError(status, firstMessage ?? fallback, {
      traceId: problem.traceId,
      fieldErrors,
    });
  }

  return new ApiError(status, problem.title?.trim() || fallback, {
    traceId: problem.traceId,
  });
}

/** Lấy thông điệp hiển thị được từ bất kỳ lỗi nào, kể cả lỗi không phải của API. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError || error instanceof NetworkError) return error.message;
  if (error instanceof Error && error.message) return error.message;
  return 'Đã có lỗi không xác định xảy ra.';
}

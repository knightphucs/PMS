import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import { ApiError } from '@/lib/api/problem';

/**
 * Gắn lỗi validate của backend ngược vào form.
 *
 * Zod đã chặn phần lớn ở client, nhưng có những luật chỉ backend biết (email đã tồn tại,
 * ngày phải ở tương lai so với giờ máy chủ) — và đăng nhập bằng mật khẩu yếu trả 400
 * chứ không phải 401. Không gắn ngược thì người dùng thấy một dòng lỗi chung chung mà
 * không biết ô nào sai.
 *
 * Key đã được `problem.ts` chuẩn hóa từ PascalCase sang camelCase; ở đây chỉ lọc bỏ
 * những key không khớp field nào để react-hook-form không giữ lỗi treo vĩnh viễn trên
 * một field không tồn tại.
 *
 * @returns `true` nếu đã gắn được ít nhất một lỗi vào field — khi đó không cần toast nữa.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  knownFields: readonly Path<T>[],
): boolean {
  if (!(error instanceof ApiError) || !error.fieldErrors) return false;

  let applied = false;

  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    if (!knownFields.includes(field as Path<T>)) continue;
    setError(field as Path<T>, { type: 'server', message: messages[0] });
    applied = true;
  }

  return applied;
}

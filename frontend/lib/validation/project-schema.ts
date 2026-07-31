import { z } from 'zod';

/** Ngày hôm nay dạng `yyyy-MM-dd`, dùng làm `min` cho `<input type="date">`. */
export function todayIsoDate(): string {
  const now = new Date();
  const offsetMinutes = now.getTimezoneOffset();
  return new Date(now.getTime() - offsetMinutes * 60_000).toISOString().slice(0, 10);
}

/**
 * Soi gương `CreateProjectRequestValidator.cs`.
 *
 * Backend yêu cầu `ExpectedCompletionDate > DateTime.UtcNow.Date`, tức là NGÀY MAI trở
 * đi tính theo UTC. Máy người dùng ở UTC+7 nên "hôm nay" theo giờ địa phương có thể vẫn
 * là hôm qua theo UTC — đó là lý do luật ở đây so với hôm nay theo giờ máy chứ không cố
 * bắt chước chính xác biên UTC. Trường hợp lệch múi giờ vẫn để backend từ chối, và lỗi
 * đó được gắn ngược vào đúng field nhờ `applyServerErrors`.
 */
export const createProjectSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Vui lòng nhập tên dự án.')
    .max(200, 'Tên dự án tối đa 200 ký tự.'),
  description: z.string().trim().max(2000, 'Mô tả tối đa 2000 ký tự.'),
  expectedCompletionDate: z
    .string()
    .min(1, 'Vui lòng chọn ngày dự kiến hoàn thành.')
    .refine((value) => value > todayIsoDate(), {
      message: 'Ngày dự kiến hoàn thành phải ở tương lai.',
    }),
});

export type CreateProjectValues = z.infer<typeof createProjectSchema>;

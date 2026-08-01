import { z } from 'zod';

/**
 * Soi gương `CreateSprintRequestValidator.cs`.
 *
 * ⚠️ KHÔNG có `rowVersion`: Sprint không dùng optimistic concurrency, nên cũng không có
 * luồng 409 "dữ liệu đã cũ" như Project/Task. Sửa đồng thời là last-write-wins.
 */
export const sprintSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, 'Vui lòng nhập tên sprint.')
      .max(200, 'Tên sprint tối đa 200 ký tự.'),
    goal: z.string().trim().max(500, 'Mục tiêu tối đa 500 ký tự.'),
    startDate: z.string().min(1, 'Vui lòng chọn ngày bắt đầu.'),
    endDate: z.string().min(1, 'Vui lòng chọn ngày kết thúc.'),
  })
  // Backend yêu cầu `EndDate > StartDate` (không được bằng nhau). Gắn lỗi vào `endDate`
  // vì đó là ô người dùng vừa sửa và sẽ sửa tiếp.
  .refine((values) => values.endDate > values.startDate, {
    message: 'Ngày kết thúc phải sau ngày bắt đầu.',
    path: ['endDate'],
  });

export type SprintValues = z.infer<typeof sprintSchema>;

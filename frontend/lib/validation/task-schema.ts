import { z } from 'zod';

/**
 * Soi gương `CreateTaskRequestValidator.cs` / `UpdateTaskRequestValidator.cs`.
 *
 * ⚠️ `dueDate` ĐƯỢC PHÉP nằm trong quá khứ — backend cố ý không chặn (task quá hạn là
 * chuyện có thật, và người dùng cần nhập được task đã trễ). Khác hẳn
 * `Project.ExpectedCompletionDate` vốn bắt buộc ở tương lai.
 *
 * `rowVersion` KHÔNG nằm trong schema: nó không phải trường người dùng nhập mà được mang
 * theo từ lần `GET /tasks/{id}` gần nhất.
 */
export const taskSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Vui lòng nhập tên task.')
    .max(200, 'Tên task tối đa 200 ký tự.'),
  priority: z.enum(['Highest', 'High', 'Medium', 'Low', 'Lowest']),
  /** Chuỗi rỗng = không đặt hạn; đổi thành `null` khi gửi lên. */
  dueDate: z.string(),
  /** Chuỗi rỗng = Backlog; đổi thành `null` khi gửi lên. */
  sprintId: z.string(),
});

export type TaskValues = z.infer<typeof taskSchema>;

/** Trường nullable phải gửi `null` THẬT, không phải chuỗi rỗng. */
export const toNullableIso = (value: string) => (value ? `${value}T00:00:00Z` : null);
export const toNullableId = (value: string) => (value ? value : null);

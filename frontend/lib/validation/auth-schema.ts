import { z } from 'zod';

/**
 * Soi gương `PMS.Application/Features/Auth/Validators/AuthValidator.cs`.
 *
 * Mục đích là phản hồi NGAY cho người dùng, không phải thay thế backend — backend vẫn
 * validate độc lập và là nguồn sự thật cuối cùng.
 *
 * ⚠️ `LoginRequestValidator` phía backend cũng áp ĐÚNG bộ luật độ phức tạp mật khẩu này.
 * Nghĩa là đăng nhập bằng mật khẩu yếu trả **400 kèm lỗi field**, không phải 401. Form
 * đăng nhập vì vậy bắt buộc phải hiển thị được lỗi cấp field, không chỉ một dòng lỗi
 * chung — nếu không người dùng sẽ thấy "sai email hoặc mật khẩu" cho một lỗi định dạng.
 */
const password = z
  .string()
  .min(1, 'Vui lòng nhập mật khẩu.')
  .min(8, 'Mật khẩu tối thiểu 8 ký tự.')
  .regex(/[A-Z]/, 'Mật khẩu phải có ít nhất 1 chữ hoa.')
  .regex(/[a-z]/, 'Mật khẩu phải có ít nhất 1 chữ thường.')
  .regex(/[0-9]/, 'Mật khẩu phải có ít nhất 1 chữ số.');

const email = z
  .string()
  .min(1, 'Vui lòng nhập email.')
  .max(256, 'Email tối đa 256 ký tự.')
  .email('Email không đúng định dạng.');

export const loginSchema = z.object({ email, password });

export const registerSchema = z
  .object({
    name: z.string().min(1, 'Vui lòng nhập họ tên.').max(100, 'Họ tên tối đa 100 ký tự.'),
    email,
    password,
    confirmPassword: z.string().min(1, 'Vui lòng nhập lại mật khẩu.'),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Mật khẩu nhập lại không khớp.',
    path: ['confirmPassword'],
  });

/**
 * Quên mật khẩu — CHỈ kiểm định dạng email.
 *
 * Không có và sẽ không bao giờ có luật kiểu "email phải tồn tại": backend cố ý trả 204 cho
 * mọi email (ADR-041) để endpoint này không thành công cụ dò xem ai đã đăng ký. Thêm một
 * phép kiểm tồn tại ở client là dựng lại đúng kênh rò rỉ đó ở phía bên kia.
 */
export const forgotPasswordSchema = z.object({ email });

export const resetPasswordSchema = z
  .object({
    newPassword: password,
    confirmPassword: z.string().min(1, 'Vui lòng nhập lại mật khẩu.'),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Mật khẩu nhập lại không khớp.',
    path: ['confirmPassword'],
  });

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;

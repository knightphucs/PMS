'use client';

import { EyeIcon, EyeOffIcon } from 'lucide-react';
import * as React from 'react';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

/**
 * shadcn/ui v4 không còn component `form` (đã bỏ khi chuyển sang Base UI), nên đây là
 * lớp mỏng nối react-hook-form với Label + Input. Ít phép màu hơn và giải thích được
 * từng dòng trong báo cáo.
 */
interface FieldProps extends React.ComponentProps<'input'> {
  label: string;
  error?: string;
  hint?: string;
}

/**
 * `type="password"` được xử lý RIÊNG ở ĐÚNG MỘT chỗ: mọi màn có ô mật khẩu (đăng nhập,
 * đăng ký, đặt lại mật khẩu, đổi mật khẩu) đều đi qua `Field`, nên thêm nút bật/tắt hiện
 * mật khẩu ở đây là tự động có mặt khắp nơi — không phải sửa bốn chỗ và chắc chắn có lúc
 * quên một chỗ.
 */
export const Field = React.forwardRef<HTMLInputElement, FieldProps>(function Field(
  { label, error, hint, id, className, type, ...props },
  ref,
) {
  const generatedId = React.useId();
  const fieldId = id ?? generatedId;
  const errorId = `${fieldId}-error`;
  const hintId = `${fieldId}-hint`;
  const isPassword = type === 'password';
  const [visible, setVisible] = React.useState(false);

  return (
    <div className="grid gap-2">
      <Label htmlFor={fieldId}>{label}</Label>
      <div className="relative">
        <Input
          id={fieldId}
          ref={ref}
          // Chỉ đổi type lúc RENDER, không đụng gì tới `value`/`onChange` của
          // react-hook-form — bật/tắt hiện mật khẩu không phải là sửa dữ liệu.
          type={isPassword ? (visible ? 'text' : 'password') : type}
          aria-invalid={error ? true : undefined}
          // Trình đọc màn hình phải đọc được lỗi, không chỉ nhìn thấy chữ đỏ.
          aria-describedby={error ? errorId : hint ? hintId : undefined}
          className={cn(
            error && 'border-destructive focus-visible:ring-destructive/30',
            isPassword && 'pr-8',
            className,
          )}
          {...props}
        />

        {isPassword ? (
          <button
            type="button"
            onClick={() => setVisible((v) => !v)}
            aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
            // `tabIndex` mặc định của button (0) — vẫn bấm được bằng bàn phím, không né nó
            // khỏi thứ tự Tab như một số nơi hay làm cho nút phụ.
            className="text-muted-foreground hover:text-foreground absolute inset-y-0 right-0 grid w-8 place-items-center transition-colors"
          >
            {visible ? <EyeOffIcon className="size-3.5" /> : <EyeIcon className="size-3.5" />}
          </button>
        ) : null}
      </div>

      {error ? (
        <p id={errorId} role="alert" className="text-destructive text-sm">
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="text-muted-foreground text-sm">
          {hint}
        </p>
      ) : null}
    </div>
  );
});

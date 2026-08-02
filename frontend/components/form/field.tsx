'use client';

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

export const Field = React.forwardRef<HTMLInputElement, FieldProps>(function Field(
  { label, error, hint, id, className, ...props },
  ref,
) {
  const generatedId = React.useId();
  const fieldId = id ?? generatedId;
  const errorId = `${fieldId}-error`;
  const hintId = `${fieldId}-hint`;

  return (
    <div className="grid gap-2">
      <Label htmlFor={fieldId}>{label}</Label>
      <Input
        id={fieldId}
        ref={ref}
        aria-invalid={error ? true : undefined}
        // Trình đọc màn hình phải đọc được lỗi, không chỉ nhìn thấy chữ đỏ.
        aria-describedby={error ? errorId : hint ? hintId : undefined}
        className={cn(error && 'border-destructive focus-visible:ring-destructive/30', className)}
        {...props}
      />
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

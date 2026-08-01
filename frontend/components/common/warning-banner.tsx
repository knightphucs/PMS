import { AlertTriangleIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

/**
 * Cảnh báo "cần chú ý nhưng chưa hỏng" — nằm giữa `Alert variant="destructive"` (đã lỗi)
 * và text thường.
 *
 * Dùng biến `--warning*` chứ không phải `bg-amber-50/text-amber-900` cứng như trước:
 * màu cố định của Tailwind không đổi theo chế độ sáng/tối, nên đúng những chỗ cảnh báo
 * này sẽ là chỗ đầu tiên vỡ khi bật dark mode.
 *
 * Chỗ dùng đầu tiên là luồng 409 của ADR-016, và luồng đó sẽ lặp lại nguyên xi ở màn
 * sửa task — nên tách ra thay vì chép lần thứ hai.
 */
export function WarningBanner({
  title,
  children,
  className,
}: {
  title: string;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      role="alert"
      className={cn(
        'border-warning-border bg-warning-surface text-warning-foreground flex gap-2.5 rounded-lg border p-3 text-sm',
        className,
      )}
    >
      <AlertTriangleIcon className="text-warning mt-0.5 size-4 shrink-0" />
      <div className="grid gap-1">
        <p className="font-medium">{title}</p>
        {children ? <div>{children}</div> : null}
      </div>
    </div>
  );
}

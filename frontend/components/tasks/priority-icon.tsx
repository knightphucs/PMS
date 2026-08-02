import {
  ChevronDownIcon,
  ChevronUpIcon,
  ChevronsDownIcon,
  ChevronsUpIcon,
  MinusIcon,
  type LucideIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import { PRIORITY_LABEL, type Priority } from '@/types/enums';

/**
 * Mũi tên lên/xuống kiểu Jira.
 *
 * Quét mắt qua một cột 20 thẻ bằng biểu tượng nhanh hơn nhiều so với đọc chữ — đây là
 * lý do mọi công cụ theo dõi việc đều dùng icon cho Priority chứ không dùng badge chữ.
 *
 * `Record<Priority, …>` để thêm mức ưu tiên mới là lỗi biên dịch tại đây.
 */
const PRIORITY_STYLE: Record<Priority, { icon: LucideIcon; tone: string }> = {
  Highest: { icon: ChevronsUpIcon, tone: 'text-[oklch(0.55_0.20_25)] dark:text-[oklch(0.72_0.17_25)]' },
  High: { icon: ChevronUpIcon, tone: 'text-[oklch(0.62_0.16_45)] dark:text-[oklch(0.76_0.14_50)]' },
  Medium: { icon: MinusIcon, tone: 'text-[oklch(0.65_0.13_75)] dark:text-[oklch(0.80_0.12_80)]' },
  Low: { icon: ChevronDownIcon, tone: 'text-[oklch(0.58_0.10_200)] dark:text-[oklch(0.74_0.09_200)]' },
  Lowest: { icon: ChevronsDownIcon, tone: 'text-muted-foreground' },
};

/** Chỉ icon — cho thẻ Kanban, nơi chiều ngang quý. Tên đầy đủ nằm ở `title`. */
export function PriorityIcon({
  priority,
  className,
}: {
  priority: Priority;
  className?: string;
}) {
  const { icon: Icon, tone } = PRIORITY_STYLE[priority];
  const nhan = `Độ ưu tiên: ${PRIORITY_LABEL[priority]}`;

  // Bọc trong <span>: icon của lucide không nhận prop `title`, mà chỉ có icon không kèm
  // chữ thì người dùng phải đoán nghĩa của mũi tên.
  return (
    <span className="inline-flex" title={nhan} aria-label={nhan} role="img">
      <Icon className={cn('size-4 shrink-0', tone, className)} aria-hidden />
    </span>
  );
}

/** Icon + chữ — cho bảng backlog và form, nơi có chỗ và cần rõ ràng. */
export function PriorityLabel({
  priority,
  className,
}: {
  priority: Priority;
  className?: string;
}) {
  const { icon: Icon, tone } = PRIORITY_STYLE[priority];
  return (
    <span className={cn('inline-flex items-center gap-1.5', className)}>
      <Icon className={cn('size-4 shrink-0', tone)} aria-hidden />
      {PRIORITY_LABEL[priority]}
    </span>
  );
}

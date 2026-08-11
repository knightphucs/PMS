'use client';

import * as icons from 'lucide-react';

import { cn } from '@/lib/utils';
import type { TaskTypeRef } from '@/types/task';

/**
 * Chip loại công việc (ADR-060) — dùng chung cho thẻ board, hàng backlog và chi tiết task.
 *
 * 🔴 Icon tra theo TÊN từ bảng của `lucide-react`. Backend đã chặn ký tự lạ bằng regex, và
 * ở đây tên không khớp rơi về `CircleDot` thay vì ném — một icon sai là hỏng nhẹ nhìn thấy
 * được, còn một màn hình trắng thì không.
 */
export function WorkItemTypeChip({
  type,
  showLabel = true,
  className,
}: {
  type: TaskTypeRef;
  showLabel?: boolean;
  className?: string;
}) {
  const Icon =
    (icons as unknown as Record<string, icons.LucideIcon | undefined>)[type.icon] ??
    icons.CircleDot;

  return (
    <span
      className={cn(
        'inline-flex min-w-0 items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium',
        className,
      )}
      style={{ color: type.color, backgroundColor: `${type.color}1A` }}
      title={type.name}
    >
      <Icon className="size-3.5 shrink-0" aria-hidden />
      {showLabel ? <span className="truncate">{type.name}</span> : null}
      {!showLabel ? <span className="sr-only">{type.name}</span> : null}
    </span>
  );
}

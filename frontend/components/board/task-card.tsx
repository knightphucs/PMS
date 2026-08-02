'use client';

import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { CalendarIcon, GitBranchIcon, MoreHorizontalIcon } from 'lucide-react';

import { AvatarStack } from '@/components/common/avatar-stack';
import { PriorityIcon } from '@/components/tasks/priority-icon';
import { Button } from '@/components/ui/button';
import { formatShortDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { TaskSummaryResponse } from '@/types/task';

interface Props {
  task: TaskSummaryResponse;
  /** `false` = không đủ quyền HOẶC thẻ đang có mutation bay dở. */
  canDrag: boolean;
  /** Lý do không kéo được, hiện ở tooltip gốc của trình duyệt. */
  disabledReason?: string;
  onOpenMenu?: (task: TaskSummaryResponse) => void;
  /** Bản vẽ trong `DragOverlay` — không gắn listener, không mờ đi. */
  overlay?: boolean;
}

export function TaskCard({ task, canDrag, disabledReason, onOpenMenu, overlay }: Props) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: task.id,
    // Đưa cả object vào `data` để `onDragStart`/`onDragEnd` biết trạng thái hiện tại của
    // thẻ mà không phải tra ngược lại trong cache.
    data: { task },
    disabled: !canDrag || overlay,
  });

  return (
    <article
      ref={overlay ? undefined : setNodeRef}
      style={overlay ? undefined : { transform: CSS.Translate.toString(transform) }}
      {...(overlay ? {} : attributes)}
      {...(overlay ? {} : listeners)}
      title={canDrag || overlay ? undefined : disabledReason}
      className={cn(
        'bg-card grid gap-2 rounded-lg border p-2.5 shadow-xs transition-shadow',
        canDrag && !overlay && 'cursor-grab hover:shadow-sm active:cursor-grabbing',
        !canDrag && !overlay && 'cursor-default',
        // Thẻ nguồn mờ đi chứ KHÔNG ẩn: ẩn thì cột tụt chiều cao và mọi thẻ dưới nó nhảy
        // lên, làm mất luôn cái đích mà người dùng đang nhắm tới.
        isDragging && 'opacity-40',
        overlay && 'shadow-lg ring-primary/40 rotate-1 cursor-grabbing ring-2',
      )}
    >
      <div className="flex items-start gap-1">
        <h3 className="line-clamp-2 flex-1 text-[13px] leading-snug font-medium">
          {task.name}
        </h3>
        {onOpenMenu && !overlay ? (
          <Button
            variant="ghost"
            size="icon-xs"
            className="-mt-0.5 -mr-1 shrink-0"
            aria-label={`Thao tác với ${task.name}`}
            // Ngăn cú bấm chậm biến thành thao tác kéo. Ràng buộc distance 6px của
            // PointerSensor đã đỡ phần lớn, nhưng không đỡ được click giữ lâu.
            onPointerDown={(event) => event.stopPropagation()}
            onClick={() => onOpenMenu(task)}
          >
            <MoreHorizontalIcon className="size-3.5" />
          </Button>
        ) : null}
      </div>

      {/* Thanh tiến độ subtask CHỈ hiện khi > 0.
          `subtaskProgress` là phần trăm 0–100, và `0` không phân biệt được "không có
          subtask" với "có subtask nhưng chưa xong cái nào" — TaskSummaryResponse không
          mang số lượng subtask. Hiện 0% trên task không có subtask nào còn tệ hơn là
          không hiện gì. */}
      {task.subtaskProgress > 0 ? (
        <div className="flex items-center gap-1.5">
          <GitBranchIcon className="text-muted-foreground size-3 shrink-0" />
          <div className="bg-muted h-1 flex-1 overflow-hidden rounded-full">
            <div
              className="bg-primary h-full rounded-full"
              style={{ width: `${task.subtaskProgress}%` }}
            />
          </div>
          <span className="text-muted-foreground text-[11px] tabular-nums">
            {Math.round(task.subtaskProgress)}%
          </span>
        </div>
      ) : null}

      <div className="flex items-center gap-1.5">
        <PriorityIcon priority={task.priority} />

        {task.dueDate ? (
          <span
            className={cn(
              'inline-flex items-center gap-1 text-[11px] tabular-nums',
              // `isOverdue` là giá trị TÍNH SẴN phía server — đừng so ngày lại ở client.
              task.isOverdue ? 'text-destructive font-medium' : 'text-muted-foreground',
            )}
          >
            <CalendarIcon className="size-3" />
            {formatShortDate(task.dueDate)}
            {task.isOverdue ? <span className="sr-only">(quá hạn)</span> : null}
          </span>
        ) : null}

        <div className="flex-1" />
        <AvatarStack people={task.assignees} max={2} size="sm" />
      </div>
    </article>
  );
}

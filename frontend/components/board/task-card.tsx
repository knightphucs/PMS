'use client';

import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { CalendarIcon, GitBranchIcon } from 'lucide-react';
import Link from 'next/link';

import { AvatarStack } from '@/components/common/avatar-stack';
import { PriorityIcon } from '@/components/tasks/priority-icon';
import { formatShortDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { TaskSummaryResponse } from '@/types/task';

interface Props {
  task: TaskSummaryResponse;
  /**
   * Đường dẫn tới chi tiết task. Bỏ trống ở bản vẽ trong `DragOverlay` — overlay chỉ là
   * ảnh, không được có gì bấm được.
   */
  href?: string;
  /** `false` = không đủ quyền HOẶC thẻ đang có mutation bay dở. */
  canDrag: boolean;
  /** Lý do không kéo được, hiện ở tooltip gốc của trình duyệt. */
  disabledReason?: string;
  /**
   * Menu thao tác, truyền vào dưới dạng slot thay vì callback: thẻ không cần biết gì về
   * sprint, quyền hay dialog nào — nó chỉ vẽ.
   */
  menu?: React.ReactNode;
  /** Bản vẽ trong `DragOverlay` — không gắn listener, không mờ đi. */
  overlay?: boolean;
}

export function TaskCard({ task, href, canDrag, disabledReason, menu, overlay }: Props) {
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
        <div className="min-w-0 flex-1">
          {/* Mã task do backend ghép sẵn (ADR-034) — dấu hiệu nhận dạng số một của một
              công cụ theo dõi việc, và là thứ người ta đọc cho nhau nghe qua điện thoại. */}
          <span className="text-muted-foreground block text-[11px] font-medium tabular-nums">
            {task.code}
          </span>

          <h3 className="line-clamp-2 text-[13px] leading-snug font-medium">
            {href && !overlay ? (
              // Kéo–thả KHÔNG bị hỏng vì link: `board-view.tsx` đặt `PointerSensor` với
              // `activationConstraint: { distance: 6 }`, nên một cú bấm không di chuyển
              // không bao giờ khởi động thao tác kéo — và một thao tác kéo đã khởi động
              // thì không kết thúc bằng `click`.
              <Link href={href} className="hover:text-primary transition-colors">
                {task.name}
              </Link>
            ) : (
              task.name
            )}
          </h3>
        </div>
        {menu && !overlay ? <div className="-mt-0.5 -mr-1 shrink-0">{menu}</div> : null}
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

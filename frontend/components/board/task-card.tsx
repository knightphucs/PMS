'use client';

import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { CalendarIcon, ChevronRightIcon, GitBranchIcon, PinIcon, UserPlusIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { AvatarStack } from '@/components/common/avatar-stack';
import { TaskCardSubtasks } from '@/components/board/task-card-subtasks';
import { PriorityIcon } from '@/components/tasks/priority-icon';
import { formatShortDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { TaskStatusRef, TaskSummaryResponse } from '@/types/task';

type TaskCardTask = Omit<TaskSummaryResponse, 'status'> & {
  status: TaskSummaryResponse['status'] | TaskStatusRef;
};

interface Props {
  task: TaskCardTask;
  /** Cần để dựng link chi tiết subtask khi mở dropdown — thẻ không tự biết mình ở project nào. */
  projectId: string;
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
  /** `undefined` = ẩn hẳn nút ghim (không đủ quyền) — cùng triết lý ẩn thay vì vô hiệu hóa. */
  onTogglePin?: (task: TaskCardTask) => void;
  /** Request ghim/gỡ ghim của CHÍNH thẻ này đang bay — vô hiệu hóa nút để khỏi bấm hai lần. */
  isPinning?: boolean;
  /** `undefined` = ẩn hẳn (Viewer, hoặc không đủ quyền giao việc). */
  onAssignClick?: (task: TaskCardTask) => void;
  /** Bản vẽ trong `DragOverlay` — không gắn listener, không mờ đi, không có gì bấm được. */
  overlay?: boolean;
}

export function TaskCard({
  task,
  projectId,
  href,
  canDrag,
  disabledReason,
  menu,
  onTogglePin,
  isPinning,
  onAssignClick,
  overlay,
}: Props) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: task.id,
    // Đưa cả object vào `data` để `onDragStart`/`onDragEnd` biết trạng thái hiện tại của
    // thẻ mà không phải tra ngược lại trong cache.
    data: { task },
    disabled: !canDrag || overlay,
  });

  // Cục bộ cho riêng thẻ này — dropdown subtask của thẻ A mở/đóng không ảnh hưởng thẻ B.
  const [subtasksOpen, setSubtasksOpen] = useState(false);

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
        // Viền/nền nhạt riêng cho thẻ đã ghim — tín hiệu nhận ra ngay không cần trỏ vào
        // nút ghim để biết trạng thái. Không dùng màu đậm: đây là một cờ phụ, không phải
        // trạng thái/độ ưu tiên nên không được cạnh tranh sự chú ý với hai thứ đó.
        task.isPinned && !overlay && 'border-primary/30 bg-primary/[0.03]',
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

        {(onTogglePin || menu) && !overlay ? (
          <div className="-mt-0.5 -mr-1 flex shrink-0 items-center">
            {onTogglePin ? (
              <button
                type="button"
                onClick={() => onTogglePin(task)}
                disabled={isPinning}
                // Ngăn cú bấm biến thành thao tác kéo — cùng lý do TaskMenu đã áp dụng:
                // ràng buộc distance 6px của PointerSensor không đỡ được click giữ lâu,
                // và TouchSensor coi 220ms giữ yên là bắt đầu kéo bất kể có click hay không.
                onPointerDown={(event) => event.stopPropagation()}
                aria-pressed={task.isPinned}
                aria-label={task.isPinned ? `Bỏ ghim ${task.name}` : `Ghim ${task.name}`}
                title={task.isPinned ? 'Bỏ ghim' : 'Ghim — luôn đứng đầu cột'}
                className={cn(
                  'grid size-6 shrink-0 place-items-center rounded transition-colors',
                  'hover:bg-accent disabled:pointer-events-none disabled:opacity-50',
                  task.isPinned ? 'text-primary' : 'text-muted-foreground',
                )}
              >
                <PinIcon className={cn('size-3.5', task.isPinned && '-rotate-45 fill-current')} />
              </button>
            ) : null}
            {menu}
          </div>
        ) : null}
      </div>

      {/* Cụm subtask: CHỈ hiện khi có subtask (`subtaskCount > 0`), không phải
          `subtaskProgress > 0` như trước — hai điều kiện đó khác nhau đúng ở task có
          subtask nhưng CHƯA xong cái nào (progress = 0 nhưng count > 0), và bản cũ bỏ sót
          case đó. Bấm để xổ ra danh sách subtask, tải lười qua `TaskCardSubtasks`. */}
      {task.subtaskCount > 0 ? (
        <div className="grid gap-1.5">
          <button
            type="button"
            onClick={() => setSubtasksOpen((open) => !open)}
            onPointerDown={(event) => event.stopPropagation()}
            aria-expanded={subtasksOpen}
            aria-label={`${subtasksOpen ? 'Thu gọn' : 'Mở'} ${task.subtaskCount} subtask`}
            className="group flex min-w-0 items-center gap-1.5"
          >
            <ChevronRightIcon
              className={cn(
                'text-muted-foreground size-3 shrink-0 transition-transform',
                subtasksOpen && 'rotate-90',
              )}
            />
            <GitBranchIcon className="text-muted-foreground size-3 shrink-0" />
            <div className="bg-muted h-1 flex-1 overflow-hidden rounded-full">
              <div
                className="bg-primary h-full rounded-full"
                style={{ width: `${task.subtaskProgress}%` }}
              />
            </div>
            <span className="text-muted-foreground shrink-0 text-[11px] tabular-nums">
              {Math.round(task.subtaskProgress)}% · {task.subtaskCount}
            </span>
          </button>

          {subtasksOpen ? <TaskCardSubtasks projectId={projectId} taskId={task.id} /> : null}
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

        {onAssignClick && !overlay ? (
          <button
            type="button"
            onClick={() => onAssignClick(task)}
            onPointerDown={(event) => event.stopPropagation()}
            aria-label={task.assignees.length > 0 ? `Đổi người đảm nhận ${task.name}` : `Giao việc ${task.name}`}
            title={task.assignees.length > 0 ? 'Đổi người đảm nhận' : 'Giao việc'}
            className="text-muted-foreground hover:text-foreground shrink-0 rounded-full transition-colors"
          >
            {task.assignees.length > 0 ? (
              <AvatarStack people={task.assignees} max={2} size="sm" />
            ) : (
              <UserPlusIcon className="size-5" />
            )}
          </button>
        ) : (
          <AvatarStack people={task.assignees} max={2} size="sm" />
        )}
      </div>
    </article>
  );
}

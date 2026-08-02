'use client';

import { useDroppable } from '@dnd-kit/core';

import { TaskCard } from '@/components/board/task-card';
import { STATUS_TONE } from '@/components/tasks/status-tone';
import { canTransition } from '@/lib/tasks/status-transitions';
import { cn } from '@/lib/utils';
import { STATUS_LABEL, type Status } from '@/types/enums';
import type { TaskSummaryResponse } from '@/types/task';

interface Props {
  status: Status;
  tasks: TaskSummaryResponse[];
  /** Thẻ đang được kéo, `null` khi không kéo gì. */
  activeTask: TaskSummaryResponse | null;
  canDragTask: (task: TaskSummaryResponse) => boolean;
  dragDisabledReason: string;
  /** Menu thao tác cho từng thẻ; `null` khi người dùng không có quyền nào. */
  renderMenu?: (task: TaskSummaryResponse) => React.ReactNode;
}

export function BoardColumn({
  status,
  tasks,
  activeTask,
  canDragTask,
  dragDisabledReason,
  renderMenu,
}: Props) {
  /**
   * 🔴 MỘT nguồn sự thật cho cả hành vi lẫn hình thức.
   *
   * `eligible` được dùng bởi `disabled` của `useDroppable` VÀ bởi `className`, trong cùng
   * một lần render — nên không thể có chuyện cột trông như thả được mà lại không thả
   * được, hay ngược lại.
   *
   * Và đây là chỗ chặn thả sai bằng CẤU TRÚC chứ không phải bằng `if` trong handler:
   * dnd-kit lọc droppable qua `getEnabled()` TRƯỚC khi chạy collision detection, nên cột
   * bị `disabled` không hề là ứng viên va chạm — `over` về `null` và `onDragEnd` không có
   * gì để làm. Không request nào được tạo ra, không toast đỏ nào bắn lên.
   */
  const eligible = activeTask !== null && canTransition(activeTask.status, status);

  const { setNodeRef, isOver } = useDroppable({
    id: status,
    disabled: !eligible,
  });

  const dragging = activeTask !== null;

  return (
    <section
      ref={setNodeRef}
      aria-label={`${STATUS_LABEL[status]} — ${tasks.length} task`}
      className={cn(
        'bg-muted/40 flex min-h-0 flex-col rounded-lg p-2 ring-1 transition-colors',
        'ring-transparent',
        dragging && !eligible && 'opacity-45',
        eligible && 'ring-primary/30',
        isOver && eligible && 'bg-primary/5 ring-primary/60 ring-2',
      )}
    >
      <header className="mb-2 flex shrink-0 items-center gap-2 px-1">
        <span className={cn('size-2 shrink-0 rounded-full', STATUS_TONE[status].dot)} />
        <h2 className="text-muted-foreground flex-1 text-xs font-semibold tracking-wide uppercase">
          {STATUS_LABEL[status]}
        </h2>
        <span className="bg-background text-muted-foreground rounded px-1.5 py-0.5 text-[11px] font-medium tabular-nums">
          {tasks.length}
        </span>
      </header>

      <div className="flex min-h-24 flex-col gap-2 overflow-y-auto">
        {tasks.length === 0 ? (
          <p className="text-muted-foreground/70 grid flex-1 place-items-center rounded-lg border border-dashed py-6 text-xs">
            {eligible ? 'Thả vào đây' : 'Trống'}
          </p>
        ) : (
          tasks.map((task) => (
            <TaskCard
              key={task.id}
              task={task}
              canDrag={canDragTask(task)}
              disabledReason={dragDisabledReason}
              menu={renderMenu?.(task)}
            />
          ))
        )}
      </div>
    </section>
  );
}

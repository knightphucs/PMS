'use client';

import { useDroppable } from '@dnd-kit/core';
import { ChevronRightIcon, PlusIcon } from 'lucide-react';

import { TaskCard } from '@/components/board/task-card';
import { columnDotStyle } from '@/components/tasks/status-tone';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { BoardColumnResponse, TaskSummaryResponse } from '@/types/task';

interface Props {
  column: BoardColumnResponse;
  tasks: TaskSummaryResponse[];
  /** Để dựng link tới chi tiết task. Cột không tự biết mình thuộc project nào. */
  projectId: string;
  /** Thẻ đang được kéo, `null` khi không kéo gì. */
  activeTask: TaskSummaryResponse | null;
  canDragTask: (task: TaskSummaryResponse) => boolean;
  dragDisabledReason: string;
  collapsed: boolean;
  onToggleCollapse: () => void;
  /** Menu thao tác cho từng thẻ; `null` khi người dùng không có quyền nào. */
  renderMenu?: (task: TaskSummaryResponse) => React.ReactNode;
  /** `undefined` = ẩn hẳn nút ghim trên mọi thẻ (không đủ quyền UpdateTask). */
  onTogglePin?: (task: TaskSummaryResponse) => void;
  /** Id các task đang có request ghim/gỡ ghim bay dở — disable đúng MỘT thẻ, không cả cột. */
  pinningIds?: ReadonlySet<string>;
  /** `undefined` = ẩn hẳn (Viewer). Mở dialog "Người đảm nhận" khi bấm cụm avatar trên thẻ. */
  onAssignClick?: (task: TaskSummaryResponse) => void;
  /**
   * `undefined` = ẩn hẳn nút "+" trên header cột (không đủ quyền CreateTask). Gọi lại với
   * chính `column.id` — task mới rơi ĐÚNG cột này, dù cột đang trống hay đã có task
   * (2026-08-06).
   */
  onCreateTask?: (columnId: string) => void;
}

export function BoardColumn({
  column,
  tasks,
  projectId,
  activeTask,
  canDragTask,
  dragDisabledReason,
  collapsed,
  onToggleCollapse,
  renderMenu,
  onTogglePin,
  pinningIds,
  onAssignClick,
  onCreateTask,
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
  /**
   * ⚠️ Sau ADR-052 điều kiện chỉ còn "khác cột đang đứng", KHÔNG còn tra ma trận chuyển
   * trạng thái: với cột do người dùng tạo thì mọi cột đều là đích hợp lệ.
   *
   * Giữ phép loại trừ chính cột nguồn vì thả về chỗ cũ là một request không làm gì —
   * backend nay trả 200 thay vì 409, nhưng không tạo ra request vẫn tốt hơn.
   */
  const eligible = activeTask !== null && activeTask.status.columnId !== column.id;

  const { setNodeRef, isOver } = useDroppable({
    id: column.id,
    // Cột đang thu vẫn nhận thả được: người dùng thu nó lại để đỡ chật, không phải để
    // khóa nó. Thả vào cột thu là cách nhanh nhất đẩy một thẻ ra khỏi tầm mắt.
    disabled: !eligible,
  });

  const dragging = activeTask !== null;

  if (collapsed) {
    return (
      <section
        ref={setNodeRef}
        aria-label={`${column.name} — ${tasks.length} task (đang thu)`}
        className={cn(
          'bg-muted/40 flex flex-col items-center gap-2 rounded-lg p-2 ring-1 transition-colors',
          'ring-transparent',
          isOver && eligible && 'bg-primary/5 ring-primary/60 ring-2',
        )}
      >
        <button
          type="button"
          onClick={onToggleCollapse}
          aria-expanded={false}
          aria-label={`Mở cột ${column.name}`}
          className="hover:bg-accent grid size-6 shrink-0 place-items-center rounded transition-colors"
        >
          <ChevronRightIcon className="size-4" />
        </button>

        <span className="size-2 shrink-0 rounded-full" style={columnDotStyle(column.color)} />

        {/* Chữ dọc: cột thu chỉ rộng ~2.5rem nên tên phải quay 90°, đúng cách Jira làm.
            `writing-mode` không có tiện ích Tailwind nên đặt thẳng bằng style. */}
        <span
          className="text-muted-foreground flex-1 text-xs font-semibold tracking-wide whitespace-nowrap uppercase"
          style={{ writingMode: 'vertical-rl' }}
        >
          {column.name}
        </span>

        <span className="bg-background text-muted-foreground rounded px-1.5 py-0.5 text-[11px] font-medium tabular-nums">
          {tasks.length}
        </span>
      </section>
    );
  }

  return (
    <section
      ref={setNodeRef}
      aria-label={`${column.name} — ${tasks.length} task`}
      className={cn(
        'bg-muted/40 flex min-h-0 flex-col rounded-lg p-2 ring-1 transition-colors',
        'ring-transparent',
        dragging && !eligible && 'opacity-45',
        eligible && 'ring-primary/30',
        isOver && eligible && 'bg-primary/5 ring-primary/60 ring-2',
      )}
    >
      <header className="mb-2 flex shrink-0 items-center gap-2 px-1">
        <button
          type="button"
          onClick={onToggleCollapse}
          aria-expanded
          aria-label={`Thu cột ${column.name}`}
          className="hover:bg-accent grid size-5 shrink-0 place-items-center rounded transition-colors"
        >
          <ChevronRightIcon className="size-3.5 rotate-90" />
        </button>

        <span className="size-2 shrink-0 rounded-full" style={columnDotStyle(column.color)} />

        <h2 className="text-muted-foreground min-w-0 flex-1 truncate text-xs font-semibold tracking-wide uppercase">
          {column.name}
        </h2>
        <span className="bg-background text-muted-foreground rounded px-1.5 py-0.5 text-[11px] font-medium tabular-nums">
          {tasks.length}
        </span>

        {onCreateTask ? (
          <Button
            variant="ghost"
            size="icon-xs"
            aria-label={`Tạo task trong ${column.name}`}
            title="Tạo task trong cột này"
            onClick={() => onCreateTask(column.id)}
          >
            <PlusIcon className="size-3.5" />
          </Button>
        ) : null}
      </header>

      <div className="flex min-h-24 flex-col gap-2 overflow-y-auto">
        {tasks.length === 0 ? (
          // Không dùng `/70`: đã đo được 2.71:1 ở chế độ sáng, dưới xa chuẩn AA 4.5:1.
          <p className="text-muted-foreground grid flex-1 place-items-center rounded-lg border border-dashed py-6 text-xs">
            {eligible ? 'Thả vào đây' : 'Trống'}
          </p>
        ) : (
          tasks.map((task) => (
            <TaskCard
              key={task.id}
              task={task}
              projectId={projectId}
              href={`/projects/${projectId}/tasks/${task.id}`}
              canDrag={canDragTask(task)}
              disabledReason={dragDisabledReason}
              menu={renderMenu?.(task)}
              onTogglePin={onTogglePin}
              isPinning={pinningIds?.has(task.id)}
              onAssignClick={onAssignClick}
            />
          ))
        )}
      </div>
    </section>
  );
}

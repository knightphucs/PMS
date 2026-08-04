'use client';

import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  pointerWithin,
  rectIntersection,
  useSensor,
  useSensors,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { useState } from 'react';
import { toast } from 'sonner';

import { BoardColumn } from '@/components/board/board-column';
import { TaskCard } from '@/components/board/task-card';
import { errorMessage } from '@/lib/api/problem';
import { useChangeTaskStatus } from '@/lib/hooks/use-board';
import { canChangeTaskStatus } from '@/lib/tasks/permissions';
import { canTransition } from '@/lib/tasks/status-transitions';
import { STATUS_LABEL, type RoleInProject, type Status } from '@/types/enums';
import type { BoardResponse, TaskSummaryResponse } from '@/types/task';

/**
 * Cảm biến.
 *
 * `distance: 6` để một cú bấm vào nút menu trên thẻ không bị nuốt thành thao tác kéo.
 * `TouchSensor` có `delay` vì nếu chỉ dùng PointerSensor thì dnd-kit đặt
 * `touch-action: none` lên thẻ và người dùng KHÔNG CUỘN được cột trên màn cảm ứng.
 */
function useBoardSensors() {
  return useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 220, tolerance: 8 } }),
    useSensor(KeyboardSensor),
  );
}

/**
 * `pointerWithin` = "cột mà con trỏ đang thực sự nằm trong", đúng với điều người dùng
 * tin là đang xảy ra.
 *
 * KHÔNG dùng `closestCorners` (khuyến nghị thường thấy cho Kanban): nó dành cho droppable
 * ở cấp THẺ. Với bốn hình chữ nhật cột lớn, nó sẽ vui vẻ chọn cột bên cạnh khi con trỏ
 * đang ở khe giữa hai cột.
 *
 * ⚠️ Nhánh dự phòng `rectIntersection` là BẮT BUỘC: kéo bằng BÀN PHÍM không có con trỏ
 * nên `pointerWithin` luôn trả rỗng — thiếu nó thì kéo bằng phím không bao giờ thả được.
 */
const collisionDetection: CollisionDetection = (args) => {
  const byPointer = pointerWithin(args);
  return byPointer.length > 0 ? byPointer : rectIntersection(args);
};

export function BoardView({
  projectId,
  sprintId,
  board,
  role,
  myEmployeeId,
  renderMenu,
}: {
  projectId: string;
  sprintId: string | null;
  board: BoardResponse;
  role: RoleInProject | null;
  myEmployeeId: string | null;
  renderMenu?: (task: TaskSummaryResponse) => React.ReactNode;
}) {
  const sensors = useBoardSensors();
  const changeStatus = useChangeTaskStatus(projectId, sprintId);
  const [activeTask, setActiveTask] = useState<TaskSummaryResponse | null>(null);

  /**
   * Các task đang có request bay dở.
   *
   * Không có nó thì người dùng kéo thẻ thứ hai trước khi mutation thứ nhất xong sẽ gặp
   * lỗi thật: mỗi `onMutate` chụp lại TOÀN BỘ board, nên rollback của #2 sẽ xóa luôn
   * thay đổi của #1. Đây là bug đúng nghĩa, không phải chuyện đánh bóng.
   */
  const [pending, setPending] = useState<ReadonlySet<string>>(new Set());

  const canDragTask = (task: TaskSummaryResponse) => {
    if (pending.has(task.id)) return false;
    // ADR-017: assignee của CHÍNH task đó, hoặc ProjectManager. Có được nhờ
    // `TaskSummaryResponse.assignees` — không phải gọi N+1 lần /tasks/{id}/assignees.
    const isAssignee = task.assignees.some((a) => a.employeeId === myEmployeeId);
    return canChangeTaskStatus(role, isAssignee);
  };

  const handleDragStart = (event: DragStartEvent) => {
    setActiveTask((event.active.data.current?.task as TaskSummaryResponse) ?? null);
  };

  const handleDragEnd = async (event: DragEndEvent) => {
    const task = event.active.data.current?.task as TaskSummaryResponse | undefined;
    setActiveTask(null);

    if (!event.over || !task) return;
    const target = event.over.id as Status;

    // Về lý thuyết không bao giờ tới được đây với bước không hợp lệ (cột đã bị `disabled`
    // nên không phải ứng viên va chạm). Giữ lại như bảo hiểm rẻ cho cảm biến bàn phím.
    if (!canTransition(task.status, target)) return;

    setPending((prev) => new Set(prev).add(task.id));
    try {
      await changeStatus.mutateAsync({ taskId: task.id, target });
    } catch (error) {
      // Hai lỗi client KHÔNG đoán trước được, và cả hai đều đã có câu tiếng Việt giải
      // thích đúng lý do ở `title` của backend:
      //   409 — task đang bị TaskLink loại IsBlockedBy chặn (chỉ khi đích là InProgress)
      //   403 — không phải assignee và cũng không phải PM
      // Cache đã tự trả thẻ về chỗ cũ ở `onError` của hook.
      toast.error(errorMessage(error));
    } finally {
      setPending((prev) => {
        const next = new Set(prev);
        next.delete(task.id);
        return next;
      });
    }
  };

  const dragDisabledReason =
    role === 'Viewer'
      ? 'Người xem không đổi được trạng thái task.'
      : 'Chỉ quản lý dự án hoặc người được giao mới đổi được trạng thái.';

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={collisionDetection}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setActiveTask(null)}
    >
      {/* Backend LUÔN trả đủ 4 cột kể cả cột rỗng — không phải tự dựng cột thiếu. */}
      <div className="grid grid-cols-1 items-start gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {board.columns.map((column) => (
          <BoardColumn
            key={column.status}
            status={column.status}
            tasks={column.tasks}
            projectId={projectId}
            activeTask={activeTask}
            canDragTask={canDragTask}
            dragDisabledReason={dragDisabledReason}
            renderMenu={renderMenu}
          />
        ))}
      </div>

      <p aria-live="polite" className="sr-only">
        {activeTask
          ? `Đang kéo ${activeTask.name} từ cột ${STATUS_LABEL[activeTask.status]}.`
          : ''}
      </p>

      {/* Đặt ở gốc DndContext, NGOÀI các cột — cột có `overflow-y-auto` nên sẽ cắt mất
          lớp phủ nếu render bên trong.
          `dropAnimation={null}` vì thẻ sẽ được thay bằng bản cập nhật lạc quan ngay lập
          tức; hiệu ứng bay-về mặc định đọc thành "thả không thành công". */}
      <DragOverlay dropAnimation={null}>
        {activeTask ? <TaskCard task={activeTask} canDrag={false} overlay /> : null}
      </DragOverlay>
    </DndContext>
  );
}

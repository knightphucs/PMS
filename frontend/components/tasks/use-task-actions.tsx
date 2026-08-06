'use client';

import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { AssigneeDialog } from '@/components/tasks/assignee-dialog';
import { TaskFormDialog } from '@/components/tasks/task-form-dialog';
import { TaskMenu } from '@/components/tasks/task-menu';
import { errorMessage } from '@/lib/api/problem';
import { useSprints } from '@/lib/hooks/use-sprints';
import { useDeleteTask, useMoveTaskToSprint } from '@/lib/hooks/use-tasks';
import { canManageTasks, canSelfAssign } from '@/lib/tasks/permissions';
import type { RoleInProject } from '@/types/enums';
import type { TaskSummaryResponse } from '@/types/task';

/**
 * Toàn bộ thao tác trên một task, gom một chỗ để Board và Backlog dùng CHUNG.
 *
 * Không gom thì hai màn hình sẽ có hai bộ dialog riêng và chắc chắn trôi khỏi nhau —
 * nhất là ở những chỗ tinh tế như luồng 409 của `rowVersion` hay thông báo xóa.
 *
 * Trả về `renderMenu` (slot cho thẻ / dòng bảng) và `dialogs` (đặt một lần ở cuối trang).
 */
export function useTaskActions({
  projectId,
  role,
  myEmployeeId,
  defaultSprintId = null,
}: {
  projectId: string;
  role: RoleInProject | null;
  myEmployeeId: string | null;
  /** Sprint gợi ý khi tạo task mới từ màn hình này. */
  defaultSprintId?: string | null;
}) {
  const sprints = useSprints(projectId);
  const moveToSprint = useMoveTaskToSprint(projectId);
  const deleteTask = useDeleteTask(projectId);

  /**
   * `null` = dialog tạo đang đóng. `{ columnId: null }` = mở từ nút "Tạo task" chung (cột
   * trái nhất). `{ columnId: "…" }` = mở từ nút "+" trên MỘT cột cụ thể (2026-08-06).
   */
  const [creating, setCreating] = useState<{ columnId: string | null } | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [assigning, setAssigning] = useState<TaskSummaryResponse | null>(null);
  const [deleting, setDeleting] = useState<TaskSummaryResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  /**
   * Task đang có request chuyển sprint bay dở.
   *
   * ⚠️ Cố ý KHÔNG cập nhật lạc quan ở đây, khác hẳn kéo–thả trên board: lạc quan đáng
   * giá khi mắt người dùng đang bám theo một vật thể di chuyển. Một dòng biến mất khỏi
   * bảng sau cú bấm menu không tạo ra kỳ vọng đó, trong khi bề mặt rollback lại gấp ba
   * số cache (backlog + hai board + taskCount của hai sprint). Làm mờ dòng là đủ.
   */
  const [movingIds, setMovingIds] = useState<ReadonlySet<string>>(new Set());

  const canManage = canManageTasks(role);
  const canAssign = canManage || canSelfAssign(role);

  const handleMoveToSprint = async (
    task: TaskSummaryResponse,
    sprintId: string | null,
  ) => {
    if (task.sprintId === sprintId) return; // no-op, không phải gọi API

    setMovingIds((prev) => new Set(prev).add(task.id));
    try {
      await moveToSprint.mutateAsync({ taskId: task.id, sprintId });
      const ten = sprintId
        ? ((sprints.data ?? []).find((s) => s.id === sprintId)?.name ?? 'sprint')
        : 'Backlog';
      toast.success(`Đã chuyển "${task.name}" sang ${ten}.`);
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setMovingIds((prev) => {
        const next = new Set(prev);
        next.delete(task.id);
        return next;
      });
    }
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);

    try {
      await deleteTask.mutateAsync(deleting.id);
      toast.success(`Đã xóa "${deleting.name}".`);
      setDeleting(null);
    } catch (error) {
      // 409 = còn subtask chưa hoàn thành. Giữ dialog mở để đọc được câu giải thích.
      setDeleteError(errorMessage(error));
    }
  };

  const renderMenu = (task: TaskSummaryResponse) => (
    <TaskMenu
      task={task}
      sprints={sprints.data ?? []}
      canManage={canManage}
      canAssign={canAssign}
      isMoving={movingIds.has(task.id)}
      actions={{
        onEdit: (t) => setEditingId(t.id),
        onAssign: setAssigning,
        onDelete: setDeleting,
        onMoveToSprint: handleMoveToSprint,
      }}
    />
  );

  const dialogs = (
    <>
      <TaskFormDialog
        projectId={projectId}
        open={creating !== null || editingId !== null}
        taskId={editingId}
        defaultSprintId={defaultSprintId}
        defaultColumnId={creating?.columnId ?? null}
        onClose={() => {
          setCreating(null);
          setEditingId(null);
        }}
      />

      <AssigneeDialog
        projectId={projectId}
        task={assigning}
        role={role}
        myEmployeeId={myEmployeeId}
        onClose={() => setAssigning(null)}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Xóa task?"
        description={
          <>
            <strong className="text-foreground">{deleting?.name}</strong> sẽ bị xóa. Nếu
            task này còn subtask chưa hoàn thành, hệ thống sẽ từ chối.
          </>
        }
        confirmLabel="Xóa task"
        pendingLabel="Đang xóa…"
        error={deleteError}
        isPending={deleteTask.isPending}
        onConfirm={handleDelete}
        onClose={() => {
          setDeleting(null);
          setDeleteError(null);
        }}
      />
    </>
  );

  return {
    canManage,
    canAssign,
    movingIds,
    renderMenu,
    dialogs,
    /**
     * `columnId` = bấm "+" trên một cột cụ thể; bỏ trống = nút "Tạo task" chung (cột trái
     * nhất, hành vi cũ).
     */
    openCreate: (columnId?: string) => setCreating({ columnId: columnId ?? null }),
    /** Mở thẳng dialog "Người đảm nhận" — dùng cho lối tắt bấm avatar trên thẻ Kanban. */
    openAssign: setAssigning,
  };
}

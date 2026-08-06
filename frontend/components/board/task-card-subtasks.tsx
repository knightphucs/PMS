'use client';

import { UserPlusIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { AvatarStack } from '@/components/common/avatar-stack';
import { AssigneeDialog } from '@/components/tasks/assignee-dialog';
import { TaskStatusChip } from '@/components/tasks/task-status-chip';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useTask } from '@/lib/hooks/use-tasks';
import { canManageTasks, canSelfAssign } from '@/lib/tasks/permissions';
import type { TaskSummaryResponse } from '@/types/task';

/**
 * Danh sách subtask xổ ra NGAY trên thẻ Kanban, khi bấm mở rộng.
 *
 * 🔴 Tải LAZY: `useTask` chỉ nạp khi component này thật sự được render (tức là đã mở), nhờ
 * `enabled: taskId !== null` sẵn có trong hook — không có endpoint hay hook mới nào, và
 * board KHÔNG kèm sẵn subtask cho mọi thẻ (sẽ phồng payload cho những board không ai mở
 * dropdown nào cả).
 *
 * 📌 Đọc-thôi: bấm một dòng mở trang chi tiết của SUBTASK đó, không đổi trạng thái ngay tại
 * đây. Cùng tinh thần `TaskSubtasks` ở màn chi tiết task, chỉ gọn hơn cho vừa một thẻ.
 */
export function TaskCardSubtasks({ projectId, taskId }: { projectId: string; taskId: string }) {
  const detail = useTask(projectId, taskId);
  const { role, myEmployeeId } = useMyProjectRole(projectId);
  const [assigning, setAssigning] = useState<TaskSummaryResponse | null>(null);

  if (detail.isPending) {
    return (
      <div className="grid gap-1 pl-4" aria-busy="true">
        <span className="sr-only">Đang tải danh sách subtask…</span>
        <Skeleton className="h-5" />
        <Skeleton className="h-5" />
      </div>
    );
  }

  if (detail.isError) {
    return (
      <p className="text-muted-foreground pl-4 text-[11px]">Không tải được danh sách subtask.</p>
    );
  }

  return (
    <>
      <ul className="grid gap-1 pl-4">
        {detail.data.subtasks.map((subtask) => (
          <li key={subtask.id} className="hover:bg-accent grid min-w-0 gap-1 rounded px-1 py-1">
          <Link
            href={`/projects/${projectId}/tasks/${subtask.id}`}
            onPointerDown={(event) => event.stopPropagation()}
            className="flex min-w-0 items-center gap-1.5 text-[11px] transition-colors"
          >
            <span className="text-muted-foreground shrink-0 font-medium tabular-nums">
              {subtask.code}
            </span>
            <span className="min-w-0 flex-1 truncate">{subtask.name}</span>
          </Link>
          <div className="flex min-w-0 items-center gap-1.5 pl-0.5">
            <TaskStatusChip status={subtask.status} className="max-w-28 shrink text-[10px]" />
            <div className="flex-1" />
            <AvatarStack people={subtask.assignees} max={2} size="sm" />
            {(canManageTasks(role) || canSelfAssign(role)) ? (
              <button
                type="button"
                onClick={() => setAssigning(subtask)}
                onPointerDown={(event) => event.stopPropagation()}
                aria-label={`Giao người đảm nhận cho ${subtask.name}`}
                title="Giao việc"
                className="text-muted-foreground hover:text-foreground grid size-5 shrink-0 place-items-center rounded transition-colors"
              >
                <UserPlusIcon className="size-3.5" />
              </button>
            ) : null}
          </div>
        </li>
        ))}
      </ul>

      <AssigneeDialog
        projectId={projectId}
        task={assigning}
        role={role}
        myEmployeeId={myEmployeeId}
        onClose={() => setAssigning(null)}
      />
    </>
  );
}

'use client';

import { HandIcon, UserPlusIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { UserAvatar } from '@/components/common/user-avatar';
import { AssigneeDialog } from '@/components/tasks/assignee-dialog';
import { Button } from '@/components/ui/button';
import { errorMessage } from '@/lib/api/problem';
import { useSelfAssignTask } from '@/lib/hooks/use-tasks';
import { canManageTasks, canSelfAssign } from '@/lib/tasks/permissions';
import { ROLE_IN_TASK_LABEL, type RoleInProject } from '@/types/enums';
import type { TaskDetailResponse } from '@/types/task';

export function TaskAssigneesField({
  projectId,
  task,
  role,
  myEmployeeId,
}: {
  projectId: string;
  task: TaskDetailResponse;
  role: RoleInProject | null;
  myEmployeeId: string | null;
}) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const selfAssign = useSelfAssignTask(projectId);

  const alreadyMine = task.assignees.some(
    (assignee) => assignee.employeeId === myEmployeeId,
  );

  // Hai quyền KHÁC nhau, không gộp: gán người khác = chỉ PM; tự nhận = PM + Member.
  const canAssignOthers = canManageTasks(role);
  const canTakeIt = canSelfAssign(role) && !alreadyMine;

  return (
    <>
      <div className="grid min-w-0 gap-2">
        {task.assignees.length === 0 ? (
          <span className="text-muted-foreground text-sm">Chưa giao cho ai</span>
        ) : (
          <ul className="grid min-w-0 gap-2">
            {task.assignees.map((assignee) => (
              <li key={assignee.employeeId} className="grid min-w-0 grid-cols-[auto_minmax(0,1fr)] items-start gap-2">
                <UserAvatar
                  id={assignee.employeeId}
                  name={assignee.employeeName}
                  size="sm"
                />
                {/* `roleInTask` chỉ có ở DTO chi tiết, thẻ Kanban không mang theo — đây là
                    chỗ duy nhất hiển thị được nó. */}
                <div className="grid min-w-0 gap-0.5">
                  <span className="break-words text-[13px] leading-5">
                    {assignee.employeeName}
                    {assignee.employeeId === myEmployeeId ? (
                      <span className="text-muted-foreground"> (bạn)</span>
                    ) : null}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    {ROLE_IN_TASK_LABEL[assignee.roleInTask]}
                  </span>
                </div>
              </li>
            ))}
          </ul>
        )}

        <div className="grid gap-1.5">
          {canTakeIt ? (
            <Button
              variant="outline"
              size="sm"
              className="w-full"
              disabled={selfAssign.isPending}
              onClick={() =>
                selfAssign.mutate(task.id, {
                  onSuccess: () => toast.success('Bạn đã nhận task này.'),
                  onError: (error) => toast.error(errorMessage(error)),
                })
              }
            >
              <HandIcon className="size-4" />
              Tự nhận việc
            </Button>
          ) : null}

          {canAssignOthers || canSelfAssign(role) ? (
            <Button
              variant="ghost"
              size="sm"
              className="w-full"
              onClick={() => setDialogOpen(true)}
            >
              <UserPlusIcon className="size-4" />
              {canAssignOthers ? 'Giao việc' : 'Xem danh sách'}
            </Button>
          ) : null}
        </div>
      </div>

      <AssigneeDialog
        projectId={projectId}
        task={dialogOpen ? task : null}
        role={role}
        myEmployeeId={myEmployeeId}
        onClose={() => setDialogOpen(false)}
      />
    </>
  );
}

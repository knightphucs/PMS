'use client';

import { CheckIcon, UserPlusIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { UserAvatar } from '@/components/common/user-avatar';
import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useMembers } from '@/lib/hooks/use-members';
import { useAssignTask, useUnassignTask } from '@/lib/hooks/use-tasks';
import { canManageTasks, canSelfAssign } from '@/lib/tasks/permissions';
import { cn } from '@/lib/utils';
import { ROLE_IN_PROJECT_LABEL, type RoleInProject } from '@/types/enums';

/** Phần duy nhất của một task mà dialog này cần. */
export interface TaskAssignable {
  id: string;
  name: string;
  assignees: readonly { employeeId: string }[];
}

/**
 * Giao việc.
 *
 * Hai quyền KHÁC nhau, không gộp được (§5 "Quy tắc gán việc"):
 *   • Gán/gỡ NGƯỜI KHÁC — chỉ ProjectManager
 *   • Tự nhận / tự rút    — Member và PM, không cần ai duyệt
 * Nên `Member` vẫn mở được dialog này, chỉ là chỉ bấm được vào dòng của chính mình.
 */
export function AssigneeDialog({
  projectId,
  task,
  role,
  myEmployeeId,
  onClose,
}: {
  projectId: string;
  /**
   * Cố ý khai bằng HÌNH DẠNG tối thiểu chứ không phải `TaskSummaryResponse`: dialog chỉ
   * đọc ba thứ dưới đây, và màn chi tiết task truyền vào `TaskDetailResponse` — nơi
   * `assignees` là `TaskAssigneeResponse[]` (có thêm `roleInTask`/`assignedDate`) chứ
   * không phải `TaskCardAssignee[]`. Ràng buộc theo tên kiểu sẽ bắt phải nặn dữ liệu chỉ
   * để qua cửa trình biên dịch.
   */
  task: TaskAssignable | null;
  role: RoleInProject | null;
  myEmployeeId: string | null;
  onClose: () => void;
}) {
  const open = task !== null;
  const members = useMembers(projectId);
  const assign = useAssignTask(projectId);
  const unassign = useUnassignTask(projectId);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const canManageOthers = canManageTasks(role);

  // Viewer không được nhận việc, nên cũng không liệt kê họ như một lựa chọn.
  const candidates = (members.data ?? []).filter(
    (m) => m.invitationStatus === 'Accepted' && m.roleInProject !== 'Viewer',
  );

  const toggle = async (employeeId: string, employeeName: string, dangGan: boolean) => {
    if (!task) return;
    setError(null);
    setBusyId(employeeId);

    try {
      if (dangGan) {
        await unassign.mutateAsync({ taskId: task.id, employeeId });
        toast.success(`Đã gỡ ${employeeName} khỏi task.`);
      } else {
        await assign.mutateAsync({ taskId: task.id, employeeId, role: 'Owner' });
        toast.success(`Đã giao task cho ${employeeName}.`);
      }
    } catch (err) {
      // 403 khi Member cố gán người khác; 409 khi tự nhận task đã có người làm.
      setError(errorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (next) return;
        setError(null);
        onClose();
      }}
    >
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Người đảm nhận</DialogTitle>
          <DialogDescription className="truncate">{task?.name}</DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        {members.isPending ? (
          <div className="grid gap-2" aria-busy="true">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-11" />
            ))}
          </div>
        ) : (
          <div className="grid max-h-72 gap-1 overflow-y-auto">
            {candidates.map((member) => {
              const dangGan = task?.assignees.some(
                (a) => a.employeeId === member.employeeId,
              );
              const isMe = member.employeeId === myEmployeeId;
              // PM bấm được mọi dòng; Member chỉ bấm được dòng của chính mình.
              const bamDuoc = canManageOthers || (isMe && canSelfAssign(role));

              return (
                <button
                  key={member.employeeId}
                  type="button"
                  disabled={!bamDuoc || busyId !== null}
                  onClick={() =>
                    toggle(member.employeeId, member.employeeName, dangGan ?? false)
                  }
                  className={cn(
                    'flex items-center gap-2.5 rounded-lg px-2 py-2 text-left text-sm transition-colors',
                    bamDuoc ? 'hover:bg-accent' : 'cursor-not-allowed opacity-60',
                    busyId === member.employeeId && 'opacity-50',
                  )}
                >
                  <UserAvatar
                    id={member.employeeId}
                    name={member.employeeName}
                    size="sm"
                  />
                  <span className="flex-1 truncate">
                    {member.employeeName}
                    {isMe ? <span className="text-muted-foreground"> (bạn)</span> : null}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    {ROLE_IN_PROJECT_LABEL[member.roleInProject]}
                  </span>
                  <CheckIcon
                    className={cn(
                      'text-primary size-4 shrink-0',
                      dangGan ? 'opacity-100' : 'opacity-0',
                    )}
                  />
                </button>
              );
            })}

            {candidates.length === 0 ? (
              <p className="text-muted-foreground grid place-items-center gap-2 rounded-lg border border-dashed px-3 py-8 text-center text-sm">
                <UserPlusIcon className="size-6" />
                Chưa có thành viên nào nhận được việc. Mời thêm người ở tab Thành viên.
              </p>
            ) : null}
          </div>
        )}

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Xong</DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

'use client';

import { toast } from 'sonner';

import { STATUS_TONE } from '@/components/tasks/status-tone';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import { useChangeTaskStatus } from '@/lib/hooks/use-board';
import { canChangeTaskStatus } from '@/lib/tasks/permissions';
import { ALLOWED_TRANSITIONS, mayFailUnpredictably } from '@/lib/tasks/status-transitions';
import { cn } from '@/lib/utils';
import { STATUS_LABEL, type RoleInProject, type Status } from '@/types/enums';
import type { TaskDetailResponse } from '@/types/task';

function StatusPill({ status }: { status: Status }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-medium',
        STATUS_TONE[status].badge,
      )}
    >
      <span className={cn('size-1.5 rounded-full', STATUS_TONE[status].dot)} />
      {STATUS_LABEL[status]}
    </span>
  );
}

/**
 * Đổi trạng thái task từ màn chi tiết.
 *
 * Ba luật, cả ba đều đã trả giá ở phần Kanban:
 *   1. `PATCH /tasks/{id}/status` **KHÔNG** cần `rowVersion` (ADR-021) — nên nó nằm ngoài
 *      trục `useTaskFieldSave` hoàn toàn.
 *   2. Chỉ liệt kê các bước trong `ALLOWED_TRANSITIONS`. State machine từ chối cả việc
 *      "đứng yên", nên trạng thái hiện tại cũng không phải một lựa chọn.
 *   3. Quyền là luật per-row của ADR-017: Assignee của CHÍNH task đó **hoặc** PM.
 */
export function TaskStatusControl({
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
  const changeStatus = useChangeTaskStatus(projectId, task.sprintId);

  const isAssignee = task.assignees.some(
    (assignee) => assignee.employeeId === myEmployeeId,
  );
  const canChange = canChangeTaskStatus(role, isAssignee);
  const targets = ALLOWED_TRANSITIONS[task.status];

  if (!canChange || targets.length === 0) {
    return <StatusPill status={task.status} />;
  }

  return (
    <Select
      value={task.status}
      disabled={changeStatus.isPending}
      onValueChange={(value) => {
        // `onValueChange` của Base UI có thể trả `null` khi bỏ chọn.
        if (!value || value === task.status) return;
        const target = value as Status;

        changeStatus.mutate(
          { taskId: task.id, target },
          {
            onSuccess: () => toast.success(`Đã chuyển sang "${STATUS_LABEL[target]}".`),
            onError: (error) =>
              toast.error(
                // Đúng một nước đi có thể 409 mà client KHÔNG đoán trước được: đích là
                // `InProgress` trong khi task đang bị một task chưa xong chặn
                // (`TaskStatusTransitionService` chỉ kiểm blocker cho nhánh này).
                mayFailUnpredictably(target)
                  ? `Không chuyển được sang "Đang làm": ${errorMessage(error)}`
                  : errorMessage(error),
              ),
          },
        );
      }}
    >
      <SelectTrigger size="sm" className="w-full" aria-label="Trạng thái task">
        {/* `SelectValue` của Base UI hiện GIÁ TRỊ THÔ — không truyền hàm định dạng thì ô
            này hiện "InProgress" thay vì "Đang làm". */}
        <SelectValue>{(current: Status) => <StatusPill status={current} />}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {/* Trạng thái hiện tại nằm trong danh sách để `SelectValue` có nhãn để hiện, nhưng
            bị vô hiệu hóa: chọn lại chính nó sẽ nhận 409 từ state machine. */}
        <SelectItem value={task.status} disabled>
          <StatusPill status={task.status} />
        </SelectItem>
        {targets.map((target) => (
          <SelectItem key={target} value={target}>
            <StatusPill status={target} />
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

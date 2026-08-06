'use client';

import { toast } from 'sonner';

import { columnChipStyle, columnDotStyle } from '@/components/tasks/status-tone';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import { useChangeTaskStatus } from '@/lib/hooks/use-board';
import { useBoardColumns } from '@/lib/hooks/use-board-columns';
import { canChangeTaskStatus } from '@/lib/tasks/permissions';
import { mayFailUnpredictably } from '@/lib/tasks/status-transitions';
import { type RoleInProject } from '@/types/enums';
import type { BoardColumnResponse, TaskDetailResponse, TaskStatusRef } from '@/types/task';

/**
 * Chip trạng thái. Màu đến từ CỘT (hex do người dùng chọn), không tra bảng enum nữa —
 * `STATUS_TONE` chỉ còn phục vụ trạng thái PROJECT (ADR-052).
 */
function StatusPill({ name, color }: { name: string; color: string }) {
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-medium"
      style={columnChipStyle(color)}
    >
      <span className="size-1.5 rounded-full" style={columnDotStyle(color)} />
      {name}
    </span>
  );
}

/**
 * Đổi trạng thái task từ màn chi tiết.
 *
 * Ba luật, và một trong ba đã đổi ở ADR-052:
 *   1. `PATCH /tasks/{id}/status` **KHÔNG** cần `rowVersion` (ADR-021) — nên nó nằm ngoài
 *      trục `useTaskFieldSave` hoàn toàn.
 *   2. 🔄 Liệt kê **MỌI cột của project**, không còn lọc theo ma trận chuyển trạng thái:
 *      cột do người dùng tạo nên không có "bước kề" nào để giới hạn. Cột hiện tại vẫn nằm
 *      trong danh sách (để `SelectValue` có nhãn) nhưng bị vô hiệu hóa — chọn lại chính nó
 *      nay trả 200 chứ không 409, nhưng vẫn là một request không làm gì.
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
  const columns = useBoardColumns(projectId);

  const isAssignee = task.assignees.some(
    (assignee) => assignee.employeeId === myEmployeeId,
  );
  const canChange = canChangeTaskStatus(role, isAssignee);

  const targets = (columns.data ?? []).filter((c) => c.id !== task.status.columnId);

  // Chưa tải xong danh sách cột thì cũng chỉ hiện chip: dựng một ô chọn rỗng rồi đổ đầy
  // sau sẽ khiến người dùng bấm vào một danh sách trống.
  if (!canChange || targets.length === 0) {
    return <StatusPill name={task.status.name} color={task.status.color} />;
  }

  const move = (target: BoardColumnResponse) =>
    changeStatus.mutate(
      { taskId: task.id, targetColumnId: target.id },
      {
        onSuccess: () => toast.success(`Đã chuyển sang "${target.name}".`),
        onError: (error) =>
          toast.error(
            // Đúng một nước đi có thể 409 mà client KHÔNG đoán trước được: cột đích thuộc
            // NHÓM `InProgress` trong khi task đang bị một task chưa xong chặn.
            mayFailUnpredictably(target.category)
              ? `Không chuyển được sang "${target.name}": ${errorMessage(error)}`
              : errorMessage(error),
          ),
      },
    );

  return (
    <Select
      value={task.status.columnId}
      disabled={changeStatus.isPending}
      onValueChange={(value) => {
        // `onValueChange` của Base UI có thể trả `null` khi bỏ chọn.
        if (!value || value === task.status.columnId) return;
        const target = targets.find((c) => c.id === value);
        if (target) move(target);
      }}
    >
      <SelectTrigger size="sm" className="w-full" aria-label="Trạng thái task">
        {/* `SelectValue` của Base UI hiện GIÁ TRỊ THÔ — không truyền hàm định dạng thì ô
            này hiện một GUID. Tra lại cột từ id thay vì đọc `task.status` để ô luôn khớp
            giá trị đang được chọn, kể cả trong nhịp render giữa lúc mutation chạy. */}
        <SelectValue>
          {(current: string) => {
            const found = columns.data?.find((c) => c.id === current);
            const ref: Pick<TaskStatusRef, 'name' | 'color'> = found ?? task.status;
            return <StatusPill name={ref.name} color={ref.color} />;
          }}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={task.status.columnId} disabled>
          <StatusPill name={task.status.name} color={task.status.color} />
        </SelectItem>
        {targets.map((target) => (
          <SelectItem key={target.id} value={target.id}>
            <StatusPill name={target.name} color={target.color} />
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

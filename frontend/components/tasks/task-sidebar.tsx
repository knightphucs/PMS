'use client';

import { useState } from 'react';
import { toast } from 'sonner';

import { UserAvatar } from '@/components/common/user-avatar';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { TaskAssigneesField } from '@/components/tasks/task-assignees-field';
import { TaskLabelsField } from '@/components/tasks/task-labels-field';
import { TaskFieldRow } from '@/components/tasks/task-section';
import { TaskStatusControl } from '@/components/tasks/task-status-control';
import { TaskWatchersField } from '@/components/tasks/task-watchers-field';
import type { SaveField } from '@/components/tasks/use-task-field-save';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import { formatDate } from '@/lib/format';
import { canManageLabels, canManageTasks } from '@/lib/tasks/permissions';
import { toDateInputValue } from '@/lib/validation/project-schema';
import { PRIORITY_ORDER, type Priority, type RoleInProject } from '@/types/enums';
import type { TaskDetailResponse } from '@/types/task';

export function TaskSidebar({
  projectId,
  task,
  role,
  myEmployeeId,
  isBusy,
  onSaveField,
}: {
  projectId: string;
  task: TaskDetailResponse;
  role: RoleInProject | null;
  myEmployeeId: string | null;
  isBusy: boolean;
  onSaveField: SaveField;
}) {
  const canEdit = canManageTasks(role);
  const [dueDraft, setDueDraft] = useState<string | null>(null);

  const save = (patch: Parameters<SaveField>[0]) => {
    void onSaveField(patch).catch((error: unknown) => toast.error(errorMessage(error)));
  };

  return (
    <div className="bg-card grid gap-3.5 rounded-lg border p-3.5">
      <TaskFieldRow label="Trạng thái">
        <TaskStatusControl
          projectId={projectId}
          task={task}
          role={role}
          myEmployeeId={myEmployeeId}
        />
      </TaskFieldRow>

      <TaskFieldRow label="Người đảm nhận" align="start">
        <TaskAssigneesField
          projectId={projectId}
          task={task}
          role={role}
          myEmployeeId={myEmployeeId}
        />
      </TaskFieldRow>

      <TaskFieldRow label="Độ ưu tiên">
        {canEdit ? (
          <Select
            value={task.priority}
            disabled={isBusy}
            onValueChange={(value) => {
              if (!value || value === task.priority) return;
              save({ priority: value as Priority });
            }}
          >
            <SelectTrigger size="sm" className="w-full" aria-label="Độ ưu tiên">
              {/* `SelectValue` của Base UI hiện giá trị THÔ — bắt buộc truyền hàm định dạng. */}
              <SelectValue>
                {(current: Priority) => <PriorityLabel priority={current} />}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {PRIORITY_ORDER.map((item) => (
                <SelectItem key={item} value={item}>
                  <PriorityLabel priority={item} />
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : (
          <PriorityLabel priority={task.priority} className="text-sm" />
        )}
      </TaskFieldRow>

      <TaskFieldRow label="Hạn hoàn thành">
        {canEdit ? (
          <Input
            type="date"
            className="h-8"
            disabled={isBusy}
            value={dueDraft ?? (task.dueDate ? toDateInputValue(task.dueDate) : '')}
            onChange={(event) => setDueDraft(event.target.value)}
            onBlur={(event) => {
              const value = event.target.value;
              setDueDraft(null);
              const next = value ? `${value}T00:00:00Z` : null;
              // So sánh phần NGÀY, không so chuỗi ISO: giá trị từ API có giờ/phút còn ô
              // `type="date"` thì không, nên so nguyên chuỗi sẽ lưu lại một giá trị y hệt
              // ở mọi lần blur — mỗi lần là một lượt PUT thừa và một dòng ActivityLog rác.
              const current = task.dueDate ? toDateInputValue(task.dueDate) : '';
              if (value === current) return;
              save({ dueDate: next });
            }}
          />
        ) : (
          <span className="text-sm">
            {task.dueDate ? formatDate(task.dueDate) : '—'}
            {/* `isOverdue` là giá trị TÍNH SẴN của backend — đừng so ngày lại ở client. */}
            {task.isOverdue ? (
              <span className="text-destructive ml-2 text-xs font-medium">Quá hạn</span>
            ) : null}
          </span>
        )}
        {canEdit && task.isOverdue ? (
          <span className="text-destructive mt-1 block text-xs font-medium">Quá hạn</span>
        ) : null}
      </TaskFieldRow>

      <TaskFieldRow label="Nhãn" align="start">
        <TaskLabelsField
          projectId={projectId}
          taskId={task.id}
          labels={task.labels}
          canEdit={canManageLabels(role)}
        />
      </TaskFieldRow>

      <TaskFieldRow label="Người theo dõi" align="start">
        <TaskWatchersField
          projectId={projectId}
          taskId={task.id}
          isWatching={task.isWatching}
          role={role}
        />
      </TaskFieldRow>

      <TaskFieldRow label="Người tạo">
        <span className="flex min-w-0 items-center gap-2 text-[13px]">
          <UserAvatar id={task.reporterId} name={task.reporterName} size="sm" />
          <span className="truncate">{task.reporterName}</span>
        </span>
      </TaskFieldRow>
    </div>
  );
}

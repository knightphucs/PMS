'use client';

import { PlusIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { PriorityIcon } from '@/components/tasks/priority-icon';
import { TaskStatusDot } from '@/components/tasks/task-status-chip';
import { TaskFormDialog } from '@/components/tasks/task-form-dialog';
import { TaskSection } from '@/components/tasks/task-section';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import type { TaskDetailResponse } from '@/types/task';

export function TaskSubtasks({
  projectId,
  parentTask,
  canManage,
}: {
  projectId: string;
  parentTask: TaskDetailResponse;
  canManage: boolean;
}) {
  const [creating, setCreating] = useState(false);

  const subtasks = parentTask.subtasks;
  // Domain chặn subtask hai cấp bằng `DomainException` (→ 409). Ẩn nút đúng hơn là để
  // người dùng bấm rồi ăn lỗi — họ không làm gì sai, cấu trúc chỉ đơn giản không cho.
  const canAdd = canManage && parentTask.parentTaskId === null;

  const addButton = canAdd ? (
    <Button variant="ghost" size="sm" onClick={() => setCreating(true)}>
      <PlusIcon className="size-4" />
      Thêm subtask
    </Button>
  ) : undefined;

  return (
    <>
      <TaskSection title="Subtask" count={subtasks.length} actions={addButton}>
        {subtasks.length === 0 ? (
          <p className="text-muted-foreground text-sm">
            {canAdd
              ? 'Chưa có subtask. Tách việc lớn thành các bước nhỏ để theo dõi tiến độ rõ hơn.'
              : 'Chưa có subtask.'}
          </p>
        ) : (
          <div className="grid gap-2">
            <div className="flex items-center gap-3">
              {/* `subtaskProgress` là giá trị TÍNH SẴN của backend — đừng đếm lại từ
                  `subtasks`. Ở màn này `0` không còn mơ hồ như trên thẻ Kanban vì đã biết
                  chắc `subtasks.length > 0`. */}
              <Progress value={parentTask.subtaskProgress} className="h-1.5 flex-1" />
              <span className="text-muted-foreground text-xs tabular-nums">
                {Math.round(parentTask.subtaskProgress)}%
              </span>
            </div>

            <div className="bg-card divide-y rounded-lg border">
              {subtasks.map((subtask) => (
                <Link
                  key={subtask.id}
                  // `replace` chứ không phải `push`: chuỗi subtask nhiều tầng mà dùng
                  // `push` thì thoát ra phải bấm Back đúng bằng số lần đã đi vào, và
                  // `router.back()` của dialog sẽ rơi về task cha thay vì về board.
                  replace
                  href={`/projects/${projectId}/tasks/${subtask.id}`}
                  className="hover:bg-accent flex items-center gap-2.5 px-3 py-2 text-[13px] transition-colors"
                >
                  {/* Chấm màu lấy từ CỘT (ADR-052) — không tra bảng enum nữa. `title` mang
                      tên cột do người dùng đặt, nên nó cũng là chú thích duy nhất đúng. */}
                  <TaskStatusDot status={subtask.status} />
                  {/* Mã do backend ghép sẵn (ADR-034) — đừng nối projectKey + number. */}
                  <span className="text-muted-foreground shrink-0 font-medium tabular-nums">
                    {subtask.code}
                  </span>
                  <span className="min-w-0 flex-1 truncate">{subtask.name}</span>
                  <PriorityIcon priority={subtask.priority} />
                </Link>
              ))}
            </div>
          </div>
        )}
      </TaskSection>

      <TaskFormDialog
        projectId={projectId}
        open={creating}
        taskId={null}
        parentTaskId={parentTask.id}
        onClose={() => setCreating(false)}
      />
    </>
  );
}

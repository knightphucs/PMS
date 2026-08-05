'use client';

import {
  CalendarRangeIcon,
  ChevronRightIcon,
  MoreHorizontalIcon,
  PencilIcon,
  PlayIcon,
  Trash2Icon,
} from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import { toast } from 'sonner';

import { AvatarStack } from '@/components/common/avatar-stack';
import { TaskStatusChip } from '@/components/tasks/task-status-chip';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { formatDate, formatDateRange } from '@/lib/format';
import { useBoard } from '@/lib/hooks/use-board';
import { useStartSprint } from '@/lib/hooks/use-sprints';
import { cn } from '@/lib/utils';
import type { SprintResponse } from '@/types/sprint';

export function SprintList({
  projectId,
  sprints,
  canManage,
  onEdit,
  onDelete,
  onComplete,
}: {
  projectId: string;
  sprints: SprintResponse[];
  canManage: boolean;
  onEdit: (sprint: SprintResponse) => void;
  onDelete: (sprint: SprintResponse) => void;
  onComplete: (sprint: SprintResponse) => void;
}) {
  return (
    <div className="grid gap-3">
      {sprints.map((sprint) => (
        <SprintRow
          key={sprint.id}
          projectId={projectId}
          sprint={sprint}
          canManage={canManage}
          onEdit={onEdit}
          onDelete={onDelete}
          onComplete={onComplete}
        />
      ))}
    </div>
  );
}

/**
 * Một sprint — thu/mở được, mở ra thì hiện danh sách task ngay tại chỗ (ADR-050).
 *
 * 📌 Bố cục theo mẫu backlog của Jira: đầu đề mang khoảng ngày + số work item + nút hành
 * động, thân là các dòng task gọn có mã, tên, chip trạng thái, hạn và người đảm nhận.
 */
function SprintRow({
  projectId,
  sprint,
  canManage,
  onEdit,
  onDelete,
  onComplete,
}: {
  projectId: string;
  sprint: SprintResponse;
  canManage: boolean;
  onEdit: (sprint: SprintResponse) => void;
  onDelete: (sprint: SprintResponse) => void;
  onComplete: (sprint: SprintResponse) => void;
}) {
  // Sprint đang chạy mở sẵn; sprint đã đóng và chưa bắt đầu thì thu lại. Đó là thứ người
  // dùng quan tâm khi mở tab này.
  const [expanded, setExpanded] = useState(sprint.status === 'Active');
  const start = useStartSprint(projectId);

  /**
   * 🔑 Chỉ nạp task khi sprint ĐANG MỞ.
   *
   * Một project có thể có hàng chục sprint; nạp task cho tất cả là hàng chục request cho
   * dữ liệu phần lớn không ai nhìn.
   *
   * ⚠️ Dùng `enabled`, KHÔNG truyền `sprintId = null` để hoãn: `null` có nghĩa riêng là
   * board "Tất cả task" của project — nhầm hai thứ đó là nạp cả project trong khi chỉ muốn
   * một sprint.
   */
  const board = useBoard(projectId, sprint.id, { enabled: expanded });
  const tasks = (board.data?.columns ?? []).flatMap((column) => column.tasks);

  const handleStart = () => {
    start.mutate(sprint.id, {
      onSuccess: () => toast.success(`Đã bắt đầu "${sprint.name}".`),
      // 409 = project đã có sprint khác đang chạy. Chữ của server nói rõ tên sprint đó.
      onError: (error) => toast.error(errorMessage(error)),
    });
  };

  return (
    <section className="bg-card rounded-lg border">
      <header className="flex flex-wrap items-center gap-x-3 gap-y-2 p-3">
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          aria-expanded={expanded}
          aria-label={`${expanded ? 'Thu' : 'Mở'} sprint ${sprint.name}`}
          className="hover:bg-accent grid size-6 shrink-0 place-items-center rounded transition-colors"
        >
          <ChevronRightIcon className={cn('size-4 transition-transform', expanded && 'rotate-90')} />
        </button>

        <Link
          href={`/projects/${projectId}/board?sprint=${sprint.id}`}
          className="hover:text-primary text-[15px] font-medium underline-offset-4 transition-colors hover:underline"
        >
          {sprint.name}
        </Link>

        <span className="text-muted-foreground inline-flex items-center gap-1.5 text-sm tabular-nums">
          <CalendarRangeIcon className="size-4" />
          {formatDateRange(sprint.startDate, sprint.endDate)}
        </span>

        <span className="text-muted-foreground text-sm tabular-nums">
          ({sprint.taskCount} task)
        </span>

        <SprintStatusBadge sprint={sprint} />

        <span className="flex-1" />

        {/* Tiến độ dạng số, cạnh nút hành động — giống ba ô đếm của Jira. */}
        <span className="bg-muted text-muted-foreground rounded-full px-2.5 py-0.5 text-xs font-medium tabular-nums">
          {sprint.doneCount}/{sprint.taskCount} xong
        </span>

        {canManage ? (
          <SprintActions
            sprint={sprint}
            isStarting={start.isPending}
            onStart={handleStart}
            onComplete={() => onComplete(sprint)}
          />
        ) : null}

        {canManage ? (
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <Button variant="ghost" size="icon-sm" aria-label={`Thao tác với ${sprint.name}`}>
                  <MoreHorizontalIcon className="size-4" />
                </Button>
              }
            />
            <DropdownMenuContent align="end" className="w-44">
              <DropdownMenuItem onClick={() => onEdit(sprint)}>
                <PencilIcon className="size-4" />
                Sửa
              </DropdownMenuItem>
              <DropdownMenuItem variant="destructive" onClick={() => onDelete(sprint)}>
                <Trash2Icon className="size-4" />
                Xóa
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
      </header>

      {sprint.goal && expanded ? (
        <p className="text-muted-foreground border-t px-3 py-2 text-sm">{sprint.goal}</p>
      ) : null}

      {expanded ? (
        <div className="border-t">
          {board.isPending ? (
            <div className="grid gap-2 p-3">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-full" />
            </div>
          ) : board.isError ? (
            <p className="text-destructive px-3 py-4 text-sm">{errorMessage(board.error)}</p>
          ) : tasks.length === 0 ? (
            <p className="text-muted-foreground px-3 py-6 text-center text-sm">
              Sprint này chưa có task nào. Thêm từ Backlog.
            </p>
          ) : (
            <ul className="divide-y">
              {tasks.map((task) => (
                <li key={task.id}>
                  <Link
                    href={`/projects/${projectId}/tasks/${task.id}`}
                    className="hover:bg-accent flex items-center gap-3 px-3 py-2 text-[13px] transition-colors"
                  >
                    {/* Mã do backend ghép sẵn (ADR-034) — đừng nối projectKey + number. */}
                    <span className="text-muted-foreground shrink-0 font-medium tabular-nums">
                      {task.code}
                    </span>
                    <span
                      className={cn(
                        'min-w-0 flex-1 truncate',
                        // Gạch ngang theo NHÓM cột, không theo tên (ADR-052).
                        task.status.category === 'Done' && 'text-muted-foreground line-through',
                      )}
                    >
                      {task.name}
                    </span>

                    <TaskStatusChip status={task.status} className="shrink-0" />

                    <span
                      className={cn(
                        'text-muted-foreground w-24 shrink-0 text-right text-xs tabular-nums',
                        task.isOverdue && 'text-destructive font-medium',
                      )}
                    >
                      {task.dueDate ? formatDate(task.dueDate) : '—'}
                    </span>

                    <AvatarStack people={task.assignees} className="shrink-0" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : null}
    </section>
  );
}

/**
 * Huy hiệu trạng thái sprint.
 *
 * 🔴 Chỗ đáng giá nhất là nhánh cuối: `status === 'Active'` mà `isActive === false` nghĩa là
 * **sprint đã quá hạn mà chưa ai đóng**. Hai trường đó nói hai chuyện khác nhau (một suy từ
 * ngày, một do người dùng bấm), và chỗ chúng lệch nhau chính là thứ cần nhắc.
 */
function SprintStatusBadge({ sprint }: { sprint: SprintResponse }) {
  if (sprint.status === 'Completed') {
    return (
      <Badge variant="secondary" className="border-0 font-medium">
        Đã đóng
      </Badge>
    );
  }

  if (sprint.status === 'Planned') {
    return (
      <Badge variant="outline" className="font-medium">
        Chưa bắt đầu
      </Badge>
    );
  }

  return sprint.isActive ? (
    <Badge
      variant="secondary"
      className="border-0 bg-emerald-100 font-medium text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"
    >
      Đang chạy
    </Badge>
  ) : (
    <Badge
      variant="secondary"
      className="border-0 bg-amber-100 font-medium text-amber-700 dark:bg-amber-950 dark:text-amber-300"
    >
      Quá hạn, chưa đóng
    </Badge>
  );
}

/** Nút hành động theo đúng trạng thái — mỗi trạng thái chỉ có một nước đi hợp lệ. */
function SprintActions({
  sprint,
  isStarting,
  onStart,
  onComplete,
}: {
  sprint: SprintResponse;
  isStarting: boolean;
  onStart: () => void;
  onComplete: () => void;
}) {
  if (sprint.status === 'Completed') return null;

  if (sprint.status === 'Planned') {
    return (
      <Button variant="outline" size="sm" disabled={isStarting} onClick={onStart}>
        <PlayIcon className="size-3.5" />
        {isStarting ? 'Đang bắt đầu…' : 'Bắt đầu sprint'}
      </Button>
    );
  }

  return (
    <Button variant="outline" size="sm" onClick={onComplete}>
      Đóng sprint
    </Button>
  );
}

export function SprintListSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="grid gap-3" aria-busy="true">
      <span className="sr-only">Đang tải danh sách sprint…</span>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="bg-card flex items-center gap-4 rounded-lg border p-3">
          <Skeleton className="size-6 rounded" />
          <div className="grid flex-1 gap-1.5">
            <Skeleton className="h-4 w-44" />
            <Skeleton className="h-3.5 w-64" />
          </div>
          <Skeleton className="h-4 w-36" />
          <Skeleton className="h-5 w-16 rounded-full" />
        </div>
      ))}
    </div>
  );
}

'use client';

import { CalendarIcon } from 'lucide-react';
import Link from 'next/link';

import { AvatarStack } from '@/components/common/avatar-stack';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { TaskStatusChip } from '@/components/tasks/task-status-chip';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { formatDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { TaskSummaryResponse } from '@/types/task';

export function BacklogTable({
  tasks,
  projectId,
  movingIds,
  renderMenu,
}: {
  tasks: TaskSummaryResponse[];
  /** Để dựng link tới chi tiết task. */
  projectId: string;
  /** Task đang có request chuyển sprint bay dở — làm mờ thay vì cập nhật lạc quan. */
  movingIds: ReadonlySet<string>;
  renderMenu: (task: TaskSummaryResponse) => React.ReactNode;
}) {
  return (
    <div className="bg-card overflow-x-auto rounded-lg border">
      <Table className="[&_td]:px-3 [&_td]:py-2 [&_td]:text-[13px] [&_th]:h-9 [&_th]:px-3">
        <TableHeader className="bg-muted/40">
          <TableRow>
            <TableHead className="w-24">Mã</TableHead>
            <TableHead>Tên task</TableHead>
            <TableHead className="w-36">Trạng thái</TableHead>
            <TableHead className="w-40">Độ ưu tiên</TableHead>
            <TableHead className="w-40">Hạn</TableHead>
            <TableHead className="w-28">Đảm nhận</TableHead>
            <TableHead className="w-12">
              <span className="sr-only">Thao tác</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {tasks.map((task) => {
            const moving = movingIds.has(task.id);

            return (
              <TableRow
                key={task.id}
                className={cn(moving && 'pointer-events-none opacity-50')}
                aria-busy={moving || undefined}
              >
                {/* Mã do backend ghép sẵn (ADR-034) — không nối projectKey + number. */}
                <TableCell className="text-muted-foreground font-medium tabular-nums">
                  {task.code}
                </TableCell>
                <TableCell className="font-medium">
                  <Link
                    href={`/projects/${projectId}/tasks/${task.id}`}
                    className="hover:text-primary underline-offset-4 transition-colors hover:underline"
                  >
                    {task.name}
                  </Link>
                </TableCell>
                <TableCell>
                  {/* 🔴 KHÔNG dùng `StatusBadge` — đó là badge của trạng thái PROJECT (enum
                      bốn giá trị). Trạng thái task nay là một CỘT mang màu riêng (ADR-052). */}
                  <TaskStatusChip status={task.status} />
                </TableCell>
                <TableCell className="text-muted-foreground">
                  <PriorityLabel priority={task.priority} />
                </TableCell>
                <TableCell>
                  {task.dueDate ? (
                    <span
                      className={cn(
                        'inline-flex items-center gap-1.5 tabular-nums',
                        // Giá trị TÍNH SẴN phía server — đừng so ngày lại ở client.
                        task.isOverdue
                          ? 'text-destructive font-medium'
                          : 'text-muted-foreground',
                      )}
                    >
                      <CalendarIcon className="size-3.5" />
                      {formatDate(task.dueDate)}
                      {task.isOverdue ? <span className="sr-only">(quá hạn)</span> : null}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </TableCell>
                <TableCell>
                  {task.assignees.length > 0 ? (
                    <AvatarStack people={task.assignees} max={3} size="sm" />
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </TableCell>
                <TableCell className="text-right">{renderMenu(task)}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

export function BacklogTableSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="bg-card rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải backlog…</span>
      <div className="divide-y">
        {Array.from({ length: rows }).map((_, index) => (
          <div key={index} className="flex items-center gap-4 px-3 py-2.5">
            <Skeleton className="h-4 w-16" />
            <Skeleton className="h-4 flex-1" />
            <Skeleton className="h-5 w-24 rounded-full" />
            <Skeleton className="h-4 w-28" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="size-6 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}

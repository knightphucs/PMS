'use client';

import { CalendarRangeIcon, MoreHorizontalIcon, PencilIcon, Trash2Icon } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDateRange } from '@/lib/format';
import type { SprintResponse } from '@/types/sprint';

export function SprintList({
  projectId,
  sprints,
  canManage,
  onEdit,
  onDelete,
}: {
  projectId: string;
  sprints: SprintResponse[];
  canManage: boolean;
  onEdit: (sprint: SprintResponse) => void;
  onDelete: (sprint: SprintResponse) => void;
}) {
  return (
    <div className="grid gap-2">
      {sprints.map((sprint) => (
        <div
          key={sprint.id}
          className="bg-card flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border p-3"
        >
          <div className="min-w-52 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <Link
                href={`/projects/${projectId}/board?sprint=${sprint.id}`}
                className="hover:text-primary text-[15px] font-medium underline-offset-4 transition-colors hover:underline"
              >
                {sprint.name}
              </Link>
              {/* ⚠️ `isActive` = hôm nay nằm trong khoảng ngày, KHÔNG phải "sprint duy
                  nhất đang chạy" — hai sprint gối ngày thì cả hai đều đang diễn ra. */}
              {sprint.isActive ? (
                <Badge
                  variant="secondary"
                  className="border-0 bg-emerald-100 font-medium text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"
                >
                  Đang diễn ra
                </Badge>
              ) : null}
            </div>
            {sprint.goal ? (
              <p className="text-muted-foreground mt-0.5 text-sm">{sprint.goal}</p>
            ) : null}
          </div>

          <span className="text-muted-foreground inline-flex items-center gap-1.5 text-sm tabular-nums">
            <CalendarRangeIcon className="size-4" />
            {formatDateRange(sprint.startDate, sprint.endDate)}
          </span>

          <span className="bg-muted text-muted-foreground rounded-full px-2.5 py-0.5 text-xs font-medium tabular-nums">
            {sprint.taskCount} task
          </span>

          {canManage ? (
            <DropdownMenu>
              <DropdownMenuTrigger
                render={
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Thao tác với ${sprint.name}`}
                  >
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
        </div>
      ))}
    </div>
  );
}

export function SprintListSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="grid gap-2" aria-busy="true">
      <span className="sr-only">Đang tải danh sách sprint…</span>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="bg-card flex items-center gap-4 rounded-lg border p-3">
          <div className="flex-1 grid gap-1.5">
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

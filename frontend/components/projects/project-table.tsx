'use client';

import { CalendarClockIcon } from 'lucide-react';

import { StatusBadge } from '@/components/projects/status-badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { formatDate, isPastDue } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { ProjectSummaryResponse } from '@/types/project';

export function ProjectTable({
  projects,
  dimmed,
}: {
  projects: ProjectSummaryResponse[];
  /** Đang tải trang mới nhưng vẫn hiện dữ liệu cũ (placeholderData). */
  dimmed?: boolean;
}) {
  return (
    <div
      className={cn(
        'overflow-x-auto rounded-lg border transition-opacity',
        dimmed && 'pointer-events-none opacity-60',
      )}
      aria-busy={dimmed}
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Tên dự án</TableHead>
            <TableHead className="w-40">Trạng thái</TableHead>
            <TableHead className="w-52">Dự kiến hoàn thành</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {projects.map((project) => {
            const overdue = isPastDue(project.expectedCompletionDate) && project.status !== 'Done';

            return (
              <TableRow key={project.id}>
                <TableCell className="font-medium">{project.name}</TableCell>
                <TableCell>
                  <StatusBadge status={project.status} />
                </TableCell>
                <TableCell>
                  <span
                    className={cn(
                      'inline-flex items-center gap-1.5',
                      overdue && 'text-destructive font-medium',
                    )}
                  >
                    {overdue ? <CalendarClockIcon className="size-4" /> : null}
                    {formatDate(project.expectedCompletionDate)}
                    {overdue ? <span className="sr-only">(đã quá hạn)</span> : null}
                  </span>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

export function ProjectTableSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách dự án…</span>
      <div className="divide-y">
        {Array.from({ length: rows }, (_, i) => (
          <div key={i} className="flex items-center gap-4 p-4">
            <Skeleton className="h-5 flex-1" />
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-5 w-32" />
          </div>
        ))}
      </div>
    </div>
  );
}

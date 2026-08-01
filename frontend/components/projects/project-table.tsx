'use client';

import { CalendarClockIcon, MoreHorizontalIcon, PencilIcon, Trash2Icon } from 'lucide-react';

import { StatusBadge } from '@/components/projects/status-badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { ROLE_IN_PROJECT_LABEL } from '@/types/enums';
import type { ProjectSummaryResponse } from '@/types/project';

export function ProjectTable({
  projects,
  dimmed,
  onEdit,
  onDelete,
}: {
  projects: ProjectSummaryResponse[];
  /** Đang tải trang mới nhưng vẫn hiện dữ liệu cũ (placeholderData). */
  dimmed?: boolean;
  onEdit: (project: ProjectSummaryResponse) => void;
  onDelete: (project: ProjectSummaryResponse) => void;
}) {
  return (
    <div
      className={cn(
        'bg-card overflow-x-auto rounded-lg border transition-opacity',
        dimmed && 'pointer-events-none opacity-60',
      )}
      aria-busy={dimmed}
    >
      {/* Nới đệm ô ngay tại nơi dùng thay vì sửa components/ui/table.tsx — file đó do
          shadcn sinh và sẽ bị ghi đè mỗi lần thêm lại component. */}
      {/* Mật độ theo thang ở `components/common/page-header.tsx`: dòng ~38px chứ không
          phải 48px. Jira/Linear dày, không thoáng — thoáng làm mắt phải quét xa hơn để
          đọc cùng một lượng thông tin. Ghi đè ở NƠI DÙNG, không sửa `components/ui/*`. */}
      <Table className="[&_td]:px-3 [&_td]:py-2 [&_td]:text-[13px] [&_th]:h-9 [&_th]:px-3">
        <TableHeader className="bg-muted/40">
          <TableRow>
            <TableHead>Tên dự án</TableHead>
            <TableHead className="w-40">Trạng thái</TableHead>
            <TableHead className="w-44">Vai trò của bạn</TableHead>
            <TableHead className="w-52">Dự kiến hoàn thành</TableHead>
            <TableHead className="w-14">
              <span className="sr-only">Thao tác</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {projects.map((project) => {
            const overdue = isPastDue(project.expectedCompletionDate) && project.status !== 'Done';
            // §10: chỉ ProjectManager mới sửa/xóa được. Ẩn nút theo đúng luật thay vì
            // hiện rồi để backend từ chối — Member và Viewer sẽ nhận 403 nếu cứ gọi.
            const canManage = project.roleInProject === 'ProjectManager';

            return (
              <TableRow key={project.id}>
                <TableCell className="font-medium">{project.name}</TableCell>
                <TableCell>
                  <StatusBadge status={project.status} />
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {ROLE_IN_PROJECT_LABEL[project.roleInProject]}
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
                <TableCell>
                  {canManage ? (
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        render={
                          <Button variant="ghost" size="icon-sm" aria-label={`Thao tác với ${project.name}`}>
                            <MoreHorizontalIcon className="size-4" />
                          </Button>
                        }
                      />
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => onEdit(project)}>
                          <PencilIcon className="size-4" />
                          Sửa
                        </DropdownMenuItem>
                        <DropdownMenuItem variant="destructive" onClick={() => onDelete(project)}>
                          <Trash2Icon className="size-4" />
                          Xóa
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  ) : null}
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
    <div className="bg-card rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách dự án…</span>
      <div className="divide-y">
        {Array.from({ length: rows }, (_, i) => (
          <div key={i} className="flex items-center gap-4 p-4">
            <Skeleton className="h-5 flex-1" />
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-5 w-32" />
            <Skeleton className="h-5 w-32" />
            <Skeleton className="h-5 w-8" />
          </div>
        ))}
      </div>
    </div>
  );
}

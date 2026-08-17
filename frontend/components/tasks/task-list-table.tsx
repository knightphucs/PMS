'use client';

import { CalendarIcon, CheckIcon, MinusIcon } from 'lucide-react';
import Link from 'next/link';

import { AvatarStack } from '@/components/common/avatar-stack';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { TaskStatusChip } from '@/components/tasks/task-status-chip';
import { WorkItemTypeChip } from '@/components/tasks/work-item-type-chip';
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
import type { TaskListFieldValue, TaskListItemResponse } from '@/types/saved-view';
import type { TaskSummaryResponse } from '@/types/task';

/**
 * Bảng danh sách task (ADR-061) — nơi một view được áp dụng.
 *
 * Bám sát `BacklogTable` về mật độ và cách hiển thị từng ô (mã, chip trạng thái, hạn quá
 * hạn, avatar) để hai màn không trông như hai sản phẩm khác nhau. Khác ở hai điểm:
 * có chip **loại công việc** (ADR-060) và có **cột trường tuỳ biến** do view chọn (ADR-059).
 */
export function TaskListTable({
  items,
  projectId,
  customColumns,
  renderMenu,
}: {
  items: TaskListItemResponse[];
  projectId: string;
  /** Trường tuỳ biến view đang hiện, theo thứ tự. Rỗng = chỉ cột dựng sẵn. */
  customColumns: { id: string; label: string }[];
  /** Nhận thẳng `TaskSummaryResponse` để cắm được `useTaskActions` dùng chung với board/backlog. */
  renderMenu: (task: TaskSummaryResponse) => React.ReactNode;
}) {
  return (
    <div className="bg-card overflow-x-auto rounded-lg border">
      <Table className="[&_td]:px-3 [&_td]:py-2 [&_td]:text-[13px] [&_th]:h-9 [&_th]:px-3">
        <TableHeader className="bg-muted/40">
          <TableRow>
            <TableHead className="w-24">Mã</TableHead>
            <TableHead className="min-w-56">Tên task</TableHead>
            <TableHead className="w-36">Trạng thái</TableHead>
            <TableHead className="w-36">Độ ưu tiên</TableHead>
            <TableHead className="w-36">Hạn</TableHead>
            <TableHead className="w-24">Đảm nhận</TableHead>
            {customColumns.map((column) => (
              <TableHead key={column.id} className="w-40">
                {column.label}
              </TableHead>
            ))}
            <TableHead className="w-12">
              <span className="sr-only">Thao tác</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map(({ task, customFields }) => (
            <TableRow key={task.id}>
              {/* Mã do backend ghép sẵn (ADR-034) — không nối projectKey + number. */}
              <TableCell className="text-muted-foreground font-medium tabular-nums">
                {task.code}
              </TableCell>
              <TableCell className="font-medium">
                <span className="flex items-center gap-2">
                  <WorkItemTypeChip type={task.type} showLabel={false} />
                  <Link
                    href={`/projects/${projectId}/tasks/${task.id}`}
                    className="hover:text-primary underline-offset-4 transition-colors hover:underline"
                  >
                    {task.name}
                  </Link>
                </span>
              </TableCell>
              <TableCell>
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
                      task.isOverdue ? 'text-destructive font-medium' : 'text-muted-foreground',
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

              {customColumns.map((column) => (
                <TableCell key={column.id}>
                  <CustomFieldCell
                    value={customFields.find((v) => v.fieldDefinitionId === column.id)}
                  />
                </TableCell>
              ))}

              <TableCell className="text-right">{renderMenu(task)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

/**
 * Một ô trường tuỳ biến.
 *
 * ⚠️ `undefined` = task CHƯA ĐIỀN trường này. Backend xoá hẳn hàng khi giá trị bị xoá
 * trắng (ADR-059), nên "không có giá trị" và "giá trị rỗng" là cùng một trạng thái — hiện
 * dấu gạch, không hiện ô trống (ô trống trông như lỗi tải).
 */
function CustomFieldCell({ value }: { value: TaskListFieldValue | undefined }) {
  if (!value) return <span className="text-muted-foreground">—</span>;

  if (value.selectedOptions.length > 0)
    return (
      <span className="flex flex-wrap gap-1">
        {value.selectedOptions.map((option) => (
          <span
            key={option.id}
            className="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium text-white"
            // Màu đã qua regex `#RRGGBB` ở backend trước khi lưu.
            style={{ backgroundColor: option.color }}
          >
            {option.label}
          </span>
        ))}
      </span>
    );

  if (value.valueBoolean !== null)
    return value.valueBoolean ? (
      <CheckIcon className="size-4" aria-label="Có" />
    ) : (
      <MinusIcon className="text-muted-foreground size-4" aria-label="Không" />
    );

  if (value.valueNumber !== null)
    return <span className="tabular-nums">{value.valueNumber}</span>;

  if (value.valueDate !== null)
    return <span className="tabular-nums">{formatDate(value.valueDate)}</span>;

  if (value.valueText) {
    // Url hiện thành link thật; Text thì không — `type` là nguồn sự thật, không đoán từ
    // nội dung (một ghi chú bắt đầu bằng "http" vẫn chỉ là ghi chú).
    if (value.type === 'Url')
      return (
        <a
          href={value.valueText}
          target="_blank"
          rel="noreferrer noopener"
          className="text-primary truncate underline-offset-4 hover:underline"
        >
          {value.valueText}
        </a>
      );

    return <span className="line-clamp-2">{value.valueText}</span>;
  }

  return <span className="text-muted-foreground">—</span>;
}

export function TaskListTableSkeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div className="bg-card rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách task…</span>
      <div className="divide-y">
        {Array.from({ length: rows }).map((_, index) => (
          <div key={index} className="flex items-center gap-4 px-3 py-2.5">
            <Skeleton className="h-4 w-16" />
            <Skeleton className="h-4 flex-1" />
            <Skeleton className="h-5 w-24 rounded-full" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="size-6 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}

'use client';

import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PAGE_SIZE_OPTIONS, type PagedResult } from '@/types/common';

interface Props {
  /** Chỉ cần phần metadata — component này không quan tâm kiểu của `items`. */
  page: Pick<
    PagedResult<unknown>,
    'page' | 'pageSize' | 'totalCount' | 'totalPages' | 'hasPreviousPage' | 'hasNextPage'
  >;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  disabled?: boolean;
}

/**
 * Bật/tắt nút dựa vào `hasPreviousPage`/`hasNextPage` do SERVER tính, không tự so
 * `page < totalPages`. Cùng lý do không tính lại `IsOverdue` hay `SubtaskProgress`:
 * giá trị đã có sẵn thì tính lại chỉ tạo thêm một chỗ có thể lệch.
 */
export function ProjectPagination({ page, onPageChange, onPageSizeChange, disabled }: Props) {
  const from = page.totalCount === 0 ? 0 : (page.page - 1) * page.pageSize + 1;
  const to = Math.min(page.page * page.pageSize, page.totalCount);

  return (
    <div className="bg-card flex flex-col-reverse items-center justify-between gap-4 rounded-lg border p-3 sm:flex-row">
      <p className="text-muted-foreground text-sm" aria-live="polite">
        Hiển thị <strong className="text-foreground">{from}</strong>–
        <strong className="text-foreground">{to}</strong> trong tổng số{' '}
        <strong className="text-foreground">{page.totalCount}</strong> dự án
      </p>

      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <span className="text-muted-foreground text-sm whitespace-nowrap">Mỗi trang</span>
          <Select
            value={String(page.pageSize)}
            onValueChange={(value) => onPageSizeChange(Number(value))}
            disabled={disabled}
          >
            <SelectTrigger size="sm" aria-label="Số dự án mỗi trang">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAGE_SIZE_OPTIONS.map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(page.page - 1)}
            disabled={disabled || !page.hasPreviousPage}
          >
            <ChevronLeftIcon className="size-4" />
            Trước
          </Button>

          <span className="text-sm whitespace-nowrap tabular-nums">
            {page.page} / {Math.max(page.totalPages, 1)}
          </span>

          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(page.page + 1)}
            disabled={disabled || !page.hasNextPage}
          >
            Sau
            <ChevronRightIcon className="size-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

'use client';

import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/**
 * Skeleton mang ĐÚNG hình dạng nội dung thật — hai cột, đúng chỗ mã task, tên, mô tả và
 * các hàng của cột phải. Khung xám chung chung là dấu hiệu rõ nhất của app tập làm: nội
 * dung nhảy chỗ khi dữ liệu về, và mắt phải tìm lại từ đầu.
 */
export function TaskDetailSkeleton({ variant }: { variant: 'page' | 'modal' }) {
  return (
    <div
      aria-busy="true"
      className={cn(
        'grid gap-6',
        variant === 'page' ? 'lg:grid-cols-[minmax(0,1fr)_20rem]' : 'lg:grid-cols-[minmax(0,1fr)_22rem]',
      )}
    >
      <span className="sr-only">Đang tải chi tiết task…</span>

      <div className="grid gap-6">
        <div className="grid gap-2">
          <Skeleton className="h-5 w-24" />
          <Skeleton className="h-6 w-3/4" />
        </div>

        <div className="grid gap-2.5">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-20" />
        </div>

        <div className="grid gap-2.5">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-24" />
        </div>

        <div className="grid gap-2.5">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-16" />
        </div>
      </div>

      <div className="bg-card grid gap-3.5 rounded-lg border p-3.5">
        {[0, 1, 2, 3, 4, 5].map((index) => (
          <div key={index} className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3">
            <Skeleton className="h-3.5 w-20" />
            <Skeleton className="h-8" />
          </div>
        ))}
      </div>
    </div>
  );
}

'use client';

import {
  AlertTriangleIcon,
  CalendarClockIcon,
  CalendarXIcon,
  ListChecksIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import type { BacklogInsightResponse } from '@/types/report';

/**
 * Bốn thẻ số cho backlog insight — cùng khuôn `StatTiles` của Thống kê: bốn giá trị đơn lẻ
 * không có gì để so sánh trực quan, nên là thẻ số chứ không phải biểu đồ (cùng lý do đã ghi
 * ở `stat-tiles.tsx`).
 */
export function BacklogInsightCards({ data }: { data: BacklogInsightResponse }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Tile
        icon={<ListChecksIcon className="size-4" />}
        label="Task còn mở"
        value={data.totalOpen}
        hint="Chưa ở cột nhóm Xong."
      />
      <Tile
        icon={<AlertTriangleIcon className="size-4" />}
        label="Quá hạn"
        value={data.overdue}
        tone={data.overdue > 0 ? 'danger' : undefined}
        hint="Đã qua hạn mà chưa Xong."
      />
      <Tile
        icon={<CalendarClockIcon className="size-4" />}
        label="Sắp đến hạn"
        value={data.dueSoon}
        hint="Trong vòng 7 ngày tới."
      />
      <Tile
        icon={<CalendarXIcon className="size-4" />}
        label="Không có hạn"
        value={data.noDueDate}
        hint="Chưa đặt ngày hết hạn."
      />
    </div>
  );
}

function Tile({
  icon,
  label,
  value,
  hint,
  tone,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
  hint: string;
  tone?: 'danger';
}) {
  return (
    <div className="bg-card grid gap-1 rounded-lg border p-4">
      <p className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium">
        {icon}
        {label}
      </p>
      <p
        className={cn(
          'text-3xl leading-tight font-semibold tabular-nums',
          tone === 'danger' && value > 0 && 'text-destructive',
        )}
      >
        {value}
      </p>
      <p className="text-muted-foreground text-xs">{hint}</p>
    </div>
  );
}

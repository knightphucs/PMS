'use client';

import { AlertTriangleIcon, CheckCircle2Icon, ListChecksIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { ProjectStatisticsResponse } from '@/types/statistics';

/**
 * Hàng số liệu đầu trang.
 *
 * 📌 Cố ý KHÔNG vẽ biểu đồ cho ba con số này. Một giá trị đơn lẻ thuộc về **thẻ số**, và
 * "tỷ lệ hoàn thành" là một tỷ lệ so với mốc 100% nên nó thuộc về **thanh mức** — không
 * phải biểu đồ tròn hai lát. Biểu đồ chỉ dành cho việc so sánh, mà ở đây không có gì để so.
 */
export function StatTiles({ stats }: { stats: ProjectStatisticsResponse }) {
  return (
    <div className="grid gap-4 sm:grid-cols-3">
      <Tile
        icon={<ListChecksIcon className="size-4" />}
        label="Tổng số task"
        value={stats.totalTasks}
        hint="Bao gồm cả subtask — subtask là công việc thật, board ẩn nó đi chỉ vì hiển thị."
      />

      <CompletionTile rate={stats.completionRate} />

      <Tile
        icon={<AlertTriangleIcon className="size-4" />}
        label="Quá hạn"
        value={stats.overdueTasks}
        tone={stats.overdueTasks > 0 ? 'danger' : undefined}
        hint="Task đã qua hạn mà chưa ở trạng thái Xong."
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

/**
 * Thanh mức: tỷ lệ so với mốc 100%.
 *
 * Rãnh nền dùng màu TRUNG TÍNH (`--muted`), không phải một bậc của thang màu biểu đồ. Đã
 * thử bậc `--viz-seq-1` và nhìn trên màn hình thì nó đọc thành một phân đoạn dữ liệu thứ
 * hai chứ không phải phần còn trống — nhất là ở chế độ tối, nơi bậc đó là một màu xanh khá
 * đậm. Rãnh phải lùi hẳn về sau; chỉ phần đã đầy mới mang màu.
 */
function CompletionTile({ rate }: { rate: number }) {
  const clamped = Math.max(0, Math.min(100, rate));

  return (
    <div className="bg-card grid gap-1 rounded-lg border p-4">
      <p className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium">
        <CheckCircle2Icon className="size-4" />
        Tỷ lệ hoàn thành
      </p>

      <p className="text-3xl leading-tight font-semibold tabular-nums">
        {clamped.toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%
      </p>

      <div
        role="meter"
        aria-valuenow={clamped}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Tỷ lệ task đã hoàn thành"
        className="bg-muted mt-1 h-2 overflow-hidden rounded-full"
      >
        <div
          className="h-full rounded-full transition-[width] duration-300"
          style={{ width: `${clamped}%`, backgroundColor: 'var(--viz-load-done)' }}
        />
      </div>

      <p className="text-muted-foreground text-xs">Số task ở trạng thái Xong trên tổng số.</p>
    </div>
  );
}

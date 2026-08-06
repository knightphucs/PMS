'use client';

import { AlertTriangleIcon, BarChart3Icon, RefreshCwIcon } from 'lucide-react';
import { useState } from 'react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useBacklogInsight } from '@/lib/hooks/use-reports';
import { PRIORITY_LABEL } from '@/types/enums';
import type { BacklogInsightResponse } from '@/types/report';

/**
 * Bản Insights ngữ cảnh của Backlog, lấy cảm hứng từ panel bên phải của Jira.
 *
 * Đây không phải một dashboard thứ hai: chỉ trả lời nhanh các câu hỏi cần thiết ngay lúc
 * sắp backlog (còn bao nhiêu việc mở, việc nào đã trễ, phân bố độ ưu tiên). Dashboard
 * "Thống kê" vẫn là nơi xem toàn cảnh dự án.
 */
export function BacklogInsightsPanel({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger render={<Button variant="outline" size="sm" />}>
        <BarChart3Icon className="size-4" />
        Insights
      </DialogTrigger>

      <DialogContent
        className="top-2 right-2 bottom-2 left-auto h-[calc(100dvh-1rem)] w-[34rem] max-w-[calc(100%-1rem)] translate-x-0 translate-y-0 grid-rows-[auto_minmax(0,1fr)] gap-2 overflow-hidden rounded-xl p-3 sm:p-4"
        showCloseButton={false}
      >
        <DialogHeader className="flex-row items-center justify-between gap-3 border-b pb-3 pr-0">
          <DialogTitle>Insights</DialogTitle>
          <DialogClose render={<Button variant="ghost" size="icon-sm" />}>
            <span aria-hidden="true" className="text-xl leading-none">×</span>
            <span className="sr-only">Đóng insights</span>
          </DialogClose>
        </DialogHeader>
        <div className="min-h-0 overflow-y-auto pr-1">
          {open ? <BacklogInsightsContent projectId={projectId} /> : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function BacklogInsightsContent({ projectId }: { projectId: string }) {
  const insight = useBacklogInsight(projectId);

  if (insight.isPending) return <InsightsSkeleton />;

  if (insight.isError) {
    return (
      <section className="grid gap-3 rounded-lg border p-4 text-sm">
        <p className="font-medium">Không tải được Insights</p>
        <p className="text-muted-foreground">Hãy thử tải lại dữ liệu backlog.</p>
        <Button variant="outline" size="sm" className="w-fit" onClick={() => void insight.refetch()}>
          <RefreshCwIcon className="size-4" />
          Thử lại
        </Button>
      </section>
    );
  }

  return <InsightSections data={insight.data} />;
}

function InsightSections({ data }: { data: BacklogInsightResponse }) {
  const scheduled = Math.max(data.totalOpen - data.noDueDate, 0);
  const onTrack = Math.max(scheduled - data.overdue - data.dueSoon, 0);
  const priorityTotal = data.byPriority.reduce((sum, item) => sum + item.count, 0);

  return (
    <div className="grid gap-2 pt-1">
      <section className="grid gap-2 rounded-lg border p-3">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="font-semibold">Sức khỏe backlog</h2>
            <p className="text-muted-foreground mt-1 text-sm">
              {data.totalOpen} task còn mở
            </p>
          </div>
          {data.overdue > 0 ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
              <AlertTriangleIcon className="size-3.5" /> Cần chú ý
            </span>
          ) : (
            <span className="rounded-full bg-emerald-500/10 px-2 py-1 text-xs font-medium text-emerald-700 dark:text-emerald-400">Ổn định</span>
          )}
        </div>

        <InsightRow label="Đúng tiến độ" value={onTrack} total={data.totalOpen} tone="bg-emerald-500" />
        <InsightRow label="Sắp đến hạn" value={data.dueSoon} total={data.totalOpen} tone="bg-amber-500" />
        <InsightRow label="Quá hạn" value={data.overdue} total={data.totalOpen} tone="bg-destructive" />
        <InsightRow label="Chưa đặt hạn" value={data.noDueDate} total={data.totalOpen} tone="bg-muted-foreground" />
      </section>

      <section className="grid gap-2 rounded-lg border p-3">
        <div>
          <h2 className="font-semibold">Phân bố độ ưu tiên</h2>
          <p className="text-muted-foreground mt-1 text-sm">Các task còn mở được xếp theo độ ưu tiên.</p>
        </div>
        <div className="grid gap-2">
          {data.byPriority.map((item) => (
            <InsightRow
              key={item.priority}
              label={PRIORITY_LABEL[item.priority]}
              value={item.count}
              total={priorityTotal}
              tone="bg-primary"
            />
          ))}
        </div>
      </section>
    </div>
  );
}

function InsightRow({ label, value, total, tone }: { label: string; value: number; total: number; tone: string }) {
  const percentage = total ? Math.round((value / total) * 100) : 0;
  return (
    <div className="grid grid-cols-[auto_1fr_auto] items-center gap-2 text-sm">
      <span className="min-w-24 text-muted-foreground">{label}</span>
      <div
        className="h-2 overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={total}
        aria-valuenow={value}
      >
        <div className={`h-full rounded-full ${tone}`} style={{ width: `${percentage}%` }} />
      </div>
      <span className="w-10 text-right text-xs font-medium tabular-nums">{value}</span>
    </div>
  );
}

function InsightsSkeleton() {
  return (
    <div className="grid gap-3" aria-busy="true">
      <Skeleton className="h-5 w-3/4" />
      <Skeleton className="h-52" />
      <Skeleton className="h-56" />
    </div>
  );
}

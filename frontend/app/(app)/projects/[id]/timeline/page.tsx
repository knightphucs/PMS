'use client';

import { GanttChartIcon } from 'lucide-react';
import { useParams } from 'next/navigation';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { SprintTimelineChart } from '@/components/statistics/sprint-timeline-chart';
import { Skeleton } from '@/components/ui/skeleton';
import { useTimeline } from '@/lib/hooks/use-reports';

/**
 * Timeline — một trong ba báo cáo kiểu Jira (§1 hạng mục 11 ARCHITECTURE.md), tách riêng
 * khỏi Backlog Insight/Velocity (2026-08-06) — xem ghi chú ở `backlog-insight/page.tsx`.
 *
 * MỌI sprint có mặt (kể cả chưa bắt đầu), khác Velocity chỉ có sprint đã đóng — đây là
 * roadmap, không phải số liệu đã chốt.
 */
export default function TimelinePage() {
  const { id } = useParams<{ id: string }>();
  const timeline = useTimeline(id);

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-5">
      <PageHeader
        title="Timeline"
        description="Mọi sprint trên một trục thời gian chung — so được lịch sprint này với sprint khác."
      />

      {timeline.isError ? (
        <QueryError
          title="Không tải được báo cáo"
          error={timeline.error}
          onRetry={() => void timeline.refetch()}
          isRetrying={timeline.isFetching}
        />
      ) : timeline.isPending ? (
        <Skeleton className="h-64" />
      ) : timeline.data.sprints.length > 0 ? (
        <SprintTimelineChart sprints={timeline.data.sprints} />
      ) : (
        <EmptyState
          icon={<GanttChartIcon className="size-8" />}
          title="Chưa có sprint nào"
          description="Tạo sprint ở tab Sprint để thấy roadmap theo thời gian."
        />
      )}
    </div>
  );
}

'use client';

import { TrendingUpIcon } from 'lucide-react';
import { useParams } from 'next/navigation';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { VelocityChart } from '@/components/statistics/velocity-chart';
import { Skeleton } from '@/components/ui/skeleton';
import { useVelocity } from '@/lib/hooks/use-reports';

/**
 * Velocity — một trong ba báo cáo kiểu Jira (§1 hạng mục 11 ARCHITECTURE.md), tách riêng
 * khỏi Backlog Insight/Timeline (2026-08-06) — xem ghi chú ở `backlog-insight/page.tsx`.
 *
 * Cùng quyền với tab Thống kê (ADR-039), không gác thêm ở client.
 */
export default function VelocityPage() {
  const { id } = useParams<{ id: string }>();
  const velocity = useVelocity(id);

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-5">
      <PageHeader
        title="Velocity"
        description="Số task Xong mỗi sprint đã đóng sổ. Chỉ sprint đã đóng mới có mặt."
      />

      {velocity.isError ? (
        <QueryError
          title="Không tải được báo cáo"
          error={velocity.error}
          onRetry={() => void velocity.refetch()}
          isRetrying={velocity.isFetching}
        />
      ) : velocity.isPending ? (
        <Skeleton className="h-72" />
      ) : velocity.data.sprints.length > 0 ? (
        <VelocityChart points={velocity.data.sprints} average={velocity.data.averageVelocity} />
      ) : (
        <EmptyState
          icon={<TrendingUpIcon className="size-8" />}
          title="Chưa có sprint nào đóng sổ"
          description="Bắt đầu và đóng ít nhất một sprint ở tab Sprint để thấy velocity."
        />
      )}
    </div>
  );
}

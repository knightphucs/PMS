'use client';

import { BarChart3Icon, TimerIcon, UsersIcon } from 'lucide-react';
import { useParams } from 'next/navigation';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { AssigneeWorkloadChart } from '@/components/statistics/assignee-workload-chart';
import {
  PriorityDistributionChart,
  StatusDistributionChart,
} from '@/components/statistics/distribution-charts';
import { SprintProgressList } from '@/components/statistics/sprint-progress-list';
import { StatTiles } from '@/components/statistics/stat-tiles';
import { Skeleton } from '@/components/ui/skeleton';
import { useProjectStatistics } from '@/lib/hooks/use-statistics';

/**
 * Tab Thống kê của một dự án.
 *
 * 📌 KHÔNG gác quyền ở client: `ProjectAction.ViewStatistics` cho **cả ba vai trò** kể từ
 * ADR-039 — thống kê là tổng hợp của dữ liệu mà mọi thành viên vốn đã đọc được qua board.
 * Người ngoài dự án nhận 404 từ API và rơi vào nhánh `QueryError`, đúng như mọi tab khác.
 */
export default function ProjectStatisticsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useProjectStatistics(id);

  return (
    // `min-w-0` + cột tường minh `minmax(0,1fr)`: Recharts `ResponsiveContainer` đo bề rộng
    // của cha, còn grid item thì mặc định `min-width:auto` — hai thứ đó gặp nhau thành một
    // vòng nở ra không có điểm dừng. Đo được ở 375px: trang phồng 459px trong khung 343px.
    // Cùng khuôn với `components/tasks/task-section.tsx`.
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-5">
      <PageHeader
        title="Thống kê"
        description="Tổng hợp toàn dự án, tính cả subtask. Số liệu do server tính sẵn — biểu đồ chỉ vẽ lại."
      />

      {query.isError ? (
        <QueryError
          title="Không tải được thống kê"
          error={query.error}
          onRetry={() => void query.refetch()}
          isRetrying={query.isFetching}
        />
      ) : query.isPending ? (
        <StatisticsSkeleton />
      ) : query.data.totalTasks === 0 ? (
        <EmptyState
          icon={<BarChart3Icon className="size-8" />}
          title="Chưa có số liệu"
          description="Dự án chưa có task nào. Tạo task đầu tiên ở tab Bảng hoặc Backlog rồi quay lại đây."
        />
      ) : (
        <>
          <StatTiles stats={query.data} />

          <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4 lg:grid-cols-2">
            <StatusDistributionChart data={query.data.byStatus} />
            <PriorityDistributionChart data={query.data.byPriority} />
          </div>

          {query.data.byAssignee.length > 0 ? (
            <AssigneeWorkloadChart data={query.data.byAssignee} />
          ) : (
            <EmptyState
              compact
              icon={<UsersIcon className="size-6" />}
              title="Chưa giao việc cho ai"
              description="Gán người đảm nhận cho task để thấy khối lượng phân bổ theo từng người."
            />
          )}

          {query.data.sprints.length > 0 ? (
            <SprintProgressList sprints={query.data.sprints} />
          ) : (
            <EmptyState
              compact
              icon={<TimerIcon className="size-6" />}
              title="Chưa có sprint nào"
              description="Tạo sprint ở tab Sprint để theo dõi tiến độ theo từng chu kỳ."
            />
          )}
        </>
      )}
    </div>
  );
}

function StatisticsSkeleton() {
  return (
    <div className="grid gap-5" aria-busy="true">
      <span className="sr-only">Đang tải thống kê dự án…</span>

      <div className="grid gap-4 sm:grid-cols-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-28" />
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="h-72" />
        <Skeleton className="h-72" />
      </div>

      <Skeleton className="h-48" />
    </div>
  );
}

'use client';

import { useParams } from 'next/navigation';

import { QueryError } from '@/components/common/query-error';
import { ProjectTabs } from '@/components/projects/project-tabs';
import { StatusBadge } from '@/components/projects/status-badge';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDate } from '@/lib/format';
import { useProjectOverview } from '@/lib/hooks/use-projects';

/**
 * Khung của trang chi tiết project.
 *
 * Đặt ở `layout.tsx` chứ không phải trong từng trang tab: layout KHÔNG bị unmount khi đổi
 * tab, nên tiêu đề và thanh tab đứng yên trong khi chỉ phần thân đổi — cùng lợi ích mà
 * `AppShell` đang cho ở tầng trên.
 *
 * Nó cũng là nơi `useProjectOverview` được mount MỘT lần cho mọi tab con: các tab và cả
 * breadcrumb trên header đều đọc lại từ cùng khóa cache đó, không ai fetch thêm.
 */
export default function ProjectDetailLayout({ children }: { children: React.ReactNode }) {
  const { id } = useParams<{ id: string }>();
  const overview = useProjectOverview(id);

  return (
    <div className="grid gap-5">
      {overview.isError ? (
        <QueryError
          title="Không mở được dự án"
          error={overview.error}
          onRetry={() => void overview.refetch()}
          isRetrying={overview.isFetching}
        />
      ) : (
        <>
          <div className="grid gap-2">
            {overview.isPending ? (
              <>
                <Skeleton className="h-7 w-72" />
                <Skeleton className="h-4 w-96" />
              </>
            ) : (
              <>
                <div className="flex flex-wrap items-center gap-3">
                  <h1 className="text-xl font-semibold tracking-tight">
                    {overview.data.name}
                  </h1>
                  <StatusBadge status={overview.data.status} />
                  <span className="text-muted-foreground text-xs">
                    Dự kiến hoàn thành {formatDate(overview.data.expectedCompletionDate)}
                  </span>
                </div>
                {overview.data.description ? (
                  <p className="text-muted-foreground max-w-3xl text-sm">
                    {overview.data.description}
                  </p>
                ) : null}
              </>
            )}
          </div>

          <div className="border-b">
            <ProjectTabs projectId={id} />
          </div>

          {children}
        </>
      )}
    </div>
  );
}

'use client';

import { useParams, useSelectedLayoutSegment } from 'next/navigation';

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
 *
 * Từ 2026-08-03 nó còn giữ slot song song `@modal` — vỏ dialog của chi tiết task. Slot
 * phải nằm ở ĐÂY chứ không thấp hơn: route chặn `(.)tasks/[taskId]` được resolve tương
 * đối với tầng chứa slot, và board (nơi bấm để mở) cũng là con của layout này.
 */
export default function ProjectDetailLayout({
  children,
  modal,
}: {
  children: React.ReactNode;
  modal: React.ReactNode;
}) {
  const { id } = useParams<{ id: string }>();
  const overview = useProjectOverview(id);

  /*
    Chi tiết task KHÔNG phải tab thứ năm — nó là trang con của dự án, nên thanh tab phải
    biến mất chứ không đứng đó với 4 mục cùng không active.

    ⚠️ Cách "gọn" hơn là gom 4 tab vào route group `[id]/(tabs)/` để chúng có layout riêng.
    ĐỪNG: flight router state giữ nguyên segment nhóm, nên `useSelectedLayoutSegment()`
    trong `project-tabs.tsx` sẽ trả `'(tabs)'` thay vì `'board'` và tab mất hẳn trạng thái
    active — hỏng im lặng, chỉ thấy bằng mắt.

    Một dòng này còn ĐÚNG HƠN route group: khi dialog mở đè lên board, slot `children` vẫn
    là board nên segment vẫn là `'board'` — tab vẫn hiện và vẫn active, đúng như Jira.
  */
  const showTabs = useSelectedLayoutSegment() !== 'tasks';

  return (
    <>
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

            {showTabs ? (
              <div className="border-b">
                <ProjectTabs projectId={id} />
              </div>
            ) : null}

            {children}
          </>
        )}
      </div>

      {/*
        Slot `@modal` nằm NGOÀI nhánh `overview.isError`: dialog chi tiết task có xử lý lỗi
        riêng của nó, không có lý do gì để một lần tải hỏng thông tin project nuốt luôn nó.
        Với mọi URL không phải task, slot này render `@modal/default.tsx` → `null`.
      */}
      {modal}
    </>
  );
}

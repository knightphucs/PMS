'use client';

import { InboxIcon, PlusIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { ApprovalStateBadge } from '@/components/requests/approval-state-badge';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { WorkItemTypeChip } from '@/components/tasks/work-item-type-chip';
import { buttonVariants } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { columnChipStyle, columnDotStyle } from '@/components/tasks/status-tone';
import { formatDate } from '@/lib/format';
import { useMyRequests } from '@/lib/hooks/use-request-portal';
import { cn } from '@/lib/utils';

/**
 * "Yêu cầu của tôi" — cổng tiếp nhận nhìn từ phía NGƯỜI GỬI (ADR-063).
 *
 * 🔴 **Đây là màn hình đầu tiên của hệ thống dành cho người KHÔNG phải thành viên project
 * nào.** Mọi màn khác đều giả định người dùng đã ở trong một dự án; màn này thì ngược lại —
 * nó phục vụ đúng những người mà `ProjectAuthorizationService` trả 404.
 *
 * Hệ quả về thiết kế: **không có lối đi nào từ đây vào trong dự án**. Mã yêu cầu, tên cột,
 * tên loại đều hiện dưới dạng chữ chứ không phải liên kết — bấm vào sẽ là một cú 404 mà
 * người dùng không hiểu vì sao. Lối duy nhất là `/requests/{id}`, và nó cũng chỉ đọc.
 *
 * 📌 Khuôn gần nhất là `/my-work` (ADR-053), và không phải trùng hợp: cả hai là endpoint
 * xuyên dự án mà quyền nằm trong chính vị từ truy vấn.
 */
export default function MyRequestsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const requests = useMyRequests(page, pageSize);

  const items = requests.data?.items ?? [];

  return (
    <div className="grid min-w-0 gap-4">
      <PageHeader
        title="Yêu cầu của tôi"
        count={requests.data?.totalCount}
        description="Những việc bạn đã gửi tới các phòng ban khác, kèm trạng thái xử lý mới nhất."
        actions={
          <Link href="/requests/new" className={cn(buttonVariants({ size: 'sm' }))}>
            <PlusIcon className="size-4" />
            Gửi yêu cầu
          </Link>
        }
      />

      {requests.isPending ? (
        <div className="grid gap-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full rounded-lg" />
          ))}
        </div>
      ) : requests.isError ? (
        <QueryError
          title="Không tải được danh sách yêu cầu"
          error={requests.error}
          onRetry={() => void requests.refetch()}
          isRetrying={requests.isRefetching}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={<InboxIcon className="size-6" />}
          title="Bạn chưa gửi yêu cầu nào"
          description="Khi cần một phòng ban khác xử lý việc gì đó, gửi yêu cầu ở đây để theo dõi được tiến độ."
          action={
            <Link href="/requests/new" className={cn(buttonVariants({ size: 'sm' }))}>
              <PlusIcon className="size-4" />
              Gửi yêu cầu đầu tiên
            </Link>
          }
        />
      ) : (
        <>
          <ul className="grid min-w-0 gap-2">
            {items.map((request) => (
              <li key={request.taskId} className="min-w-0">
                <Link
                  href={`/requests/${request.taskId}`}
                  className="bg-card hover:bg-accent/50 block min-w-0 rounded-lg border p-3 transition-colors"
                >
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <span className="text-muted-foreground shrink-0 font-mono text-xs">
                      {request.code}
                    </span>
                    <WorkItemTypeChip
                      type={{
                        typeId: request.taskId,
                        name: request.typeName,
                        icon: request.typeIcon,
                        color: request.typeColor,
                      }}
                    />
                    <span className="min-w-0 flex-1 truncate font-medium">{request.name}</span>
                    <ApprovalStateBadge state={request.approvalState} />
                    {/* Tên cột do ĐỘI XỬ LÝ đặt — người gửi đọc đúng ngôn ngữ quy trình của
                        họ, thay vì một enum bốn giá trị hệ thống bịa ra (ADR-052). */}
                    <span
                      className="inline-flex shrink-0 items-center gap-1.5 rounded px-2 py-0.5 text-xs font-medium"
                      style={columnChipStyle(request.statusColor)}
                    >
                      <span
                        className="size-1.5 shrink-0 rounded-full"
                        style={columnDotStyle(request.statusColor)}
                      />
                      {request.statusName}
                    </span>
                  </div>

                  <div className="text-muted-foreground mt-1.5 flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                    <span className="truncate">Gửi tới {request.projectName}</span>
                    <PriorityLabel priority={request.priority} />
                    <span>Gửi {formatDate(request.createdAt)}</span>
                    {request.dueDate ? <span>Hạn {formatDate(request.dueDate)}</span> : null}
                  </div>
                </Link>
              </li>
            ))}
          </ul>

          {requests.data ? (
            <ProjectPagination
              page={requests.data}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
              disabled={requests.isFetching}
              unitLabel="yêu cầu"
            />
          ) : null}
        </>
      )}
    </div>
  );
}

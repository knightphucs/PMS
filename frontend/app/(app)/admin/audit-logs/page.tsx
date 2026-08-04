'use client';

import { ScrollTextIcon } from 'lucide-react';
import { useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { UserAvatar } from '@/components/common/user-avatar';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDateTime, formatRelativeTime } from '@/lib/format';
import { useSystemAuditLogs } from '@/lib/hooks/use-activity';
import { ACTIVITY_ACTION_LABEL } from '@/types/enums';
import { DEFAULT_PAGE_SIZE } from '@/types/common';

/** Nhãn tiếng Việt cho loại đối tượng — danh sách CỐ ĐỊNH ở server, không mở rộng được. */
const ENTITY_LABEL: Record<string, string> = {
  Employee: 'Tài khoản',
  Label: 'Nhãn toàn cục',
  RolePermission: 'Phân quyền vai trò',
};

/**
 * Nhật ký cấp hệ thống — đối trọng của quyết định "SystemAdmin không có đặc quyền nghiệp vụ
 * nào" (ADR-042): họ mất quyền đọc xuyên project, nhưng vẫn phải có trách nhiệm giải trình
 * cho những việc họ THẬT SỰ làm.
 *
 * 🔴 Cố ý KHÔNG có bộ lọc `entityType` và KHÔNG có ô tìm kiếm:
 * - `entityType` cố định ở server (`Employee` / `Label` / `RolePermission`). Dựng ô lọc ở
 *   client là hứa một khả năng mà API từ chối theo thiết kế, không phải theo thiếu sót.
 * - `?search=` bị `ActivityLogRepository` **nhận rồi bỏ qua im lặng** — dựng ô tìm sẽ trả
 *   về nguyên trang chưa lọc kèm HTTP 200, tức là nói dối người dùng.
 */
export default function AdminAuditLogsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE * 2);

  const query = useSystemAuditLogs({ page, pageSize });

  return (
    <div className="grid gap-5">
      <PageHeader
        title="Nhật ký hệ thống"
        count={query.data?.totalCount}
        description="Các thao tác quản trị: khóa/mở tài khoản, đổi vai trò, sửa nhãn toàn cục, đổi quyền của vai trò. Hoạt động trong dự án nằm ở tab Lịch sử của từng dự án."
      />

      {query.isError ? (
        <QueryError
          title="Không tải được nhật ký hệ thống"
          error={query.error}
          onRetry={() => void query.refetch()}
          isRetrying={query.isFetching}
        />
      ) : query.isPending ? (
        <AuditSkeleton />
      ) : query.data.items.length === 0 ? (
        <EmptyState
          icon={<ScrollTextIcon className="size-8" />}
          title="Chưa có thao tác quản trị nào"
          description="Nhật ký chỉ ghi thao tác cấp hệ thống. Tạo dự án hay sửa task không xuất hiện ở đây — đó là chủ ý, không phải thiếu sót."
        />
      ) : (
        <>
          <ol
            className="bg-card divide-y rounded-lg border"
            aria-busy={query.isFetching || undefined}
          >
            {query.data.items.map((log) => (
              <li key={log.id} className="flex gap-3 px-3 py-2.5">
                <UserAvatar id={log.actorId} name={log.actorName} size="sm" />

                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <span className="text-[13px] font-medium">{log.actorName}</span>
                    <Badge variant="secondary">
                      {ACTIVITY_ACTION_LABEL[log.action] ?? log.action}
                    </Badge>
                    <Badge variant="outline">
                      {ENTITY_LABEL[log.entityType] ?? log.entityType}
                    </Badge>
                    <span
                      className="text-muted-foreground text-xs"
                      title={formatDateTime(log.createdAt)}
                    >
                      {formatRelativeTime(log.createdAt)}
                    </span>
                  </div>

                  <p className="mt-0.5 text-sm leading-relaxed break-words">{log.detail}</p>
                </div>
              </li>
            ))}
          </ol>

          <ProjectPagination
            page={query.data}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            disabled={query.isFetching}
            unitLabel="bản ghi"
          />
        </>
      )}
    </div>
  );
}

function AuditSkeleton({ rows = 10 }: { rows?: number }) {
  return (
    <div className="bg-card divide-y rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải nhật ký hệ thống…</span>
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex gap-3 px-3 py-2.5">
          <Skeleton className="size-7 shrink-0 rounded-full" />
          <div className="grid flex-1 gap-1.5">
            <Skeleton className="h-4 w-64" />
            <Skeleton className="h-4 w-full max-w-md" />
          </div>
        </div>
      ))}
    </div>
  );
}

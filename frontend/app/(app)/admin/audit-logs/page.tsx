'use client';

import { ScrollTextIcon, SearchIcon, SearchXIcon } from 'lucide-react';
import { useEffect, useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { UserAvatar } from '@/components/common/user-avatar';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { useDebounced } from '@/lib/hooks/use-debounced';
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
 * 🔴 Cố ý KHÔNG có bộ lọc `entityType`: danh sách loại đối tượng được hard-code ở server
 * (`Employee` / `Label` / `RolePermission`). Dựng ô lọc ở client là hứa một khả năng mà API
 * từ chối **theo thiết kế**, không phải theo thiếu sót.
 *
 * ✅ Ô tìm thì CÓ, và chỉ có từ 2026-08-04. Trước đó `ActivityLogRepository` nhận `?search=`
 * rồi **bỏ qua im lặng** — trả HTTP 200 kèm nguyên trang chưa lọc, tức một câu trả lời sai
 * mà client không có cách nào phát hiện. Nay repository lọc thật trên `Detail`, có
 * `ActivityLogsTests.Search_thuc_su_loc_chu_khong_bi_bo_qua_im_lang` giữ (kèm khẳng định
 * "từ khóa không tồn tại phải cho trang rỗng", để một bộ lọc luôn-khớp không lọt).
 */
export default function AdminAuditLogsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE * 2);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);

  useEffect(() => {
    setPage(1);
  }, [search, pageSize]);

  const query = useSystemAuditLogs({ page, pageSize, search: search || undefined });

  return (
    <div className="grid gap-5">
      <PageHeader
        title="Nhật ký hệ thống"
        count={query.data?.totalCount}
        description="Các thao tác quản trị: khóa/mở tài khoản, đổi vai trò, sửa nhãn toàn cục, đổi quyền của vai trò. Hoạt động trong dự án nằm ở tab Lịch sử của từng dự án."
      />

      <div className="bg-card flex flex-wrap items-center gap-3 rounded-lg border p-3">
        <div className="relative min-w-56 flex-1">
          <SearchIcon className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Tìm trong nội dung thao tác…"
            aria-label="Tìm trong nội dung thao tác"
            className="pl-9"
          />
        </div>
        {search ? (
          <span className="text-muted-foreground text-sm" aria-live="polite">
            {query.data?.totalCount ?? 0} kết quả cho “{search}”
          </span>
        ) : null}
      </div>

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
        search ? (
          <EmptyState
            icon={<SearchXIcon className="size-8" />}
            title={`Không có thao tác nào khớp “${search}”`}
            description="Ô tìm lọc theo nội dung mô tả của thao tác, ví dụ một địa chỉ email hoặc tên nhãn."
          />
        ) : (
          <EmptyState
            icon={<ScrollTextIcon className="size-8" />}
            title="Chưa có thao tác quản trị nào"
            description="Nhật ký chỉ ghi thao tác cấp hệ thống. Tạo dự án hay sửa task không xuất hiện ở đây — đó là chủ ý, không phải thiếu sót."
          />
        )
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

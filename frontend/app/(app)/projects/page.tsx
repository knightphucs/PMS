'use client';

import { FolderPlusIcon, SearchIcon, SearchXIcon } from 'lucide-react';
import { useEffect, useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { CreateProjectDialog } from '@/components/projects/create-project-dialog';
import { DeleteProjectDialog } from '@/components/projects/delete-project-dialog';
import { EditProjectDialog } from '@/components/projects/edit-project-dialog';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { ProjectTable, ProjectTableSkeleton } from '@/components/projects/project-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useDebounced } from '@/lib/hooks/use-debounced';
import { useProjects } from '@/lib/hooks/use-projects';
import { DEFAULT_PAGE_SIZE } from '@/types/common';
import type { ProjectSummaryResponse } from '@/types/project';

export default function ProjectsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);

  // Giữ cả đối tượng chứ không chỉ id: dialog cần `name` để hiện ngay tiêu đề mà không
  // phải đợi request chi tiết về.
  const [editing, setEditing] = useState<ProjectSummaryResponse | null>(null);
  const [deleting, setDeleting] = useState<ProjectSummaryResponse | null>(null);

  // Đổi từ khóa mà giữ nguyên số trang sẽ rơi vào trang trống khi kết quả mới ít hơn.
  useEffect(() => {
    setPage(1);
  }, [search, pageSize]);

  const query = useProjects({ page, pageSize, search: search || undefined, sortBy: 'name' });

  const showSkeleton = query.isPending;
  const hasResults = (query.data?.items.length ?? 0) > 0;

  return (
    <div className="grid gap-6">
      <PageHeader
        title="Dự án"
        count={query.data?.totalCount}
        description="Các dự án bạn đang tham gia."
        actions={<CreateProjectDialog />}
      />

      <div className="bg-card flex flex-wrap items-center gap-3 rounded-lg border p-3">
        <div className="relative min-w-56 flex-1">
          <SearchIcon className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Tìm theo tên dự án…"
            aria-label="Tìm dự án theo tên"
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
          title="Không tải được danh sách dự án"
          error={query.error}
          onRetry={() => void query.refetch()}
          isRetrying={query.isFetching}
        />
      ) : showSkeleton ? (
        <ProjectTableSkeleton rows={pageSize > 10 ? 10 : pageSize} />
      ) : hasResults ? (
        <>
          {/* Vẫn hiện dữ liệu trang cũ trong lúc tải trang mới (placeholderData), chỉ
              làm mờ đi — bảng không sập xuống skeleton ở mỗi lần bấm phân trang. */}
          <ProjectTable
            projects={query.data!.items}
            dimmed={query.isFetching}
            onEdit={setEditing}
            onDelete={setDeleting}
          />
          <ProjectPagination
            page={query.data!}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            disabled={query.isFetching}
          />
        </>
      ) : search ? (
        <EmptyState
          icon={<SearchXIcon className="size-8" />}
          title="Không tìm thấy dự án nào"
          description={`Không có dự án nào khớp với "${search}".`}
          action={
            <Button variant="outline" onClick={() => setSearchInput('')}>
              Xóa bộ lọc
            </Button>
          }
        />
      ) : (
        <EmptyState
          icon={<FolderPlusIcon className="size-8" />}
          title="Chưa có dự án nào"
          description="Tạo dự án đầu tiên để bắt đầu. Bạn sẽ là quản lý của dự án mình tạo."
          action={<CreateProjectDialog />}
        />
      )}

      <EditProjectDialog project={editing} onClose={() => setEditing(null)} />
      <DeleteProjectDialog project={deleting} onClose={() => setDeleting(null)} />
    </div>
  );
}

'use client';

import { FolderPlusIcon, RefreshCwIcon, SearchIcon, SearchXIcon } from 'lucide-react';
import { useEffect, useState } from 'react';

import { CreateProjectDialog } from '@/components/projects/create-project-dialog';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { ProjectTable, ProjectTableSkeleton } from '@/components/projects/project-table';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { errorMessage } from '@/lib/api/problem';
import { useDebounced } from '@/lib/hooks/use-debounced';
import { useProjects } from '@/lib/hooks/use-projects';
import { DEFAULT_PAGE_SIZE } from '@/types/common';

export default function ProjectsPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);

  // Đổi từ khóa mà giữ nguyên số trang sẽ rơi vào trang trống khi kết quả mới ít hơn.
  useEffect(() => {
    setPage(1);
  }, [search, pageSize]);

  const query = useProjects({ page, pageSize, search: search || undefined, sortBy: 'name' });

  const showSkeleton = query.isPending;
  const hasResults = (query.data?.items.length ?? 0) > 0;

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="grid gap-1">
          <h1 className="text-2xl font-semibold tracking-tight">Dự án</h1>
          <p className="text-muted-foreground text-sm">
            Các dự án bạn đang tham gia.
          </p>
        </div>
        <CreateProjectDialog />
      </div>

      <div className="relative max-w-sm">
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

      {query.isError ? (
        <Alert variant="destructive">
          <AlertTitle>Không tải được danh sách dự án</AlertTitle>
          <AlertDescription className="grid gap-3">
            <span>{errorMessage(query.error)}</span>
            <Button
              variant="outline"
              size="sm"
              className="w-fit"
              onClick={() => void query.refetch()}
              disabled={query.isFetching}
            >
              <RefreshCwIcon className="size-4" />
              {query.isFetching ? 'Đang thử lại…' : 'Thử lại'}
            </Button>
          </AlertDescription>
        </Alert>
      ) : showSkeleton ? (
        <ProjectTableSkeleton rows={pageSize > 10 ? 10 : pageSize} />
      ) : hasResults ? (
        <>
          {/* Vẫn hiện dữ liệu trang cũ trong lúc tải trang mới (placeholderData), chỉ
              làm mờ đi — bảng không sập xuống skeleton ở mỗi lần bấm phân trang. */}
          <ProjectTable projects={query.data!.items} dimmed={query.isFetching} />
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
    </div>
  );
}

function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="grid place-items-center gap-3 rounded-lg border border-dashed px-6 py-16 text-center">
      <div className="text-muted-foreground">{icon}</div>
      <div className="grid gap-1">
        <p className="font-medium">{title}</p>
        <p className="text-muted-foreground max-w-md text-sm">{description}</p>
      </div>
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}

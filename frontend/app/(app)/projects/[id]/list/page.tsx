'use client';

import { PlusIcon, SlidersHorizontalIcon } from 'lucide-react';
import { useParams } from 'next/navigation';
import { useMemo, useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { TaskListTable, TaskListTableSkeleton } from '@/components/tasks/task-list-table';
import { useTaskActions } from '@/components/tasks/use-task-actions';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { FilterBuilder } from '@/components/views/filter-builder';
import { ViewBar } from '@/components/views/view-bar';
import { useDebounced } from '@/lib/hooks/use-debounced';
import { useFieldDefinitions } from '@/lib/hooks/use-custom-fields';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useSavedViews, useTaskQuery } from '@/lib/hooks/use-saved-views';
import { canManageTasks } from '@/lib/tasks/permissions';
import { DEFAULT_PAGE_SIZE } from '@/types/common';
import type {
  SavedViewColumnDto,
  SavedViewFilterDto,
  SavedViewResponse,
  TaskField,
} from '@/types/saved-view';

/** Trạng thái view đang xem — tách khỏi bản đã lưu để so được "có thay đổi chưa lưu". */
interface ViewState {
  sortBy: TaskField | null;
  sortDescending: boolean;
  groupBy: TaskField | null;
  filters: SavedViewFilterDto[];
  columns: SavedViewColumnDto[];
}

const BLANK: ViewState = {
  sortBy: null,
  sortDescending: false,
  groupBy: null,
  filters: [],
  columns: [],
};

function toState(view: SavedViewResponse): ViewState {
  return {
    sortBy: view.sortBy,
    sortDescending: view.sortDescending,
    groupBy: view.groupBy,
    filters: view.filters,
    columns: view.columns,
  };
}

/**
 * Danh sách task + view lưu được (ADR-061).
 *
 * 🔑 Đây là màn mà một **hàng đợi** sẽ sống trên đó: *"mọi Change Request đang chờ tôi
 * duyệt"* là một view chia sẻ, không phải một màn hình mới. Vì vậy trạng thái lọc nằm ở
 * component chứ không nằm trong URL — nó quá lớn để nhét vào query string, và view đã lưu
 * chính là cách chia sẻ nó.
 */
export default function ProjectListPage() {
  const { id } = useParams<{ id: string }>();

  const [activeViewId, setActiveViewId] = useState<string | null>(null);
  const [state, setState] = useState<ViewState>(BLANK);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [showFilters, setShowFilters] = useState(false);

  const debouncedSearch = useDebounced(search, 300);

  const views = useSavedViews(id);
  const definitions = useFieldDefinitions(id);
  const { role, myEmployeeId } = useMyProjectRole(id);
  const taskActions = useTaskActions({ projectId: id, role, myEmployeeId });

  const request = useMemo(
    () => ({
      filters: state.filters,
      sortBy: state.sortBy,
      sortDescending: state.sortDescending,
      search: debouncedSearch.trim() || null,
      page,
      pageSize,
    }),
    [state.filters, state.sortBy, state.sortDescending, debouncedSearch, page, pageSize],
  );

  const result = useTaskQuery(id, request);

  const activeView = views.data?.find((v) => v.id === activeViewId) ?? null;

  // So sánh bằng JSON: `filters`/`columns` là mảng đối tượng nhỏ (trần 20/30 phần tử ở
  // backend), nên một phép so chuỗi rẻ hơn hẳn việc tự viết so sâu — và không có rủi ro
  // quên một trường khi DTO đổi.
  const isDirty = activeView
    ? JSON.stringify(toState(activeView)) !== JSON.stringify(state)
    : state.filters.length > 0 || state.columns.length > 0 || state.sortBy !== null;

  const handleSelectView = (viewId: string | null) => {
    setActiveViewId(viewId);
    const next = viewId ? views.data?.find((v) => v.id === viewId) : null;
    setState(next ? toState(next) : BLANK);
    setPage(1);
  };

  const handleSaved = (view: SavedViewResponse) => {
    setActiveViewId(view.id);
    setState(toState(view));
  };

  /** Cột trường tuỳ biến view đang chọn. Bỏ id không còn khớp trường nào (trường đã xoá). */
  const customColumns = useMemo(
    () =>
      state.columns
        .filter((c) => c.fieldDefinitionId)
        .map((c) => definitions.data?.find((d) => d.id === c.fieldDefinitionId))
        .filter((d): d is NonNullable<typeof d> => d !== undefined)
        .map((d) => ({ id: d.id, label: d.label })),
    [state.columns, definitions.data],
  );

  const items = result.data?.items ?? [];

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4">
      <PageHeader
        title="Danh sách"
        count={result.data?.totalCount}
        description="Lọc, sắp xếp và lưu lại góc nhìn của riêng bạn hoặc của cả đội."
        actions={
          taskActions.canManage ? (
            <Button size="sm" onClick={() => taskActions.openCreate()}>
              <PlusIcon className="size-4" />
              Tạo task
            </Button>
          ) : null
        }
      />

      <div className="grid gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <ViewBar
            projectId={id}
            views={views.data ?? []}
            activeViewId={activeViewId}
            isDirty={isDirty}
            canShare={canManageTasks(role)}
            currentState={state}
            onSelect={handleSelectView}
            onSaved={handleSaved}
          />

          <Button
            size="sm"
            variant="outline"
            aria-expanded={showFilters}
            onClick={() => setShowFilters((open) => !open)}
          >
            <SlidersHorizontalIcon className="size-4" />
            Bộ lọc
            {state.filters.length > 0 ? ` (${state.filters.length})` : ''}
          </Button>

          <Input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              // Đổi từ khoá mà giữ nguyên số trang sẽ hiện một trang trống khi kết quả mới
              // ít hơn — người dùng tưởng không tìm thấy gì.
              setPage(1);
            }}
            placeholder="Tìm theo tên task…"
            className="h-8 w-56"
            aria-label="Tìm theo tên task"
          />
        </div>

        {showFilters ? (
          <div className="bg-card grid gap-3 rounded-lg border p-3">
            <FilterBuilder
              projectId={id}
              filters={state.filters}
              onChange={(filters) => {
                setState((prev) => ({ ...prev, filters }));
                setPage(1);
              }}
            />
          </div>
        ) : null}
      </div>

      {result.isError ? (
        <QueryError
          title="Không tải được danh sách task"
          error={result.error}
          onRetry={() => void result.refetch()}
          isRetrying={result.isFetching}
        />
      ) : result.isPending ? (
        <TaskListTableSkeleton />
      ) : items.length === 0 ? (
        <EmptyState
          title="Không có task nào khớp"
          description={
            state.filters.length > 0 || debouncedSearch
              ? 'Thử bớt điều kiện lọc hoặc đổi từ khoá tìm kiếm.'
              : 'Dự án này chưa có task nào.'
          }
        />
      ) : (
        <>
          <TaskListTable
            items={items}
            projectId={id}
            customColumns={customColumns}
            renderMenu={taskActions.renderMenu}
          />
          <ProjectPagination
            page={result.data}
            unitLabel="task"
            disabled={result.isFetching}
            onPageChange={setPage}
            onPageSizeChange={(next) => {
              setPageSize(next);
              setPage(1);
            }}
          />
        </>
      )}

      {taskActions.dialogs}
    </div>
  );
}

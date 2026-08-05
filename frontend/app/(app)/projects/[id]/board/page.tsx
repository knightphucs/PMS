'use client';

import { PlusIcon } from 'lucide-react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { Suspense } from 'react';

import { BoardSkeleton } from '@/components/board/board-skeleton';
import { BoardView } from '@/components/board/board-view';
import { ManageColumnsDialog } from '@/components/board/manage-columns-dialog';
import { SprintSwitcher } from '@/components/board/sprint-switcher';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { useTaskActions } from '@/components/tasks/use-task-actions';
import { Button } from '@/components/ui/button';
import { useBoard } from '@/lib/hooks/use-board';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useSprints } from '@/lib/hooks/use-sprints';

/**
 * ⚠️ `useSearchParams` bắt `next build` đòi một biên `<Suspense>`. Nó qua được
 * `npm run dev` và CHỈ fail lúc build — nên bọc sẵn ngay từ đầu.
 */
export default function BoardPage() {
  return (
    <Suspense fallback={<BoardSkeleton />}>
      <BoardContent />
    </Suspense>
  );
}

function BoardContent() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();

  // Sprint nằm trong URL chứ không phải state: chia sẻ được link tới đúng bảng đang xem,
  // và nút Back của trình duyệt quay lại đúng sprint trước đó.
  const sprintId = searchParams.get('sprint');

  const sprints = useSprints(id);
  const board = useBoard(id, sprintId);
  const { role, myEmployeeId } = useMyProjectRole(id);
  const taskActions = useTaskActions({
    projectId: id,
    role,
    myEmployeeId,
    // Đang xem bảng của sprint nào thì task tạo mới gợi ý luôn sprint đó.
    defaultSprintId: sprintId,
  });

  const handleSprintChange = (next: string | null) => {
    const params = new URLSearchParams(searchParams.toString());
    if (next) params.set('sprint', next);
    else params.delete('sprint');

    const query = params.toString();
    // `scroll: false` để đổi sprint không giật trang về đầu.
    router.replace(`/projects/${id}/board${query ? `?${query}` : ''}`, { scroll: false });
  };

  const taskCount = board.data?.columns.reduce((sum, column) => sum + column.tasks.length, 0);

  return (
    // 🔴 `grid-cols-[minmax(0,1fr)]` là BẮT BUỘC, không phải trang trí.
    //
    // `BoardView` là một grid item cuộn ngang. `min-w-0` trên chính nó chưa đủ: một grid
    // `grid` trần có track ngầm cỡ `auto`, và `auto` phân giải thành **max-content** — tức
    // là track vẫn phồng theo tổng bề rộng mọi cột rồi đẩy tràn cả trang. Ràng track về
    // `minmax(0,1fr)` mới cắt được đường đó.
    //
    // Đã đo: thiếu dòng này thì ở 375px `scrollWidth = 657` trong khi `clientWidth = 375`.
    // Đây đúng lớp lỗi `min-width:auto` đã cắn dự án ba lần trong một phiên (xem §0a).
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4">
      <PageHeader
        title="Bảng công việc"
        count={taskCount}
        description="Kéo thẻ sang cột khác để đổi trạng thái."
        actions={
          <>
            <SprintSwitcher
              sprints={sprints.data}
              isLoading={sprints.isPending}
              value={sprintId}
              onChange={handleSprintChange}
            />
            {/* Quản lý cột: PM-only, cùng ngưỡng quyền với tạo task
                (`ProjectAction.ManageBoardColumns`, ADR-052). */}
            {taskActions.canManage ? <ManageColumnsDialog projectId={id} /> : null}

            {/* MỘT nút tạo task ở đây, KHÔNG phải nút `+` trên từng cột:
                `CreateTaskRequest` không có trường cột nên task mới luôn rơi vào cột TRÁI
                NHẤT — nút `+` trên cột "Hoàn thành" sẽ là một lời nói dối. */}
            {taskActions.canManage ? (
              <Button size="sm" onClick={taskActions.openCreate}>
                <PlusIcon className="size-4" />
                Tạo task
              </Button>
            ) : null}
          </>
        }
      />

      {board.isError ? (
        <QueryError
          title="Không tải được bảng công việc"
          error={board.error}
          onRetry={() => void board.refetch()}
          isRetrying={board.isFetching}
        />
      ) : board.isPending ? (
        <BoardSkeleton />
      ) : (
        <BoardView
          projectId={id}
          sprintId={sprintId}
          board={board.data}
          role={role}
          myEmployeeId={myEmployeeId}
          renderMenu={taskActions.renderMenu}
        />
      )}

      {taskActions.dialogs}
    </div>
  );
}

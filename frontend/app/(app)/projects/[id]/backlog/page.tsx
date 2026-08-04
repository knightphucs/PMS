'use client';

import { ListTodoIcon, PlusIcon } from 'lucide-react';
import { useParams } from 'next/navigation';

import { BacklogTable, BacklogTableSkeleton } from '@/components/backlog/backlog-table';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { useTaskActions } from '@/components/tasks/use-task-actions';
import { Button } from '@/components/ui/button';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useBacklog } from '@/lib/hooks/use-tasks';

/**
 * Backlog = task chưa xếp sprint (`sprintId == null`), chỉ task GỐC, sắp theo độ ưu tiên.
 *
 * Chuyển task vào sprint bằng MENU chứ không phải kéo–thả, có chủ đích:
 *   • `PUT /tasks/{id}/sprint` chỉ diễn đạt được "vào sprint này", không có ngữ nghĩa vị
 *     trí — kéo thả sẽ hứa một thứ tự mà backend không lưu được (cùng lý do bỏ
 *     `@dnd-kit/sortable` ở board).
 *   • Backlog là màn PHÂN LOẠI, không phải sắp xếp không gian: xếp 8 task vào sprint là
 *     8 cú bấm menu so với 8 lần kéo qua một danh sách đang cuộn, và menu chạy được với
 *     bàn phím miễn phí.
 */
export default function BacklogPage() {
  const { id } = useParams<{ id: string }>();
  const backlog = useBacklog(id);
  const { role, myEmployeeId } = useMyProjectRole(id);
  const taskActions = useTaskActions({
    projectId: id,
    role,
    myEmployeeId,
    // Task tạo từ màn này mặc định nằm lại Backlog.
    defaultSprintId: null,
  });

  const createButton = taskActions.canManage ? (
    <Button size="sm" onClick={taskActions.openCreate}>
      <PlusIcon className="size-4" />
      Tạo task
    </Button>
  ) : undefined;

  return (
    <div className="grid gap-4">
      <PageHeader
        title="Backlog"
        count={backlog.data?.length}
        description="Task chưa xếp vào sprint nào. Dùng menu ở cuối dòng để chuyển sang sprint."
        actions={createButton}
      />

      {backlog.isError ? (
        <QueryError
          title="Không tải được backlog"
          error={backlog.error}
          onRetry={() => void backlog.refetch()}
          isRetrying={backlog.isFetching}
        />
      ) : backlog.isPending ? (
        <BacklogTableSkeleton />
      ) : backlog.data.length === 0 ? (
        <EmptyState
          icon={<ListTodoIcon className="size-8" />}
          title="Backlog trống"
          description="Mọi task đều đã được xếp vào sprint. Task mới chưa gán sprint sẽ xuất hiện ở đây."
          action={createButton}
        />
      ) : (
        <BacklogTable
          tasks={backlog.data}
          projectId={id}
          movingIds={taskActions.movingIds}
          renderMenu={taskActions.renderMenu}
        />
      )}

      {taskActions.dialogs}
    </div>
  );
}

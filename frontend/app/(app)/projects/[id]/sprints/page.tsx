'use client';

import { PlusIcon, TimerIcon } from 'lucide-react';
import { useParams } from 'next/navigation';
import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { CompleteSprintDialog } from '@/components/sprints/complete-sprint-dialog';
import { SprintFormDialog } from '@/components/sprints/sprint-form-dialog';
import { SprintList, SprintListSkeleton } from '@/components/sprints/sprint-list';
import { Button } from '@/components/ui/button';
import { errorMessage } from '@/lib/api/problem';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useDeleteSprint, useSprints } from '@/lib/hooks/use-sprints';
import { canManageSprints } from '@/lib/tasks/permissions';
import type { SprintResponse } from '@/types/sprint';

export default function SprintsPage() {
  const { id } = useParams<{ id: string }>();
  const sprints = useSprints(id);
  const { role, isResolving } = useMyProjectRole(id);
  const deleteSprint = useDeleteSprint(id);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<SprintResponse | null>(null);
  const [deleting, setDeleting] = useState<SprintResponse | null>(null);
  const [completing, setCompleting] = useState<SprintResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const canManage = !isResolving && canManageSprints(role);

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const openEdit = (sprint: SprintResponse) => {
    setEditing(sprint);
    setFormOpen(true);
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);

    try {
      await deleteSprint.mutateAsync(deleting.id);
      toast.success(
        deleting.taskCount > 0
          ? `Đã xóa "${deleting.name}". ${deleting.taskCount} task đã quay về Backlog.`
          : `Đã xóa "${deleting.name}".`,
      );
      setDeleting(null);
    } catch (error) {
      setDeleteError(errorMessage(error));
    }
  };

  const createButton = canManage ? (
    <Button size="sm" onClick={openCreate}>
      <PlusIcon className="size-4" />
      Tạo sprint
    </Button>
  ) : undefined;

  return (
    <div className="grid gap-4">
      <PageHeader
        title="Sprint"
        count={sprints.data?.length}
        description="Các chu kỳ làm việc của dự án. Bấm tên sprint để mở bảng của riêng nó."
        actions={createButton}
      />

      {sprints.isError ? (
        <QueryError
          title="Không tải được danh sách sprint"
          error={sprints.error}
          onRetry={() => void sprints.refetch()}
          isRetrying={sprints.isFetching}
        />
      ) : sprints.isPending ? (
        <SprintListSkeleton />
      ) : sprints.data.length === 0 ? (
        <EmptyState
          icon={<TimerIcon className="size-8" />}
          title="Chưa có sprint nào"
          description="Tạo sprint đầu tiên rồi kéo task từ Backlog vào để bắt đầu một chu kỳ làm việc."
          action={createButton}
        />
      ) : (
        <SprintList
          projectId={id}
          sprints={sprints.data}
          canManage={canManage}
          onEdit={openEdit}
          onDelete={setDeleting}
          onComplete={setCompleting}
        />
      )}

      <CompleteSprintDialog
        projectId={id}
        sprint={completing}
        onClose={() => setCompleting(null)}
      />

      <SprintFormDialog
        projectId={id}
        open={formOpen}
        sprint={editing}
        onClose={() => {
          setFormOpen(false);
          setEditing(null);
        }}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Xóa sprint?"
        description={
          <>
            Sprint <strong className="text-foreground">{deleting?.name}</strong> sẽ bị xóa.
            {deleting && deleting.taskCount > 0 ? (
              <>
                {' '}
                {deleting.taskCount} task của sprint này sẽ{' '}
                <strong className="text-foreground">quay về Backlog</strong>, không bị xóa
                theo.
              </>
            ) : null}
          </>
        }
        confirmLabel="Xóa sprint"
        pendingLabel="Đang xóa…"
        error={deleteError}
        isPending={deleteSprint.isPending}
        onConfirm={handleDelete}
        onClose={() => {
          setDeleting(null);
          setDeleteError(null);
        }}
      />
    </div>
  );
}

'use client';

import { PlusIcon, TagsIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { LabelFormDialog } from '@/components/admin/label-form-dialog';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useDeleteLabel, useLabels } from '@/lib/hooks/use-labels';
import type { LabelResponse } from '@/types/label';

/**
 * Nhãn là dữ liệu TOÀN CỤC (ADR-037) — không thuộc project nào. Vì thế màn quản lý nằm ở
 * khu quản trị chứ không trong một project, và xóa một nhãn gỡ chip khỏi board của MỌI dự án.
 */
export default function AdminLabelsPage() {
  const labels = useLabels();
  const deleteLabel = useDeleteLabel();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<LabelResponse | null>(null);
  const [deleting, setDeleting] = useState<LabelResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);

    try {
      await deleteLabel.mutateAsync(deleting.id);
      toast.success(`Đã xóa nhãn "${deleting.name}".`);
      setDeleting(null);
    } catch (error) {
      setDeleteError(errorMessage(error));
    }
  };

  const createButton = (
    <Button size="sm" onClick={openCreate}>
      <PlusIcon className="size-4" />
      Tạo nhãn
    </Button>
  );

  return (
    <div className="grid gap-5">
      <PageHeader
        title="Nhãn toàn cục"
        count={labels.data?.length}
        description="Nhãn dùng chung cho mọi dự án. Sửa hoặc xóa ở đây ảnh hưởng tới tất cả task đang gắn nhãn đó."
        actions={createButton}
      />

      {labels.isError ? (
        <QueryError
          title="Không tải được danh sách nhãn"
          error={labels.error}
          onRetry={() => void labels.refetch()}
          isRetrying={labels.isFetching}
        />
      ) : labels.isPending ? (
        <LabelListSkeleton />
      ) : labels.data.length === 0 ? (
        <EmptyState
          icon={<TagsIcon className="size-8" />}
          title="Chưa có nhãn nào"
          description="Nhãn giúp phân loại task xuyên dự án — ví dụ “bug”, “tài liệu”, “nợ kỹ thuật”."
          action={createButton}
        />
      ) : (
        <ul className="bg-card divide-y rounded-lg border">
          {labels.data.map((label) => (
            <li key={label.id} className="flex flex-wrap items-center gap-3 px-3 py-2.5">
              <span
                aria-hidden
                className="size-4 shrink-0 rounded-full border"
                style={{ backgroundColor: label.color }}
              />
              <span className="min-w-0 flex-1 text-[13px] font-medium break-words">
                {label.name}
              </span>
              <code className="text-muted-foreground text-xs tabular-nums">{label.color}</code>

              <div className="flex gap-1">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setEditing(label);
                    setFormOpen(true);
                  }}
                >
                  Sửa
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-destructive hover:text-destructive"
                  onClick={() => setDeleting(label)}
                >
                  Xóa
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      <LabelFormDialog
        open={formOpen}
        label={editing}
        onClose={() => {
          setFormOpen(false);
          setEditing(null);
        }}
      />

      <ConfirmDialog
        open={deleting !== null}
        title="Xóa nhãn?"
        description={
          <>
            Nhãn <strong className="text-foreground">{deleting?.name}</strong> sẽ bị gỡ khỏi{' '}
            <strong className="text-foreground">mọi task ở mọi dự án</strong> đang gắn nó,
            không chỉ dự án của bạn. Task thì không bị ảnh hưởng gì khác.
          </>
        }
        confirmLabel="Xóa nhãn"
        pendingLabel="Đang xóa…"
        variant="destructive"
        error={deleteError}
        isPending={deleteLabel.isPending}
        onConfirm={handleDelete}
        onClose={() => {
          setDeleting(null);
          setDeleteError(null);
        }}
      />
    </div>
  );
}

function LabelListSkeleton({ rows = 6 }: { rows?: number }) {
  return (
    <div className="bg-card divide-y rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách nhãn…</span>
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex items-center gap-3 px-3 py-2.5">
          <Skeleton className="size-4 shrink-0 rounded-full" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-20" />
        </div>
      ))}
    </div>
  );
}

'use client';

import { MessageSquareIcon, PencilIcon, Trash2Icon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { QueryError } from '@/components/common/query-error';
import { UserAvatar } from '@/components/common/user-avatar';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { errorMessage } from '@/lib/api/problem';
import { formatDateTime, formatRelativeTime } from '@/lib/format';
import {
  useCreateComment,
  useDeleteComment,
  useTaskComments,
  useUpdateComment,
} from '@/lib/hooks/use-comments';
import { canComment, canDeleteComment, canEditComment } from '@/lib/tasks/permissions';
import type { CommentResponse } from '@/types/comment';
import type { RoleInProject } from '@/types/enums';

/** Hằng ở tầng module: đưa vào khóa query nên phải ổn định giữa các lần render. */
const PAGE_SIZE = 20;

export function TaskComments({
  projectId,
  taskId,
  role,
  myEmployeeId,
}: {
  projectId: string;
  taskId: string;
  role: RoleInProject | null;
  myEmployeeId: string | null;
}) {
  const [page, setPage] = useState(1);
  const comments = useTaskComments(projectId, taskId, { page, pageSize: PAGE_SIZE });
  const create = useCreateComment(projectId, taskId);
  const update = useUpdateComment(projectId, taskId);
  const remove = useDeleteComment(projectId, taskId);

  const [draft, setDraft] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState('');
  const [deleting, setDeleting] = useState<CommentResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const mayWrite = canComment(role);

  const submitNew = async () => {
    const content = draft.trim();
    if (!content) return;

    try {
      await create.mutateAsync({ content });
      setDraft('');
      // Comment mới nằm ở trang đầu (backend sắp xếp mới nhất trước) — đang ở trang 3 mà
      // gửi xong thì không thấy gì, trông như thất bại.
      setPage(1);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const submitEdit = async (id: string) => {
    const content = editDraft.trim();
    if (!content) return;

    try {
      await update.mutateAsync({ id, body: { content } });
      setEditingId(null);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);

    try {
      await remove.mutateAsync(deleting.id);
      setDeleting(null);
    } catch (error) {
      setDeleteError(errorMessage(error));
    }
  };

  return (
    <div className="grid gap-3">
      {mayWrite ? (
        <div className="grid gap-2">
          <Textarea
            rows={3}
            value={draft}
            placeholder="Viết bình luận…"
            onChange={(event) => setDraft(event.target.value)}
          />
          <div className="flex justify-end">
            <Button
              size="sm"
              disabled={!draft.trim() || create.isPending}
              onClick={() => void submitNew()}
            >
              {create.isPending ? 'Đang gửi…' : 'Bình luận'}
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-muted-foreground text-sm">
          Vai trò của bạn chỉ đọc được bình luận, không viết được.
        </p>
      )}

      {comments.isError ? (
        <QueryError
          title="Không tải được bình luận"
          error={comments.error}
          onRetry={() => void comments.refetch()}
          isRetrying={comments.isFetching}
        />
      ) : comments.isPending ? (
        <div className="grid gap-3" aria-busy="true">
          <span className="sr-only">Đang tải bình luận…</span>
          {[0, 1].map((index) => (
            <div key={index} className="flex gap-2.5">
              <Skeleton className="size-7 shrink-0 rounded-full" />
              <div className="grid flex-1 gap-1.5">
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3.5 w-full" />
              </div>
            </div>
          ))}
        </div>
      ) : comments.data.items.length === 0 ? (
        <EmptyState
          compact
          icon={<MessageSquareIcon className="size-6" />}
          title="Chưa có bình luận nào"
          description={
            mayWrite
              ? 'Đặt câu hỏi hoặc ghi lại quyết định để cả nhóm cùng thấy.'
              : 'Khi có người thảo luận về task này, bình luận sẽ hiện ở đây.'
          }
        />
      ) : (
        <div className="grid gap-4">
          {comments.data.items.map((comment) => {
            const isAuthor = comment.authorId === myEmployeeId;
            const isEditing = editingId === comment.id;

            return (
              <article key={comment.id} className="flex gap-2.5">
                <UserAvatar id={comment.authorId} name={comment.authorName} size="sm" />

                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                    <span className="text-[13px] font-medium">{comment.authorName}</span>
                    <span
                      className="text-muted-foreground text-xs"
                      title={formatDateTime(comment.createdAt)}
                    >
                      {formatRelativeTime(comment.createdAt)}
                    </span>
                    {comment.updatedAt ? (
                      <span
                        className="text-muted-foreground text-xs"
                        title={formatDateTime(comment.updatedAt)}
                      >
                        · đã sửa
                      </span>
                    ) : null}
                  </div>

                  {isEditing ? (
                    <div className="mt-1.5 grid gap-2">
                      <Textarea
                        autoFocus
                        rows={3}
                        value={editDraft}
                        onChange={(event) => setEditDraft(event.target.value)}
                      />
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          disabled={!editDraft.trim() || update.isPending}
                          onClick={() => void submitEdit(comment.id)}
                        >
                          {update.isPending ? 'Đang lưu…' : 'Lưu'}
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => setEditingId(null)}>
                          Hủy
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <p className="mt-0.5 text-sm leading-relaxed whitespace-pre-wrap">
                      {comment.content}
                    </p>
                  )}
                </div>

                {!isEditing ? (
                  <div className="flex shrink-0 gap-0.5">
                    {/* 🔴 Sửa = CHỈ tác giả — PM cũng không (ADR-026). Đây là chỗ dễ nhầm
                        nhất vì mọi quyền khác PM đều override được. */}
                    {canEditComment(isAuthor) ? (
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label="Sửa bình luận"
                        onClick={() => {
                          setEditingId(comment.id);
                          setEditDraft(comment.content);
                        }}
                      >
                        <PencilIcon className="size-3.5" />
                      </Button>
                    ) : null}

                    {/* Xóa = tác giả HOẶC ProjectManager. */}
                    {canDeleteComment(role, isAuthor) ? (
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label="Xóa bình luận"
                        onClick={() => setDeleting(comment)}
                      >
                        <Trash2Icon className="size-3.5" />
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </article>
            );
          })}

          {comments.data.totalPages > 1 ? (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={!comments.data.hasPreviousPage || comments.isFetching}
                onClick={() => setPage((current) => current - 1)}
              >
                Mới hơn
              </Button>
              <span className="text-muted-foreground text-xs tabular-nums">
                {comments.data.page} / {comments.data.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!comments.data.hasNextPage || comments.isFetching}
                onClick={() => setPage((current) => current + 1)}
              >
                Cũ hơn
              </Button>
            </div>
          ) : null}
        </div>
      )}

      <ConfirmDialog
        open={deleting !== null}
        title="Xóa bình luận?"
        description="Bình luận sẽ bị xóa vĩnh viễn — đây là xóa cứng, không khôi phục được."
        confirmLabel="Xóa bình luận"
        pendingLabel="Đang xóa…"
        error={deleteError}
        isPending={remove.isPending}
        onConfirm={() => void handleDelete()}
        onClose={() => {
          setDeleting(null);
          setDeleteError(null);
        }}
      />
    </div>
  );
}

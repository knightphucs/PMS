'use client';

import { DownloadIcon, PaperclipIcon, Trash2Icon, UploadIcon } from 'lucide-react';
import { useRef, useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { QueryError } from '@/components/common/query-error';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { TaskSection } from '@/components/tasks/task-section';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ApiError, errorMessage } from '@/lib/api/problem';
import { downloadAttachmentToDisk } from '@/lib/attachments/download';
import { formatDateTime, formatFileSize } from '@/lib/format';
import {
  useDeleteAttachment,
  useTaskAttachments,
  useUploadTaskAttachment,
} from '@/lib/hooks/use-attachments';
import { canDeleteAttachment, canUploadAttachment } from '@/lib/tasks/permissions';
import { ATTACHMENT_ACCEPT, ATTACHMENT_MAX_BYTES } from '@/types/attachment';
import type { AttachmentResponse } from '@/types/attachment';
import type { RoleInProject } from '@/types/enums';

/**
 * 🔴 Bốn mã lỗi, bốn thông điệp — không gộp thành "Tải file thất bại".
 *
 * Backend bỏ công phân biệt chúng qua chín bước kiểm tra (ADR-035) chính vì hành động
 * khắc phục của người dùng khác hẳn nhau: đổi tên file, nén nhỏ lại, đổi định dạng, hay
 * đi xin quyền. Gộp lại là vứt toàn bộ công đó đi.
 */
function uploadErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return errorMessage(error);

  switch (error.status) {
    case 413:
      return `File quá lớn. Giới hạn là ${formatFileSize(ATTACHMENT_MAX_BYTES)} — hãy nén lại hoặc chia nhỏ.`;
    case 415:
      return 'Định dạng này không được hỗ trợ. Xem danh sách đuôi file cho phép ngay dưới nút tải lên.';
    case 400:
      // Gồm cả "tên file có ý đồ / đuôi kép" lẫn "nội dung không khớp đuôi đã khai"
      // (bước magic number). Backend đã viết câu cụ thể cho từng trường hợp — hiện nguyên
      // văn thay vì đoán lại xem là trường hợp nào.
      return errorMessage(error);
    case 403:
      return 'Bạn không có quyền tải file lên dự án này. Vai trò Người xem chỉ được tải file về.';
    default:
      return errorMessage(error);
  }
}

export function TaskAttachments({
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
  const attachments = useTaskAttachments(projectId, taskId);
  const upload = useUploadTaskAttachment(projectId, taskId);
  const remove = useDeleteAttachment(projectId, taskId);

  const inputRef = useRef<HTMLInputElement>(null);
  const [deleting, setDeleting] = useState<AttachmentResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const canUpload = canUploadAttachment(role);

  const handleFile = async (file: File) => {
    // Chặn trước ở client cho đúng một trường hợp: kích thước. Nó đo được ngay, và bắn
    // 20MB lên mạng rồi mới nhận 413 là lãng phí thấy được. Mọi phép kiểm còn lại (đuôi,
    // content-type, magic number) đều PHẢI để backend làm — client chỉ nhìn được thứ
    // người gửi tự khai.
    if (file.size > ATTACHMENT_MAX_BYTES) {
      toast.error(
        `"${file.name}" nặng ${formatFileSize(file.size)}, vượt giới hạn ${formatFileSize(ATTACHMENT_MAX_BYTES)}.`,
      );
      return;
    }

    try {
      await upload.mutateAsync(file);
      toast.success(`Đã tải lên "${file.name}".`);
    } catch (error) {
      toast.error(uploadErrorMessage(error));
    }
  };

  const handleDownload = async (attachment: AttachmentResponse) => {
    setDownloadingId(attachment.id);
    try {
      await downloadAttachmentToDisk(attachment.id, attachment.fileName);
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setDownloadingId(null);
    }
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);

    try {
      await remove.mutateAsync(deleting.id);
      toast.success(`Đã xóa "${deleting.fileName}".`);
      setDeleting(null);
    } catch (error) {
      setDeleteError(errorMessage(error));
    }
  };

  const uploadButton = canUpload ? (
    <Button
      variant="ghost"
      size="sm"
      disabled={upload.isPending}
      onClick={() => inputRef.current?.click()}
    >
      <UploadIcon className="size-4" />
      {upload.isPending ? 'Đang tải lên…' : 'Tải file lên'}
    </Button>
  ) : undefined;

  return (
    <>
      <TaskSection
        title="Tệp đính kèm"
        count={attachments.data?.length}
        actions={uploadButton}
      >
        <input
          ref={inputRef}
          type="file"
          className="sr-only"
          accept={ATTACHMENT_ACCEPT}
          onChange={(event) => {
            const file = event.target.files?.[0];
            // Reset value để chọn LẠI đúng file vừa hỏng vẫn kích hoạt `change`.
            event.target.value = '';
            if (file) void handleFile(file);
          }}
        />

        {attachments.isError ? (
          <QueryError
            title="Không tải được danh sách tệp"
            error={attachments.error}
            onRetry={() => void attachments.refetch()}
            isRetrying={attachments.isFetching}
          />
        ) : attachments.isPending ? (
          <div className="grid gap-1.5" aria-busy="true">
            <Skeleton className="h-11" />
            <Skeleton className="h-11" />
          </div>
        ) : attachments.data.length === 0 ? (
          <EmptyState
            compact
            icon={<PaperclipIcon className="size-6" />}
            title="Chưa có tệp nào"
            description={
              canUpload
                ? `Ảnh, PDF, tài liệu — tối đa ${formatFileSize(ATTACHMENT_MAX_BYTES)} mỗi tệp.`
                : 'Vai trò của bạn chỉ tải tệp về được, không tải lên.'
            }
            action={uploadButton}
          />
        ) : (
          <div className="bg-card divide-y rounded-lg border">
            {attachments.data.map((attachment) => (
              <div key={attachment.id} className="flex items-center gap-3 px-3 py-2">
                <PaperclipIcon className="text-muted-foreground size-4 shrink-0" />

                <div className="min-w-0 flex-1">
                  <p className="truncate text-[13px] font-medium">{attachment.fileName}</p>
                  <p className="text-muted-foreground text-xs">
                    {formatFileSize(attachment.sizeBytes)} · {attachment.uploaderName} ·{' '}
                    {formatDateTime(attachment.createdAt)}
                  </p>
                </div>

                {/* Tải VỀ mở cho mọi vai trò kể cả Viewer: đọc đi qua `ProjectAction.View`. */}
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={`Tải "${attachment.fileName}" về`}
                  disabled={downloadingId === attachment.id}
                  onClick={() => void handleDownload(attachment)}
                >
                  <DownloadIcon className="size-4" />
                </Button>

                {canDeleteAttachment(role, attachment.uploaderId === myEmployeeId) ? (
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Xóa "${attachment.fileName}"`}
                    onClick={() => setDeleting(attachment)}
                  >
                    <Trash2Icon className="size-4" />
                  </Button>
                ) : null}
              </div>
            ))}
          </div>
        )}
      </TaskSection>

      <ConfirmDialog
        open={deleting !== null}
        title="Xóa tệp đính kèm?"
        description={
          <>
            <strong className="text-foreground">{deleting?.fileName}</strong> sẽ bị xóa khỏi
            task và khỏi ổ đĩa. Không khôi phục được.
          </>
        }
        confirmLabel="Xóa tệp"
        pendingLabel="Đang xóa…"
        error={deleteError}
        isPending={remove.isPending}
        onConfirm={() => void handleDelete()}
        onClose={() => {
          setDeleting(null);
          setDeleteError(null);
        }}
      />
    </>
  );
}

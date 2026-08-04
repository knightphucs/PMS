'use client';

import { PencilIcon } from 'lucide-react';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { TaskSection } from '@/components/tasks/task-section';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { errorMessage } from '@/lib/api/problem';

const MAX_LENGTH = 4000;

export function TaskDescription({
  description,
  canEdit,
  isBusy,
  onSave,
}: {
  description: string | null;
  canEdit: boolean;
  isBusy: boolean;
  onSave: (value: string | null) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(description ?? '');

  // Người khác vừa sửa (hoặc 409 vừa tải lại) thì bản nháp phải theo — nhưng CHỈ khi không
  // đang gõ dở, nếu không thì một lượt refetch nền sẽ xóa mất chữ người dùng đang viết.
  useEffect(() => {
    if (!editing) setDraft(description ?? '');
  }, [description, editing]);

  const submit = async () => {
    try {
      const trimmed = draft.trim();
      await onSave(trimmed ? trimmed : null);
      setEditing(false);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <TaskSection
      title="Mô tả"
      actions={
        canEdit && !editing ? (
          <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>
            <PencilIcon className="size-4" />
            {description ? 'Sửa' : 'Thêm mô tả'}
          </Button>
        ) : undefined
      }
    >
      {editing ? (
        <div className="grid gap-2">
          <Textarea
            autoFocus
            rows={6}
            maxLength={MAX_LENGTH}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Bối cảnh, tiêu chí hoàn thành, đường dẫn liên quan…"
          />
          <div className="flex items-center gap-2">
            {/* Khóa khi `isBusy` — trong đó có cả lượt tải lại sau 409. Bấm lúc đó là gửi
                lại đúng `rowVersion` đã chết. */}
            <Button size="sm" disabled={isBusy} onClick={() => void submit()}>
              {isBusy ? 'Đang lưu…' : 'Lưu'}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              disabled={isBusy}
              onClick={() => {
                setDraft(description ?? '');
                setEditing(false);
              }}
            >
              Hủy
            </Button>
            <span className="text-muted-foreground ml-auto text-xs tabular-nums">
              {draft.length}/{MAX_LENGTH}
            </span>
          </div>
        </div>
      ) : description ? (
        // `whitespace-pre-wrap`: mô tả là văn bản thuần, xuống dòng của người viết là
        // thông tin. Cố ý KHÔNG render markdown — không có gì làm sạch HTML ở đây.
        //
        // `break-words` là BẮT BUỘC đi kèm: `whitespace-pre-wrap` giữ nguyên mọi token dài,
        // nên một URL dán vào đây sẽ đẩy rộng cả dialog chi tiết Task. Đây là lỗi đã thấy
        // thật, không phải phòng xa.
        <p className="text-sm leading-relaxed break-words whitespace-pre-wrap">{description}</p>
      ) : (
        <p className="text-muted-foreground text-sm">
          {canEdit
            ? 'Chưa có mô tả. Thêm bối cảnh để người nhận việc không phải hỏi lại.'
            : 'Chưa có mô tả.'}
        </p>
      )}
    </TaskSection>
  );
}

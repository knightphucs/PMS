'use client';

import { ArrowUpRightIcon, CornerLeftUpIcon, PencilIcon, XIcon } from 'lucide-react';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { errorMessage } from '@/lib/api/problem';
import type { TaskDetailResponse } from '@/types/task';

export function TaskDetailHeader({
  projectId,
  task,
  variant,
  canEdit,
  isBusy,
  onSaveName,
  onRequestClose,
}: {
  projectId: string;
  task: TaskDetailResponse;
  variant: 'page' | 'modal';
  canEdit: boolean;
  isBusy: boolean;
  onSaveName: (name: string) => Promise<void>;
  onRequestClose?: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(task.name);

  useEffect(() => {
    if (!editing) setDraft(task.name);
  }, [task.name, editing]);

  const submit = async () => {
    const trimmed = draft.trim();
    if (!trimmed || trimmed === task.name) {
      setEditing(false);
      return;
    }

    try {
      await onSaveName(trimmed);
      setEditing(false);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <div className="grid gap-2">
      <div className="text-muted-foreground flex flex-wrap items-center gap-2 text-xs">
        {/* 🔴 Mã do BACKEND ghép sẵn (ADR-034). Đừng nối `projectKey` + `number` ở đây —
            hai nơi định dạng thì chắc chắn có lúc lệch. */}
        <span className="bg-muted text-foreground rounded px-1.5 py-0.5 font-semibold tabular-nums">
          {task.code}
        </span>

        {task.parentTaskId ? (
          <Link
            replace
            href={`/projects/${projectId}/tasks/${task.parentTaskId}`}
            className="hover:text-foreground inline-flex items-center gap-1 underline-offset-4 transition-colors hover:underline"
          >
            <CornerLeftUpIcon className="size-3.5" />
            Mở task cha
          </Link>
        ) : null}

        {/* Trong dialog, người dùng cần một đường thoát ra trang đầy đủ — cùng lý do Jira
            có nút "open in full page" trên mọi modal issue.

            🔴 PHẢI là `<a>` thường, KHÔNG phải `next/link` — đây là bẫy CẤU TRÚC của ADR-043
            chứ không phải sơ suất. Khi dialog đang mở, URL hiện tại ĐÃ ĐÚNG là chuỗi bên
            dưới: intercepting route `(.)` giữ nguyên đường dẫn, và chính điều đó là thứ làm
            cho dialog chia sẻ link được. Hệ quả: soft navigation tới chính URL đang đứng
            không đổi router state, nên `<Link>` ở đây là một NO-OP im lặng (đã từng như vậy
            tới 2026-08-05). Intercepting route CHỈ áp cho soft navigation — chỉ một lần tải
            trang đầy đủ mới render trang thật.

            Kiểm chứng đúng không phải nhìn URL (nó không đổi) mà là: dialog biến mất VÀ thanh
            tab dự án ẩn (`showTabs === false` khi segment là 'tasks').

            📌 Link "Mở task cha" phía trên vẫn là `<Link>` và vẫn đúng: `parentTaskId` là một
            taskId KHÁC nên URL đổi thật, và ở lại trong dialog là hành vi mong muốn. */}
        {variant === 'modal' ? (
          <a
            href={`/projects/${projectId}/tasks/${task.id}`}
            className="hover:text-foreground inline-flex items-center gap-1 underline-offset-4 transition-colors hover:underline"
          >
            <ArrowUpRightIcon className="size-3.5" />
            Mở trang riêng
          </a>
        ) : null}

        <span className="flex-1" />

        {variant === 'modal' && onRequestClose ? (
          <Button variant="ghost" size="icon-sm" aria-label="Đóng" onClick={onRequestClose}>
            <XIcon className="size-4" />
          </Button>
        ) : null}
      </div>

      {editing ? (
        <div className="flex items-start gap-2">
          <Input
            autoFocus
            value={draft}
            maxLength={200}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') void submit();
              if (event.key === 'Escape') {
                setDraft(task.name);
                setEditing(false);
              }
            }}
            className="text-lg font-semibold"
          />
          <Button size="sm" disabled={isBusy} onClick={() => void submit()}>
            {isBusy ? 'Đang lưu…' : 'Lưu'}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            disabled={isBusy}
            onClick={() => {
              setDraft(task.name);
              setEditing(false);
            }}
          >
            Hủy
          </Button>
        </div>
      ) : (
        <div className="flex items-start gap-2">
          {/* `min-w-0` bắt buộc với `flex-1`: flex item mặc định không co nhỏ hơn nội dung,
              nên một tên task dài không dấu cách sẽ đẩy cả hàng (kể cả nút Sửa) ra ngoài. */}
          <h1 className="min-w-0 text-lg leading-snug font-semibold tracking-tight break-words">
            {task.name}
          </h1>
          {canEdit ? (
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label="Sửa tên task"
              onClick={() => setEditing(true)}
            >
              <PencilIcon className="size-4" />
            </Button>
          ) : null}
        </div>
      )}
    </div>
  );
}

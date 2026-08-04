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
            có nút "open in full page" trên mọi modal issue. */}
        {variant === 'modal' ? (
          <Link
            href={`/projects/${projectId}/tasks/${task.id}`}
            className="hover:text-foreground inline-flex items-center gap-1 underline-offset-4 transition-colors hover:underline"
          >
            <ArrowUpRightIcon className="size-3.5" />
            Mở trang riêng
          </Link>
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
          <h1 className="flex-1 text-lg leading-snug font-semibold tracking-tight">
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

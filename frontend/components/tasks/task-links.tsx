'use client';

import { LinkIcon, PlusIcon, XIcon } from 'lucide-react';
import Link from 'next/link';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { QueryError } from '@/components/common/query-error';
import { STATUS_TONE } from '@/components/tasks/status-tone';
import { TaskSection } from '@/components/tasks/task-section';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { FormError } from '@/components/form/form-error';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useCreateTaskLink, useDeleteTaskLink, useTaskLinks } from '@/lib/hooks/use-task-links';
import { useProjectTaskOptions } from '@/lib/hooks/use-tasks';
import { cn } from '@/lib/utils';
import { LINK_TYPE_LABEL, STATUS_LABEL, type LinkType } from '@/types/enums';
import type { TaskLinkResponse } from '@/types/task-link';

/** Thứ tự nhóm khi hiển thị — quan hệ chặn lên trước vì nó ảnh hưởng tới việc làm được. */
const GROUP_ORDER: readonly LinkType[] = ['IsBlockedBy', 'Blocks', 'RelatesTo', 'Duplicates'];

export function TaskLinks({
  projectId,
  taskId,
  canManage,
}: {
  projectId: string;
  taskId: string;
  canManage: boolean;
}) {
  const links = useTaskLinks(projectId, taskId);
  const removeLink = useDeleteTaskLink(projectId);
  const [creating, setCreating] = useState(false);

  const grouped = useMemo(() => {
    const map = new Map<LinkType, TaskLinkResponse[]>();
    for (const link of links.data ?? []) {
      const bucket = map.get(link.linkType);
      if (bucket) bucket.push(link);
      else map.set(link.linkType, [link]);
    }
    return GROUP_ORDER.filter((type) => map.has(type)).map(
      (type) => [type, map.get(type)!] as const,
    );
  }, [links.data]);

  const addButton = canManage ? (
    <Button variant="ghost" size="sm" onClick={() => setCreating(true)}>
      <PlusIcon className="size-4" />
      Thêm liên kết
    </Button>
  ) : undefined;

  return (
    <>
      <TaskSection title="Liên kết" count={links.data?.length} actions={addButton}>
        {links.isError ? (
          <QueryError
            title="Không tải được danh sách liên kết"
            error={links.error}
            onRetry={() => void links.refetch()}
            isRetrying={links.isFetching}
          />
        ) : links.isPending ? (
          <div className="grid gap-1.5" aria-busy="true">
            <Skeleton className="h-9" />
            <Skeleton className="h-9" />
          </div>
        ) : links.data.length === 0 ? (
          <EmptyState
            compact
            icon={<LinkIcon className="size-6" />}
            title="Chưa có liên kết nào"
            description="Nối task này với task khác để thấy được phụ thuộc: đang chặn, bị chặn, liên quan hay trùng lặp."
            action={addButton}
          />
        ) : (
          <div className="grid gap-3">
            {grouped.map(([type, items]) => (
              <div key={type} className="grid gap-1.5">
                <p className="text-muted-foreground text-xs font-medium">
                  {LINK_TYPE_LABEL[type]}
                </p>
                <div className="bg-card divide-y rounded-lg border">
                  {items.map((link) => (
                    <div key={link.id} className="flex items-center gap-2.5 px-3 py-2">
                      <span
                        className={cn(
                          'size-2 shrink-0 rounded-full',
                          STATUS_TONE[link.relatedTaskStatus].dot,
                        )}
                        title={STATUS_LABEL[link.relatedTaskStatus]}
                      />
                      <Link
                        replace
                        href={`/projects/${projectId}/tasks/${link.relatedTaskId}`}
                        className="hover:text-primary flex min-w-0 flex-1 items-center gap-2 text-[13px] underline-offset-4 transition-colors hover:underline"
                      >
                        <span className="text-muted-foreground shrink-0 font-medium tabular-nums">
                          {link.relatedTaskCode}
                        </span>
                        <span className="truncate">{link.relatedTaskName}</span>
                      </Link>

                      {canManage ? (
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          aria-label={`Gỡ liên kết tới ${link.relatedTaskCode}`}
                          disabled={removeLink.isPending}
                          onClick={() => {
                            removeLink.mutate(link.id, {
                              onSuccess: () =>
                                toast.success(`Đã gỡ liên kết tới ${link.relatedTaskCode}.`),
                              onError: (error) => toast.error(errorMessage(error)),
                            });
                          }}
                        >
                          <XIcon className="size-4" />
                        </Button>
                      ) : null}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </TaskSection>

      <CreateLinkDialog
        projectId={projectId}
        taskId={taskId}
        open={creating}
        existing={links.data ?? []}
        onClose={() => setCreating(false)}
      />
    </>
  );
}

function CreateLinkDialog({
  projectId,
  taskId,
  open,
  existing,
  onClose,
}: {
  projectId: string;
  taskId: string;
  open: boolean;
  existing: TaskLinkResponse[];
  onClose: () => void;
}) {
  const options = useProjectTaskOptions(projectId, open);
  const createLink = useCreateTaskLink(projectId, taskId);

  const [linkType, setLinkType] = useState<LinkType>('RelatesTo');
  const [query, setQuery] = useState('');
  const [targetId, setTargetId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const linkedIds = new Set(existing.map((link) => link.relatedTaskId));

  const candidates = (options.data?.items ?? [])
    .filter((task) => task.id !== taskId && !linkedIds.has(task.id))
    .filter((task) => {
      // Lọc phía client — `?search=` bị `TaskRepository` bỏ qua (xem `useProjectTaskOptions`).
      const needle = query.trim().toLowerCase();
      if (!needle) return true;
      return (
        task.name.toLowerCase().includes(needle) || task.code.toLowerCase().includes(needle)
      );
    })
    .slice(0, 30);

  const submit = async () => {
    if (!targetId) return;
    setError(null);

    try {
      await createLink.mutateAsync({ targetTaskId: targetId, linkType });
      toast.success('Đã tạo liên kết.');
      handleClose();
    } catch (err) {
      // 409 có HAI nghĩa khác nhau — "đã có liên kết tương đương" (kể cả khi người dùng
      // chọn chiều ngược lại: `Blocks(A,B)` và `IsBlockedBy(B,A)` là cùng một sự thật,
      // ADR-038) và "tạo ra vòng chặn". Backend đã viết câu riêng cho từng ca; hiện nguyên
      // văn thay vì gộp thành một thông điệp chung vô dụng.
      setError(errorMessage(err));
    }
  };

  const handleClose = () => {
    setLinkType('RelatesTo');
    setQuery('');
    setTargetId(null);
    setError(null);
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && handleClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Thêm liên kết</DialogTitle>
          <DialogDescription>
            Quan hệ được đọc từ phía task đang mở: &quot;Đang chặn&quot; nghĩa là task này
            chặn task kia.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <FormError message={error} />

          <div className="grid gap-2">
            <Label htmlFor="link-type">Loại quan hệ</Label>
            <Select
              value={linkType}
              onValueChange={(value) => value && setLinkType(value as LinkType)}
            >
              <SelectTrigger id="link-type" className="w-full">
                {/* `SelectValue` của Base UI hiện GIÁ TRỊ THÔ — phải truyền hàm định dạng,
                    nếu không ô này hiện "RelatesTo" thay vì "Liên quan tới". */}
                <SelectValue>{(current: LinkType) => LINK_TYPE_LABEL[current]}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {GROUP_ORDER.map((type) => (
                  <SelectItem key={type} value={type}>
                    {LINK_TYPE_LABEL[type]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="link-target">Task liên quan</Label>
            <Input
              id="link-target"
              placeholder="Lọc theo mã hoặc tên…"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />

            <div className="max-h-56 overflow-y-auto rounded-lg border">
              {options.isError ? (
                <p className="text-destructive p-3 text-sm">{errorMessage(options.error)}</p>
              ) : options.isPending ? (
                <div className="grid gap-1 p-2" aria-busy="true">
                  <Skeleton className="h-8" />
                  <Skeleton className="h-8" />
                  <Skeleton className="h-8" />
                </div>
              ) : candidates.length === 0 ? (
                <p className="text-muted-foreground p-3 text-sm">
                  Không có task nào phù hợp.
                </p>
              ) : (
                candidates.map((task) => (
                  <button
                    key={task.id}
                    type="button"
                    onClick={() => setTargetId(task.id)}
                    className={cn(
                      'flex w-full items-center gap-2 px-3 py-2 text-left text-[13px] transition-colors',
                      targetId === task.id ? 'bg-primary/10 text-primary' : 'hover:bg-accent',
                    )}
                  >
                    <span className="text-muted-foreground shrink-0 font-medium tabular-nums">
                      {task.code}
                    </span>
                    <span className="truncate">{task.name}</span>
                  </button>
                ))
              )}
            </div>
          </div>
        </div>

        <DialogFooter className="mt-2">
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button disabled={!targetId || createLink.isPending} onClick={() => void submit()}>
            {createLink.isPending ? 'Đang tạo…' : 'Tạo liên kết'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

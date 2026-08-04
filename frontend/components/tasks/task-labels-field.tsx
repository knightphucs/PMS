'use client';

import { PlusIcon, XIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useAttachLabel, useDetachLabel, useLabels } from '@/lib/hooks/use-labels';
import { cn } from '@/lib/utils';
import type { LabelResponse } from '@/types/label';

/**
 * Chip nhãn có màu do người dùng chọn (`#RRGGBB`), nên nền là màu tùy ý còn chữ phải tự
 * chọn đen/trắng theo độ sáng — nếu không thì nhãn vàng sẽ có chữ trắng trên nền vàng.
 * Dùng công thức luminance tương đối của WCAG chứ không phải trung bình RGB.
 */
function readableTextColor(hex: string): string {
  const value = hex.replace('#', '');
  if (value.length !== 6) return '#000000';

  const channel = (start: number) => {
    const srgb = parseInt(value.slice(start, start + 2), 16) / 255;
    return srgb <= 0.03928 ? srgb / 12.92 : ((srgb + 0.055) / 1.055) ** 2.4;
  };

  const luminance = 0.2126 * channel(0) + 0.7152 * channel(2) + 0.0722 * channel(4);
  return luminance > 0.45 ? '#111827' : '#ffffff';
}

export function LabelChip({
  label,
  onRemove,
}: {
  label: LabelResponse;
  onRemove?: () => void;
}) {
  return (
    <span
      className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium"
      style={{ backgroundColor: label.color, color: readableTextColor(label.color) }}
    >
      {label.name}
      {onRemove ? (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Gỡ nhãn ${label.name}`}
          className="opacity-70 transition-opacity hover:opacity-100"
        >
          <XIcon className="size-3" />
        </button>
      ) : null}
    </span>
  );
}

export function TaskLabelsField({
  projectId,
  taskId,
  labels,
  canEdit,
}: {
  projectId: string;
  taskId: string;
  labels: LabelResponse[];
  canEdit: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');

  const allLabels = useLabels();
  const attach = useAttachLabel(projectId);
  const detach = useDetachLabel(projectId);

  const attachedIds = new Set(labels.map((label) => label.id));
  const candidates = (allLabels.data ?? []).filter((label) => {
    const needle = query.trim().toLowerCase();
    return !needle || label.name.toLowerCase().includes(needle);
  });

  const toggle = (label: LabelResponse) => {
    const isAttached = attachedIds.has(label.id);
    const mutation = isAttached ? detach : attach;

    // Cả gắn lẫn gỡ đều idempotent ở backend, nên không cần dò trạng thái trước khi gọi.
    mutation.mutate(
      { taskId, labelId: label.id },
      { onError: (error) => toast.error(errorMessage(error)) },
    );
  };

  return (
    <div className="flex flex-wrap items-center gap-1">
      {labels.length === 0 && !canEdit ? (
        <span className="text-muted-foreground text-sm">—</span>
      ) : null}

      {labels.map((label) => (
        <LabelChip
          key={label.id}
          label={label}
          onRemove={canEdit ? () => toggle(label) : undefined}
        />
      ))}

      {canEdit ? (
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger
            render={
              <Button variant="ghost" size="icon-sm" aria-label="Gắn nhãn">
                <PlusIcon className="size-3.5" />
              </Button>
            }
          />
          <PopoverContent align="start" className="w-60 p-2">
            <Input
              autoFocus
              placeholder="Tìm nhãn…"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              className="mb-2 h-8"
            />

            {allLabels.isError ? (
              <p className="text-destructive px-1 py-2 text-sm">
                {errorMessage(allLabels.error)}
              </p>
            ) : allLabels.isPending ? (
              <div className="grid gap-1" aria-busy="true">
                <Skeleton className="h-7" />
                <Skeleton className="h-7" />
              </div>
            ) : candidates.length === 0 ? (
              <p className="text-muted-foreground px-1 py-2 text-sm">
                {query.trim()
                  ? 'Không có nhãn nào khớp.'
                  : 'Chưa có nhãn nào trong hệ thống.'}
              </p>
            ) : (
              <div className="max-h-56 overflow-y-auto">
                {candidates.map((label) => (
                  <button
                    key={label.id}
                    type="button"
                    onClick={() => toggle(label)}
                    className={cn(
                      'hover:bg-accent flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm transition-colors',
                      attachedIds.has(label.id) && 'bg-accent/50',
                    )}
                  >
                    <span
                      className="size-3 shrink-0 rounded-full"
                      style={{ backgroundColor: label.color }}
                    />
                    <span className="flex-1 truncate">{label.name}</span>
                    {attachedIds.has(label.id) ? (
                      <XIcon className="text-muted-foreground size-3.5" />
                    ) : null}
                  </button>
                ))}
              </div>
            )}
          </PopoverContent>
        </Popover>
      ) : null}
    </div>
  );
}

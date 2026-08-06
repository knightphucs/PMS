'use client';

import { useState } from 'react';
import { toast } from 'sonner';

import { FormError } from '@/components/form/form-error';
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
import { formatDate } from '@/lib/format';
import { useCompleteSprint, useSprintCompletionPreview } from '@/lib/hooks/use-sprints';
import type { SprintResponse } from '@/types/sprint';

/** Giá trị cho ô chọn khi người dùng muốn đẩy task về Backlog. */
const BACKLOG = '__backlog__';

/**
 * Đóng sprint — **hỏi task chưa xong đi đâu, không tự quyết hộ** (ADR-050).
 *
 * 🔴 Đây là toàn bộ lý do tồn tại của dialog này. Hai phương án tự động đều đã bị loại có
 * lý do ghi trong ADR: đẩy hết về Backlog bắt đội chạy sprint liên tiếp kéo lại từng task
 * bằng tay; im lặng dồn sang sprint kế chính là cách làm sprint đó vỡ kế hoạch. Cả hai
 * quyết hộ một thứ mà **chỉ người đóng sprint mới biết**.
 */
export function CompleteSprintDialog({
  projectId,
  sprint,
  onClose,
}: {
  projectId: string;
  sprint: SprintResponse | null;
  onClose: () => void;
}) {
  const preview = useSprintCompletionPreview(projectId, sprint?.id ?? null);
  const complete = useCompleteSprint(projectId);

  const [target, setTarget] = useState<string>(BACKLOG);
  const [error, setError] = useState<string | null>(null);

  const unfinished = preview.data?.unfinishedCount ?? 0;

  const confirm = async () => {
    if (!sprint) return;
    setError(null);

    try {
      await complete.mutateAsync({
        sprintId: sprint.id,
        // `null` = Backlog, và đó là một lựa chọn hợp lệ chứ không phải "chưa chọn".
        body: { targetSprintId: target === BACKLOG ? null : target },
      });

      toast.success(
        unfinished > 0
          ? `Đã đóng "${sprint.name}". ${unfinished} task chưa xong đã chuyển đi.`
          : `Đã đóng "${sprint.name}".`,
      );
      onClose();
    } catch (caught) {
      // Giữ dialog mở: 409 "sprint chưa bắt đầu" và 400 "sprint đích đã đóng" đều có câu
      // tiếng Việt riêng và đều sửa được ngay tại chỗ.
      setError(errorMessage(caught));
    }
  };

  return (
    <Dialog
      open={sprint !== null}
      onOpenChange={(next) => {
        if (!next) {
          setTarget(BACKLOG);
          setError(null);
          onClose();
        }
      }}
    >
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Đóng sprint &ldquo;{sprint?.name}&rdquo;?</DialogTitle>
          <DialogDescription>
            Sprint đã đóng thì không mở lại được. Task đã hoàn thành ở lại sprint này — đó là
            thứ các báo cáo tốc độ đếm.
          </DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        {preview.isPending ? (
          <Skeleton className="h-16 w-full" />
        ) : preview.isError ? (
          <p className="text-destructive text-sm">{errorMessage(preview.error)}</p>
        ) : (
          <div className="grid gap-3">
            <div className="bg-muted/40 grid gap-1 rounded-lg p-3 text-sm">
              <span>
                <b className="tabular-nums">{preview.data.doneCount}</b> task đã hoàn thành
              </span>
              <span>
                <b className="tabular-nums">{unfinished}</b> task chưa xong
              </span>
            </div>

            {unfinished > 0 ? (
              <div className="grid gap-2">
                <Label htmlFor="sprint-target">Chuyển task chưa xong sang</Label>
                <Select value={target} onValueChange={(v) => setTarget(v ?? BACKLOG)}>
                  <SelectTrigger id="sprint-target" className="w-full">
                    {/* `SelectValue` của Base UI hiện giá trị thô — phải tự định dạng. */}
                    <SelectValue>
                      {(current: string) =>
                        current === BACKLOG
                          ? 'Backlog'
                          : (preview.data.availableTargets.find((s) => s.id === current)?.name ??
                            'Backlog')
                      }
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={BACKLOG}>Backlog</SelectItem>
                    {preview.data.availableTargets.map((option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.name} · {formatDate(option.startDate)} –{' '}
                        {formatDate(option.endDate)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                {/* Nói rõ khi không còn lựa chọn nào ngoài Backlog, thay vì để người dùng mở
                    ô chọn ra và tự đoán vì sao nó trống. */}
                {preview.data.availableTargets.length === 0 ? (
                  <p className="text-muted-foreground text-xs">
                    Chưa có sprint nào khác đang mở, nên task chỉ đi về Backlog được.
                  </p>
                ) : null}
              </div>
            ) : null}
          </div>
        )}

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button
            type="button"
            disabled={complete.isPending || preview.isPending}
            onClick={() => void confirm()}
          >
            {complete.isPending ? 'Đang đóng…' : 'Đóng sprint'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

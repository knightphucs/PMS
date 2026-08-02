'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Field } from '@/components/form/field';
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
import { errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useCreateSprint, useUpdateSprint } from '@/lib/hooks/use-sprints';
import { toDateInputValue } from '@/lib/validation/project-schema';
import { sprintSchema, type SprintValues } from '@/lib/validation/sprint-schema';
import type { SprintResponse } from '@/types/sprint';

/** ⚠️ Phải khớp ĐÚNG tên property của `CreateSprintRequest`. */
const FIELDS = ['name', 'goal', 'startDate', 'endDate'] as const;

/** `<input type="date">` cho `yyyy-MM-dd`; backend nhận ISO đầy đủ. */
const toIso = (value: string) => `${value}T00:00:00Z`;

function todayPlus(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return toDateInputValue(date.toISOString());
}

/**
 * Một dialog dùng cho cả tạo lẫn sửa — `sprint === null` là chế độ tạo.
 *
 * Gộp được vì khác hẳn Project/Task: sprint KHÔNG có `rowVersion`, nên form sửa không
 * phải nạp chi tiết trước để lấy token, không có luồng 409, không có cảnh báo "dữ liệu
 * đã cũ". Toàn bộ dữ liệu cần thiết đã nằm trong danh sách sprint.
 */
export function SprintFormDialog({
  projectId,
  open,
  sprint,
  onClose,
}: {
  projectId: string;
  open: boolean;
  sprint: SprintResponse | null;
  onClose: () => void;
}) {
  const isEdit = sprint !== null;
  const [formError, setFormError] = useState<string | null>(null);
  const createSprint = useCreateSprint(projectId);
  const updateSprint = useUpdateSprint(projectId, sprint?.id ?? '');

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SprintValues>({
    resolver: zodResolver(sprintSchema),
    defaultValues: { name: '', goal: '', startDate: '', endDate: '' },
  });

  useEffect(() => {
    if (!open) return;

    reset(
      sprint
        ? {
            name: sprint.name,
            goal: sprint.goal,
            startDate: toDateInputValue(sprint.startDate),
            endDate: toDateInputValue(sprint.endDate),
          }
        : // Sprint hai tuần bắt đầu hôm nay — mặc định của đại đa số nhóm Scrum.
          { name: '', goal: '', startDate: todayPlus(0), endDate: todayPlus(14) },
    );
  }, [open, sprint, reset]);

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);
      const body = {
        name: values.name,
        goal: values.goal,
        startDate: toIso(values.startDate),
        endDate: toIso(values.endDate),
      };

      try {
        if (isEdit) {
          await updateSprint.mutateAsync(body);
          toast.success(`Đã cập nhật "${values.name}".`);
        } else {
          await createSprint.mutateAsync(body);
          toast.success(`Đã tạo sprint "${values.name}".`);
        }
        handleOpenChange(false);
      } catch (error) {
        if (!applyServerErrors(error, setError, FIELDS)) {
          setFormError(errorMessage(error));
        }
      }
    },
    // Xóa lỗi máy chủ cũ khi validate client hỏng, nếu không hai thông báo mâu thuẫn
    // cùng hiện.
    () => setFormError(null),
  );

  const handleOpenChange = (next: boolean) => {
    if (next) return;
    reset();
    setFormError(null);
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa sprint' : 'Tạo sprint'}</DialogTitle>
          <DialogDescription>
            Sprint là một chu kỳ làm việc có thời hạn. Task chưa gán sprint nào thì nằm ở
            Backlog.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Tên sprint"
            autoFocus
            placeholder="Sprint 1"
            error={errors.name?.message}
            {...register('name')}
          />

          <div className="grid gap-2">
            <Label htmlFor="sprint-goal">Mục tiêu</Label>
            <textarea
              id="sprint-goal"
              rows={2}
              placeholder="Nhóm muốn đạt được gì trong sprint này?"
              aria-invalid={errors.goal ? true : undefined}
              className="border-input placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:border-destructive w-full resize-y rounded-lg border bg-transparent px-3 py-2 text-sm outline-none focus-visible:ring-3"
              {...register('goal')}
            />
            {errors.goal ? (
              <p role="alert" className="text-destructive text-sm">
                {errors.goal.message}
              </p>
            ) : null}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Bắt đầu"
              type="date"
              error={errors.startDate?.message}
              {...register('startDate')}
            />
            <Field
              label="Kết thúc"
              type="date"
              error={errors.endDate?.message}
              {...register('endDate')}
            />
          </div>

          <DialogFooter className="mt-2">
            <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Đang lưu…' : isEdit ? 'Lưu' : 'Tạo sprint'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

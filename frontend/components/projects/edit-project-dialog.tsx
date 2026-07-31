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
import { Skeleton } from '@/components/ui/skeleton';
import { ApiError, errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useProject, useUpdateProject } from '@/lib/hooks/use-projects';
import {
  toDateInputValue,
  todayIsoDate,
  updateProjectSchema,
  type UpdateProjectValues,
} from '@/lib/validation/project-schema';
import type { ProjectSummaryResponse } from '@/types/project';

const FIELDS = ['name', 'description', 'expectedCompletionDate'] as const;

interface Props {
  project: ProjectSummaryResponse | null;
  onClose: () => void;
}

/**
 * Sửa project — màn hình duy nhất hiện nay đi qua trọn vẹn luồng optimistic concurrency
 * của ADR-016.
 *
 * Ba bước bắt buộc, bỏ bước nào cũng hỏng:
 * 1. Nạp CHI TIẾT khi mở dialog. Danh sách không có `rowVersion` lẫn `description`, và
 *    quan trọng hơn là `rowVersion` phải mới — nạp lúc mở chứ không phải lúc dựng bảng.
 * 2. Gửi lại `rowVersion` nguyên vẹn khi PUT.
 * 3. Nhận 409 thì **tải lại rồi để người dùng quyết định**, tuyệt đối không tự động gửi
 *    lại — làm vậy là ghi đè thay đổi của người khác, đúng cái mà `RowVersion` sinh ra
 *    để chặn.
 */
export function EditProjectDialog({ project, onClose }: Props) {
  const open = project !== null;
  const detail = useProject(project?.id ?? null);
  const updateProject = useUpdateProject(project?.id ?? '');
  const [formError, setFormError] = useState<string | null>(null);
  const [staleWarning, setStaleWarning] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UpdateProjectValues>({
    resolver: zodResolver(updateProjectSchema),
    defaultValues: { name: '', description: '', expectedCompletionDate: '' },
  });

  // Đổ dữ liệu vào form khi chi tiết về (và khi nạp LẠI sau 409).
  useEffect(() => {
    if (!detail.data) return;
    reset({
      name: detail.data.name,
      description: detail.data.description,
      expectedCompletionDate: toDateInputValue(detail.data.expectedCompletionDate),
    });
  }, [detail.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!detail.data) return;
    setFormError(null);

    try {
      await updateProject.mutateAsync({
        name: values.name,
        description: values.description,
        expectedCompletionDate: `${values.expectedCompletionDate}T00:00:00Z`,
        // Token của lần GET gần nhất, không phải của lần dựng bảng.
        rowVersion: detail.data.rowVersion,
      });

      toast.success(`Đã cập nhật "${values.name}".`);
      handleOpenChange(false);
    } catch (error) {
      if (error instanceof ApiError && error.isConflict) {
        // 409 = có người khác sửa project này trong lúc form đang mở. Nạp lại để lấy
        // rowVersion mới; useEffect ở trên sẽ ghi đè form bằng dữ liệu mới nhất.
        setStaleWarning(true);
        setFormError(null);
        await detail.refetch();
        return;
      }

      setStaleWarning(false);
      if (!applyServerErrors(error, setError, FIELDS)) {
        setFormError(errorMessage(error));
      }
    }
  });

  const handleOpenChange = (next: boolean) => {
    if (next) return;
    reset();
    setFormError(null);
    setStaleWarning(false);
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Sửa dự án</DialogTitle>
          <DialogDescription>
            Thay đổi được lưu ngay khi bấm Lưu.
          </DialogDescription>
        </DialogHeader>

        {detail.isPending ? (
          <div className="grid gap-4" aria-busy="true">
            <span className="sr-only">Đang tải thông tin dự án…</span>
            <Skeleton className="h-16" />
            <Skeleton className="h-24" />
            <Skeleton className="h-16" />
          </div>
        ) : detail.isError ? (
          <div className="grid gap-4">
            <FormError message={errorMessage(detail.error)} />
            <Button variant="outline" onClick={() => void detail.refetch()}>
              Thử lại
            </Button>
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            {staleWarning ? (
              <div
                role="alert"
                className="rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200"
              >
                <p className="font-medium">Người khác vừa sửa dự án này.</p>
                <p className="mt-1">
                  Biểu mẫu đã được tải lại theo dữ liệu mới nhất. Kiểm tra lại rồi bấm Lưu
                  nếu bạn vẫn muốn áp dụng thay đổi của mình.
                </p>
              </div>
            ) : null}

            <FormError message={formError} />

            <Field
              label="Tên dự án"
              autoFocus
              error={errors.name?.message}
              {...register('name')}
            />

            <div className="grid gap-2">
              <Label htmlFor="edit-project-description">Mô tả</Label>
              <textarea
                id="edit-project-description"
                rows={3}
                aria-invalid={errors.description ? true : undefined}
                className="border-input placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:border-destructive w-full resize-y rounded-lg border bg-transparent px-3 py-2 text-sm outline-none focus-visible:ring-3"
                {...register('description')}
              />
              {errors.description ? (
                <p role="alert" className="text-destructive text-sm">
                  {errors.description.message}
                </p>
              ) : null}
            </div>

            <Field
              label="Dự kiến hoàn thành"
              type="date"
              min={todayIsoDate()}
              error={errors.expectedCompletionDate?.message}
              {...register('expectedCompletionDate')}
            />

            <DialogFooter className="mt-2">
              <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
              <Button type="submit" disabled={isSubmitting || detail.isFetching}>
                {isSubmitting ? 'Đang lưu…' : 'Lưu'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

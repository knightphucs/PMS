'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { PlusIcon } from 'lucide-react';
import { useState } from 'react';
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
  DialogTrigger,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useCreateProject } from '@/lib/hooks/use-projects';
import {
  createProjectSchema,
  todayIsoDate,
  type CreateProjectValues,
} from '@/lib/validation/project-schema';

const FIELDS = ['name', 'description', 'expectedCompletionDate'] as const;

export function CreateProjectDialog() {
  const [open, setOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const createProject = useCreateProject();

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CreateProjectValues>({
    resolver: zodResolver(createProjectSchema),
    defaultValues: { name: '', description: '', expectedCompletionDate: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await createProject.mutateAsync({
        name: values.name,
        // ⚠️ Gửi chuỗi rỗng chứ KHÔNG gửi null: `ProjectService.UpdateAsync` gọi
        // `.Trim()` trên trường này nên null sẽ thành lỗi 500 chứ không phải lỗi validate.
        description: values.description,
        // `<input type="date">` cho "2026-12-31"; backend nhận DateTime nên gửi kèm giờ.
        expectedCompletionDate: `${values.expectedCompletionDate}T00:00:00Z`,
      });

      toast.success(`Đã tạo dự án "${values.name}".`);
      reset();
      setOpen(false);
    } catch (error) {
      if (!applyServerErrors(error, setError, FIELDS)) {
        setFormError(errorMessage(error));
      }
    }
  });

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    // Đóng rồi mở lại phải là form sạch, không còn lỗi của lần trước.
    if (!next) {
      reset();
      setFormError(null);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger
        render={
          <Button>
            <PlusIcon className="size-4" />
            Tạo dự án
          </Button>
        }
      />

      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Tạo dự án mới</DialogTitle>
          <DialogDescription>
            Bạn sẽ tự động trở thành quản lý của dự án này.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Tên dự án"
            autoFocus
            placeholder="Hệ thống quản lý kho"
            error={errors.name?.message}
            {...register('name')}
          />

          <div className="grid gap-2">
            <Label htmlFor="project-description">Mô tả</Label>
            <textarea
              id="project-description"
              rows={3}
              placeholder="Mục tiêu và phạm vi của dự án"
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
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Đang tạo…' : 'Tạo dự án'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

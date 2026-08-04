'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { WarningBanner } from '@/components/common/warning-banner';
import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { PriorityLabel } from '@/components/tasks/priority-icon';
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
import { Textarea } from '@/components/ui/textarea';
import { ApiError, errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useCreateTask, useTask, useUpdateTask } from '@/lib/hooks/use-tasks';
import { useSprints } from '@/lib/hooks/use-sprints';
import { toDateInputValue } from '@/lib/validation/project-schema';
import {
  taskSchema,
  toNullableId,
  toNullableIso,
  toNullableText,
  type TaskValues,
} from '@/lib/validation/task-schema';
import { PRIORITY_ORDER, type Priority } from '@/types/enums';

/** ⚠️ Khớp ĐÚNG tên property của Create/UpdateTaskRequest. */
const FIELDS = ['name', 'priority', 'dueDate', 'sprintId', 'description'] as const;

/** `Select` không nhận chuỗi rỗng làm value — dùng token này cho "Backlog". */
const BACKLOG = 'backlog';

interface Props {
  projectId: string;
  open: boolean;
  /** `null` = chế độ tạo. */
  taskId: string | null;
  /** Sprint mặc định khi tạo mới (đang mở board của sprint nào thì gợi ý sprint đó). */
  defaultSprintId?: string | null;
  /**
   * Có giá trị = đang tạo **subtask** của task đó. Chỉ có nghĩa ở chế độ tạo.
   *
   * Mặc định `null` nên mọi nơi gọi cũ giữ nguyên hành vi từng byte — `CreateTaskRequest`
   * vốn đã luôn gửi `parentTaskId: null`.
   */
  parentTaskId?: string | null;
  onClose: () => void;
}

/**
 * Tạo / sửa task.
 *
 * Chế độ SỬA đi trọn vẹn luồng optimistic concurrency của ADR-016 — giống hệt
 * `EditProjectDialog`, và vì cùng một lý do. Ba bước, bỏ bước nào cũng hỏng:
 *   1. Nạp CHI TIẾT khi mở dialog (`useTask` có `staleTime: 0`) — `rowVersion` phải mới.
 *   2. Gửi lại `rowVersion` nguyên vẹn khi PUT.
 *   3. Nhận 409 thì TẢI LẠI rồi để người dùng quyết định. Tuyệt đối không tự gửi lại —
 *      làm vậy là ghi đè thay đổi của người khác, đúng cái mà `RowVersion` sinh ra để chặn.
 */
export function TaskFormDialog({
  projectId,
  open,
  taskId,
  defaultSprintId = null,
  parentTaskId = null,
  onClose,
}: Props) {
  const isEdit = taskId !== null;
  const isSubtask = !isEdit && parentTaskId !== null;
  const detail = useTask(projectId, open && isEdit ? taskId : null);
  const sprints = useSprints(open ? projectId : null);
  const createTask = useCreateTask(projectId);
  const updateTask = useUpdateTask(projectId, taskId ?? '');

  const [formError, setFormError] = useState<string | null>(null);
  const [staleWarning, setStaleWarning] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<TaskValues>({
    resolver: zodResolver(taskSchema),
    defaultValues: { name: '', priority: 'Medium', dueDate: '', sprintId: '', description: '' },
  });

  const priority = watch('priority');
  const sprintId = watch('sprintId');

  // Đổ dữ liệu vào form khi chi tiết về — VÀ khi nạp LẠI sau 409.
  useEffect(() => {
    if (!open) return;

    if (!isEdit) {
      reset({
        name: '',
        priority: 'Medium',
        dueDate: '',
        sprintId: defaultSprintId ?? '',
        description: '',
      });
      return;
    }

    if (!detail.data) return;
    reset({
      name: detail.data.name,
      priority: detail.data.priority,
      dueDate: detail.data.dueDate ? toDateInputValue(detail.data.dueDate) : '',
      sprintId: detail.data.sprintId ?? '',
      // 🔴 Mang mô tả hiện tại vào form. Bỏ dòng này là PUT gửi `description: undefined`,
      // backend bind `null` và mô tả bị xóa — xem chú thích ở `taskSchema.description`.
      description: detail.data.description ?? '',
    });
  }, [open, isEdit, detail.data, defaultSprintId, reset]);

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);

      try {
        if (isEdit) {
          if (!detail.data) return;
          await updateTask.mutateAsync({
            name: values.name,
            priority: values.priority,
            dueDate: toNullableIso(values.dueDate),
            // 🔴 `PUT /tasks/{id}` GHI ĐÈ TOÀN PHẦN — mọi trường không gửi đều thành `null`.
            // Đây không phải PATCH.
            description: toNullableText(values.description),
            // Token của lần GET GẦN NHẤT, không phải của lúc dựng bảng.
            rowVersion: detail.data.rowVersion,
          });
          toast.success(`Đã cập nhật "${values.name}".`);
        } else {
          await createTask.mutateAsync({
            name: values.name,
            projectId,
            // Subtask luôn vào Backlog: `TaskRepository` lọc board/backlog theo
            // `ParentTaskId == null` nên sprint của subtask là giá trị không ai nhìn thấy.
            sprintId: isSubtask ? null : toNullableId(values.sprintId),
            parentTaskId,
            dueDate: toNullableIso(values.dueDate),
            priority: values.priority,
            description: toNullableText(values.description),
          });
          toast.success(
            isSubtask ? `Đã tạo subtask "${values.name}".` : `Đã tạo task "${values.name}".`,
          );
        }
        handleOpenChange(false);
      } catch (error) {
        if (isEdit && error instanceof ApiError && error.isConflict) {
          // 409 = có người khác sửa task này trong lúc form đang mở. Nạp lại để lấy
          // `rowVersion` mới; useEffect ở trên sẽ ghi đè form bằng dữ liệu mới nhất.
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
    },
    () => setFormError(null),
  );

  const handleOpenChange = (next: boolean) => {
    if (next) return;
    reset();
    setFormError(null);
    setStaleWarning(false);
    onClose();
  };

  const loadingDetail = isEdit && detail.isPending;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>
            {isEdit ? 'Sửa task' : isSubtask ? 'Tạo subtask' : 'Tạo task'}
          </DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Trạng thái đổi bằng cách kéo thẻ trên bảng, không sửa ở đây.'
              : isSubtask
                ? 'Subtask luôn bắt đầu ở trạng thái Cần làm và thuộc về task cha, không nằm trên bảng.'
                : 'Task mới luôn bắt đầu ở trạng thái Cần làm.'}
          </DialogDescription>
        </DialogHeader>

        {loadingDetail ? (
          <div className="grid gap-4" aria-busy="true">
            <span className="sr-only">Đang tải thông tin task…</span>
            <Skeleton className="h-16" />
            <Skeleton className="h-16" />
            <Skeleton className="h-16" />
          </div>
        ) : isEdit && detail.isError ? (
          <div className="grid gap-4">
            <FormError message={errorMessage(detail.error)} />
            <Button variant="outline" onClick={() => void detail.refetch()}>
              Thử lại
            </Button>
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            {staleWarning ? (
              <WarningBanner title="Người khác vừa sửa task này.">
                Biểu mẫu đã được tải lại theo dữ liệu mới nhất. Kiểm tra lại rồi bấm Lưu
                nếu bạn vẫn muốn áp dụng thay đổi của mình.
              </WarningBanner>
            ) : null}

            <FormError message={formError} />

            <Field
              label="Tên task"
              autoFocus
              placeholder="Việc cần làm là gì?"
              error={errors.name?.message}
              {...register('name')}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="grid gap-2">
                <Label htmlFor="task-priority">Độ ưu tiên</Label>
                <Select
                  value={priority}
                  onValueChange={(value) => setValue('priority', value as Priority)}
                >
                  <SelectTrigger id="task-priority" className="w-full">
                    <SelectValue>
                      {(current: Priority) => <PriorityLabel priority={current} />}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {PRIORITY_ORDER.map((item) => (
                      <SelectItem key={item} value={item}>
                        <PriorityLabel priority={item} />
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <Field
                label="Hạn hoàn thành"
                type="date"
                // Backend CHO PHÉP hạn ở quá khứ (task trễ là chuyện có thật), nên
                // KHÔNG đặt `min` như ở form dự án.
                error={errors.dueDate?.message}
                {...register('dueDate')}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="task-description">Mô tả</Label>
              <Textarea
                id="task-description"
                rows={4}
                placeholder="Bối cảnh, tiêu chí hoàn thành, đường dẫn liên quan…"
                aria-invalid={errors.description ? true : undefined}
                aria-describedby={errors.description ? 'task-description-error' : undefined}
                {...register('description')}
              />
              {errors.description ? (
                <p id="task-description-error" role="alert" className="text-destructive text-sm">
                  {errors.description.message}
                </p>
              ) : null}
            </div>

            {/* Chuyển sprint khi SỬA đi qua `PUT /tasks/{id}/sprint` (endpoint riêng,
                không cần rowVersion) chứ không qua `PUT /tasks/{id}` — nên ô này chỉ
                hiện ở chế độ tạo. Subtask cũng không có ô này: nó không lên board. */}
            {!isEdit && !isSubtask ? (
              <div className="grid gap-2">
                <Label htmlFor="task-sprint">Sprint</Label>
                <Select
                  value={sprintId || BACKLOG}
                  onValueChange={(value) =>
                    // `onValueChange` của Base UI có thể trả `null` khi bỏ chọn — cả hai
                    // trường hợp đều nghĩa là Backlog.
                    setValue('sprintId', !value || value === BACKLOG ? '' : value)
                  }
                >
                  <SelectTrigger id="task-sprint" className="w-full">
                    <SelectValue>
                      {(current: string) =>
                        current === BACKLOG
                          ? 'Backlog (chưa xếp sprint)'
                          : ((sprints.data ?? []).find((s) => s.id === current)?.name ??
                            'Backlog (chưa xếp sprint)')
                      }
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={BACKLOG}>Backlog (chưa xếp sprint)</SelectItem>
                    {(sprints.data ?? []).map((sprint) => (
                      <SelectItem key={sprint.id} value={sprint.id}>
                        {sprint.name}
                        {sprint.isActive ? ' • đang diễn ra' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}

            <DialogFooter className="mt-2">
              <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
              <Button type="submit" disabled={isSubmitting || detail.isFetching}>
                {isSubmitting ? 'Đang lưu…' : isEdit ? 'Lưu' : 'Tạo task'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

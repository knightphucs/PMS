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
import { ApiError, errorMessage } from '@/lib/api/problem';
import { useDeleteProject } from '@/lib/hooks/use-projects';
import type { ProjectSummaryResponse } from '@/types/project';

interface Props {
  project: ProjectSummaryResponse | null;
  onClose: () => void;
}

/**
 * Xóa project.
 *
 * Backend trả **409** kèm thông điệp đếm chính xác số task chưa hoàn thành nếu project
 * còn việc dở (ADR-008 — cố ý không cascade để không ai mất dữ liệu vì một cú bấm).
 * Thông điệp đó đã đủ rõ và đã ở `title`, nên hiển thị nguyên văn thay vì viết lại —
 * dịch lại chỉ làm mất con số cụ thể mà người dùng cần để biết phải làm gì tiếp.
 */
export function DeleteProjectDialog({ project, onClose }: Props) {
  const open = project !== null;
  const deleteProject = useDeleteProject();
  const [error, setError] = useState<string | null>(null);

  const handleDelete = async () => {
    if (!project) return;
    setError(null);

    try {
      await deleteProject.mutateAsync(project.id);
      toast.success(`Đã xóa dự án "${project.name}".`);
      handleOpenChange(false);
    } catch (err) {
      // 409 ở đây không phải lỗi hệ thống mà là một câu trả lời nghiệp vụ hợp lệ:
      // "còn N task chưa hoàn thành". Giữ dialog mở để người dùng đọc được.
      if (err instanceof ApiError && err.isConflict) {
        setError(err.message);
        return;
      }
      setError(errorMessage(err));
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (next) return;
    setError(null);
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Xóa dự án?</DialogTitle>
          <DialogDescription>
            Dự án <strong className="text-foreground">{project?.name}</strong> sẽ bị xóa
            cùng toàn bộ sprint của nó. Thao tác này không hoàn tác được từ giao diện.
          </DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button
            type="button"
            variant="destructive"
            onClick={handleDelete}
            disabled={deleteProject.isPending}
          >
            {deleteProject.isPending ? 'Đang xóa…' : 'Xóa dự án'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

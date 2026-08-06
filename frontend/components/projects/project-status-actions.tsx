'use client';

import { CheckCircle2Icon, RotateCcwIcon } from 'lucide-react';
import { useState } from 'react';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { Button } from '@/components/ui/button';
import { errorMessage } from '@/lib/api/problem';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useCompleteProject, useReopenProject } from '@/lib/hooks/use-projects';
import { canManageProject } from '@/lib/tasks/permissions';
import type { Status } from '@/types/enums';

/**
 * Đổi trạng thái dự án — đường ghi DUY NHẤT cho `Project.Status` từ giao diện (ADR-048).
 *
 * Trước 2026-08-05 trường này chỉ đọc được: nó nằm trong DTO, là khóa `sortBy`, hiện trên
 * badge ở hai màn — nhưng mọi project tạo qua API vĩnh viễn ở `ToDo` vì `Project.Complete()`
 * chỉ có đúng một caller là `DbSeeder`. Một trường chết đội lốt tính năng.
 *
 * 🔴 Gác bằng quyền **TẦNG 2** (`lib/tasks/permissions.ts`), không phải `system-permissions.ts`:
 * đây là quyền theo từng dự án, đọc từ `RoleInProject`. `SystemAdmin` KHÔNG có đặc quyền
 * nghiệp vụ nào (ADR-042) — họ nhận 404 y hệt người ngoài.
 */
export function ProjectStatusActions({
  projectId,
  status,
}: {
  projectId: string;
  status: Status;
}) {
  const { role, isResolving } = useMyProjectRole(projectId);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isDone = status === 'Done';
  const complete = useCompleteProject(projectId);
  const reopen = useReopenProject(projectId);
  const mutation = isDone ? reopen : complete;

  // `isResolving` để nút không nháy hiện rồi biến mất trong lúc overview đang tải.
  if (isResolving || !canManageProject(role)) return null;

  const confirm = async () => {
    setError(null);
    try {
      await mutation.mutateAsync();
      setConfirming(false);
    } catch (caught) {
      // Giữ dialog mở và hiện chữ của server. Ca đáng giá nhất là 409 "chỉ mở lại được
      // project đang ở trạng thái Hoàn thành" — nó nói đúng thứ người dùng cần biết, và
      // `onSettled` đã invalidate nên lần thử kế tiếp chạy trên dữ liệu mới.
      setError(errorMessage(caught));
    }
  };

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => {
          setError(null);
          setConfirming(true);
        }}
      >
        {isDone ? <RotateCcwIcon className="size-4" /> : <CheckCircle2Icon className="size-4" />}
        {isDone ? 'Mở lại dự án' : 'Đánh dấu hoàn thành'}
      </Button>

      <ConfirmDialog
        open={confirming}
        variant="default"
        title={isDone ? 'Mở lại dự án?' : 'Đánh dấu dự án đã hoàn thành?'}
        description={
          isDone
            ? // Nói thẳng đích đến: người dùng nào cũng đoán "mở lại" là quay về đầu, mà
              // backend thì đưa về InProgress. Để họ phát hiện sau khi bấm là một bất ngờ
              // không cần thiết.
              'Dự án sẽ chuyển về trạng thái Đang thực hiện (không quay về Cần làm). Mọi thành viên sẽ nhận thông báo.'
            : 'Dự án sẽ chuyển sang trạng thái Hoàn thành. Task và sprint không bị ảnh hưởng, và bạn có thể mở lại bất cứ lúc nào.'
        }
        confirmLabel={isDone ? 'Mở lại' : 'Đánh dấu hoàn thành'}
        pendingLabel="Đang cập nhật…"
        error={error}
        isPending={mutation.isPending}
        onConfirm={() => void confirm()}
        onClose={() => setConfirming(false)}
      />
    </>
  );
}

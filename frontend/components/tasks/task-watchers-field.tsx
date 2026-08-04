'use client';

import { EyeIcon, EyeOffIcon } from 'lucide-react';
import { toast } from 'sonner';

import { AvatarStack } from '@/components/common/avatar-stack';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useToggleWatch, useWatchers } from '@/lib/hooks/use-watchers';
import { canWatch } from '@/lib/tasks/permissions';
import type { RoleInProject } from '@/types/enums';

/**
 * Theo dõi task.
 *
 * 🔴 `Viewer` THEO DÕI ĐƯỢC — đây là thao tác ghi duy nhất mà vai trò đó làm được
 * (ADR-036), và hợp lý vì nó chỉ ảnh hưởng hộp thông báo của chính họ. Nút gác bằng
 * `canWatch(role)` chứ KHÔNG bằng một phép kiểm `role !== 'Viewer'` chung chung.
 */
export function TaskWatchersField({
  projectId,
  taskId,
  isWatching,
  role,
}: {
  projectId: string;
  taskId: string;
  isWatching: boolean;
  role: RoleInProject | null;
}) {
  const watchers = useWatchers(projectId, taskId);
  const toggle = useToggleWatch(projectId, taskId);

  return (
    <div className="grid gap-2">
      {watchers.isError ? (
        <span className="text-destructive text-xs">{errorMessage(watchers.error)}</span>
      ) : watchers.isPending ? (
        <Skeleton className="h-6 w-24" />
      ) : watchers.data.length === 0 ? (
        <span className="text-muted-foreground text-sm">Chưa có ai theo dõi</span>
      ) : (
        <AvatarStack
          size="sm"
          people={watchers.data.map((watcher) => ({
            employeeId: watcher.employeeId,
            employeeName: watcher.employeeName,
          }))}
        />
      )}

      {canWatch(role) ? (
        <Button
          variant="outline"
          size="sm"
          className="w-full"
          disabled={toggle.isPending}
          onClick={() =>
            toggle.mutate(isWatching, {
              onSuccess: (state) =>
                toast.success(
                  state.isWatching
                    ? 'Đang theo dõi task này. Bạn sẽ nhận thông báo khi có thay đổi.'
                    : 'Đã bỏ theo dõi task này.',
                ),
              onError: (error) => toast.error(errorMessage(error)),
            })
          }
        >
          {isWatching ? (
            <>
              <EyeOffIcon className="size-4" />
              Bỏ theo dõi
            </>
          ) : (
            <>
              <EyeIcon className="size-4" />
              Theo dõi
            </>
          )}
        </Button>
      ) : null}
    </div>
  );
}

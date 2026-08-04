'use client';

import { useQuery } from '@tanstack/react-query';
import { useParams, useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { QueryError } from '@/components/common/query-error';
import { Skeleton } from '@/components/ui/skeleton';
import { getTask } from '@/lib/api/endpoints/tasks';

/**
 * Trang PHÂN GIẢI cho `/tasks/{taskId}` — không hiển thị gì, chỉ đổi sang URL đầy đủ
 * `/projects/{projectId}/tasks/{taskId}`.
 *
 * **Vì sao cần nó:** `NotificationResponse` chỉ mang `relatedEntityId` (= taskId) chứ không
 * mang `projectId`, mà route thật của task lại nằm dưới `projects/[id]`. Không có trang này
 * thì mọi thông báo loại Task đều không bấm được — hoặc phải nhồi thêm một trường vào DTO
 * chỉ để phục vụ điều hướng.
 *
 * Lợi kèm: `/tasks/{id}` trở thành một deep link ngắn, chia sẻ được, không cần biết project.
 *
 * ⚠️ Cố ý dùng khóa cache RIÊNG (`task-resolve`) chứ không phải `taskKeys.detail`: dữ liệu
 * ở đây có `rowVersion`, và nhét nó vào khóa chi tiết là gieo sẵn một token có thể đã cũ
 * cho form sửa đọc phải — đúng cái bug mà `useTask` đang chặn bằng `staleTime/gcTime: 0`.
 */
export default function TaskResolverPage() {
  const { taskId } = useParams<{ taskId: string }>();
  const router = useRouter();

  const task = useQuery({
    queryKey: ['task-resolve', taskId],
    queryFn: ({ signal }) => getTask(taskId, signal),
    gcTime: 0,
  });

  const projectId = task.data?.projectId;

  useEffect(() => {
    // `replace` chứ không phải `push`: trang này không đáng một mục trong lịch sử, và bấm
    // Back từ chi tiết task sẽ rơi ngược vào đây rồi bị đẩy đi tiếp — một vòng lặp.
    if (projectId) router.replace(`/projects/${projectId}/tasks/${taskId}`);
  }, [projectId, router, taskId]);

  if (task.isError) {
    return (
      <QueryError
        title="Không mở được task"
        error={task.error}
        onRetry={() => void task.refetch()}
        isRetrying={task.isFetching}
      />
    );
  }

  return (
    <div className="grid max-w-3xl gap-3" aria-busy="true">
      <span className="sr-only">Đang mở task…</span>
      <Skeleton className="h-7 w-64" />
      <Skeleton className="h-4 w-96" />
      <Skeleton className="h-40 w-full" />
    </div>
  );
}

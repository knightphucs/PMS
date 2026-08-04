'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { getWatchers, unwatchTask, watchTask } from '@/lib/api/endpoints/watchers';
import { taskDetailKeys, taskKeys } from '@/lib/hooks/keys';

export function useWatchers(projectId: string, taskId: string | null) {
  return useQuery({
    queryKey: taskDetailKeys.watchers(projectId, taskId ?? ''),
    queryFn: ({ signal }) => getWatchers(taskId!, signal),
    enabled: taskId !== null,
  });
}

/**
 * Bật/tắt theo dõi task.
 *
 * Nhận `isWatching` hiện tại rồi tự chọn endpoint, thay vì bắt component nhớ gọi cái nào —
 * cả hai đều idempotent nên gọi nhầm chiều cũng không hỏng dữ liệu, chỉ hiển thị sai.
 *
 * Làm mới **cả `taskKeys.detail`**: `TaskDetailResponse.isWatching` là một bản sao của
 * cùng thông tin, không làm mới thì nút và danh sách người theo dõi lệch nhau.
 */
export function useToggleWatch(projectId: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (isWatching: boolean) =>
      isWatching ? unwatchTask(taskId) : watchTask(taskId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.detail(projectId, taskId) });
    },
  });
}

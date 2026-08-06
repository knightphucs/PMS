'use client';

import type { UseQueryResult } from '@tanstack/react-query';
import { useState } from 'react';

import { ApiError, errorMessage } from '@/lib/api/problem';
import { useUpdateTask } from '@/lib/hooks/use-tasks';
import type { TaskDetailResponse, UpdateTaskRequest } from '@/types/task';

type Editable = Pick<
  UpdateTaskRequest,
  'name' | 'description' | 'dueDate' | 'priority' | 'storyPoints'
>;

export type SaveField = (patch: Partial<Editable>) => Promise<void>;

/**
 * Trục ghi của màn chi tiết task — MỘT chỗ duy nhất gọi `PUT /tasks/{id}`.
 *
 * 🔴 **`PUT /tasks/{id}` GHI ĐÈ TOÀN PHẦN, không phải PATCH.** `TaskService.UpdateAsync`
 * gán thẳng cả bốn trường từ request; trường nào không gửi thì record C# bind `null` và
 * dữ liệu cũ **biến mất im lặng**. Nên sửa mỗi mô tả vẫn phải gửi kèm tên, hạn, ưu tiên.
 * Đó là lý do màn này không cho mỗi khối tự gọi mutation của riêng nó: bốn nơi ghi là bốn
 * cơ hội quên một trường.
 *
 * Ba bước của ADR-016 giữ nguyên như `TaskFormDialog`:
 *   1. `rowVersion` lấy từ lần GET gần nhất (`useTask` có `staleTime/gcTime: 0`).
 *   2. Gửi lại nguyên vẹn khi PUT.
 *   3. 409 thì TẢI LẠI và dựng cờ `isStale` — **tuyệt đối không tự gửi lại**, vì như vậy
 *      là ghi đè thay đổi của người khác, đúng thứ `RowVersion` sinh ra để chặn.
 */
export function useTaskFieldSave(
  projectId: string,
  taskId: string,
  detail: UseQueryResult<TaskDetailResponse>,
) {
  const updateTask = useUpdateTask(projectId, taskId);
  const [isStale, setIsStale] = useState(false);

  const save: SaveField = async (patch) => {
    const current = detail.data;
    if (!current) return;

    try {
      await updateTask.mutateAsync({
        name: patch.name ?? current.name,
        priority: patch.priority ?? current.priority,
        storyPoints: patch.storyPoints ?? current.storyPoints,
        // `?? current` không dùng được cho hai trường nullable: xóa hạn/xóa mô tả gửi
        // `null`, mà `null ?? current` sẽ lấy lại giá trị cũ và phép xóa im lặng không
        // có tác dụng. Phải phân biệt "không truyền" (undefined) với "truyền null".
        dueDate: patch.dueDate !== undefined ? patch.dueDate : current.dueDate,
        description:
          patch.description !== undefined ? patch.description : current.description,
        rowVersion: current.rowVersion,
      });
      setIsStale(false);
    } catch (error) {
      if (error instanceof ApiError && error.isConflict) {
        setIsStale(true);
        await detail.refetch();
        // Nuốt lỗi: cờ `isStale` + dữ liệu vừa tải lại đã là toàn bộ thông điệp. Ném tiếp
        // sẽ khiến mỗi khối lại hiện thêm một toast đỏ cho cùng một sự việc.
        return;
      }
      throw error;
    }
  };

  return {
    save,
    isStale,
    dismissStale: () => setIsStale(false),
    /**
     * 🔴 Khóa MỌI nút lưu khi cờ này bật. `detail.isFetching` gồm cả lượt tải lại sau 409:
     * bấm Lưu trong lúc đó là gửi lại đúng `rowVersion` đã chết → 409 vĩnh viễn.
     */
    isBusy: updateTask.isPending || detail.isFetching,
    lastError: updateTask.error ? errorMessage(updateTask.error) : null,
  };
}

import type { Status } from '@/types/enums';
import type { BoardResponse, TaskSummaryResponse } from '@/types/task';

/**
 * Biến đổi thuần túy trên cache `BoardResponse`, tách khỏi React để test được.
 *
 * Dùng cho cập nhật lạc quan khi kéo–thả: thẻ phải nhảy cột NGAY rồi mới gọi API. Chờ
 * round-trip mới di chuyển là cảm giác chậm chạp điển hình của app sinh viên.
 */

/**
 * Gỡ task khỏi cột nguồn, thêm vào CUỐI cột đích, đổi `status`.
 *
 * Trả về chính `board` nếu không tìm thấy task — người gọi so sánh tham chiếu được và
 * TanStack sẽ không re-render vô ích.
 */
export function moveTaskInBoard(
  board: BoardResponse,
  taskId: string,
  target: Status,
): BoardResponse {
  const moved = board.columns.flatMap((column) => column.tasks).find((t) => t.id === taskId);
  if (!moved || moved.status === target) return board;

  const updated: TaskSummaryResponse = {
    ...moved,
    status: target,
    // ⚠️ Trường tính sẵn DUY NHẤT được phép sờ vào ở đây, và chỉ vì luật của nó nằm
    // ngay trong entity: `TaskItem.IsOverdue` có `&& Status != Status.Done`. Task chuyển
    // sang Done thì hết quá hạn, tức khắc.
    //
    // Mọi trường tính sẵn khác (`subtaskProgress`) giữ NGUYÊN — không có luật nào cho
    // phép suy ra giá trị mới ở client, và lượt invalidate ở `onSettled` sẽ chữa lành.
    isOverdue: target === 'Done' ? false : moved.isOverdue,
  };

  return {
    ...board,
    columns: board.columns.map((column) => {
      if (column.status === moved.status) {
        return { ...column, tasks: column.tasks.filter((t) => t.id !== taskId) };
      }
      if (column.status === target) {
        return { ...column, tasks: [...column.tasks, updated] };
      }
      return column;
    }),
  };
}

/**
 * Thay một thẻ bằng bản server vừa trả về, GIỮ NGUYÊN vị trí hiện tại.
 *
 * Dùng ở `onSuccess` để đồng bộ các trường tính sẵn (`subtaskProgress`, `isOverdue`) mà
 * không phải refetch — refetch ở đây làm cả board nháy sau mỗi lần kéo.
 *
 * Nếu server báo status khác với chỗ thẻ đang nằm (hiếm: có người khác vừa đổi), gọi
 * `moveTaskInBoard` để đưa nó về đúng cột.
 */
export function patchTaskInBoard(board: BoardResponse, task: TaskSummaryResponse): BoardResponse {
  const current = board.columns.find((column) =>
    column.tasks.some((t) => t.id === task.id),
  );
  if (!current) return board;
  if (current.status !== task.status) {
    return patchTaskInBoard(moveTaskInBoard(board, task.id, task.status), task);
  }

  return {
    ...board,
    columns: board.columns.map((column) =>
      column.status === task.status
        ? { ...column, tasks: column.tasks.map((t) => (t.id === task.id ? task : t)) }
        : column,
    ),
  };
}

/** Tìm một thẻ trên board mà không phải duyệt cột ở nơi gọi. */
export function findTaskInBoard(
  board: BoardResponse,
  taskId: string,
): TaskSummaryResponse | undefined {
  return board.columns.flatMap((column) => column.tasks).find((t) => t.id === taskId);
}

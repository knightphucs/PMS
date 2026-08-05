import type { BoardResponse, TaskSummaryResponse } from '@/types/task';

/**
 * Biến đổi thuần túy trên cache `BoardResponse`, tách khỏi React để test được.
 *
 * Dùng cho cập nhật lạc quan khi kéo–thả: thẻ phải nhảy cột NGAY rồi mới gọi API. Chờ
 * round-trip mới di chuyển là cảm giác chậm chạp điển hình của app sinh viên.
 */

/**
 * Gỡ task khỏi cột nguồn, thêm vào CUỐI cột đích, đổi tham chiếu cột trong `status`.
 *
 * Trả về chính `board` nếu không tìm thấy task — người gọi so sánh tham chiếu được và
 * TanStack sẽ không re-render vô ích.
 */
export function moveTaskInBoard(
  board: BoardResponse,
  taskId: string,
  targetColumnId: string,
): BoardResponse {
  const moved = board.columns.flatMap((column) => column.tasks).find((t) => t.id === taskId);
  if (!moved || moved.status.columnId === targetColumnId) return board;
  const targetColumn = board.columns.find((group) => group.column.id === targetColumnId)?.column;
  if (!targetColumn) return board;

  const updated: TaskSummaryResponse = {
    ...moved,
    status: {
      columnId: targetColumn.id,
      name: targetColumn.name,
      color: targetColumn.color,
      category: targetColumn.category,
    },
  };

  return {
    ...board,
    columns: board.columns.map((column) => {
      if (column.column.id === moved.status.columnId) {
        return { ...column, tasks: column.tasks.filter((t) => t.id !== taskId) };
      }
      if (column.column.id === targetColumnId) {
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

  if (current.column.id !== task.status.columnId) {
    const movedBoard = moveTaskInBoard(board, task.id, task.status.columnId);

    // 🔴 Chốt chặn ĐỆ QUY VÔ HẠN, không phải phòng thủ thừa. `moveTaskInBoard` trả về CHÍNH
    // `board` khi cột đích không có trên board đang xem — chuyện xảy ra thật sau ADR-052:
    // người khác vừa tạo một cột mới, hoặc board đang lọc theo sprint. Không có dòng này
    // thì hàm gọi lại chính nó với đúng đối số cũ, mãi mãi, và tab treo cứng.
    if (movedBoard === board) return board;

    return patchTaskInBoard(movedBoard, task);
  }

  return {
    ...board,
    columns: board.columns.map((column) =>
      column.column.id === task.status.columnId
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

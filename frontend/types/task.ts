/** Soi gương `PMS.Application/Features/Tasks/TaskDtos.cs`. */

import type { Priority, RoleInTask, Status } from './enums';
import type { LabelResponse } from './label';

/**
 * Người đảm nhận rút gọn, chỉ đủ vẽ avatar trên thẻ.
 *
 * Khác `TaskAssigneeResponse` ở chỗ bỏ `roleInTask` và `assignedDate` — board trả về
 * hàng chục task một lượt. Cần đầy đủ thì gọi `GET /tasks/{id}/assignees`.
 */
export interface TaskCardAssignee {
  employeeId: string;
  employeeName: string;
}

export interface TaskSummaryResponse {
  id: string;
  /** Số thứ tự trong project. Dùng khi cần sắp xếp/tra cứu bằng số. */
  number: number;
  /**
   * Mã hiển thị đã ghép sẵn, dạng `PMS-12` (ADR-034).
   *
   * ⚠️ **Đừng tự nối** từ `projectKey` + `number`: backend đã ghép rồi, và hai nơi định
   * dạng thì chắc chắn có lúc lệch nhau.
   */
  code: string;
  name: string;
  status: Status;
  priority: Priority;
  dueDate: string | null;
  /** ⚠️ Tính sẵn phía server. ĐỪNG tự tính lại — `lib/format.ts:isPastDue` chỉ dành cho Project. */
  isOverdue: boolean;
  /** `null` = đang ở Backlog. */
  sprintId: string | null;
  parentTaskId: string | null;
  /**
   * ⚠️ Phần trăm **0–100**, không phải tỉ lệ 0–1.
   *
   * `0` KHÔNG phân biệt được "không có subtask" với "có subtask nhưng chưa xong cái nào"
   * — `TaskSummaryResponse` không mang số lượng subtask. Nên chỉ vẽ thanh tiến độ khi
   * giá trị `> 0`; hiện `0%` trên task không có subtask nào còn tệ hơn là không hiện gì.
   */
  subtaskProgress: number;
  assignees: TaskCardAssignee[];
  /** Chip nhãn trên thẻ Kanban. Rỗng nếu task chưa gắn nhãn nào. */
  labels: LabelResponse[];
}

export interface TaskAssigneeResponse {
  employeeId: string;
  employeeName: string;
  roleInTask: RoleInTask;
  assignedDate: string;
}

export interface TaskDetailResponse {
  id: string;
  number: number;
  /** Mã hiển thị `PMS-12` — xem ghi chú ở `TaskSummaryResponse.code`. */
  code: string;
  name: string;
  /** `null` khi chưa có mô tả. Backend chuẩn hóa chuỗi rỗng/toàn khoảng trắng thành `null`. */
  description: string | null;
  status: Status;
  priority: Priority;
  dueDate: string | null;
  isOverdue: boolean;
  projectId: string;
  /** Mã ngắn của project (`PMS`) — nửa đầu của `code`. Hữu ích cho breadcrumb. */
  projectKey: string;
  sprintId: string | null;
  parentTaskId: string | null;
  /** Người TẠO task — khác với người được giao làm (mô hình Jira). */
  reporterId: string;
  reporterName: string;
  assignees: TaskAssigneeResponse[];
  subtasks: TaskSummaryResponse[];
  labels: LabelResponse[];
  /**
   * NGƯỜI ĐANG GỌI có đang theo dõi task này không — giá trị phụ thuộc người hỏi, không
   * phải thuộc tính của task. Dùng cho nút Theo dõi/Bỏ theo dõi (ADR-036).
   */
  isWatching: boolean;
  subtaskProgress: number;
  /** Base64. BẮT BUỘC gửi lại khi `PUT /tasks/{id}` (ADR-016). */
  rowVersion: string;
}

export interface BoardColumn {
  status: Status;
  tasks: TaskSummaryResponse[];
}

export interface BoardResponse {
  projectId: string;
  /** `null` = board "tất cả task" của project. */
  sprintId: string | null;
  /**
   * Backend LUÔN trả đủ **4 cột** theo thứ tự `ToDo, InProgress, Review, Done`, kể cả
   * cột rỗng — không phải tự dựng cột thiếu.
   */
  columns: BoardColumn[];
}

export interface CreateTaskRequest {
  name: string;
  projectId: string;
  /** `null` = đưa thẳng vào Backlog. */
  sprintId: string | null;
  /** `null` = task gốc. Có giá trị = subtask (chỉ một cấp, domain tự chặn cấp hai). */
  parentTaskId: string | null;
  dueDate: string | null;
  priority: Priority;
  /** Bỏ qua hoặc gửi `null` nếu chưa có mô tả — ĐỪNG gửi chuỗi `"string"`. */
  description?: string | null;
}

/**
 * ⚠️ Đây là request DUY NHẤT của Task cần `rowVersion` (ADR-021).
 * `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` thì KHÔNG — state machine và
 * ràng buộc sprint đã tự bảo vệ chúng.
 */
export interface UpdateTaskRequest {
  name: string;
  dueDate: string | null;
  priority: Priority;
  rowVersion: string;
  description?: string | null;
}

/** `{ "target": "InProgress" }` — tên trường là `target`, không phải `status`. */
export interface ChangeTaskStatusRequest {
  target: Status;
}

/** `null` = đưa task về Backlog. */
export interface MoveTaskToSprintRequest {
  sprintId: string | null;
}

export interface AssignTaskRequest {
  employeeId: string;
  role: RoleInTask;
}

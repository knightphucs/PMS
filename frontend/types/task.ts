/** Soi gương `PMS.Application/Features/Tasks/TaskDtos.cs`. */

import type { Priority, RoleInTask } from './enums';
import type { LabelResponse } from './label';

/**
 * Nhóm ngữ nghĩa của một cột (ADR-052) — soi gương `StatusCategory` phía backend.
 *
 * 🔴 Đây là thứ DUY NHẤT client được phép suy luận từ. Tên cột là chuỗi do người dùng đặt
 * ("Đã ship", "Hủy bỏ"), nên mọi phép kiểm "task này xong chưa" phải đọc `category`, KHÔNG
 * so tên và cũng không so `columnId` với một hằng nào.
 */
export type StatusCategory = 'ToDo' | 'InProgress' | 'Done';

/**
 * Tham chiếu cột được gắn vào task. Cột là dữ liệu do từng project cấu hình, vì vậy
 * client không được suy tên/màu/trạng thái hoàn thành từ một enum cố định.
 *
 * ⚠️ Không có `order`: thẻ không cần biết cột đứng thứ mấy, và board trả hàng chục thẻ một
 * lượt. Cần danh sách cột đầy đủ thì gọi `GET /projects/{id}/columns`.
 */
export interface TaskStatusRef {
  columnId: string;
  name: string;
  /** Mã màu `#RRGGBB` do người dùng chọn — dùng cho chip trạng thái. */
  color: string;
  category: StatusCategory;
}

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
  /** Cột đang đứng (ADR-052) — object, KHÔNG còn là chuỗi enum. */
  status: TaskStatusRef;
  priority: Priority;
  /** Story Point ước lượng; 0 nghĩa là chưa ước lượng. */
  storyPoints: number;
  dueDate: string | null;
  /** ⚠️ Tính sẵn phía server. ĐỪNG tự tính lại — `lib/format.ts:isPastDue` chỉ dành cho Project. */
  isOverdue: boolean;
  /** `null` = đang ở Backlog. */
  sprintId: string | null;
  parentTaskId: string | null;
  /**
   * ⚠️ Phần trăm **0–100**, không phải tỉ lệ 0–1.
   *
   * `0` không phân biệt được "không có subtask" với "có subtask nhưng chưa xong cái nào"
   * — dùng `subtaskCount` cho việc đó, `subtaskProgress` chỉ để vẽ thanh tiến độ.
   */
  subtaskProgress: number;
  /**
   * Số subtask TRỰC TIẾP (không đệ quy — chỉ một cấp cha–con). `0` = không có subtask nào,
   * khác `subtaskProgress === 0` (có subtask, chưa xong cái nào). Dùng để quyết định có vẽ
   * nút mở rộng danh sách subtask ngay trên thẻ Kanban hay không.
   */
  subtaskCount: number;
  /** Ghim — task ghim luôn đứng đầu cột trên board, cho MỌI người xem project (2026-08-06). */
  isPinned: boolean;
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
  /** Cột đang đứng (ADR-052) — object, KHÔNG còn là chuỗi enum. */
  status: TaskStatusRef;
  priority: Priority;
  storyPoints: number;
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

export interface BoardColumnResponse {
  id: string;
  name: string;
  color: string;
  order: number;
  category: TaskStatusRef['category'];
  taskCount: number;
}

/** Một cột trên board kèm task trong đó. Soi gương `BoardColumnGroup` phía backend. */
export interface BoardColumn {
  column: BoardColumnResponse;
  tasks: TaskSummaryResponse[];
}

export interface BoardResponse {
  projectId: string;
  /** `null` = board "tất cả task" của project. */
  sprintId: string | null;
  /**
   * Backend LUÔN trả đủ **MỌI cột của project**, kể cả cột rỗng, đã sắp theo `order`
   * trái→phải — không phải tự dựng cột thiếu và cũng không phải tự sắp xếp.
   *
   * ⚠️ Số cột **không cố định 4** kể từ ADR-052: người dùng thêm/xóa được. Đừng viết code
   * dựa trên độ dài mảng này.
   */
  columns: BoardColumn[];
}

export interface CreateBoardColumnRequest {
  name: string;
  /** `#RRGGBB` — server validate bằng regex, gửi sai định dạng nhận 400. */
  color: string;
  category: StatusCategory;
}

export type UpdateBoardColumnRequest = CreateBoardColumnRequest;

/**
 * Xóa cột. `targetColumnId` **bắt buộc khi cột còn task** — server trả 400 kèm số task nếu
 * thiếu. Không có đường "xóa cuốn theo task".
 */
export interface DeleteBoardColumnRequest {
  targetColumnId: string | null;
}

/** Gửi TRỌN danh sách theo thứ tự mới, không phải "chuyển cột X tới vị trí n". */
export interface ReorderBoardColumnsRequest {
  orderedColumnIds: string[];
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
  storyPoints: number;
  /** Bỏ qua hoặc gửi `null` nếu chưa có mô tả — ĐỪNG gửi chuỗi `"string"`. */
  description?: string | null;
  /**
   * Cột đích khi bấm "+" trên MỘT cột cụ thể (2026-08-06). Bỏ qua hoặc gửi `null` = cột
   * trái nhất của project (hành vi cũ) — nút "Tạo task" chung và tạo subtask đều bỏ trường
   * này. Phải cùng project với `projectId`, không thì 404.
   */
  boardColumnId?: string | null;
}

/**
 * ⚠️ Đây là request DUY NHẤT của Task cần `rowVersion` (ADR-021).
 * `PATCH /tasks/{id}/status` và `PUT /tasks/{id}/sprint` thì KHÔNG.
 */
export interface UpdateTaskRequest {
  name: string;
  dueDate: string | null;
  priority: Priority;
  storyPoints: number;
  rowVersion: string;
  description?: string | null;
}

/**
 * `{ "targetColumnId": "…" }` — ADR-052 đổi từ `target: Status` sang id cột.
 *
 * 📌 Hai hệ quả so với trước:
 * - Kéo thẻ về **đúng cột đang đứng** nay trả **200** (no-op), không còn 409.
 * - Không còn "nhảy bước": mọi cột đều tới thẳng được.
 *
 * Guard duy nhất còn lại: cột đích thuộc nhóm `InProgress` mà task đang bị chặn → **409**.
 */
export interface ChangeTaskStatusRequest {
  targetColumnId: string;
}

/** `null` = đưa task về Backlog. */
export interface MoveTaskToSprintRequest {
  sprintId: string | null;
}

/** Ghim/gỡ ghim — task ghim luôn đứng đầu cột trên board (2026-08-06). */
export interface PinTaskRequest {
  pinned: boolean;
}

export interface AssignTaskRequest {
  employeeId: string;
  role: RoleInTask;
}

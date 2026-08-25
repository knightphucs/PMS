/**
 * Loại công việc theo project (ADR-060) — Sự cố / Yêu cầu / Change Request / Bảo trì…
 *
 * 🔴 Mọi task BẮT BUỘC thuộc đúng một loại. Project mới được cấp sẵn loại "Task", nên
 * không có trạng thái "task chưa có loại" cần xử lý ở UI.
 */

export interface WorkItemTypeFieldResponse {
  fieldDefinitionId: string;
  label: string;
  /** Bắt buộc với LOẠI này — cùng một trường có thể bắt buộc ở loại A và tuỳ chọn ở loại B. */
  isRequired: boolean;
  order: number;
}

export interface WorkItemTypeResponse {
  id: string;
  projectId: string;
  name: string;
  /** Tên icon lucide-react. Tên lạ rơi về icon mặc định — hỏng nhẹ và nhìn thấy được. */
  icon: string;
  color: string;
  order: number;
  fields: WorkItemTypeFieldResponse[];
  /** Số task đang mang loại này — UI bắt chọn loại đích trước khi xoá. */
  taskCount: number;

  /**
   * Loại này có nhận yêu cầu từ **người ngoài project** qua cổng tiếp nhận không (ADR-063).
   *
   * 🔴 Cờ này CHẶN THẬT: `POST /request-portal/.../requests` từ chối mọi loại có
   * `isRequestable === false`, và có mutation test canh. Không phải một nhãn hiển thị.
   */
  isRequestable: boolean;

  /** Chỉ dẫn hiện trên đầu form tiếp nhận. `null` = không hiện khối chỉ dẫn nào. */
  requestInstructions: string | null;
}

export interface WorkItemTypeFieldRequest {
  fieldDefinitionId: string;
  isRequired: boolean;
}

export interface CreateWorkItemTypeRequest {
  name: string;
  icon: string;
  color: string;
  fields?: WorkItemTypeFieldRequest[];
  /**
   * ⚠️ Backend mặc định `false` khi trường vắng mặt, và `PUT` là **ghi đè toàn phần**
   * (cùng khuôn `PUT /tasks/{id}`, ADR-044) — nên một request sửa loại mà quên gửi cờ này
   * sẽ **TẮT** cổng của một loại đang bật, im lặng. Form luôn gửi trọn cả hai trường.
   */
  isRequestable?: boolean;
  requestInstructions?: string | null;
}

export type UpdateWorkItemTypeRequest = CreateWorkItemTypeRequest;

export interface ReorderWorkItemTypesRequest {
  typeIds: string[];
}

/** `targetTypeId` bắt buộc khi loại còn task — xem `deleteWorkItemType`. */
export interface DeleteWorkItemTypeRequest {
  targetTypeId?: string | null;
}

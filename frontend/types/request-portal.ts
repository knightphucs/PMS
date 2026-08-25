/**
 * Cổng yêu cầu (ADR-063) — bề mặt của **người gửi**, tách hẳn khỏi bề mặt làm việc.
 *
 * 🔴 **Người dùng những kiểu này thường KHÔNG phải thành viên project.** Đó là điều khác
 * biệt duy nhất nhưng quan trọng nhất so với mọi file `types/*` khác ở đây: mọi trường
 * thêm vào là một quyết định bảo mật, không phải một tiện ích hiển thị.
 *
 * Backend gác bằng chính vị từ truy vấn (`ReporterId == me`) chứ không qua
 * `ProjectAuthorizationService` — xem XML doc của `RequestPortalService`.
 */
import type { FieldOptionResponse, FieldType } from './custom-field';
import type { Priority } from './enums';
import type { TaskApprovalState } from './saved-view';
import type { StatusCategory } from './task';

// ---------- (1) danh mục cổng ----------

export interface RequestPortalTypeSummary {
  workItemTypeId: string;
  name: string;
  /** Tên icon lucide-react. Tên lạ rơi về icon mặc định. */
  icon: string;
  color: string;
}

export interface RequestPortalProjectResponse {
  projectId: string;
  projectName: string;
  projectKey: string;
  types: RequestPortalTypeSummary[];
}

// ---------- (2) lược đồ form ----------

/**
 * Một ô trên form tiếp nhận.
 *
 * 📌 Dùng lại `FieldType` và `FieldOptionResponse` của ADR-059 nguyên vẹn — frontend đã có
 * bộ render theo kiểu, không dựng lại. Đó chính là điều ADR-063 tuyên bố: *"loại việc ĐÃ LÀ
 * request type"*, không có khái niệm form nào riêng.
 */
export interface RequestPortalFieldSchema {
  fieldDefinitionId: string;
  label: string;
  type: FieldType;
  isRequired: boolean;
  order: number;
  options: FieldOptionResponse[];
}

export interface RequestPortalTypeForm {
  workItemTypeId: string;
  name: string;
  icon: string;
  color: string;
  /** `null` = không hiện khối chỉ dẫn nào (luật 3 Doctrine: chưa dùng thì TỰ ẨN). */
  requestInstructions: string | null;
  fields: RequestPortalFieldSchema[];
}

export interface RequestPortalFormResponse {
  projectId: string;
  projectName: string;
  projectKey: string;
  types: RequestPortalTypeForm[];
}

// ---------- (3) gửi yêu cầu ----------

/**
 * ⚠️ **Không** có `boardColumnId`, `sprintId`, `storyPoints`, `assigneeIds`. Người gửi
 * không biết — và không được quyết — yêu cầu của họ rơi vào cột nào hay ai xử lý.
 */
export interface SubmitRequestRequest {
  workItemTypeId: string;
  name: string;
  description?: string | null;
  priority: Priority;
  dueDate?: string | null;
  fieldValues?: SetRequestFieldValue[];
}

/** Giống `SetFieldValueRequest` của ADR-059; chỉ trường ứng với `type` được backend đọc. */
export interface SetRequestFieldValue {
  fieldDefinitionId: string;
  valueText?: string | null;
  valueNumber?: number | null;
  valueDate?: string | null;
  valueBoolean?: boolean | null;
  selectedOptionIds?: string[] | null;
}

// ---------- (4)(5) yêu cầu của tôi ----------

export interface MyRequestResponse {
  taskId: string;
  code: string;
  name: string;
  projectId: string;
  projectName: string;
  typeName: string;
  typeIcon: string;
  typeColor: string;
  /** TÊN CỘT do đội xử lý đặt, không phải một enum — người gửi đọc đúng ngôn ngữ của đội đó. */
  statusName: string;
  statusColor: string;
  statusCategory: StatusCategory;
  priority: Priority;
  dueDate: string | null;
  createdAt: string;
  approvalState: TaskApprovalState;
}

/**
 * Chi tiết một yêu cầu của CHÍNH người gửi — chỉ đọc.
 *
 * ⚠️ **Cố ý chưa có bình luận.** `CommentService` đi qua `ProjectAction.CreateComment` nên
 * người ngoài nhận 404. Mở đường hồi đáp cần một quyết định sản phẩm riêng (ai đọc được
 * bình luận nội bộ của đội xử lý?) — ghi rõ ở ADR-063 chứ không bỏ quên.
 */
export interface MyRequestDetailResponse extends MyRequestResponse {
  description: string | null;
  fieldValues: RequestFieldValue[];
}

export interface RequestFieldValue {
  fieldDefinitionId: string;
  label: string;
  type: FieldType;
  isRequired: boolean;
  valueText: string | null;
  valueNumber: number | null;
  valueDate: string | null;
  valueBoolean: boolean | null;
  selectedOptions: FieldOptionResponse[];
}

/**
 * Trường tuỳ biến theo project (ADR-059) — hậu bản của cột board tuỳ biến (ADR-052).
 *
 * 🔴 Hai nhóm quyền KHÁC nhau, đừng gộp khi gác UI:
 *  - Sửa **lược đồ** (thêm/sửa/xoá/sắp xếp trường) → PM (`canManageTasks`).
 *  - Nhập **giá trị** trên một task → ai sửa được task thì sửa được.
 *  - **Đọc** lược đồ → mọi thành viên, kể cả Viewer. Ẩn đi thì khối trường tuỳ biến của họ
 *    rỗng mà không có lý do nào giải thích được.
 */

/**
 * Danh mục ĐÓNG — backend không nhận giá trị ngoài danh sách này (ADR-059).
 * Thêm kiểu mới là thay đổi có chủ đích ở cả backend lẫn 4 chỗ của frontend:
 * ô nhập, cách hiển thị, cách so sánh, và form khai báo.
 */
export type FieldType =
  | 'Text'
  | 'Number'
  | 'Date'
  | 'Checkbox'
  | 'Url'
  | 'SingleSelect'
  | 'MultiSelect';

export const FIELD_TYPE_LABEL: Record<FieldType, string> = {
  Text: 'Văn bản',
  Number: 'Số',
  Date: 'Ngày',
  Checkbox: 'Có / Không',
  Url: 'Đường dẫn',
  SingleSelect: 'Chọn một',
  MultiSelect: 'Chọn nhiều',
};

/** Hai kiểu duy nhất có danh sách lựa chọn. */
export function isSelectType(type: FieldType): boolean {
  return type === 'SingleSelect' || type === 'MultiSelect';
}

export interface FieldOptionResponse {
  id: string;
  label: string;
  /** `#RRGGBB`. Backend đã validate bằng regex — giá trị này đi thẳng vào `style`. */
  color: string;
  order: number;
}

export interface FieldDefinitionResponse {
  id: string;
  projectId: string;
  label: string;
  type: FieldType;
  order: number;
  options: FieldOptionResponse[];
  /** Số task đang có giá trị — nuôi cảnh báo trước khi xoá trường. */
  valueCount: number;
}

export interface FieldOptionRequest {
  label: string;
  color: string;
}

export interface CreateFieldDefinitionRequest {
  label: string;
  type: FieldType;
  /** Bắt buộc (>= 1) với SingleSelect/MultiSelect, bỏ qua với kiểu khác. */
  options?: FieldOptionRequest[];
}

/**
 * ⚠️ KHÔNG có `type`: kiểu không đổi được sau khi tạo (ADR-059). Đừng dựng ô chọn kiểu ở
 * form sửa — nó sẽ là một ô hứa một việc backend từ chối làm.
 */
export interface UpdateFieldDefinitionRequest {
  label: string;
  options?: FieldOptionRequest[];
}

export interface ReorderFieldDefinitionsRequest {
  fieldIds: string[];
}

/**
 * Giá trị của MỘT trường trên MỘT task.
 *
 * 📌 `GET /tasks/{id}/field-values` trả về **mọi** trường của project, kể cả trường task
 * chưa điền (mọi `value*` đều `null`). Frontend dựng thẳng form từ mảng này — không phải
 * tự gộp "danh sách trường" với "danh sách giá trị".
 */
export interface FieldValueResponse {
  fieldDefinitionId: string;
  label: string;
  type: FieldType;
  valueText: string | null;
  valueNumber: number | null;
  valueDate: string | null;
  valueBoolean: boolean | null;
  selectedOptions: FieldOptionResponse[];
}

/** Gửi tất cả `null`/mảng rỗng = XOÁ giá trị của trường đó. */
export interface SetFieldValueRequest {
  fieldDefinitionId: string;
  valueText?: string | null;
  valueNumber?: number | null;
  valueDate?: string | null;
  valueBoolean?: boolean | null;
  selectedOptionIds?: string[] | null;
}

/**
 * ⚠️ Ghi kiểu **PATCH**: chỉ trường có mặt trong `values` bị đụng tới. Gửi trọn bộ giá trị
 * mỗi lần lưu sẽ biến mỗi lần gõ thành một cơ hội ghi đè công của người khác.
 */
export interface SetFieldValuesRequest {
  values: SetFieldValueRequest[];
}

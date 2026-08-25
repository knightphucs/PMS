import type { FieldOptionResponse, FieldType } from './custom-field';
import type { TaskSummaryResponse } from './task';

/**
 * View lưu được trên danh sách task (ADR-061) — mảnh cuối của nền tảng mở rộng.
 *
 * 🔴 Hai mức quyền KHÁC nhau, đừng gộp khi gác UI:
 *  - View **RIÊNG**: ai cũng tạo/sửa/xoá của chính mình, kể cả `Viewer` — nó chỉ đổi cách
 *    họ nhìn danh sách, không ai khác thấy (cùng lý lẽ "theo dõi task" của ADR-036).
 *  - View **CHIA SẺ**: cần quyền PM. Đây là cấu hình cả đội nhìn thấy.
 *
 * Đừng tự suy quyền sửa ở frontend — backend trả sẵn `canEdit` trên từng view, vì luật là
 * "chủ sở hữu HOẶC PM" mà vế thứ hai frontend chỉ biết qua vai trò. Hai nơi cùng dựng một
 * luật thì chắc chắn có lúc lệch (ADR-034).
 */

/**
 * Trường **dựng sẵn** lọc/sắp xếp/hiển thị được. Danh mục ĐÓNG — backend từ chối giá trị
 * ngoài danh sách này.
 *
 * 📌 Cố ý chưa có `Labels`/`Watchers`: quan hệ N–N nên "bằng" có hai nghĩa (chứa nhãn này /
 * có đúng tập nhãn này), và chọn nhầm nghĩa thì người dùng không có cách nào biết.
 */
export type TaskField =
  | 'Name'
  | 'BoardColumn'
  | 'Category'
  | 'Priority'
  | 'WorkItemType'
  | 'Sprint'
  | 'Assignee'
  | 'Reporter'
  | 'DueDate'
  | 'StoryPoints'
  | 'CreatedAt'
  /**
   * Trạng thái duyệt hiện thời (ADR-063) — giá trị là một `TaskApprovalState`.
   *
   * ⚠️ Trường dựng sẵn ĐẦU TIÊN không phải một cột của bảng Tasks: nó là phép chiếu của
   * bảng `Approvals` xuống task. Hệ quả ở UI: **không sắp xếp theo nó được** (backend cho
   * nó rơi về thứ tự mặc định), nên đừng bày nó ra ở ô "Sắp xếp theo".
   */
  | 'ApprovalState';

export const TASK_FIELD_LABEL: Record<TaskField, string> = {
  Name: 'Tên task',
  BoardColumn: 'Cột',
  Category: 'Nhóm trạng thái',
  Priority: 'Độ ưu tiên',
  WorkItemType: 'Loại công việc',
  Sprint: 'Sprint',
  Assignee: 'Người đảm nhận',
  Reporter: 'Người tạo',
  DueDate: 'Hạn hoàn thành',
  StoryPoints: 'Story Points',
  CreatedAt: 'Ngày tạo',
  ApprovalState: 'Trạng thái duyệt',
};

/**
 * Các trường SẮP XẾP được. `ApprovalState` cố ý vắng mặt — xem XML doc của nó.
 *
 * 🔴 Danh sách này tồn tại thay vì `Object.keys(TASK_FIELD_LABEL)`: bày ra một khoá sắp
 * xếp mà backend lặng lẽ bỏ qua là hứa với người dùng một thứ tự không tồn tại, và họ chỉ
 * phát hiện bằng cách nhìn kết quả và tự nghi ngờ mắt mình.
 */
export const SORTABLE_TASK_FIELDS: readonly TaskField[] = [
  'Name',
  'BoardColumn',
  'Category',
  'Priority',
  'WorkItemType',
  'Sprint',
  'Reporter',
  'DueDate',
  'StoryPoints',
  'CreatedAt',
];

/**
 * Trạng thái duyệt của một task, nhìn từ phía người lọc danh sách (ADR-063).
 *
 * ⚠️ KHÔNG phải `ApprovalStatus` (vòng đời của MỘT yêu cầu duyệt). Đây là phép chiếu của
 * các yêu cầu **còn hiệu lực** xuống chính task — hai enum không thay thế nhau được.
 *
 * 📌 Cố ý không có "chờ TÔI duyệt": một `SavedView` lưu giá trị **literal**, nên một view
 * CHIA SẺ mang điều kiện đó sẽ nói dối mọi người trừ tác giả của nó.
 */
export type TaskApprovalState = 'None' | 'Pending' | 'Approved' | 'Rejected';

export const TASK_APPROVAL_STATE_LABEL: Record<TaskApprovalState, string> = {
  None: 'Không cần duyệt',
  Pending: 'Đang chờ ký',
  Approved: 'Đã duyệt, chờ chuyển',
  Rejected: 'Bị từ chối',
};

/** Danh mục ĐÓNG. Toán tử nào hợp lệ với trường nào do `FilterValueKind` quyết định. */
export type FilterOperator =
  | 'Equals'
  | 'NotEquals'
  | 'Contains'
  | 'GreaterThan'
  | 'GreaterThanOrEqual'
  | 'LessThan'
  | 'LessThanOrEqual'
  | 'IsEmpty'
  | 'IsNotEmpty';

export const FILTER_OPERATOR_LABEL: Record<FilterOperator, string> = {
  Equals: 'bằng',
  NotEquals: 'khác',
  Contains: 'chứa',
  GreaterThan: 'lớn hơn',
  GreaterThanOrEqual: 'từ',
  LessThan: 'nhỏ hơn',
  LessThanOrEqual: 'đến',
  IsEmpty: 'chưa có',
  IsNotEmpty: 'đã có',
};

/** Hai toán tử duy nhất KHÔNG mang giá trị so sánh. */
export function isUnaryOperator(operator: FilterOperator): boolean {
  return operator === 'IsEmpty' || operator === 'IsNotEmpty';
}

/**
 * Hình dạng dữ liệu của một trường — quyết định toán tử nào hợp lệ và ô nhập nào được vẽ.
 *
 * 🔴 **Bản sao có chủ đích của `TaskFilterCatalog` ở backend.** Chấp nhận trùng vì nếu
 * không thì UI bày ra những toán tử backend sẽ từ chối, và người dùng chỉ biết sau khi bấm
 * Lưu. Backend vẫn là chốt chặn thật — đây chỉ là lớp không-bày-ra-thứ-sẽ-hỏng, đúng khuôn
 * `lib/tasks/permissions.ts` (bản sao của `ProjectPermissions.cs`).
 */
export type FilterValueKind = 'Text' | 'Number' | 'Date' | 'Boolean' | 'Reference' | 'Enum';

export function kindOfTaskField(field: TaskField): FilterValueKind {
  switch (field) {
    case 'Name':
      return 'Text';
    case 'BoardColumn':
    case 'WorkItemType':
    case 'Sprint':
    case 'Assignee':
    case 'Reporter':
      return 'Reference';
    case 'Category':
    case 'Priority':
    case 'ApprovalState':
      return 'Enum';
    case 'DueDate':
    case 'CreatedAt':
      return 'Date';
    case 'StoryPoints':
      return 'Number';
  }
}

export function kindOfFieldType(type: FieldType): FilterValueKind {
  switch (type) {
    case 'Text':
    case 'Url':
      return 'Text';
    case 'Number':
      return 'Number';
    case 'Date':
      return 'Date';
    case 'Checkbox':
      return 'Boolean';
    // Lọc theo Select là lọc theo LỰA CHỌN nào đang được chọn — giá trị là id của option,
    // không phải nhãn. So theo nhãn sẽ vỡ ngay lần đầu người dùng đổi tên một lựa chọn.
    case 'SingleSelect':
    case 'MultiSelect':
      return 'Reference';
  }
}

/** Toán tử hợp lệ với một kiểu giá trị — cùng luật với `TaskFilterCatalog.Supports`. */
export function operatorsFor(kind: FilterValueKind): FilterOperator[] {
  const base: FilterOperator[] = ['Equals', 'NotEquals', 'IsEmpty', 'IsNotEmpty'];

  if (kind === 'Text') return ['Equals', 'NotEquals', 'Contains', 'IsEmpty', 'IsNotEmpty'];

  // So sánh có thứ tự chỉ cho số và ngày. Enum KHÔNG có: `Priority.Highest = 0` nên
  // "lớn hơn Medium" trả về những việc ÍT ưu tiên hơn — đúng theo số, ngược hẳn với chữ.
  if (kind === 'Number' || kind === 'Date')
    return [
      'Equals',
      'NotEquals',
      'GreaterThan',
      'GreaterThanOrEqual',
      'LessThan',
      'LessThanOrEqual',
      'IsEmpty',
      'IsNotEmpty',
    ];

  return base;
}

/** Giá trị hợp lệ của `Category`/`Priority` — gửi lên dưới dạng TÊN, không phải số. */
export const CATEGORY_VALUES = ['ToDo', 'InProgress', 'Done'] as const;
export const PRIORITY_VALUES = ['Highest', 'High', 'Medium', 'Low', 'Lowest'] as const;

export interface SavedViewFilterDto {
  /** Loại trừ lẫn nhau với `fieldDefinitionId` — đúng một trong hai. */
  field: TaskField | null;
  fieldDefinitionId: string | null;
  operator: FilterOperator;
  /** `null` với `IsEmpty`/`IsNotEmpty`. Enum gửi TÊN, tham chiếu gửi id. */
  value: string | null;
}

export interface SavedViewColumnDto {
  field: TaskField | null;
  fieldDefinitionId: string | null;
}

export interface SavedViewResponse {
  id: string;
  projectId: string;
  name: string;
  ownerId: string;
  ownerName: string;
  isShared: boolean;
  order: number;
  sortBy: TaskField | null;
  sortDescending: boolean;
  groupBy: TaskField | null;
  filters: SavedViewFilterDto[];
  columns: SavedViewColumnDto[];
  /** Backend trả lời thay vì frontend tự suy — xem chú thích đầu file. */
  canEdit: boolean;
}

export interface CreateSavedViewRequest {
  name: string;
  isShared: boolean;
  sortBy: TaskField | null;
  sortDescending: boolean;
  groupBy: TaskField | null;
  filters?: SavedViewFilterDto[];
  columns?: SavedViewColumnDto[];
}

/** Ghi đè TOÀN PHẦN: `filters`/`columns` gửi lên thay thế hẳn cái cũ (ADR-044). */
export type UpdateSavedViewRequest = CreateSavedViewRequest;

export interface ReorderSavedViewsRequest {
  viewIds: string[];
}

/**
 * Đầu vào một lượt chạy. KHÔNG mang `viewId`: client gửi thẳng trạng thái view đang mở,
 * nên "xem thử trước khi lưu" dùng chung đúng đường này.
 */
export interface TaskQueryRequest {
  filters?: SavedViewFilterDto[];
  sortBy?: TaskField | null;
  sortDescending?: boolean;
  search?: string | null;
  page?: number;
  pageSize?: number;
}

export interface TaskListFieldValue {
  fieldDefinitionId: string;
  label: string;
  type: FieldType;
  valueText: string | null;
  valueNumber: number | null;
  valueDate: string | null;
  valueBoolean: boolean | null;
  selectedOptions: FieldOptionResponse[];
}

/**
 * Một dòng trên màn danh sách.
 *
 * ⚠️ `customFields` nằm NGOÀI `task` có chủ đích: `TaskSummaryResponse` nuôi cả board lẫn
 * backlog, và nhồi thêm vào đó sẽ buộc mọi query của hai màn kia phải nhớ include giá trị
 * trường — thiếu một chỗ là mảng rỗng một cách im lặng.
 */
export interface TaskListItemResponse {
  task: TaskSummaryResponse;
  customFields: TaskListFieldValue[];
}

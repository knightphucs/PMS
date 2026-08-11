import type {
  CreateFieldDefinitionRequest,
  FieldDefinitionResponse,
  FieldValueResponse,
  ReorderFieldDefinitionsRequest,
  SetFieldValuesRequest,
  UpdateFieldDefinitionRequest,
} from '@/types/custom-field';

import { apiFetch } from '../http';

/**
 * Trường tuỳ biến theo project (ADR-059).
 *
 * Route chia hai nhóm theo đúng hai mức quyền: `/projects/{id}/fields*` là LƯỢC ĐỒ (PM),
 * `/tasks/{id}/field-values` là GIÁ TRỊ (ai sửa được task thì sửa được).
 */

/** Đọc mở cho mọi thành viên. Người ngoài project nhận **404**, không phải 403 (ADR-019). */
export function listFieldDefinitions(projectId: string, signal?: AbortSignal) {
  return apiFetch<FieldDefinitionResponse[]>(`/projects/${projectId}/fields`, { signal });
}

/** **409** trùng tên trường · **400** kiểu Select mà không có lựa chọn nào. */
export function createFieldDefinition(projectId: string, body: CreateFieldDefinitionRequest) {
  return apiFetch<FieldDefinitionResponse>(`/projects/${projectId}/fields`, {
    method: 'POST',
    body,
  });
}

/**
 * ⚠️ Backend khớp lựa chọn theo **Label**, không theo id. Hệ quả cần nói cho người dùng
 * biết: đổi màu hoặc đổi thứ tự KHÔNG mất giá trị đã nhập, nhưng **đổi tên một lựa chọn
 * thì mất** — về mặt dữ liệu nó là một lựa chọn khác.
 */
export function updateFieldDefinition(fieldId: string, body: UpdateFieldDefinitionRequest) {
  return apiFetch<FieldDefinitionResponse>(`/fields/${fieldId}`, { method: 'PUT', body });
}

/**
 * Xoá trường và MỌI giá trị của nó.
 *
 * 📌 Khác `DELETE /columns/{id}` — cái đó có thân request vì task bắt buộc phải có cột đích.
 * Ở đây "task không có giá trị cho trường này" là trạng thái hợp lệ, nên không có thân.
 */
export function deleteFieldDefinition(fieldId: string) {
  return apiFetch<void>(`/fields/${fieldId}`, { method: 'DELETE' });
}

/** Gửi TRỌN danh sách theo thứ tự mới — server từ chối danh sách thiếu hoặc trùng. */
export function reorderFieldDefinitions(
  projectId: string,
  body: ReorderFieldDefinitionsRequest,
) {
  return apiFetch<void>(`/projects/${projectId}/fields/order`, { method: 'PUT', body });
}

/** Trả về MỌI trường của project kèm giá trị (null nếu chưa điền). */
export function listFieldValues(taskId: string, signal?: AbortSignal) {
  return apiFetch<FieldValueResponse[]>(`/tasks/${taskId}/field-values`, { signal });
}

/** PATCH: chỉ trường có mặt trong body bị đụng tới. **403** với Viewer. */
export function setFieldValues(taskId: string, body: SetFieldValuesRequest) {
  return apiFetch<FieldValueResponse[]>(`/tasks/${taskId}/field-values`, {
    method: 'PATCH',
    body,
  });
}

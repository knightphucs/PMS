import type { PagedRequest, PagedResult } from '@/types/common';
import type {
  CreateProjectRequest,
  ProjectDetailResponse,
  ProjectSummaryResponse,
  UpdateProjectRequest,
} from '@/types/project';

import { apiFetch } from '../http';

/**
 * `GET /projects` — chỉ trả project mà người gọi là thành viên.
 *
 * Tên query param khớp `PagedRequest` phía backend. Binding của ASP.NET không phân biệt
 * hoa thường nên camelCase an toàn.
 */
export function listProjects(params: PagedRequest, signal?: AbortSignal) {
  return apiFetch<PagedResult<ProjectSummaryResponse>>('/projects', {
    query: { ...params },
    signal,
  });
}

export function getProject(id: string, signal?: AbortSignal) {
  return apiFetch<ProjectDetailResponse>(`/projects/${id}`, { signal });
}

/**
 * Người tạo tự động trở thành `ProjectManager` của project đó — không phải gọi thêm
 * endpoint nào để tự thêm mình vào.
 */
export function createProject(body: CreateProjectRequest) {
  return apiFetch<ProjectSummaryResponse>('/projects', { method: 'POST', body });
}

/**
 * ⚠️ `body.rowVersion` phải là giá trị lấy từ lần `getProject` GẦN NHẤT (ADR-016).
 * Gửi token cũ → **409**, nghĩa là người khác đã sửa project trong lúc form đang mở.
 * Cách xử lý đúng là tải lại rồi để người dùng quyết định, không phải ghi đè.
 */
export function updateProject(id: string, body: UpdateProjectRequest) {
  return apiFetch<ProjectDetailResponse>(`/projects/${id}`, { method: 'PUT', body });
}

/**
 * Xóa **mềm**. Trả **409** nếu project còn task chưa `Done` (ADR-008) — cố ý không
 * cascade, để người dùng không mất dữ liệu vì một cú bấm.
 */
export function deleteProject(id: string) {
  return apiFetch<void>(`/projects/${id}`, { method: 'DELETE' });
}

/**
 * `POST /projects/{id}/complete` — đưa `Project.Status` về `Done`. PM-only (ADR-048).
 *
 * ⚠️ **KHÔNG body, KHÔNG `rowVersion`** — cố ý tách khỏi `PUT` vì đây là một chuyển trạng
 * thái chứ không phải một lần ghi đè trường, cùng lý do `PATCH /tasks/{id}/status` (ADR-021).
 *
 * **Idempotent**: gọi trên project đã `Done` trả 200 và không sinh log/thông báo nào.
 * Người ngoài dự án nhận **404** chứ không phải 403 (ADR-019) — đừng suy quyền từ mã lỗi.
 */
export function completeProject(id: string) {
  return apiFetch<ProjectDetailResponse>(`/projects/${id}/complete`, { method: 'POST' });
}

/**
 * `POST /projects/{id}/reopen` — PM-only, không body, không `rowVersion`.
 *
 * 🔴 Đưa về **`InProgress`, KHÔNG về `ToDo`**: project từng chạy tới Done thì công việc đã
 * thật sự diễn ra, quay về "Cần làm" là xóa mất sự thật đó.
 *
 * Trả **409** nếu project chưa ở `Done`. UI đã ẩn nút trong trường hợp đó, nên 409 chỉ xảy
 * ra khi cache cũ hơn thực tế — xử lý bằng cách tải lại chứ không phải bằng một thông điệp
 * mới, chữ tiếng Việt của server đã đúng.
 */
export function reopenProject(id: string) {
  return apiFetch<ProjectDetailResponse>(`/projects/${id}/reopen`, { method: 'POST' });
}

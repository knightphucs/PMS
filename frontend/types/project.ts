/** Soi gương `PMS.Application/Features/Projects/ProjectDtos.cs`. */

import type { InvitationStatus, RoleInProject, Status } from './enums';

/** Kiểu phần tử của `GET /projects` (danh sách). */
export interface ProjectSummaryResponse {
  id: string;
  name: string;
  status: Status;
  expectedCompletionDate: string;
}

export interface ProjectMemberResponse {
  employeeId: string;
  employeeName: string;
  /**
   * Nguồn sự thật để ẩn/hiện nút. ĐỪNG đoán quyền từ mã lỗi: người ngoài project nhận
   * 404 chứ không phải 403 (ADR-006/019), nên 404 không nói lên điều gì về quyền.
   */
  roleInProject: RoleInProject;
  invitationStatus: InvitationStatus;
  joinedDate: string | null;
}

export interface ProjectDetailResponse {
  id: string;
  name: string;
  description: string;
  status: Status;
  expectedCompletionDate: string;
  members: ProjectMemberResponse[];
  /**
   * Chuỗi base64. BẮT BUỘC gửi lại nguyên vẹn khi `PUT /projects/{id}` (ADR-016);
   * gửi giá trị cũ -> 409, phải tải lại rồi thử lại.
   */
  rowVersion: string;
}

export interface CreateProjectRequest {
  name: string;
  /** Gửi `""` chứ KHÔNG gửi `null` — `ProjectService` gọi `.Trim()` trên trường này. */
  description: string;
  /** ISO 8601. Backend bắt buộc phải ở tương lai. */
  expectedCompletionDate: string;
}

/** ADR-021: `rowVersion` bắt buộc ở đây, nhưng KHÔNG cần khi đổi status hay đổi sprint. */
export interface UpdateProjectRequest extends CreateProjectRequest {
  rowVersion: string;
}

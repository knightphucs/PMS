/** Soi gương `PMS.Application/Features/Projects/ProjectDtos.cs`. */

import type { InvitationStatus, RoleInProject, Status } from './enums';


/** Kiểu phần tử của `GET /projects` (danh sách). */
export interface ProjectSummaryResponse {
  id: string;
  name: string;
  status: Status;
  expectedCompletionDate: string;
  /**
   * Vai trò của **người đang đăng nhập** trong project này, không phải thuộc tính của
   * project. Dùng để ẩn/hiện nút Sửa/Xóa mà không phải gọi thêm endpoint thành viên cho
   * từng dòng. Xem §10 — đừng đoán quyền từ mã lỗi.
   */
  roleInProject: RoleInProject;
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

export interface InviteMemberRequest {
  /** Mời bằng EMAIL, không phải id. 404 nếu email chưa có tài khoản — cố ý không tự tạo hộ. */
  email: string;
  role: RoleInProject;
}

export interface ChangeMemberRoleRequest {
  role: RoleInProject;
}

/** Kiểu phần tử của `GET /projects/invitations` — lời mời đang chờ TÔI phản hồi. */
export interface MyInvitationResponse {
  projectId: string;
  projectName: string;
  /** Vai trò được mời vào, chưa có hiệu lực cho tới khi chấp nhận. */
  role: RoleInProject;
  invitedAt: string;
}

/**
 * Mời một email qua LINK gửi bằng email — khác {@link InviteMemberRequest}, hoạt động cả
 * khi email chưa có tài khoản trong hệ thống. Xem `POST /projects/{id}/members/invitations`.
 */
export interface InviteExternalRequest {
  email: string;
  role: RoleInProject;
}

/** Trả về sau khi tạo lời mời qua email — KHÔNG mang token, token chỉ nằm trong nội dung email. */
export interface ExternalInvitationResponse {
  id: string;
  projectId: string;
  email: string;
  role: RoleInProject;
  expiresAt: string;
}

/** Xem trước một lời mời từ token trong link — `GET /invitations/{token}`, public. */
export interface InvitationPreviewResponse {
  projectId: string;
  projectName: string;
  email: string;
  role: RoleInProject;
  expiresAt: string;
}

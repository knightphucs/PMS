import type {
  ChangeMemberRoleRequest,
  ExternalInvitationResponse,
  InviteExternalRequest,
  InviteMemberRequest,
  MyInvitationResponse,
  ProjectMemberResponse,
} from '@/types/project';

import { apiFetch } from '../http';

/**
 * 🔴 Swagger của controller này NÓI DỐI ở bốn chỗ. Các kiểu dưới đây lấy theo **chữ ký
 * method C#**, không theo attribute `[ProducesResponseType]`. Đừng "sửa" lại theo Swagger:
 *
 *   1. `GET /{id}/members`      attribute nói `PagedResult<>`, thực tế là **mảng trần**
 *   2. `GET /projects/invitations` attribute nói `PagedResult<ProjectMemberResponse>`,
 *                                thực tế là `MyInvitationResponse[]`
 *   3. `PUT .../role`           attribute nói `ProjectDetailResponse`, thực tế là
 *                                `ProjectMemberResponse`
 *   4. `accept` / `decline`     attribute nói 201, thực tế trả **200**
 *
 * Type theo attribute thì biên dịch vẫn qua và chỉ vỡ lúc chạy với `undefined`.
 */

export function listMembers(projectId: string, signal?: AbortSignal) {
  return apiFetch<ProjectMemberResponse[]>(`/projects/${projectId}/members`, { signal });
}

/**
 * Mời bằng email.
 * - **404** email chưa có tài khoản (cố ý không tạo hộ, tránh tài khoản rác không ai sở hữu)
 * - **400** tự mời chính mình
 * - **409** đã là thành viên hoặc đang chờ phản hồi
 *
 * Mời lại người đã `Declined` thì ĐƯỢC, và trạng thái quay về `Pending`.
 */
export function inviteMember(projectId: string, body: InviteMemberRequest) {
  return apiFetch<ProjectMemberResponse>(`/projects/${projectId}/members`, {
    method: 'POST',
    body,
  });
}

/**
 * Mời qua LINK gửi bằng email — khác {@link inviteMember}, hoạt động cả khi email chưa
 * có tài khoản. Không thêm ngay: người nhận phải bấm link rồi đăng nhập/đăng ký mới vào.
 *
 * - **400** tự mời chính mình, hoặc token không hợp lệ (không áp dụng ở đây)
 * - **409** email đã ứng với một tài khoản đang là thành viên/đang chờ phản hồi trong project
 *
 * Mời lại cùng email khi đang có lời mời Pending sẽ vô hiệu lời mời cũ (coi như resend).
 */
export function inviteExternalMember(projectId: string, body: InviteExternalRequest) {
  return apiFetch<ExternalInvitationResponse>(`/projects/${projectId}/members/invitations`, {
    method: 'POST',
    body,
  });
}

/**
 * Đổi vai trò. Đổi sang đúng vai trò đang có là no-op, vẫn trả 200.
 *
 * **409** khi hạ xuống `Viewer` mà người đó còn task chưa `Done`, hoặc khi hạ vai trò
 * của `ProjectManager` cuối cùng.
 */
export function changeMemberRole(
  projectId: string,
  employeeId: string,
  body: ChangeMemberRoleRequest,
) {
  return apiFetch<ProjectMemberResponse>(
    `/projects/${projectId}/members/${employeeId}/role`,
    { method: 'PUT', body },
  );
}

/**
 * Gỡ thành viên — kiêm luôn "rời dự án" khi `employeeId` là chính mình.
 *
 * **409** nếu người đó còn task chưa `Done`, hoặc là `ProjectManager` cuối cùng.
 * ⚠️ Người mới chỉ `Pending` gọi endpoint này nhận **404** — họ phải dùng `/decline`.
 * ⚠️ Sau khi TỰ rời, mọi `GET` trên project đó trả 404 (ADR-006/019) nên phải điều
 * hướng đi ngay, đừng để trang tự tải lại rồi hiện "không tìm thấy".
 */
export function removeMember(projectId: string, employeeId: string) {
  return apiFetch<void>(`/projects/${projectId}/members/${employeeId}`, { method: 'DELETE' });
}

/** Lời mời đang chờ TÔI phản hồi. Mảng trần. */
export function listMyInvitations(signal?: AbortSignal) {
  return apiFetch<MyInvitationResponse[]>('/projects/invitations', { signal });
}

export function acceptInvitation(projectId: string) {
  return apiFetch<ProjectMemberResponse>(`/projects/${projectId}/members/me/accept`, {
    method: 'POST',
  });
}

export function declineInvitation(projectId: string) {
  return apiFetch<ProjectMemberResponse>(`/projects/${projectId}/members/me/decline`, {
    method: 'POST',
  });
}

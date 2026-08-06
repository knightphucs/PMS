import type { InvitationPreviewResponse, ProjectMemberResponse } from '@/types/project';

import { apiFetch } from '../http';

/**
 * Lời mời project qua token trong link (`/invitations/{token}`) — tách khỏi `members.ts`
 * vì đây là `InvitationsController` bên backend, không phải `ProjectMembersController`, và
 * {@link getInvitationPreview} PHẢI gọi được khi chưa đăng nhập.
 */

/**
 * Xem trước một lời mời — public, không cần đăng nhập. `anonymous: true` để không kích
 * hoạt vòng refresh token vô ích khi khách chưa từng đăng nhập.
 *
 * Token hỏng/hết hạn/đã dùng đều trả **400** với cùng một thông điệp chung — không phân
 * biệt để tránh lộ thông tin.
 */
export function getInvitationPreview(token: string, signal?: AbortSignal) {
  return apiFetch<InvitationPreviewResponse>(`/invitations/${token}`, {
    anonymous: true,
    signal,
  });
}

/**
 * Chấp nhận lời mời — cần đăng nhập, và email tài khoản đang đăng nhập phải KHỚP email
 * được mời (**403** nếu lệch). Tạo thành viên Accepted ngay, không cần bước accept-trong-app
 * thứ hai vì việc đăng nhập đúng email đã là bước xác minh.
 */
export function acceptExternalInvitation(token: string) {
  return apiFetch<ProjectMemberResponse>(`/invitations/${token}/accept`, { method: 'POST' });
}

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  acceptInvitation,
  changeMemberRole,
  declineInvitation,
  inviteMember,
  listMembers,
  listMyInvitations,
  removeMember,
} from '@/lib/api/endpoints/members';
import { invitationKeys, memberKeys } from '@/lib/hooks/keys';
import { projectKeys } from '@/lib/hooks/use-projects';
import type { ChangeMemberRoleRequest, InviteMemberRequest } from '@/types/project';

export function useMembers(projectId: string) {
  return useQuery({
    queryKey: memberKeys.all(projectId),
    queryFn: ({ signal }) => listMembers(projectId, signal),
  });
}

/**
 * Danh sách thành viên đổi thì `projectKeys.overview` cũng cũ theo — `useMyProjectRole`
 * đọc vai trò từ đó. Không làm mới cả hai thì tự hạ vai trò của mình xong nút vẫn còn.
 */
function invalidateMembership(
  queryClient: ReturnType<typeof useQueryClient>,
  projectId: string,
) {
  void queryClient.invalidateQueries({ queryKey: memberKeys.all(projectId) });
  void queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) });
}

export function useInviteMember(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: InviteMemberRequest) => inviteMember(projectId, body),
    onSuccess: () => invalidateMembership(queryClient, projectId),
  });
}

export function useChangeMemberRole(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ employeeId, ...body }: ChangeMemberRoleRequest & { employeeId: string }) =>
      changeMemberRole(projectId, employeeId, body),
    onSuccess: () => invalidateMembership(queryClient, projectId),
  });
}

/**
 * Gỡ thành viên — kiêm "rời dự án" khi `employeeId` là chính mình.
 *
 * ⚠️ Khi TỰ rời, nơi gọi phải `router.replace('/projects')` NGAY. Lời gọi `GET` tiếp theo
 * trên project đó trả 404 một cách cố ý (ADR-006/019), nên để trang tự làm mới sẽ hiện
 * "không tìm thấy dữ liệu" — đúng kỹ thuật nhưng trông như lỗi.
 */
export function useRemoveMember(projectId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (employeeId: string) => removeMember(projectId, employeeId),
    onSuccess: () => {
      invalidateMembership(queryClient, projectId);
      // Tự rời thì project biến mất khỏi danh sách của mình.
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
  });
}

export function useMyInvitations() {
  return useQuery({
    queryKey: invitationKeys.all,
    queryFn: ({ signal }) => listMyInvitations(signal),
  });
}

export function useRespondToInvitation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, accept }: { projectId: string; accept: boolean }) =>
      accept ? acceptInvitation(projectId) : declineInvitation(projectId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: invitationKeys.all });
      // Chấp nhận lời mời làm project xuất hiện trong danh sách của mình.
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
  });
}

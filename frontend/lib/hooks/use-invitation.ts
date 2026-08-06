'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { acceptExternalInvitation, getInvitationPreview } from '@/lib/api/endpoints/invitations';
import { invitationPreviewKeys } from '@/lib/hooks/keys';
import { projectKeys } from '@/lib/hooks/use-projects';

/** Xem trước lời mời từ token trong link — public, chạy được cả khi chưa đăng nhập. */
export function useInvitationPreview(token: string) {
  return useQuery({
    queryKey: invitationPreviewKeys.detail(token),
    queryFn: ({ signal }) => getInvitationPreview(token, signal),
    // Token dùng một lần: preview hỏng thì hỏng luôn (đã dùng/hết hạn), retry không giúp gì.
    retry: false,
  });
}

export function useAcceptInvitation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (token: string) => acceptExternalInvitation(token),
    onSuccess: () => {
      // Chấp nhận xong thì project mới xuất hiện trong danh sách "project của tôi".
      void queryClient.invalidateQueries({ queryKey: projectKeys.all });
    },
  });
}

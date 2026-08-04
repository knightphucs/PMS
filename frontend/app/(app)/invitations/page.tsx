'use client';

import { MailOpenIcon } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import {
  InvitationList,
  InvitationListSkeleton,
} from '@/components/invitations/invitation-list';
import { errorMessage } from '@/lib/api/problem';
import { useMyInvitations, useRespondToInvitation } from '@/lib/hooks/use-members';
import type { MyInvitationResponse } from '@/types/project';

/**
 * "Lời mời của tôi" — mảnh còn thiếu khiến luồng mời thành viên chưa khép kín: PM mời
 * được từ tab Thành viên, nhưng trước trang này người được mời không có chỗ nào để bấm
 * Chấp nhận.
 *
 * Trang nằm NGOÀI `projects/[id]` vì lời mời là xuyên project — và cũng vì người được
 * mời chưa có quyền gì trên project đó: mọi request lên `/projects/{id}` còn trả 404 cho
 * tới khi lời mời được chấp nhận (ADR-006/019).
 */
export default function InvitationsPage() {
  const router = useRouter();
  const invitations = useMyInvitations();
  const respond = useRespondToInvitation();

  const [declining, setDeclining] = useState<MyInvitationResponse | null>(null);
  const [declineError, setDeclineError] = useState<string | null>(null);
  const [pendingProjectId, setPendingProjectId] = useState<string | null>(null);

  const handleAccept = async (invitation: MyInvitationResponse) => {
    setPendingProjectId(invitation.projectId);

    try {
      await respond.mutateAsync({ projectId: invitation.projectId, accept: true });
      toast.success(`Đã tham gia "${invitation.projectName}".`);
      router.push(`/projects/${invitation.projectId}/board`);
    } catch (error) {
      // 409 = lời mời đã được trả lời ở nơi khác (tab thứ hai, hoặc PM đã gỡ). Thông điệp
      // của backend nói đúng chuyện đó rồi; việc của trang là làm mới cho khớp thực tế.
      toast.error(errorMessage(error));
      void invitations.refetch();
    } finally {
      setPendingProjectId(null);
    }
  };

  const handleDecline = async () => {
    if (!declining) return;
    setDeclineError(null);
    setPendingProjectId(declining.projectId);

    try {
      await respond.mutateAsync({ projectId: declining.projectId, accept: false });
      toast.success(`Đã từ chối lời mời vào "${declining.projectName}".`);
      setDeclining(null);
    } catch (error) {
      setDeclineError(errorMessage(error));
    } finally {
      setPendingProjectId(null);
    }
  };

  return (
    <div className="grid gap-4">
      <PageHeader
        title="Lời mời của tôi"
        count={invitations.data?.length}
        description="Những dự án đang mời bạn tham gia. Chấp nhận rồi mới thấy được nội dung bên trong."
      />

      {invitations.isError ? (
        <QueryError
          title="Không tải được danh sách lời mời"
          error={invitations.error}
          onRetry={() => void invitations.refetch()}
          isRetrying={invitations.isFetching}
        />
      ) : invitations.isPending ? (
        <InvitationListSkeleton />
      ) : invitations.data.length === 0 ? (
        <EmptyState
          icon={<MailOpenIcon className="size-8" />}
          title="Không có lời mời nào"
          description="Khi ai đó mời bạn vào dự án của họ, lời mời sẽ xuất hiện ở đây."
        />
      ) : (
        <InvitationList
          invitations={invitations.data}
          pendingProjectId={pendingProjectId}
          onAccept={(invitation) => void handleAccept(invitation)}
          onDecline={setDeclining}
        />
      )}

      <ConfirmDialog
        open={declining !== null}
        title="Từ chối lời mời?"
        description={
          <>
            Bạn sẽ không tham gia dự án{' '}
            <strong className="text-foreground">{declining?.projectName}</strong>. Muốn vào
            lại thì phải được mời lần nữa.
          </>
        }
        confirmLabel="Từ chối"
        pendingLabel="Đang gửi…"
        error={declineError}
        isPending={respond.isPending}
        onConfirm={() => void handleDecline()}
        onClose={() => {
          setDeclining(null);
          setDeclineError(null);
        }}
      />
    </div>
  );
}

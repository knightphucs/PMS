'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { errorMessage } from '@/lib/api/problem';
import { useRemoveMember } from '@/lib/hooks/use-members';
import type { ProjectMemberResponse } from '@/types/project';

export function RemoveMemberDialog({
  projectId,
  member,
  isMe,
  onClose,
}: {
  projectId: string;
  member: ProjectMemberResponse | null;
  isMe: boolean;
  onClose: () => void;
}) {
  const router = useRouter();
  const removeMember = useRemoveMember(projectId);
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = async () => {
    if (!member) return;
    setError(null);

    try {
      await removeMember.mutateAsync(member.employeeId);

      if (isMe) {
        toast.success('Bạn đã rời khỏi dự án.');
        // 🔴 Phải rời trang NGAY. Lời gọi GET tiếp theo trên project này trả 404 một cách
        // CỐ Ý (ADR-006/019) vì ta không còn là thành viên — để trang tự làm mới sẽ hiện
        // "không tìm thấy dữ liệu", đúng kỹ thuật nhưng trông hệt như lỗi.
        router.replace('/projects');
        return;
      }

      toast.success(`Đã gỡ ${member.employeeName} khỏi dự án.`);
      onClose();
    } catch (err) {
      // 409: người này còn task chưa hoàn thành, hoặc là quản lý dự án cuối cùng.
      setError(errorMessage(err));
    }
  };

  return (
    <ConfirmDialog
      open={member !== null}
      title={isMe ? 'Rời khỏi dự án?' : 'Gỡ thành viên?'}
      description={
        isMe ? (
          <>
            Bạn sẽ mất quyền truy cập dự án này và phải được mời lại để quay vào.
          </>
        ) : (
          <>
            <strong className="text-foreground">{member?.employeeName}</strong> sẽ mất
            quyền truy cập dự án này.
          </>
        )
      }
      confirmLabel={isMe ? 'Rời dự án' : 'Gỡ thành viên'}
      pendingLabel={isMe ? 'Đang rời…' : 'Đang gỡ…'}
      error={error}
      isPending={removeMember.isPending}
      onConfirm={handleConfirm}
      onClose={() => {
        setError(null);
        onClose();
      }}
    />
  );
}

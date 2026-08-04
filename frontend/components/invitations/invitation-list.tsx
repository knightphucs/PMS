'use client';

import { CheckIcon, XIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDate } from '@/lib/format';
import { ROLE_IN_PROJECT_LABEL } from '@/types/enums';
import type { MyInvitationResponse } from '@/types/project';

export function InvitationList({
  invitations,
  pendingProjectId,
  onAccept,
  onDecline,
}: {
  invitations: MyInvitationResponse[];
  /** Project đang có request bay — khóa đúng thẻ đó chứ không khóa cả danh sách. */
  pendingProjectId: string | null;
  onAccept: (invitation: MyInvitationResponse) => void;
  onDecline: (invitation: MyInvitationResponse) => void;
}) {
  return (
    <div className="grid gap-2">
      {invitations.map((invitation) => {
        const isPending = pendingProjectId === invitation.projectId;

        return (
          <div
            key={invitation.projectId}
            className="bg-card flex flex-wrap items-center gap-x-4 gap-y-3 rounded-lg border p-3"
          >
            <div className="min-w-52 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                {/* Chưa chấp nhận thì CHƯA phải thành viên — mọi request lên project này
                    còn trả 404 (ADR-006/019). Nên tên project ở đây là chữ, không phải
                    link: link sẽ dẫn thẳng tới một trang "không tìm thấy". */}
                <span className="text-[15px] font-medium">{invitation.projectName}</span>
                <Badge variant="secondary" className="font-medium">
                  {ROLE_IN_PROJECT_LABEL[invitation.role]}
                </Badge>
              </div>
              {/* Dữ liệu cũ có `invitedAt` là mốc canh `0001-01-01` (xem `isSentinelDate`
                  trong lib/format.ts) — khi đó `formatDate` trả '—' và cả dòng này bị bỏ
                  hẳn, thay vì hiện "Được mời ngày —". */}
              {formatDate(invitation.invitedAt) !== '—' ? (
                <p className="text-muted-foreground mt-0.5 text-xs">
                  Được mời ngày {formatDate(invitation.invitedAt)}
                </p>
              ) : null}
            </div>

            <div className="flex items-center gap-2">
              <Button
                size="sm"
                disabled={isPending}
                onClick={() => onAccept(invitation)}
              >
                <CheckIcon className="size-4" />
                Chấp nhận
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={isPending}
                onClick={() => onDecline(invitation)}
              >
                <XIcon className="size-4" />
                Từ chối
              </Button>
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function InvitationListSkeleton({ rows = 2 }: { rows?: number }) {
  return (
    <div className="grid gap-2" aria-busy="true">
      <span className="sr-only">Đang tải danh sách lời mời…</span>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="bg-card flex items-center gap-4 rounded-lg border p-3">
          <div className="grid flex-1 gap-1.5">
            <Skeleton className="h-4 w-52" />
            <Skeleton className="h-3.5 w-36" />
          </div>
          <Skeleton className="h-8 w-28" />
          <Skeleton className="h-8 w-24" />
        </div>
      ))}
    </div>
  );
}

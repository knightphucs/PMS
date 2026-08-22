'use client';

import { CheckIcon, ClockIcon, ShieldCheckIcon, XIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { QueryError } from '@/components/common/query-error';
import { UserAvatar } from '@/components/common/user-avatar';
import { TaskSection } from '@/components/tasks/task-section';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useCancelApproval, useDecideApproval, useTaskApprovals } from '@/lib/hooks/use-approvals';
import { formatDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { ApprovalResponse, ApprovalStatus } from '@/types/approval';

/**
 * Khối "Phê duyệt" ở chi tiết task (ADR-062).
 *
 * 🔴 **Khối này TỰ ẨN hoàn toàn** khi loại việc của task không có cổng duyệt nào và task
 * cũng chưa từng đi qua cổng nào (luật 3 của Doctrine §0 — tiền lệ đã chạy: khối trường tuỳ
 * biến tự ẩn khi project chưa khai trường). Một khối trống mang tiêu đề "Phê duyệt" trên mọi
 * task của mọi project chưa dùng tính năng là nhiễu thuần tuý.
 *
 * 📌 **Không có nút "Gửi duyệt", và đó là chủ ý.** Yêu cầu duyệt tự sinh khi người dùng kéo
 * task vào cột có cổng (ADR-062 quyết định a). Khối này chỉ là nơi *quyết định* và *đọc lại
 * lịch sử* — hai việc mà một cái nút gửi không giúp được gì.
 */
export function ApprovalPanel({
  projectId,
  taskId,
  myEmployeeId,
}: {
  projectId: string;
  taskId: string;
  /** Cần để phân biệt "bạn đã ký rồi" với "bạn không phải người ký" — hai tình huống khác hẳn. */
  myEmployeeId: string | null;
}) {
  const approvals = useTaskApprovals(projectId, taskId);

  if (approvals.isPending) {
    return (
      <TaskSection title="Phê duyệt">
        <Skeleton className="h-16 w-full" />
      </TaskSection>
    );
  }

  if (approvals.isError) {
    return (
      <TaskSection title="Phê duyệt">
        <QueryError
          title="Không tải được trạng thái duyệt"
          error={approvals.error}
          onRetry={() => void approvals.refetch()}
        />
      </TaskSection>
    );
  }

  const { hasGate, active, history } = approvals.data;

  // Tự ẩn: không có cổng nào áp lên loại việc này, và cũng không có lịch sử nào để đọc.
  if (!hasGate && history.length === 0) return null;

  const past = history.filter((a) => a.id !== active?.id);

  return (
    <TaskSection title="Phê duyệt">
      {active ? (
        <ActiveApproval
          projectId={projectId}
          taskId={taskId}
          approval={active}
          myEmployeeId={myEmployeeId}
        />
      ) : (
        <p className="text-muted-foreground bg-muted/40 rounded-lg border border-dashed px-3 py-2.5 text-sm">
          Task này chưa có yêu cầu duyệt nào đang chờ. Yêu cầu sẽ được gửi tự động khi bạn
          chuyển task sang cột cần duyệt.
        </p>
      )}

      {past.length > 0 ? (
        <div className="grid gap-2">
          <h3 className="text-muted-foreground text-xs font-medium">Lịch sử duyệt</h3>
          {past.map((approval) => (
            <PastApproval key={approval.id} approval={approval} />
          ))}
        </div>
      ) : null}
    </TaskSection>
  );
}

function ActiveApproval({
  projectId,
  taskId,
  approval,
  myEmployeeId,
}: {
  projectId: string;
  taskId: string;
  approval: ApprovalResponse;
  myEmployeeId: string | null;
}) {
  const [comment, setComment] = useState('');
  const decide = useDecideApproval(projectId, taskId);
  const cancel = useCancelApproval(projectId, taskId);
  const isBusy = decide.isPending || cancel.isPending;

  const submit = (decision: 'Approve' | 'Reject') => {
    decide.mutate(
      { approvalId: approval.id, body: { decision, comment: comment.trim() || null } },
      {
        onSuccess: () => {
          setComment('');
          toast.success(decision === 'Approve' ? 'Đã duyệt.' : 'Đã từ chối.');
        },
        onError: (error: unknown) => toast.error(errorMessage(error)),
      },
    );
  };

  return (
    <div className="bg-card grid gap-3 rounded-lg border p-3.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="grid gap-0.5">
          <div className="flex items-center gap-2 text-sm font-medium">
            <StatusBadge status={approval.status} />
            <span className="text-muted-foreground">
              để vào &ldquo;{approval.targetColumnName}&rdquo;
            </span>
          </div>
          <p className="text-muted-foreground text-xs">
            {approval.requestedByName} gửi {formatDate(approval.requestedAt)} · đã có{' '}
            <span className="tabular-nums">
              {approval.approveCount}/{approval.minApprovals}
            </span>{' '}
            lượt duyệt
          </p>
        </div>

        {approval.canCancel ? (
          <Button
            variant="ghost"
            size="sm"
            disabled={isBusy}
            onClick={() =>
              cancel.mutate(approval.id, {
                onSuccess: () => toast.success('Đã huỷ yêu cầu duyệt.'),
                onError: (error: unknown) => toast.error(errorMessage(error)),
              })
            }
          >
            Huỷ yêu cầu
          </Button>
        ) : null}
      </div>

      {approval.decisions.length > 0 ? <DecisionList approval={approval} /> : null}

      {approval.canDecide ? (
        <div className="grid gap-2 border-t pt-3">
          <Input
            value={comment}
            onChange={(event) => setComment(event.target.value)}
            placeholder="Lý do (không bắt buộc, nhưng nên có khi từ chối)"
            maxLength={1000}
            disabled={isBusy}
          />
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={isBusy} onClick={() => submit('Approve')}>
              <CheckIcon className="size-4" />
              Duyệt
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={isBusy}
              onClick={() => submit('Reject')}
            >
              <XIcon className="size-4" />
              Từ chối
            </Button>
          </div>
        </div>
      ) : null}

      {/* Nói rõ vì sao KHÔNG có nút, thay vì hiện một nút xám. Người bị chặn vì đã bỏ phiếu
          và người bị chặn vì không có quyền là hai tình huống khác nhau hẳn.
          ⚠️ So với CHÍNH MÌNH (`myEmployeeId`), không phải với người gửi yêu cầu — hai thứ
          đó trùng nhau đủ thường để một bản sai vẫn trông đúng khi thử qua loa. */}
      {!approval.canDecide && approval.status === 'Pending' ? (
        <p className="text-muted-foreground border-t pt-3 text-xs">
          {myEmployeeId !== null && approval.decisions.some((d) => d.approverId === myEmployeeId)
            ? 'Bạn đã quyết định rồi — đang chờ những người duyệt còn lại.'
            : 'Bạn không nằm trong danh sách người duyệt của luật này.'}
        </p>
      ) : null}

      {approval.status === 'Rejected' ? (
        <p className="text-muted-foreground border-t pt-3 text-xs">
          Yêu cầu đã bị từ chối và vẫn đang chặn cột này. Huỷ yêu cầu rồi chuyển cột lần nữa
          nếu muốn gửi lại.
        </p>
      ) : null}
    </div>
  );
}

function PastApproval({ approval }: { approval: ApprovalResponse }) {
  return (
    <div className="bg-muted/40 grid gap-1.5 rounded-lg border px-3 py-2">
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <StatusBadge status={approval.status} />
        <span className="text-muted-foreground">
          &ldquo;{approval.targetColumnName}&rdquo; · {approval.requestedByName} gửi{' '}
          {formatDate(approval.requestedAt)}
        </span>
      </div>
      {approval.decisions.length > 0 ? <DecisionList approval={approval} /> : null}
    </div>
  );
}

function DecisionList({ approval }: { approval: ApprovalResponse }) {
  return (
    <ul className="grid gap-1.5">
      {approval.decisions.map((decision) => (
        <li key={decision.id} className="flex min-w-0 items-start gap-2 text-xs">
          <UserAvatar
            id={decision.approverId}
            name={decision.approverName}
            className="size-5 shrink-0"
          />
          <div className="min-w-0">
            <span className="font-medium">{decision.approverName}</span>{' '}
            <span
              className={cn(
                'font-medium',
                decision.decision === 'Approve' ? 'text-emerald-600' : 'text-destructive',
              )}
            >
              {decision.decision === 'Approve' ? 'đã duyệt' : 'đã từ chối'}
            </span>{' '}
            <span className="text-muted-foreground">{formatDate(decision.decidedAt)}</span>
            {decision.comment ? (
              <p className="text-muted-foreground break-words">{decision.comment}</p>
            ) : null}
          </div>
        </li>
      ))}
    </ul>
  );
}

const STATUS_LABEL: Record<ApprovalStatus, string> = {
  Pending: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Bị từ chối',
  Cancelled: 'Đã huỷ',
};

const STATUS_TONE: Record<ApprovalStatus, string> = {
  Pending: 'bg-amber-500/10 text-amber-700 dark:text-amber-400',
  Approved: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400',
  Rejected: 'bg-destructive/10 text-destructive',
  Cancelled: 'bg-muted text-muted-foreground',
};

function StatusBadge({ status }: { status: ApprovalStatus }) {
  const Icon =
    status === 'Approved' ? ShieldCheckIcon : status === 'Rejected' ? XIcon : ClockIcon;

  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium',
        STATUS_TONE[status],
      )}
    >
      <Icon className="size-3.5" />
      {STATUS_LABEL[status]}
    </span>
  );
}

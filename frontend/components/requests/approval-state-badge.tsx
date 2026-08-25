import { CircleCheckIcon, CircleMinusIcon, CircleSlashIcon, ClockIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { TASK_APPROVAL_STATE_LABEL, type TaskApprovalState } from '@/types/saved-view';

/**
 * Trạng thái duyệt của một yêu cầu, nhìn từ phía NGƯỜI GỬI (ADR-063).
 *
 * 🔴 `None` **tự ẩn** thay vì hiện "Không cần duyệt" — luật 3 của Doctrine (§0). Phần lớn
 * yêu cầu trong hệ thống không đi qua cổng duyệt nào, nên một huy hiệu nói "không cần" trên
 * mọi hàng là nhiễu thuần tuý: nó chiếm chỗ để thông báo một điều không xảy ra.
 *
 * ⚠️ `Rejected` KHÔNG phải một kết thúc — lời từ chối vẫn đang CHẶN cho tới khi có người
 * huỷ tường minh (ADR-062 quyết định c). Vì vậy nó dùng tông cảnh báo, không phải tông
 * "đã đóng sổ".
 */
export function ApprovalStateBadge({
  state,
  className,
}: {
  state: TaskApprovalState;
  className?: string;
}) {
  if (state === 'None') return null;

  const tone = {
    Pending: {
      Icon: ClockIcon,
      cls: 'bg-amber-500/15 text-amber-600 dark:text-amber-400',
    },
    Approved: {
      Icon: CircleCheckIcon,
      cls: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400',
    },
    Rejected: {
      Icon: CircleSlashIcon,
      cls: 'bg-red-500/15 text-red-600 dark:text-red-400',
    },
  }[state];

  const Icon = tone?.Icon ?? CircleMinusIcon;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium',
        tone?.cls,
        className,
      )}
    >
      <Icon className="size-3.5 shrink-0" aria-hidden />
      {TASK_APPROVAL_STATE_LABEL[state]}
    </span>
  );
}

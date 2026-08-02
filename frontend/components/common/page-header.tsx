import { cn } from '@/lib/utils';

/**
 * ═══════════════════════════════════════════════════════════════════════════════
 * THANG CỠ CHỮ VÀ MẬT ĐỘ — luật chung cho mọi màn hình
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Jira/Linear DÀY, không thoáng. Trước phiên này app rải `text-sm` khắp nơi và không có
 * thang nào cả, nên mỗi màn hình lại có một mật độ khác.
 *
 *   Tiêu đề trang     text-xl  font-semibold tracking-tight
 *   Tiêu đề mục       text-sm  font-semibold
 *   Thân bảng         text-[13px]
 *   Phụ đề / mô tả    text-sm  text-muted-foreground
 *   Nhãn phụ, meta    text-xs  text-muted-foreground
 *
 *   Dòng bảng         [&_td]:px-3 [&_td]:py-2 [&_th]:h-9 [&_th]:px-3   → ~38px
 *   Thẻ Kanban        p-2.5, tên text-[13px] leading-snug line-clamp-2
 *   Khe giữa cột      gap-3, đệm trong cột p-2
 *
 * Ghi ở đây vì đây là component mà mọi trang mới đều import — đọc một lần là thấy.
 */
export function PageHeader({
  title,
  count,
  description,
  actions,
  className,
}: {
  title: React.ReactNode;
  /** Hiện trong pill cạnh tiêu đề. Bỏ qua khi chưa tải xong để tránh nháy 0 → N. */
  count?: number;
  description?: React.ReactNode;
  actions?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn('flex flex-wrap items-center justify-between gap-4', className)}>
      <div className="grid gap-1">
        <div className="flex items-center gap-2.5">
          <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
          {count !== undefined ? (
            <span className="bg-muted text-muted-foreground rounded-full px-2.5 py-0.5 text-xs font-medium tabular-nums">
              {count}
            </span>
          ) : null}
        </div>
        {description ? <p className="text-muted-foreground text-sm">{description}</p> : null}
      </div>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  );
}

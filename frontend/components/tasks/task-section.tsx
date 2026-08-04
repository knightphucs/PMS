'use client';

import { cn } from '@/lib/utils';

/**
 * Khối nội dung ở cột trái của chi tiết task.
 *
 * Tồn tại để năm khối (mô tả, subtask, đính kèm, liên kết, thảo luận) có **một** định
 * nghĩa về tiêu đề, khoảng cách và chỗ đặt nút hành động — thay vì năm bản chép tay chắc
 * chắn sẽ trôi khỏi nhau.
 */
export function TaskSection({
  title,
  count,
  actions,
  children,
  className,
}: {
  title: string;
  /** Hiện trong pill cạnh tiêu đề. Bỏ qua khi chưa tải xong để tránh nháy 0 → N. */
  count?: number;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    // 🔴 Hai lớp chống tràn ngang, cả hai đều cần thiết — đây là chỗ rò rỉ đã làm tràn cả
    // màn chi tiết Task khi mô tả chứa một URL không có dấu cách (đo được: cột trái 632px
    // mà khối này phồng lên 1165px).
    //
    // 1. `min-w-0` — `<section>` là grid item của cột trái, mà grid item mặc định có
    //    `min-width:auto`, tức nó nở theo nội dung dài nhất thay vì co về bề rộng cột.
    //
    // 2. `grid-cols-[minmax(0,1fr)]` — khai báo cột TƯỜNG MINH thay vì để grid tự sinh cột
    //    ẩn `auto`. Cột ẩn `auto` có sàn là min-content của nội dung, và đây là cái bẫy
    //    thật sự: `break-words` (`overflow-wrap: break-word`) cho phép ngắt để KHỎI tràn
    //    nhưng **không hề làm giảm min-content**, nên track vẫn phồng và chữ vẫn không có
    //    bề rộng hữu hạn nào để ngắt theo. Sửa ở đây một lần là xong cho cả năm khối
    //    (mô tả, subtask, đính kèm, liên kết, thảo luận) thay vì vá từng thẻ <p>.
    <section className={cn('grid min-w-0 grid-cols-[minmax(0,1fr)] gap-2.5', className)}>
      <div className="flex min-h-8 flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          {title}
          {count !== undefined ? (
            <span className="bg-muted text-muted-foreground rounded-full px-2 py-0.5 text-xs font-medium tabular-nums">
              {count}
            </span>
          ) : null}
        </h2>
        {actions}
      </div>
      {children}
    </section>
  );
}

/** Hàng nhãn–giá trị của cột phải. Chỉ là bố cục, không có logic. */
export function TaskFieldRow({
  label,
  children,
  align = 'center',
}: {
  label: string;
  children: React.ReactNode;
  /** `start` cho ô nhiều dòng (nhãn, người theo dõi) để nhãn không trôi xuống giữa. */
  align?: 'center' | 'start';
}) {
  return (
    <div
      className={cn(
        'grid grid-cols-[7rem_minmax(0,1fr)] gap-3',
        align === 'center' ? 'items-center' : 'items-start',
      )}
    >
      <span className="text-muted-foreground text-xs font-medium">{label}</span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}

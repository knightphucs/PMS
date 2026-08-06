import { columnChipStyle, columnDotStyle } from '@/components/tasks/status-tone';
import { cn } from '@/lib/utils';
import type { TaskStatusRef } from '@/types/task';

/**
 * Chip trạng thái của một TASK — màu lấy từ CỘT, không tra bảng enum (ADR-052).
 *
 * 🔴 Tách ra dùng chung vì sau ADR-052 nó xuất hiện ở bốn chỗ (chi tiết task, backlog,
 * subtask, ô chọn trạng thái) và cả bốn phải hiện giống hệt nhau. Trước đây mỗi chỗ tự tra
 * `STATUS_TONE[status]` + `STATUS_LABEL[status]` — hai lời gọi luôn đi cùng nhau nhưng
 * không có gì buộc chúng phải thế, nên chúng đã bắt đầu trôi khỏi nhau.
 *
 * ⚠️ ĐỪNG dùng cho trạng thái PROJECT: cái đó vẫn là enum bốn giá trị và có
 * `components/projects/status-badge.tsx` riêng.
 */
export function TaskStatusChip({
  status,
  className,
}: {
  status: TaskStatusRef;
  className?: string;
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-medium',
        className,
      )}
      style={columnChipStyle(status.color)}
    >
      <span className="size-1.5 shrink-0 rounded-full" style={columnDotStyle(status.color)} />
      {status.name}
    </span>
  );
}

/** Chỉ chấm màu — dùng ở danh sách subtask, nơi không đủ chỗ cho cả chip. */
export function TaskStatusDot({ status, className }: { status: TaskStatusRef; className?: string }) {
  return (
    <span
      className={cn('size-2 shrink-0 rounded-full', className)}
      style={columnDotStyle(status.color)}
      title={status.name}
    />
  );
}

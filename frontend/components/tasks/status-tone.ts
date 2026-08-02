import type { Status } from '@/types/enums';

/**
 * Một định nghĩa màu trạng thái duy nhất, dùng chung bởi `StatusBadge` (bảng dự án) và
 * header cột Kanban. Trước đây bảng màu chỉ nằm trong `status-badge.tsx`, nên cột board
 * chắc chắn sẽ trôi khỏi nó.
 *
 * Giữ bảng màu của Tailwind ở đây thay vì đổi sang biến CSS: bốn màu này đã có sẵn biến
 * thể `dark:` và đọc tốt ở cả hai chế độ, mà chúng mang nghĩa NGỮ NGHĨA cố định (xám =
 * chưa bắt đầu, xanh lá = xong) chứ không phải màu thương hiệu — đổi theo `--primary`
 * là làm mất chính thông tin đó.
 */
export const STATUS_TONE: Record<Status, { badge: string; dot: string }> = {
  ToDo: {
    badge: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
    dot: 'bg-slate-400 dark:bg-slate-500',
  },
  InProgress: {
    badge: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
    dot: 'bg-blue-500',
  },
  Review: {
    badge: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
    dot: 'bg-amber-500',
  },
  Done: {
    badge: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
    dot: 'bg-emerald-500',
  },
};

import { STATUS_TONE } from '@/components/tasks/status-tone';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { STATUS_LABEL, type Status } from '@/types/enums';

/**
 * Bảng màu đã chuyển sang `components/tasks/status-tone.ts` để header cột Kanban dùng
 * chung cùng một định nghĩa — hai nơi tự khai màu là hai nơi sẽ trôi khỏi nhau.
 * `Record<Status, …>` bên đó vẫn giữ tính chất "thêm giá trị enum là lỗi biên dịch".
 */
export function StatusBadge({ status }: { status: Status }) {
  return (
    <Badge
      variant="secondary"
      className={cn('border-0 font-medium', STATUS_TONE[status].badge)}
    >
      {STATUS_LABEL[status]}
    </Badge>
  );
}

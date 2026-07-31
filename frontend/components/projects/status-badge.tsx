import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { STATUS_LABEL, type Status } from '@/types/enums';

/**
 * Màu theo trạng thái. Bảng này phủ hết 4 giá trị của `Status` nên `Record` sẽ báo lỗi
 * biên dịch nếu backend thêm giá trị mới mà đây quên cập nhật — cùng tinh thần với
 * `ProjectPermissionsTests` bắt lỗi khi thêm `ProjectAction` mà quên khai báo.
 */
const STATUS_STYLE: Record<Status, string> = {
  ToDo: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  InProgress: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  Review: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  Done: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
};

export function StatusBadge({ status }: { status: Status }) {
  return (
    <Badge variant="secondary" className={cn('border-0 font-medium', STATUS_STYLE[status])}>
      {STATUS_LABEL[status]}
    </Badge>
  );
}

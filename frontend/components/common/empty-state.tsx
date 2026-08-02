import { cn } from '@/lib/utils';

/**
 * Trạng thái rỗng dùng chung.
 *
 * Trích ra từ `app/(app)/projects/page.tsx` — nó sắp có sáu chỗ dùng (4 cột board,
 * backlog, sprint, thành viên), chép tay từng chỗ là bảo đảm chúng lệch nhau.
 *
 * `compact` cho cột Kanban: cột hẹp và có tới bốn cột cạnh nhau nên đệm 16px của bản
 * đầy đủ chiếm hết chiều cao hữu ích.
 */
export function EmptyState({
  icon,
  title,
  description,
  action,
  compact = false,
  className,
}: {
  icon?: React.ReactNode;
  title: string;
  description?: string;
  action?: React.ReactNode;
  compact?: boolean;
  className?: string;
}) {
  return (
    <div
      className={cn(
        'grid place-items-center gap-3 rounded-lg border border-dashed text-center',
        compact ? 'px-3 py-8' : 'bg-card px-6 py-16',
        className,
      )}
    >
      {icon ? (
        <div
          className={cn(
            'bg-muted text-muted-foreground grid place-items-center rounded-full',
            compact ? 'size-9' : 'size-14',
          )}
        >
          {icon}
        </div>
      ) : null}
      <div className="grid gap-1">
        <p className={cn('font-medium', compact && 'text-sm')}>{title}</p>
        {description ? (
          <p className="text-muted-foreground max-w-md text-sm">{description}</p>
        ) : null}
      </div>
      {action ? <div className={compact ? '' : 'mt-2'}>{action}</div> : null}
    </div>
  );
}

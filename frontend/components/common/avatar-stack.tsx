import { UserAvatar } from '@/components/common/user-avatar';
import { AvatarGroup, AvatarGroupCount } from '@/components/ui/avatar';
import { cn } from '@/lib/utils';

/**
 * Avatar chồng lên nhau + `+N` khi tràn.
 *
 * Thẻ Kanban không có avatar trông như một danh sách chết — đây là tín hiệu "có người
 * trong sản phẩm này" rẻ nhất (§5.1 xếp nó thứ hai sau mã task).
 */
export function AvatarStack({
  people,
  max = 3,
  size = 'sm',
  className,
}: {
  people: readonly { employeeId: string; employeeName: string }[];
  max?: number;
  size?: 'sm' | 'default' | 'lg';
  className?: string;
}) {
  if (people.length === 0) return null;

  const shown = people.slice(0, max);
  const overflow = people.length - shown.length;

  return (
    <AvatarGroup
      className={cn('items-center', className)}
      // Danh sách đầy đủ trong title: `+2` một mình không nói cho ai biết là ai.
      title={people.map((p) => p.employeeName).join(', ')}
    >
      {shown.map((person) => (
        <UserAvatar
          key={person.employeeId}
          id={person.employeeId}
          name={person.employeeName}
          size={size}
        />
      ))}
      {overflow > 0 ? (
        <AvatarGroupCount className={cn(size === 'sm' && 'size-6 text-xs')}>
          +{overflow}
        </AvatarGroupCount>
      ) : null}
    </AvatarGroup>
  );
}

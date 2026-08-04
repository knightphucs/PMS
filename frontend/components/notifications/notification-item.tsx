'use client';

import {
  AtSignIcon,
  BellIcon,
  ClockAlertIcon,
  LogOutIcon,
  MailIcon,
  MessageSquareIcon,
  RefreshCwIcon,
  ShieldIcon,
  UserMinusIcon,
  UserPlusIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/skeleton';
import { formatRelativeTime } from '@/lib/format';
import { notificationHref } from '@/lib/notifications/navigation';
import { cn } from '@/lib/utils';
import { NOTIFICATION_TYPE_LABEL, type NotificationType } from '@/types/enums';
import type { NotificationResponse } from '@/types/notification';

/**
 * Icon chỉ để quét mắt nhanh — nó KHÔNG quyết định điều hướng (đó là việc của
 * `notificationHref`, dựa trên `relatedEntityKind`, ADR-025).
 */
const TYPE_ICON: Record<NotificationType, LucideIcon> = {
  TaskAssigned: UserPlusIcon,
  TaskUnassigned: UserMinusIcon,
  DueSoon: ClockAlertIcon,
  CommentAdded: MessageSquareIcon,
  StatusChanged: RefreshCwIcon,
  InvitedToProject: MailIcon,
  InvitationAccepted: UserPlusIcon,
  InvitationDeclined: UserMinusIcon,
  RoleChanged: ShieldIcon,
  RemovedFromProject: UserMinusIcon,
  MemberLeftProject: LogOutIcon,
  ProjectStatusChanged: RefreshCwIcon,
  Mentioned: AtSignIcon,
};

export function NotificationItem({
  notification,
  onActivate,
  compact = false,
}: {
  notification: NotificationResponse;
  /** Gọi trước khi điều hướng — nơi gọi dùng để đánh dấu đã đọc và đóng popover. */
  onActivate: (notification: NotificationResponse) => void;
  compact?: boolean;
}) {
  const Icon = TYPE_ICON[notification.type] ?? BellIcon;
  const href = notificationHref(notification);

  const body = (
    <>
      <span
        className={cn(
          'mt-0.5 grid size-7 shrink-0 place-items-center rounded-full',
          notification.isRead
            ? 'bg-muted text-muted-foreground'
            : 'bg-primary/10 text-primary',
        )}
      >
        <Icon className="size-3.5" />
      </span>

      <span className="min-w-0 flex-1">
        <span
          className={cn(
            'block text-[13px] leading-snug',
            notification.isRead ? 'text-muted-foreground' : 'font-medium',
          )}
        >
          {/* `content` do backend soạn sẵn (đã có tên người, tên task) — hiện nguyên văn. */}
          {notification.content}
        </span>
        <span className="text-muted-foreground mt-0.5 block text-xs">
          {NOTIFICATION_TYPE_LABEL[notification.type]} · {formatRelativeTime(notification.createdAt)}
        </span>
      </span>

      {/* Chấm chưa đọc: tín hiệu thứ HAI bên cạnh độ đậm của chữ. Chỉ dùng độ đậm thì
          người dùng màn hình nhỏ hoặc mắt kém không phân biệt được. */}
      {notification.isRead ? null : (
        <span
          aria-label="Chưa đọc"
          className="bg-primary mt-2 size-2 shrink-0 rounded-full"
        />
      )}
    </>
  );

  const className = cn(
    'flex w-full items-start gap-2.5 rounded-lg px-2.5 text-left transition-colors',
    compact ? 'py-2' : 'py-2.5',
    notification.isRead ? 'hover:bg-accent' : 'bg-primary/[0.04] hover:bg-accent',
  );

  // Không có đích đến (`relatedEntityKind === 'None'`, hoặc đã bị gỡ khỏi project) thì đây
  // vẫn phải là NÚT, không phải link chết: bấm vào vẫn đánh dấu đã đọc được.
  if (href === null) {
    return (
      <button type="button" className={className} onClick={() => onActivate(notification)}>
        {body}
      </button>
    );
  }

  return (
    <Link href={href} className={className} onClick={() => onActivate(notification)}>
      {body}
    </Link>
  );
}

export function NotificationItemSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="grid gap-1" aria-busy="true">
      <span className="sr-only">Đang tải thông báo…</span>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="flex items-start gap-2.5 px-2.5 py-2.5">
          <Skeleton className="size-7 shrink-0 rounded-full" />
          <div className="grid flex-1 gap-1.5">
            <Skeleton className="h-3.5 w-full max-w-72" />
            <Skeleton className="h-3 w-32" />
          </div>
        </div>
      ))}
    </div>
  );
}

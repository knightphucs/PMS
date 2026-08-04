'use client';

import { BellIcon, CheckCheckIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { QueryError } from '@/components/common/query-error';
import {
  NotificationItem,
  NotificationItemSkeleton,
} from '@/components/notifications/notification-item';
import { Button } from '@/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { errorMessage } from '@/lib/api/problem';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
  useUnreadCount,
} from '@/lib/hooks/use-notifications';

const PREVIEW_SIZE = 10;

/**
 * Chuông thông báo trên header.
 *
 * Danh sách chỉ tải khi popover MỞ (`enabled` gián tiếp qua `open`): badge cần hỏi lại
 * theo nhịp 60s, còn 10 dòng nội dung thì không — tải sẵn chúng cho mọi trang là trả tiền
 * cho thứ hầu hết thời gian không ai nhìn.
 */
export function NotificationBell() {
  const [open, setOpen] = useState(false);

  const unread = useUnreadCount();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();

  const count = unread.data?.unreadCount ?? 0;
  const badge = count > 99 ? '99+' : String(count);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            className="relative"
            aria-label={count > 0 ? `Thông báo (${count} chưa đọc)` : 'Thông báo'}
          >
            <BellIcon className="size-4" />
            {count > 0 ? (
              <span className="bg-primary text-primary-foreground absolute -top-0.5 -right-0.5 grid min-w-4 place-items-center rounded-full px-1 text-[10px] leading-4 font-semibold tabular-nums">
                {badge}
              </span>
            ) : null}
          </Button>
        }
      />

      <PopoverContent align="end" className="w-[22rem] p-0">
        <div className="flex items-center justify-between border-b px-3 py-2">
          <span className="text-sm font-semibold">Thông báo</span>
          <Button
            variant="ghost"
            size="sm"
            disabled={count === 0 || markAll.isPending}
            onClick={() => {
              markAll.mutate(undefined, {
                // `markedCount === 0` KHÔNG phải lỗi (idempotent, ADR-024) — nhưng nút đã
                // bị khóa khi count === 0 nên tới đây thì 0 nghĩa là ai đó vừa đọc ở tab khác.
                onSuccess: (result) =>
                  toast.success(
                    result.markedCount > 0
                      ? `Đã đánh dấu ${result.markedCount} thông báo là đã đọc.`
                      : 'Không còn thông báo chưa đọc.',
                  ),
                onError: (error) => toast.error(errorMessage(error)),
              });
            }}
          >
            <CheckCheckIcon className="size-4" />
            Đánh dấu đã đọc
          </Button>
        </div>

        {open ? (
          <NotificationPreview
            onActivate={(notification) => {
              if (!notification.isRead) markRead.mutate(notification.id);
              setOpen(false);
            }}
          />
        ) : null}

        <div className="border-t p-1">
          <Link
            href="/notifications"
            onClick={() => setOpen(false)}
            className="hover:bg-accent text-muted-foreground hover:text-foreground block rounded-md px-2.5 py-2 text-center text-sm font-medium transition-colors"
          >
            Xem tất cả thông báo
          </Link>
        </div>
      </PopoverContent>
    </Popover>
  );
}

function NotificationPreview({
  onActivate,
}: {
  onActivate: (notification: { id: string; isRead: boolean }) => void;
}) {
  const notifications = useNotifications({ page: 1, pageSize: PREVIEW_SIZE });

  if (notifications.isError) {
    return (
      <div className="p-2">
        <QueryError
          title="Không tải được thông báo"
          error={notifications.error}
          onRetry={() => void notifications.refetch()}
          isRetrying={notifications.isFetching}
        />
      </div>
    );
  }

  if (notifications.isPending) {
    return (
      <div className="p-1">
        <NotificationItemSkeleton rows={4} />
      </div>
    );
  }

  if (notifications.data.items.length === 0) {
    return (
      <div className="p-2">
        <EmptyState
          compact
          icon={<BellIcon className="size-6" />}
          title="Chưa có thông báo nào"
          description="Khi có người giao việc hoặc bình luận, bạn sẽ thấy ở đây."
        />
      </div>
    );
  }

  return (
    <div className="max-h-96 overflow-y-auto p-1">
      {notifications.data.items.map((notification) => (
        <NotificationItem
          key={notification.id}
          compact
          notification={notification}
          onActivate={onActivate}
        />
      ))}
    </div>
  );
}

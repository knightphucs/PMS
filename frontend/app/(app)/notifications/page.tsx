'use client';

import { BellIcon, CheckCheckIcon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import {
  NotificationItem,
  NotificationItemSkeleton,
} from '@/components/notifications/notification-item';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { Button } from '@/components/ui/button';
import { errorMessage } from '@/lib/api/problem';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
  useUnreadCount,
} from '@/lib/hooks/use-notifications';
import { cn } from '@/lib/utils';

type Filter = 'all' | 'unread';

/** `undefined` = không lọc; backend loại bỏ query param rỗng ở `buildUrl`. */
const IS_READ: Record<Filter, boolean | undefined> = {
  all: undefined,
  unread: false,
};

export default function NotificationsPage() {
  const [filter, setFilter] = useState<Filter>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const notifications = useNotifications({ page, pageSize, isRead: IS_READ[filter] });
  const unread = useUnreadCount();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();

  const unreadCount = unread.data?.unreadCount ?? 0;

  const changeFilter = (next: Filter) => {
    setFilter(next);
    // Đang ở trang 5 của "Tất cả" mà đổi sang "Chưa đọc" thì trang 5 rất có thể rỗng —
    // trông như không có gì trong khi thực ra có.
    setPage(1);
  };

  return (
    <div className="grid gap-4">
      <PageHeader
        title="Thông báo"
        count={notifications.data?.totalCount}
        description="Việc được giao, bình luận mới, thay đổi trạng thái và lời mời dự án."
        actions={
          <Button
            variant="outline"
            size="sm"
            disabled={unreadCount === 0 || markAll.isPending}
            onClick={() =>
              markAll.mutate(undefined, {
                onSuccess: (result) =>
                  toast.success(
                    result.markedCount > 0
                      ? `Đã đánh dấu ${result.markedCount} thông báo là đã đọc.`
                      : 'Không còn thông báo chưa đọc.',
                  ),
                onError: (error) => toast.error(errorMessage(error)),
              })
            }
          >
            <CheckCheckIcon className="size-4" />
            Đánh dấu tất cả đã đọc
          </Button>
        }
      />

      <div className="flex items-center gap-1">
        {(
          [
            ['all', 'Tất cả'],
            ['unread', `Chưa đọc${unreadCount > 0 ? ` (${unreadCount})` : ''}`],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            aria-pressed={filter === value}
            onClick={() => changeFilter(value)}
            className={cn(
              'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
              filter === value
                ? 'bg-primary/10 text-primary'
                : 'text-muted-foreground hover:bg-accent hover:text-foreground',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {notifications.isError ? (
        <QueryError
          title="Không tải được thông báo"
          error={notifications.error}
          onRetry={() => void notifications.refetch()}
          isRetrying={notifications.isFetching}
        />
      ) : notifications.isPending ? (
        <div className="bg-card rounded-lg border p-1">
          <NotificationItemSkeleton rows={6} />
        </div>
      ) : notifications.data.items.length === 0 ? (
        <EmptyState
          icon={<BellIcon className="size-8" />}
          title={filter === 'unread' ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo nào'}
          description={
            filter === 'unread'
              ? 'Bạn đã đọc hết. Đổi sang "Tất cả" để xem lại những thông báo cũ.'
              : 'Khi có người giao việc, bình luận hoặc mời bạn vào dự án, thông báo sẽ xuất hiện ở đây.'
          }
        />
      ) : (
        <>
          <div className="bg-card rounded-lg border p-1">
            {notifications.data.items.map((notification) => (
              <NotificationItem
                key={notification.id}
                notification={notification}
                onActivate={(item) => {
                  if (!item.isRead) markRead.mutate(item.id);
                }}
              />
            ))}
          </div>

          <ProjectPagination
            page={notifications.data}
            unitLabel="thông báo"
            disabled={notifications.isFetching}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </>
      )}
    </div>
  );
}

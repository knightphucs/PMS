'use client';

import { HistoryIcon } from 'lucide-react';
import { useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { QueryError } from '@/components/common/query-error';
import { UserAvatar } from '@/components/common/user-avatar';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDateTime, formatRelativeTime } from '@/lib/format';
import { useTaskActivity } from '@/lib/hooks/use-activity';

const PAGE_SIZE = 20;

export function TaskActivity({ projectId, taskId }: { projectId: string; taskId: string }) {
  const [page, setPage] = useState(1);
  const activity = useTaskActivity(projectId, taskId, { page, pageSize: PAGE_SIZE });

  if (activity.isError) {
    return (
      <QueryError
        title="Không tải được lịch sử"
        error={activity.error}
        onRetry={() => void activity.refetch()}
        isRetrying={activity.isFetching}
      />
    );
  }

  if (activity.isPending) {
    return (
      <div className="grid gap-3" aria-busy="true">
        <span className="sr-only">Đang tải lịch sử…</span>
        {[0, 1, 2].map((index) => (
          <div key={index} className="flex gap-2.5">
            <Skeleton className="size-7 shrink-0 rounded-full" />
            <Skeleton className="h-4 flex-1" />
          </div>
        ))}
      </div>
    );
  }

  if (activity.data.items.length === 0) {
    return (
      <EmptyState
        compact
        icon={<HistoryIcon className="size-6" />}
        title="Chưa có hoạt động nào"
        description="Mọi thay đổi trên task — đổi trạng thái, giao việc, bình luận — sẽ được ghi lại ở đây."
      />
    );
  }

  return (
    <div className="grid gap-4">
      <ol className="grid gap-3">
        {activity.data.items.map((log) => (
          <li key={log.id} className="flex items-start gap-2.5">
            <UserAvatar id={log.actorId} name={log.actorName} size="sm" />
            <div className="min-w-0 flex-1">
              {/*
                `detail` do backend soạn sẵn bằng tiếng Việt và đã có đủ chủ ngữ/tân ngữ
                ("Chuyển trạng thái từ Cần làm sang Đang làm"). Hiện NGUYÊN VĂN — ghép lại
                từ `action` + tên người ở client sẽ ra một câu thứ hai nói cùng một việc,
                và hai câu đó chắc chắn có lúc mâu thuẫn.
              */}
              <p className="text-[13px] leading-snug">
                <span className="font-medium">{log.actorName}</span> — {log.detail}
              </p>
              <p
                className="text-muted-foreground text-xs"
                title={formatDateTime(log.createdAt)}
              >
                {formatRelativeTime(log.createdAt)}
              </p>
            </div>
          </li>
        ))}
      </ol>

      {activity.data.totalPages > 1 ? (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!activity.data.hasPreviousPage || activity.isFetching}
            onClick={() => setPage((current) => current - 1)}
          >
            Mới hơn
          </Button>
          <span className="text-muted-foreground text-xs tabular-nums">
            {activity.data.page} / {activity.data.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={!activity.data.hasNextPage || activity.isFetching}
            onClick={() => setPage((current) => current + 1)}
          >
            Cũ hơn
          </Button>
        </div>
      ) : null}
    </div>
  );
}

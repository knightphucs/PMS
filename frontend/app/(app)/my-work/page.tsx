'use client';

import { CheckCircle2Icon, LayoutGridIcon, ListIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { AvatarStack } from '@/components/common/avatar-stack';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { TaskStatusChip } from '@/components/tasks/task-status-chip';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { formatDate } from '@/lib/format';
import { useMyWork } from '@/lib/hooks/use-my-work';
import { cn } from '@/lib/utils';
import type { MyWorkGroup } from '@/types/my-work';
import type { TaskSummaryResponse } from '@/types/task';

type ViewMode = 'grouped' | 'flat';

/**
 * "Việc của tôi" — việc được giao cho CHÍNH mình, xuyên mọi dự án (ADR-053).
 *
 * 📌 Đây là màn hình duy nhất không nằm dưới `/projects/{id}`, và cũng là màn hình duy nhất
 * trả lời được câu *"sáng nay tôi cần làm gì"* — mọi màn khác đều bắt chọn dự án trước.
 *
 * 🔴 Phạm vi do SERVER quyết: hạn ≤ hôm nay, gồm cả **quá hạn**. Lọc "đúng hôm nay" sẽ giấu
 * việc trễ đi, mà đó chính là thứ cần thấy nhất.
 */
export default function MyWorkPage() {
  const myWork = useMyWork();
  const [view, setView] = useState<ViewMode>('grouped');

  const flatTasks = (myWork.data?.groups ?? []).flatMap((group) =>
    group.tasks.map((task) => ({ task, group })),
  );

  return (
    <div className="grid min-w-0 gap-4">
      <PageHeader
        title="Việc của tôi"
        count={myWork.data?.totalTasks}
        description={
          myWork.data
            ? `Việc được giao cho bạn, có hạn tới ${formatDate(myWork.data.today)} — gồm cả việc đã quá hạn.`
            : 'Việc được giao cho bạn, có hạn tới hôm nay.'
        }
        actions={
          <div className="flex items-center gap-1">
            {/* Đổi cách xem: gom theo dự án (mặc định) hay một danh sách phẳng theo hạn.
                Hai câu hỏi khác nhau — "dự án nào đang cần tôi" và "việc nào gấp nhất". */}
            <Button
              variant={view === 'grouped' ? 'secondary' : 'ghost'}
              size="sm"
              aria-pressed={view === 'grouped'}
              onClick={() => setView('grouped')}
            >
              <LayoutGridIcon className="size-4" />
              Theo dự án
            </Button>
            <Button
              variant={view === 'flat' ? 'secondary' : 'ghost'}
              size="sm"
              aria-pressed={view === 'flat'}
              onClick={() => setView('flat')}
            >
              <ListIcon className="size-4" />
              Theo hạn
            </Button>
          </div>
        }
      />

      {myWork.data && myWork.data.overdueTasks > 0 ? (
        <p className="text-destructive text-sm font-medium">
          {myWork.data.overdueTasks} việc đã quá hạn.
        </p>
      ) : null}

      {myWork.isError ? (
        <QueryError
          title="Không tải được việc của bạn"
          error={myWork.error}
          onRetry={() => void myWork.refetch()}
          isRetrying={myWork.isFetching}
        />
      ) : myWork.isPending ? (
        <MyWorkSkeleton />
      ) : myWork.data.totalTasks === 0 ? (
        <EmptyState
          icon={<CheckCircle2Icon className="size-8" />}
          title="Không có việc nào tới hạn"
          description="Bạn không có task nào được giao mà tới hạn hôm nay hoặc đã quá hạn."
        />
      ) : view === 'grouped' ? (
        <div className="grid gap-4">
          {myWork.data.groups.map((group) => (
            <ProjectGroup key={group.projectId} group={group} />
          ))}
        </div>
      ) : (
        <div className="bg-card divide-y rounded-lg border">
          {/* Danh sách phẳng đã theo đúng thứ tự server trả (hạn tăng dần, rồi độ ưu tiên),
              nên không sắp lại ở client — sắp hai nơi thì chắc chắn có lúc lệch. */}
          {flatTasks.map(({ task, group }) => (
            <TaskRow
              key={task.id}
              task={task}
              projectId={group.projectId}
              projectKey={group.projectKey}
              showProject
            />
          ))}
        </div>
      )}
    </div>
  );
}

function ProjectGroup({ group }: { group: MyWorkGroup }) {
  return (
    <section className="grid min-w-0 gap-2">
      <div className="flex items-center gap-2">
        <Link
          href={`/projects/${group.projectId}/board`}
          className="hover:text-primary text-sm font-semibold underline-offset-4 transition-colors hover:underline"
        >
          {group.projectName}
        </Link>
        <span className="bg-muted text-muted-foreground rounded px-1.5 py-0.5 text-[11px] font-medium tabular-nums">
          {group.tasks.length}
        </span>
      </div>

      <div className="bg-card divide-y rounded-lg border">
        {group.tasks.map((task) => (
          <TaskRow
            key={task.id}
            task={task}
            projectId={group.projectId}
            projectKey={group.projectKey}
          />
        ))}
      </div>
    </section>
  );
}

function TaskRow({
  task,
  projectId,
  projectKey,
  showProject = false,
}: {
  task: TaskSummaryResponse;
  projectId: string;
  projectKey: string;
  showProject?: boolean;
}) {
  return (
    <Link
      href={`/projects/${projectId}/tasks/${task.id}`}
      className="hover:bg-accent flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-[13px] transition-colors"
    >
      {/* Mã do backend ghép sẵn (ADR-034) — đừng nối projectKey + number ở đây. */}
      <span className="text-muted-foreground shrink-0 font-medium tabular-nums">{task.code}</span>

      <span className="min-w-0 flex-1 truncate">{task.name}</span>

      {/* Ở chế độ phẳng, mã task đã mang tiền tố dự án nhưng nó ngắn — hiện thêm tên dự án
          để không phải đoán `PRJ6` là dự án nào. */}
      {showProject ? (
        <span className="text-muted-foreground shrink-0 text-xs">{projectKey}</span>
      ) : null}

      <PriorityLabel priority={task.priority} className="shrink-0" />

      <TaskStatusChip status={task.status} className="shrink-0" />

      <span
        className={cn(
          'w-24 shrink-0 text-right text-xs tabular-nums',
          task.isOverdue ? 'text-destructive font-medium' : 'text-muted-foreground',
        )}
      >
        {task.dueDate ? formatDate(task.dueDate) : '—'}
      </span>

      <AvatarStack people={task.assignees} className="shrink-0" />
    </Link>
  );
}

function MyWorkSkeleton() {
  return (
    <div className="grid gap-4" aria-busy="true">
      <span className="sr-only">Đang tải việc của bạn…</span>
      {Array.from({ length: 2 }).map((_, group) => (
        <div key={group} className="grid gap-2">
          <Skeleton className="h-4 w-40" />
          <div className="bg-card divide-y rounded-lg border">
            {Array.from({ length: 3 }).map((__, row) => (
              <div key={row} className="flex items-center gap-3 px-3 py-2.5">
                <Skeleton className="h-4 w-16" />
                <Skeleton className="h-4 flex-1" />
                <Skeleton className="h-5 w-20 rounded" />
                <Skeleton className="h-4 w-16" />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

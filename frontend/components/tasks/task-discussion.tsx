'use client';

import { useState } from 'react';

import { TaskActivity } from '@/components/tasks/task-activity';
import { TaskComments } from '@/components/tasks/task-comments';
import { cn } from '@/lib/utils';
import type { RoleInProject } from '@/types/enums';

type Tab = 'comments' | 'activity';

/**
 * Cặp tab "Bình luận | Lịch sử".
 *
 * 🔑 Đây là tab **CỤC BỘ** (`useState`), KHÔNG phải segment định tuyến — khác hẳn bốn tab
 * của project. Lý do: nó không đáng một URL riêng (không ai chia sẻ link "tab lịch sử của
 * task X"), và quan trọng hơn, đổi tab qua router sẽ mất vị trí cuộn của cả trang trong
 * khi cụm này nằm tận cuối một cột dài.
 *
 * Dùng `<button>` + `aria-selected` chứ không dùng `components/ui/tabs`: cả hai khối con
 * đều tự quản lý phân trang riêng, nên giữ chúng mount/unmount theo tab là cách rẻ nhất
 * để không phải đồng bộ hai state phân trang.
 */
export function TaskDiscussion({
  projectId,
  taskId,
  role,
  myEmployeeId,
}: {
  projectId: string;
  taskId: string;
  role: RoleInProject | null;
  myEmployeeId: string | null;
}) {
  const [tab, setTab] = useState<Tab>('comments');

  return (
    <section className="grid gap-3">
      <div role="tablist" aria-label="Thảo luận và lịch sử" className="flex gap-1 border-b">
        {(
          [
            ['comments', 'Bình luận'],
            ['activity', 'Lịch sử'],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            role="tab"
            aria-selected={tab === value}
            onClick={() => setTab(value)}
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors',
              tab === value
                ? 'border-primary text-primary'
                : 'text-muted-foreground hover:text-foreground border-transparent',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'comments' ? (
        <TaskComments
          projectId={projectId}
          taskId={taskId}
          role={role}
          myEmployeeId={myEmployeeId}
        />
      ) : (
        <TaskActivity projectId={projectId} taskId={taskId} />
      )}
    </section>
  );
}

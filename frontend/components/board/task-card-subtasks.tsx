'use client';

import Link from 'next/link';

import { TaskStatusDot } from '@/components/tasks/task-status-chip';
import { Skeleton } from '@/components/ui/skeleton';
import { useTask } from '@/lib/hooks/use-tasks';

/**
 * Danh sách subtask xổ ra NGAY trên thẻ Kanban, khi bấm mở rộng.
 *
 * 🔴 Tải LAZY: `useTask` chỉ nạp khi component này thật sự được render (tức là đã mở), nhờ
 * `enabled: taskId !== null` sẵn có trong hook — không có endpoint hay hook mới nào, và
 * board KHÔNG kèm sẵn subtask cho mọi thẻ (sẽ phồng payload cho những board không ai mở
 * dropdown nào cả).
 *
 * 📌 Đọc-thôi: bấm một dòng mở trang chi tiết của SUBTASK đó, không đổi trạng thái ngay tại
 * đây. Cùng tinh thần `TaskSubtasks` ở màn chi tiết task, chỉ gọn hơn cho vừa một thẻ.
 */
export function TaskCardSubtasks({ projectId, taskId }: { projectId: string; taskId: string }) {
  const detail = useTask(projectId, taskId);

  if (detail.isPending) {
    return (
      <div className="grid gap-1 pl-4" aria-busy="true">
        <span className="sr-only">Đang tải danh sách subtask…</span>
        <Skeleton className="h-5" />
        <Skeleton className="h-5" />
      </div>
    );
  }

  if (detail.isError) {
    return (
      <p className="text-muted-foreground pl-4 text-[11px]">Không tải được danh sách subtask.</p>
    );
  }

  return (
    <ul className="grid gap-0.5 pl-4">
      {detail.data.subtasks.map((subtask) => (
        <li key={subtask.id} className="min-w-0">
          <Link
            href={`/projects/${projectId}/tasks/${subtask.id}`}
            className="hover:bg-accent flex min-w-0 items-center gap-1.5 rounded px-1 py-1 text-[11px] transition-colors"
          >
            {/* Chấm màu lấy từ CỘT (ADR-052), không tra bảng enum. */}
            <TaskStatusDot status={subtask.status} />
            <span className="min-w-0 flex-1 truncate">{subtask.name}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

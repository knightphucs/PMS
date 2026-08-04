'use client';

import {
  BarChart3Icon,
  KanbanSquareIcon,
  ListTodoIcon,
  TimerIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';
import { useSelectedLayoutSegment } from 'next/navigation';

import { cn } from '@/lib/utils';

const TABS: { segment: string; label: string; icon: LucideIcon }[] = [
  { segment: 'board', label: 'Bảng', icon: KanbanSquareIcon },
  { segment: 'backlog', label: 'Backlog', icon: ListTodoIcon },
  { segment: 'sprints', label: 'Sprint', icon: TimerIcon },
  { segment: 'members', label: 'Thành viên', icon: UsersIcon },
  // Cả ba vai trò đều xem được (ADR-039) nên tab hiện với mọi thành viên, không gác.
  { segment: 'statistics', label: 'Thống kê', icon: BarChart3Icon },
];

/**
 * Tab định tuyến — `<Link>` thật, KHÔNG dùng `components/ui/tabs`.
 *
 * Base UI `Tabs` quản lý focus và roving-tabindex của riêng nó, sẽ tranh với router mỗi
 * lần điều hướng. Mà ở đây tab là URL thật: chia sẻ link được, nút Back của trình duyệt
 * hoạt động đúng, và mỗi tab giữ được query string riêng (`?sprint=` của board).
 *
 * `useSelectedLayoutSegment` chứ không phải `usePathname().startsWith`: nó trả thẳng
 * segment con đang active nên không cần cắt chuỗi, và không nhầm khi id project tình cờ
 * chứa tên tab.
 */
export function ProjectTabs({ projectId }: { projectId: string }) {
  const active = useSelectedLayoutSegment();

  return (
    <nav aria-label="Khu vực của dự án" className="-mb-px flex gap-1 overflow-x-auto">
      {TABS.map(({ segment, label, icon: Icon }) => {
        const isActive = active === segment;

        return (
          <Link
            key={segment}
            href={`/projects/${projectId}/${segment}`}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              'flex shrink-0 items-center gap-2 border-b-2 px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'border-primary text-primary'
                : 'text-muted-foreground hover:text-foreground border-transparent',
            )}
          >
            <Icon className="size-4" />
            {label}
          </Link>
        );
      })}
    </nav>
  );
}

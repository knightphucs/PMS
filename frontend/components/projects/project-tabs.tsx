'use client';

import {
  BarChart3Icon,
  GanttChartIcon,
  KanbanSquareIcon,
  ListTodoIcon,
  SettingsIcon,
  TableIcon,
  TimerIcon,
  TrendingUpIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';
import { useSelectedLayoutSegment } from 'next/navigation';

import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { canManageTasks } from '@/lib/tasks/permissions';
import { cn } from '@/lib/utils';

/**
 * Các khu vực của MỘT dự án — nguồn sự thật duy nhất.
 *
 * Export vì `SidebarNav` dựng lại đúng các mục này thành khối "ngữ cảnh dự án" (2026-08-05).
 * Chép tay sang đó thì thêm một tab ở đây sẽ âm thầm để sidebar thiếu một mục — đúng lớp
 * lỗi "hai nơi định dạng thì chắc chắn có lúc lệch" mà ADR-034 đã trả giá một lần.
 *
 * Backlog Insight là ngữ cảnh của Backlog, nên mở dưới dạng panel "Insights" tại chính màn
 * Backlog thay vì là một tab cạnh dashboard tổng quan.
 */
export interface ProjectSection {
  segment: string;
  label: string;
  icon: LucideIcon;
  /**
   * Chỉ hiện với người quản lý được task (PM).
   *
   * 🔴 **Ẩn chứ không vô hiệu hoá** — luật 3 và 5 của Doctrine chống rối (§0
   * `ARCHITECTURE.md`): một tab xám vẫn gợi ý rằng đâu đó có cách bật nó lên, còn một tab
   * dẫn tới màn "bạn không có quyền" thì tốn của người dùng một cú bấm để biết điều đó.
   */
  requiresManage?: boolean;
}

export const PROJECT_SECTIONS: ProjectSection[] = [
  { segment: 'board', label: 'Bảng', icon: KanbanSquareIcon },
  // Danh sách + view lưu được (ADR-061). Đứng ngay sau Bảng vì đây là góc nhìn thứ hai của
  // CÙNG một tập task, còn Backlog/Sprint là chuyện lập kế hoạch.
  { segment: 'list', label: 'Danh sách', icon: TableIcon },
  { segment: 'backlog', label: 'Backlog', icon: ListTodoIcon },
  { segment: 'sprints', label: 'Sprint', icon: TimerIcon },
  { segment: 'members', label: 'Thành viên', icon: UsersIcon },
  // Cả ba vai trò đều xem được (ADR-039) nên tab hiện với mọi thành viên, không gác.
  { segment: 'statistics', label: 'Thống kê', icon: BarChart3Icon },
  // Hai báo cáo dưới cùng cùng quyền với Thống kê — không tạo action mới.
  { segment: 'velocity', label: 'Velocity', icon: TrendingUpIcon },
  { segment: 'timeline', label: 'Timeline', icon: GanttChartIcon },
  // 🔴 Cấu hình gom về ĐÂY, không nằm trên header trang Bảng (ADR-061, luật 5 của Doctrine).
  // Trước đó ba dialog quản lý (cột · trường · loại việc) treo trên màn LÀM VIỆC, và tầng
  // "bộ máy quy trình" sắp đổ thêm hai bề mặt nữa lên đó — sáu nút cấu hình trên một màn
  // người ta dùng để nhìn công việc.
  { segment: 'settings', label: 'Cấu hình', icon: SettingsIcon, requiresManage: true },
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
  const { role } = useMyProjectRole(projectId);
  const canManage = canManageTasks(role);

  return (
    <nav aria-label="Khu vực của dự án" className="-mb-px flex gap-1 overflow-x-auto">
      {PROJECT_SECTIONS.filter((s) => !s.requiresManage || canManage).map(({ segment, label, icon: Icon }) => {
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

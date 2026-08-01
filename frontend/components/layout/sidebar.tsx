'use client';

import {
  BarChart3Icon,
  BellIcon,
  FolderKanbanIcon,
  KanbanSquareIcon,
  ListTodoIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

interface NavItem {
  label: string;
  icon: LucideIcon;
  href?: string;
}

/**
 * Điều hướng chính.
 *
 * Các mục chưa làm được hiển thị ở trạng thái **vô hiệu hóa kèm nhãn "Sắp có"**, không
 * phải link hỏng. Đây là đảo lại quyết định trước đó ("chưa làm thì đừng hiện") — lý do
 * cũ vẫn đúng ở phần *link trỏ tới trang không tồn tại thì tệ hơn không có link*, nhưng
 * nó không áp cho mục bị vô hiệu hóa: không bấm được thì không dẫn đi đâu cả.
 *
 * Đổi lại được hai thứ: người dùng thấy được phạm vi sản phẩm thay vì tưởng ứng dụng chỉ
 * có một trang, và thanh điều hướng không còn trông trống trải vì chỉ có mỗi một mục.
 */
const NAV_GROUPS: { title: string; items: NavItem[] }[] = [
  {
    title: 'Công việc',
    items: [
      { label: 'Dự án', icon: FolderKanbanIcon, href: '/projects' },
      // ⚠️ Hai mục này KHÔNG thể có `href`, kể cả sau khi màn hình đã làm xong: cả hai
      // thuộc phạm vi MỘT project (`/projects/{id}/board`) mà app-shell không biết
      // project nào đang mở — nó nằm trên segment `[id]`. Vào Board/Backlog qua tab của
      // trang chi tiết dự án. Đừng nhớ "project vừa mở" vào store: giá trị đó nói dối
      // ngay khi người dùng mở hai tab trình duyệt.
      { label: 'Bảng Kanban', icon: KanbanSquareIcon },
      { label: 'Backlog', icon: ListTodoIcon },
    ],
  },
  {
    title: 'Khác',
    items: [
      { label: 'Thông báo', icon: BellIcon },
      { label: 'Nhân sự', icon: UsersIcon },
      { label: 'Thống kê', icon: BarChart3Icon },
    ],
  },
];

export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();

  return (
    <nav aria-label="Điều hướng chính" className="flex flex-col gap-6 p-3">
      {NAV_GROUPS.map((group) => (
        <div key={group.title} className="grid gap-1">
          <p className="text-muted-foreground px-3 pb-1 text-xs font-medium tracking-wide uppercase">
            {group.title}
          </p>

          {group.items.map(({ label, icon: Icon, href }) => {
            if (!href) {
              return (
                <span
                  key={label}
                  aria-disabled="true"
                  className="text-muted-foreground/50 flex cursor-not-allowed items-center gap-3 rounded-lg px-3 py-2 text-sm"
                >
                  <Icon className="size-4 shrink-0" />
                  <span className="flex-1">{label}</span>
                  <span className="bg-muted text-muted-foreground rounded px-1.5 py-0.5 text-[10px] font-medium">
                    Sắp có
                  </span>
                </span>
              );
            }

            const active = pathname === href || pathname.startsWith(`${href}/`);

            return (
              <Link
                key={label}
                href={href}
                onClick={onNavigate}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  active
                    ? 'bg-primary/10 text-primary'
                    : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                )}
              >
                <Icon className="size-4 shrink-0" />
                {label}
              </Link>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

export function SidebarBrand() {
  return (
    <Link href="/projects" className="flex h-14 items-center gap-2.5 border-b px-4 font-semibold">
      <span className="bg-primary text-primary-foreground grid size-8 shrink-0 place-items-center rounded-lg text-xs font-bold">
        PMS
      </span>
      <span className="truncate">Quản lý dự án</span>
    </Link>
  );
}

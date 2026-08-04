'use client';

import {
  BellIcon,
  FolderKanbanIcon,
  MailIcon,
  ShieldCheckIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  SYSTEM_PERMISSIONS,
  hasPermission,
  type SystemPermission,
} from '@/lib/auth/system-permissions';
import { useMyInvitations } from '@/lib/hooks/use-members';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/store/auth-store';

interface NavItem {
  label: string;
  icon: LucideIcon;
  href: string;
  /** Khóa để tra số đếm ở `SidebarNav` — bản thân NAV_GROUPS là hằng, không giữ state. */
  badge?: 'invitations';
  /** Chỉ hiện khi người dùng có quyền tầng 1 này (ADR-045). */
  permission?: SystemPermission;
}

/**
 * Điều hướng chính. **Mọi mục ở đây đều dẫn tới một trang có thật.**
 *
 * Trước 2026-08-04 danh sách này có bốn mục vô hiệu hóa kèm nhãn "Sắp có". Nay bỏ hẳn cơ
 * chế đó, vì hai nhóm mục ấy hóa ra là hai chuyện khác nhau:
 *
 * • **Nhân sự / Thống kê** — chỉ là chưa làm. Nay đã có trang thật, nên chúng thành link.
 *
 * • **Bảng Kanban / Backlog** — 🔴 KHÔNG BAO GIỜ đặt được ở đây, kể cả khi màn hình đã
 *   xong từ lâu: cả hai thuộc phạm vi MỘT project (`/projects/{id}/board`) mà `AppShell`
 *   không biết project nào đang mở — nó nằm TRÊN segment `[id]`. Giữ chúng với nhãn "Sắp
 *   có" là hứa một thứ sẽ không bao giờ tới. Đã gỡ hẳn; đường vào là tab của trang chi
 *   tiết dự án. Và đừng nhớ "project vừa mở" vào store để lách: giá trị đó nói dối ngay
 *   khi người dùng mở hai tab trình duyệt.
 *
 * Kết quả: không còn nhánh render cho mục không có `href`, và `href` thành bắt buộc trong
 * kiểu `NavItem` — sai sót tương lai bị chặn ở tầng kiểu chứ không bằng kỷ luật.
 */
const NAV_GROUPS: { title: string; items: NavItem[] }[] = [
  {
    title: 'Công việc',
    items: [
      { label: 'Dự án', icon: FolderKanbanIcon, href: '/projects' },
      { label: 'Lời mời', icon: MailIcon, href: '/invitations', badge: 'invitations' },
    ],
  },
  {
    title: 'Khác',
    items: [
      { label: 'Thông báo', icon: BellIcon, href: '/notifications' },
      // Gác bằng QUYỀN chứ không bằng `systemRole === 'SystemAdmin'` (ADR-045): quyền đổi
      // được bằng dữ liệu ở /admin/roles, còn vai trò nay chỉ là định danh.
      {
        label: 'Quản trị',
        icon: ShieldCheckIcon,
        href: '/admin/employees',
        permission: SYSTEM_PERMISSIONS.employeesManage,
      },
    ],
  },
];

export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);

  // Sidebar nằm trong `AppShell` nên KHÔNG bị unmount khi đổi trang — query này mount đúng
  // một lần cho cả phiên, và `useMyInvitations` dùng chung khóa với trang /invitations nên
  // mở trang đó không tốn thêm request nào.
  const invitations = useMyInvitations();
  const badgeCounts: Record<NonNullable<NavItem['badge']>, number> = {
    invitations: invitations.data?.length ?? 0,
  };

  return (
    <nav aria-label="Điều hướng chính" className="flex flex-col gap-6 p-3">
      {NAV_GROUPS.map((group) => (
        <div key={group.title} className="grid gap-1">
          <p className="text-muted-foreground px-3 pb-1 text-xs font-medium tracking-wide uppercase">
            {group.title}
          </p>

          {group.items.map(({ label, icon: Icon, href, badge, permission }) => {
            const count = badge ? badgeCounts[badge] : 0;

            // Ẩn hẳn thay vì vô hiệu hóa: một mục xám không bấm được chỉ khiến người dùng
            // tự hỏi mình đang thiếu gì, mà câu trả lời thì họ không tự tra được.
            if (permission && !hasPermission(user, permission)) return null;

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
                <span className="flex-1">{label}</span>
                {count > 0 ? (
                  <span className="bg-primary text-primary-foreground rounded-full px-1.5 py-0.5 text-[10px] font-semibold tabular-nums">
                    {count}
                  </span>
                ) : null}
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

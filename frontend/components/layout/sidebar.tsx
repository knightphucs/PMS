'use client';

import {
  ArrowLeftIcon,
  BellIcon,
  CheckSquareIcon,
  FolderKanbanIcon,
  MailIcon,
  ShieldCheckIcon,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { PROJECT_SECTIONS } from '@/components/projects/project-tabs';
import {
  SYSTEM_PERMISSIONS,
  hasPermission,
  type SystemPermission,
} from '@/lib/auth/system-permissions';
import { useMyInvitations } from '@/lib/hooks/use-members';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useProjectOverview } from '@/lib/hooks/use-projects';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/store/auth-store';
import { ROLE_IN_PROJECT_LABEL } from '@/types/enums';

interface NavItem {
  label: string;
  icon: LucideIcon;
  href: string;
  /** Khóa để tra số đếm ở `GlobalNav` — bản thân NAV_GROUPS là hằng, không giữ state. */
  badge?: 'invitations';
  /** Chỉ hiện khi người dùng có quyền tầng 1 này (ADR-045). */
  permission?: SystemPermission;
}

/**
 * Điều hướng TOÀN CỤC — chỉ hiện khi người dùng **không** đang ở trong một dự án.
 *
 * 📌 **Vì sao có điều kiện đó** (đổi 2026-08-05, theo mô hình Jira): trước đây sidebar hiện
 * đồng thời mục "Dự án", khối khu vực của dự án đang mở, VÀ danh sách "Dự án của tôi" — tức
 * **ba đường tới cùng một chỗ**, trong khi trang mặc định sau đăng nhập vốn đã là `/projects`.
 * Sidebar dài gần hết cột mà phần lớn là lối đi trùng nhau.
 *
 * Jira giải bằng cách **tách hai ngữ cảnh**: ở ngoài thì sidebar là điều hướng toàn cục, vào
 * trong một dự án thì sidebar **thuộc về dự án đó** và đường ra nằm ở breadcrumb. Ta làm y
 * vậy, cộng thêm một link "Tất cả dự án" hiện rõ ở đầu — vì đã bỏ nav toàn cục khỏi tầm mắt
 * thì phải trả lại đường về ở chỗ nhìn thấy được, không thể chỉ trông vào breadcrumb.
 */
const NAV_GROUPS: { title: string; items: NavItem[] }[] = [
  {
    title: 'Công việc',
    items: [
      // Đặt TRƯỚC "Dự án": đây là màn hình duy nhất trả lời được "sáng nay tôi cần làm gì"
      // mà không bắt chọn dự án trước (ADR-053).
      { label: 'Việc của tôi', icon: CheckSquareIcon, href: '/my-work' },
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

/**
 * Id của project đang mở, rút thẳng từ URL — `null` khi không ở trong project nào.
 *
 * `/projects` trần KHÔNG khớp (không có segment sau), nên trang danh sách vẫn thuộc ngữ cảnh
 * toàn cục. Mọi route con đều khớp, gồm cả `/projects/{id}/tasks/{taskId}`.
 *
 * 📌 Đọc từ URL chứ không từ store là có chủ đích: URL vốn đã thuộc về **từng tab**, nên hai
 * tab mở hai dự án khác nhau thì mỗi tab đọc ra đúng id của chính nó. Một biến "project vừa
 * mở" trong store thì nói dối ngay ở tab thứ hai.
 */
function projectIdFromPath(pathname: string): string | null {
  return /^\/projects\/([^/]+)/.exec(pathname)?.[1] ?? null;
}

const LINK_BASE =
  'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors';
const LINK_ACTIVE = 'bg-primary/10 text-primary';
const LINK_IDLE = 'text-muted-foreground hover:bg-accent hover:text-foreground';
const GROUP_TITLE = 'text-muted-foreground px-3 pb-1 text-xs font-medium tracking-wide uppercase';

export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  const projectId = projectIdFromPath(pathname);

  return projectId ? (
    <ProjectNav projectId={projectId} pathname={pathname} onNavigate={onNavigate} />
  ) : (
    <GlobalNav pathname={pathname} onNavigate={onNavigate} />
  );
}

function GlobalNav({ pathname, onNavigate }: { pathname: string; onNavigate?: () => void }) {
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
          <p className={GROUP_TITLE}>{group.title}</p>

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
                className={cn(LINK_BASE, active ? LINK_ACTIVE : LINK_IDLE)}
              >
                <Icon className="size-4 shrink-0" />
                <span className="min-w-0 flex-1 truncate">{label}</span>
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

/**
 * Sidebar khi đang ở TRONG một dự án — thay thế hẳn nav toàn cục.
 *
 * Các mục đọc từ hằng `PROJECT_SECTIONS` **dùng chung với `ProjectTabs`**: chép tay sang hai
 * nơi thì thêm một khu vực ở tab sẽ âm thầm để sidebar thiếu một mục.
 *
 * Không gác quyền mục nào — cả ba vai trò đều xem được cả năm khu vực (Thống kê: ADR-039).
 */
function ProjectNav({
  projectId,
  pathname,
  onNavigate,
}: {
  projectId: string;
  pathname: string;
  onNavigate?: () => void;
}) {
  // Cùng khóa cache mà `app/(app)/projects/[id]/layout.tsx` đã nạp (staleTime 5 phút), nên
  // khối này KHÔNG sinh thêm request nào — chỉ đọc ké.
  const overview = useProjectOverview(projectId);
  const { role } = useMyProjectRole(projectId);

  const name = overview.data?.name;

  return (
    <nav aria-label="Điều hướng dự án" className="flex flex-col gap-4 p-3">
      <Link
        href="/projects"
        onClick={onNavigate}
        className="text-muted-foreground hover:text-foreground flex items-center gap-2 px-3 py-1 text-xs font-medium transition-colors"
      >
        <ArrowLeftIcon className="size-3.5 shrink-0" />
        Tất cả dự án
      </Link>

      {/* Đầu đề dự án. Dòng phụ là VAI TRÒ của chính người đang xem, không phải trạng thái
          dự án — trạng thái đã nằm ngay trên header trang, còn vai trò thì không hiện ở đâu
          khác. Nó cũng là câu trả lời cho "vì sao tôi không thấy nút Sửa". */}
      <div className="flex items-center gap-2.5 px-3">
        <span className="bg-primary/15 text-primary grid size-8 shrink-0 place-items-center rounded-lg text-sm font-bold">
          {name ? name.charAt(0).toUpperCase() : '·'}
        </span>
        <span className="grid min-w-0">
          <span className="truncate text-sm font-semibold" title={name}>
            {name ?? 'Đang tải…'}
          </span>
          {role ? (
            <span className="text-muted-foreground truncate text-xs">
              {ROLE_IN_PROJECT_LABEL[role]}
            </span>
          ) : null}
        </span>
      </div>

      <div className="grid gap-1">
        <p className={GROUP_TITLE}>Lập kế hoạch</p>
        {PROJECT_SECTIONS.filter((s) => s.segment !== 'members').map(
          ({ segment, label, icon: Icon }) => (
            <ProjectNavLink
              key={segment}
              projectId={projectId}
              segment={segment}
              label={label}
              icon={Icon}
              pathname={pathname}
              onNavigate={onNavigate}
            />
          ),
        )}
      </div>

      <div className="grid gap-1">
        <p className={GROUP_TITLE}>Quản lý</p>
        {PROJECT_SECTIONS.filter((s) => s.segment === 'members').map(
          ({ segment, label, icon: Icon }) => (
            <ProjectNavLink
              key={segment}
              projectId={projectId}
              segment={segment}
              label={label}
              icon={Icon}
              pathname={pathname}
              onNavigate={onNavigate}
            />
          ),
        )}
      </div>
    </nav>
  );
}

function ProjectNavLink({
  projectId,
  segment,
  label,
  icon: Icon,
  pathname,
  onNavigate,
}: {
  projectId: string;
  segment: string;
  label: string;
  icon: LucideIcon;
  pathname: string;
  onNavigate?: () => void;
}) {
  const href = `/projects/${projectId}/${segment}`;
  const active = pathname === href || pathname.startsWith(`${href}/`);

  return (
    <Link
      href={href}
      onClick={onNavigate}
      aria-current={active ? 'page' : undefined}
      className={cn(LINK_BASE, active ? LINK_ACTIVE : LINK_IDLE)}
    >
      <Icon className="size-4 shrink-0" />
      <span className="min-w-0 flex-1 truncate">{label}</span>
    </Link>
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

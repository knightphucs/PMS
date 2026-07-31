'use client';

import { FolderKanbanIcon } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { UserMenu } from '@/components/layout/user-menu';
import { cn } from '@/lib/utils';

/**
 * Khung ứng dụng dùng chung.
 *
 * Đặt ở `app/(app)/layout.tsx` nên nó KHÔNG bị unmount khi chuyển route trong nhóm —
 * đây chính là thứ App Router cho mà Pages Router không có, kể cả khi mọi trang đều là
 * client component (ADR-028).
 *
 * Các mục điều hướng của những phiên sau (Board, Backlog, Thông báo) chưa thêm vào đây:
 * hiện menu trỏ tới trang chưa tồn tại thì tệ hơn là không hiện.
 */
const NAV_ITEMS = [{ href: '/projects', label: 'Dự án', icon: FolderKanbanIcon }] as const;

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  return (
    <div className="bg-background min-h-svh">
      <header className="bg-background/95 supports-[backdrop-filter]:bg-background/60 sticky top-0 z-40 border-b backdrop-blur">
        <div className="flex h-14 items-center gap-6 px-4 sm:px-6">
          <Link href="/projects" className="flex items-center gap-2 font-semibold">
            <span className="bg-primary text-primary-foreground grid size-7 place-items-center rounded text-xs font-bold">
              PMS
            </span>
            <span className="hidden sm:inline">Quản lý dự án</span>
          </Link>

          <nav aria-label="Điều hướng chính" className="flex flex-1 items-center gap-1">
            {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
              const active = pathname === href || pathname.startsWith(`${href}/`);
              return (
                <Link
                  key={href}
                  href={href}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                    active
                      ? 'bg-accent text-accent-foreground'
                      : 'text-muted-foreground hover:text-foreground hover:bg-accent/50',
                  )}
                >
                  <Icon className="size-4" />
                  {label}
                </Link>
              );
            })}
          </nav>

          <UserMenu />
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl px-4 py-8 sm:px-6">{children}</main>
    </div>
  );
}

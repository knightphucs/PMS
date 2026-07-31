'use client';

import { MenuIcon } from 'lucide-react';
import { useState } from 'react';

import { SidebarBrand, SidebarNav } from '@/components/layout/sidebar';
import { UserMenu } from '@/components/layout/user-menu';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * Khung ứng dụng dùng chung: cột điều hướng cố định bên trái + vùng nội dung bên phải.
 *
 * Đặt ở `app/(app)/layout.tsx` nên nó KHÔNG bị unmount khi chuyển route trong nhóm —
 * đây chính là thứ App Router cho mà Pages Router không có, kể cả khi mọi trang đều là
 * client component (ADR-028). Với bố cục hai cột thì lợi ích đó thấy được bằng mắt: đổi
 * trang thì cột trái đứng yên, không nháy và không cuộn lại từ đầu.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <div className="bg-muted/30 min-h-svh">
      {/* Cột điều hướng — cố định từ md trở lên, trượt vào từ trái ở màn hình nhỏ */}
      <aside
        className={cn(
          'bg-background fixed inset-y-0 left-0 z-50 w-60 border-r transition-transform duration-200 md:translate-x-0',
          mobileOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <SidebarBrand />
        <SidebarNav onNavigate={() => setMobileOpen(false)} />
      </aside>

      {/* Lớp phủ chỉ tồn tại ở màn hình nhỏ khi menu đang mở */}
      {mobileOpen ? (
        <button
          type="button"
          aria-label="Đóng menu"
          onClick={() => setMobileOpen(false)}
          className="fixed inset-0 z-40 bg-black/40 md:hidden"
        />
      ) : null}

      <div className="md:pl-60">
        <header className="bg-background/80 sticky top-0 z-30 flex h-14 items-center gap-3 border-b px-4 backdrop-blur sm:px-6">
          <Button
            variant="ghost"
            size="icon-sm"
            className="md:hidden"
            aria-label="Mở menu"
            onClick={() => setMobileOpen(true)}
          >
            <MenuIcon className="size-4" />
          </Button>

          <div className="flex-1" />
          <UserMenu />
        </header>

        <main className="mx-auto w-full max-w-5xl px-4 py-6 sm:px-6 sm:py-8">{children}</main>
      </div>
    </div>
  );
}

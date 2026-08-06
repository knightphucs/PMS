'use client';

import { LogOutIcon, UserIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';

import { UserAvatar } from '@/components/common/user-avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/lib/hooks/use-auth';
import { SYSTEM_ROLE_LABEL } from '@/types/enums';

export function UserMenu() {
  const { user, logout } = useAuth();
  const [loggingOut, setLoggingOut] = useState(false);

  if (!user) return null;

  const handleLogout = async () => {
    setLoggingOut(true);
    // `logout` đã tự nuốt lỗi mạng và luôn dọn phiên phía client, nên không cần try/catch.
    await logout();
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" className="h-auto gap-2 px-2 py-1.5">
            <UserAvatar id={user.id} name={user.name} className="text-xs" />
            <span className="hidden text-sm font-medium sm:inline">{user.name}</span>
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-64">
        {/* 🔴 KHÔNG dùng `DropdownMenuLabel` ở đây, dù nghe có vẻ đúng tên.
            Nó ánh xạ sang `Menu.GroupLabel` của Base UI, thứ BẮT BUỘC phải nằm trong một
            `Menu.Group` — thiếu là ném "MenuGroupContext is missing" và **cả menu sập**,
            kéo theo lối ra duy nhất để Đăng xuất. Lỗi này đã tồn tại trên `main` và không
            có test nào chạm tới, vì mở menu người dùng là thao tác chưa ai kiểm bằng tay
            (đã kiểm chứng bằng cách stash thay đổi của phiên 2026-08-05 và mở lại — vẫn sập).

            Và bọc vào `Menu.Group` chỉ là vá bề mặt: khối này là DANH TÍNH của người đang
            đăng nhập, không phải nhãn cho một nhóm mục nào. Một `<div>` thường mới đúng
            ngữ nghĩa — nó không hứa với trình đọc màn hình một quan hệ không tồn tại. */}
        <div className="grid gap-1 px-1.5 py-1">
          <span className="text-sm font-medium">{user.name}</span>
          <span className="text-muted-foreground truncate text-xs">{user.email}</span>
          <Badge variant="secondary" className="mt-1 w-fit">
            {SYSTEM_ROLE_LABEL[user.systemRole]}
          </Badge>
        </div>
        <DropdownMenuSeparator />
        {/* shadcn v4 chạy trên Base UI — KHÔNG có `asChild`. Mục menu là link thì dùng prop
            `render`, và giữ nội dung ở children như bình thường. */}
        <DropdownMenuItem render={<Link href="/profile" />}>
          <UserIcon className="size-4" />
          Hồ sơ của tôi
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={handleLogout} disabled={loggingOut}>
          <LogOutIcon className="size-4" />
          {loggingOut ? 'Đang đăng xuất…' : 'Đăng xuất'}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

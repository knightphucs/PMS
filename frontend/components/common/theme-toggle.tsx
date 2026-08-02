'use client';

import { CheckIcon, MonitorIcon, MoonIcon, SunIcon, type LucideIcon } from 'lucide-react';
import { useTheme } from 'next-themes';
import { useEffect, useState } from 'react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

const OPTIONS: { value: string; label: string; icon: LucideIcon }[] = [
  { value: 'light', label: 'Sáng', icon: SunIcon },
  { value: 'dark', label: 'Tối', icon: MoonIcon },
  { value: 'system', label: 'Theo hệ thống', icon: MonitorIcon },
];

export function ThemeToggle() {
  const { theme, resolvedTheme, setTheme } = useTheme();

  // Chủ đề thật chỉ biết được ở phía client (đọc localStorage + prefers-color-scheme).
  // Render icon theo nó ngay lần đầu sẽ lệch với HTML do server sinh ra và React báo
  // hydration mismatch ở MỌI lần tải trang.
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  const Icon = resolvedTheme === 'dark' ? MoonIcon : SunIcon;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label="Đổi giao diện sáng/tối">
            {mounted ? <Icon className="size-4" /> : <span className="size-4" />}
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-44">
        {OPTIONS.map(({ value, label, icon: OptionIcon }) => (
          <DropdownMenuItem key={value} onClick={() => setTheme(value)}>
            <OptionIcon className="size-4" />
            <span className="flex-1">{label}</span>
            <CheckIcon
              className={cn('size-4', mounted && theme === value ? 'opacity-100' : 'opacity-0')}
            />
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

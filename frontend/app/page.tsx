'use client';

import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { Skeleton } from '@/components/ui/skeleton';
import { useSessionBootstrap } from '@/lib/hooks/use-auth';

/**
 * Cổng vào: đưa người dùng tới /projects hoặc /login tùy phiên hiện có.
 *
 * Phải đợi bootstrap xong (`status !== 'unknown'`) mới quyết định — điều hướng sớm sẽ
 * đá cả người đang có cookie hợp lệ về màn đăng nhập.
 */
export default function HomePage() {
  const status = useSessionBootstrap();
  const router = useRouter();

  useEffect(() => {
    if (status === 'authenticated') router.replace('/projects');
    else if (status === 'anonymous') router.replace('/login');
  }, [status, router]);

  return (
    <div className="flex min-h-svh items-center justify-center p-8" aria-busy="true">
      <span className="sr-only">Đang tải…</span>
      <Skeleton className="h-32 w-full max-w-md" />
    </div>
  );
}

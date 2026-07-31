'use client';

import { usePathname, useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { Skeleton } from '@/components/ui/skeleton';
import { useSessionBootstrap } from '@/lib/hooks/use-auth';

/**
 * Chặn truy cập các trang cần đăng nhập.
 *
 * ⚠️ VÌ SAO KHÔNG DÙNG `middleware.ts` — đây là thứ dễ làm sai nhất ở bước này.
 * Route guard bằng middleware của Next hoạt động bằng cách đọc cookie phiên. Nhưng cookie
 * refresh của chúng ta có `Path=/api/v1/auth`, mà trình duyệt chỉ đính cookie vào những
 * request có đường dẫn khớp Path — request tới `/projects` thì KHÔNG khớp. Middleware vì
 * vậy luôn thấy "không có cookie" và sẽ đá cả người đã đăng nhập về /login.
 *
 * Nới Path ra `/` để middleware đọc được thì lại đánh mất chính điều đang bảo vệ: cookie
 * sẽ đi kèm mọi request nghiệp vụ và biến chúng thành mục tiêu CSRF. Nên guard đặt ở
 * client, và đây là đánh đổi có ý thức — xem ADR-028.
 */
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const status = useSessionBootstrap();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status !== 'anonymous') return;

    // Nhớ đích đến để đăng nhập xong quay lại đúng chỗ, thay vì luôn về /projects.
    const next = encodeURIComponent(pathname);
    router.replace(`/login?next=${next}`);
  }, [status, pathname, router]);

  // 'unknown' = ĐANG khôi phục phiên, chưa biết. Hiện khung chờ chứ không hiện nội dung
  // (rò rỉ trong một nhịp) và cũng không điều hướng (nháy trang với người đã đăng nhập).
  if (status !== 'authenticated') {
    return <BootstrapSkeleton />;
  }

  return <>{children}</>;
}

function BootstrapSkeleton() {
  return (
    <div className="space-y-4 p-8" aria-busy="true" aria-live="polite">
      <span className="sr-only">Đang khôi phục phiên đăng nhập…</span>
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-4 w-96" />
      <div className="space-y-2 pt-4">
        {Array.from({ length: 5 }, (_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    </div>
  );
}

/**
 * Ngược lại của AuthGuard: dùng cho /login và /register.
 *
 * Người đã đăng nhập mà mở lại /login thì đưa thẳng vào ứng dụng — đứng nhập lại lần nữa
 * là vô nghĩa, và với luồng này còn nguy hiểm hơn: đăng nhập lại cấp một refresh token
 * mới trong khi cái cũ vẫn còn hạn.
 */
export function GuestOnly({ children }: { children: React.ReactNode }) {
  const status = useSessionBootstrap();
  const router = useRouter();

  useEffect(() => {
    if (status === 'authenticated') router.replace('/projects');
  }, [status, router]);

  if (status === 'unknown') {
    return (
      <div className="flex min-h-svh items-center justify-center" aria-busy="true">
        <Skeleton className="h-96 w-full max-w-md" />
      </div>
    );
  }

  return <>{children}</>;
}

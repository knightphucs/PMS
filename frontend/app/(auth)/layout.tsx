import { GuestOnly } from '@/components/auth/auth-guard';

/**
 * Route group `(auth)` — dấu ngoặc nghĩa là tên nhóm KHÔNG vào URL, nên đường dẫn vẫn là
 * /login chứ không phải /auth/login. Nhờ vậy hai màn đăng nhập/đăng ký dùng chung một
 * layout căn giữa mà không phải lồng thêm một cấp URL.
 */
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <GuestOnly>
      <div className="bg-muted/30 flex min-h-svh flex-col items-center justify-center gap-6 p-6">
        <div className="flex items-center gap-2 font-semibold">
          <span className="bg-primary text-primary-foreground grid size-8 place-items-center rounded text-sm font-bold">
            PMS
          </span>
          <span className="text-lg">Quản lý dự án</span>
        </div>
        <div className="w-full max-w-md">{children}</div>
      </div>
    </GuestOnly>
  );
}

import { AuthGuard } from '@/components/auth/auth-guard';
import { AppShell } from '@/components/layout/app-shell';

/**
 * Route group `(app)` — mọi trang cần đăng nhập nằm trong đây.
 *
 * Guard đặt một lần ở layout thay vì lặp ở từng trang: thêm trang mới vào nhóm là tự
 * được bảo vệ, không phải nhớ. Cùng nguyên tắc "bảo đảm bằng cấu trúc hơn bảo đảm bằng
 * kỷ luật lập trình viên" mà ADR-023 đã dùng cho chữ ký repository phía backend.
 */
export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <AuthGuard>
      <AppShell>{children}</AppShell>
    </AuthGuard>
  );
}

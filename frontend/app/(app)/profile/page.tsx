'use client';

import { InfoIcon } from 'lucide-react';
import Link from 'next/link';

import { PageHeader } from '@/components/common/page-header';
import { UserAvatar } from '@/components/common/user-avatar';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { SYSTEM_ROLE_LABEL } from '@/types/enums';
import { useAuthStore } from '@/store/auth-store';

/**
 * Hồ sơ cá nhân — **CHỈ ĐỌC** (ADR-049).
 *
 * 🔴 Không có nút Sửa, và đó là quyết định chứ không phải việc chưa làm xong. Backend hiện
 * không có `PUT /employees/me` lẫn đổi mật khẩu khi đã đăng nhập; và kể cả khi thêm vào,
 * `GET /auth/me` dựng DTO **từ CLAIM chứ không đọc DB** (ADR-045), nên đổi tên sẽ không
 * hiện ra cho tới khi access token được làm mới — tối đa 15 phút. Một màn hình báo "đã lưu"
 * rồi vẫn hiện tên cũ là lời nói dối tệ hơn hẳn việc không có nút. Xem ADR-049 để biết hai
 * đường ra đã cân nhắc cho phiên sau.
 *
 * Nguồn dữ liệu là `useAuthStore`, KHÔNG gọi `/auth/me`: phiên đã khôi phục qua
 * `/auth/refresh` và cả hai endpoint đều dựng DTO từ cùng một bộ claim, nên một request
 * nữa không mang lại thông tin nào mới.
 *
 * 📌 **Cố ý KHÔNG liệt kê `permissions`** (gỡ 2026-08-05, ngay trong ngày dựng). Bản đầu có
 * một thẻ "Quyền hệ thống" đổ ra `projects:create`, `employees:manage`… — đó là màn hình cho
 * **người viết code**, không phải cho người dùng. Người dùng cuối không hành động được gì
 * với danh sách đó: họ không tự cấp quyền cho mình, và khi thiếu quyền thì UI đã ẩn nút rồi.
 * Vai trò hệ thống hiện ở badge là đủ; ai cần xem ma trận quyền thì vào `/admin/roles`.
 */
export default function ProfilePage() {
  const user = useAuthStore((s) => s.user);

  // `AuthGuard` ở `app/(app)/layout.tsx` đã chặn người chưa đăng nhập; nhánh này chỉ để
  // khoảnh khắc giữa hai lần render không nổ.
  if (!user) return null;

  return (
    <div className="grid min-w-0 gap-5">
      <PageHeader title="Hồ sơ của tôi" description="Thông tin tài khoản của bạn." />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-4">
          <UserAvatar id={user.id} name={user.name} className="size-14 text-lg" />
          <div className="grid min-w-0 gap-1">
            <span className="truncate text-base font-semibold">{user.name}</span>
            <span className="text-muted-foreground truncate text-sm">{user.email}</span>
            <Badge variant="secondary" className="mt-1 w-fit">
              {SYSTEM_ROLE_LABEL[user.systemRole]}
            </Badge>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="text-muted-foreground flex gap-3 text-sm">
          <InfoIcon className="mt-0.5 size-4 shrink-0" />
          <div className="grid min-w-0 gap-1">
            <span>
              Hệ thống chưa có đường sửa hồ sơ, nên tên và email chỉ đổi được qua quản trị viên.
            </span>
            <span>
              Muốn đổi mật khẩu, dùng luồng{' '}
              <Link href="/forgot-password" className="text-primary underline-offset-4 hover:underline">
                đặt lại mật khẩu qua email
              </Link>
              .
            </span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

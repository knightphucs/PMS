'use client';

import { ShieldXIcon } from 'lucide-react';

import { EmptyState } from '@/components/common/empty-state';
import { hasAnyPermission, type SystemPermission } from '@/lib/auth/system-permissions';
import { useAuthStore } from '@/store/auth-store';

/**
 * Ẩn nội dung khỏi người không có quyền tầng 1 (ADR-045).
 *
 * 🔴 Đây là **tiện ích giao diện, KHÔNG phải chốt chặn an ninh.** Chốt chặn thật nằm ở
 * policy `[Authorize(Policy = ...)]` phía backend; component này chỉ để người dùng khỏi
 * nhìn một trang trống rồi tự hỏi vì sao mọi thứ báo lỗi. Đừng bao giờ dựa vào nó để bảo
 * vệ dữ liệu — dữ liệu chỉ được bảo vệ vì API từ chối, không vì UI không vẽ.
 *
 * Khác `AuthGuard` một điểm quan trọng: **không điều hướng đi đâu cả.** Người dùng đã đăng
 * nhập hợp lệ và URL của họ có thật; đá họ về `/projects` sẽ trông như ứng dụng hỏng. Nói
 * thẳng là thiếu quyền, đúng chỗ họ đang đứng.
 */
export function PermissionGate({
  permissions,
  children,
}: {
  /** Cần ÍT NHẤT MỘT trong số này. */
  permissions: readonly SystemPermission[];
  children: React.ReactNode;
}) {
  const user = useAuthStore((s) => s.user);

  if (!hasAnyPermission(user, permissions)) {
    return (
      <EmptyState
        icon={<ShieldXIcon className="size-8" />}
        title="Bạn không có quyền truy cập khu vực này"
        description="Khu quản trị dành cho tài khoản được cấp quyền hệ thống. Nếu bạn cho rằng đây là nhầm lẫn, hãy liên hệ quản trị viên — quyền vừa được cấp chỉ có hiệu lực sau khi bạn đăng nhập lại."
      />
    );
  }

  return <>{children}</>;
}

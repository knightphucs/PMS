'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  SYSTEM_PERMISSIONS,
  hasPermission,
  type SystemPermission,
} from '@/lib/auth/system-permissions';
import { cn } from '@/lib/utils';
import { useAuthStore } from '@/store/auth-store';

/**
 * Bốn tab của khu quản trị, mỗi tab gác bằng ĐÚNG quyền mà API của nó đòi.
 *
 * ⚠️ Dùng `usePathname()` chứ không `useSelectedLayoutSegment()` — và tuyệt đối KHÔNG gom
 * bốn trang vào route group `admin/(tabs)/`: flight router state giữ nguyên segment nhóm
 * nên `useSelectedLayoutSegment()` sẽ trả `'(tabs)'` và cả thanh tab mất trạng thái active,
 * hỏng im lặng chỉ thấy bằng mắt (bài học ADR-043).
 */
const TABS: { href: string; label: string; permission: SystemPermission }[] = [
  { href: '/admin/employees', label: 'Nhân sự', permission: SYSTEM_PERMISSIONS.employeesManage },
  { href: '/admin/roles', label: 'Phân quyền', permission: SYSTEM_PERMISSIONS.rolesManage },
  { href: '/admin/labels', label: 'Nhãn toàn cục', permission: SYSTEM_PERMISSIONS.labelsManage },
  { href: '/admin/audit-logs', label: 'Nhật ký hệ thống', permission: SYSTEM_PERMISSIONS.auditRead },
];

export const ADMIN_PERMISSIONS = TABS.map((t) => t.permission);

export function AdminTabs() {
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);

  // Chỉ hiện tab mà người này VÀO ĐƯỢC. Hiện đủ bốn rồi để họ bấm vào cái thứ năm và ăn
  // 403 là bắt người dùng tự dò ranh giới quyền của mình.
  const visible = TABS.filter((tab) => hasPermission(user, tab.permission));

  if (visible.length <= 1) return null;

  return (
    <div className="border-b">
      <nav aria-label="Khu quản trị" className="-mb-px flex gap-1 overflow-x-auto">
        {visible.map((tab) => {
          const active = pathname === tab.href || pathname.startsWith(`${tab.href}/`);

          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={active ? 'page' : undefined}
              className={cn(
                'border-b-2 px-3 py-2 text-sm font-medium whitespace-nowrap transition-colors',
                active
                  ? 'border-primary text-foreground'
                  : 'text-muted-foreground hover:text-foreground border-transparent',
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}

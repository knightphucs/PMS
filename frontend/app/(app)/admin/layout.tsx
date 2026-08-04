'use client';

import { ADMIN_PERMISSIONS, AdminTabs } from '@/components/admin/admin-tabs';
import { PermissionGate } from '@/components/admin/permission-gate';

/**
 * Khu quản trị hệ thống.
 *
 * Thừa kế `AuthGuard` + `AppShell` từ `app/(app)/layout.tsx` — không dựng lại.
 *
 * ⚠️ Bốn trang con nằm THẲNG dưới `admin/`, KHÔNG gom vào route group `admin/(tabs)/`.
 * Route group không tính vào đường dẫn nhưng flight router state vẫn giữ segment nhóm, nên
 * `useSelectedLayoutSegment()` trả `'(tabs)'` và thanh tab mất trạng thái active — hỏng im
 * lặng, chỉ phát hiện bằng mắt (bài học đã trả giá ở ADR-043).
 *
 * 📌 Gác bằng PERMISSION chứ không bằng `systemRole === 'SystemAdmin'`. Sau ADR-045 vai trò
 * chỉ còn là định danh; quyền mới là thứ quyết định, và nó đổi được bằng dữ liệu ở
 * `/admin/roles`. Kiểm theo vai trò là dựng lại đúng mô hình cũ ở nửa client.
 */
export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <PermissionGate permissions={ADMIN_PERMISSIONS}>
      <div className="grid gap-5">
        <AdminTabs />
        {children}
      </div>
    </PermissionGate>
  );
}

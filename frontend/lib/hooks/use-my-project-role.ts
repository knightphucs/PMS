'use client';

import { useMemo } from 'react';

import { useProjectOverview } from '@/lib/hooks/use-projects';
import { useAuthStore } from '@/store/auth-store';
import type { RoleInProject } from '@/types/enums';

/**
 * Vai trò của TÔI trong project — nguồn duy nhất để ẩn/hiện mọi nút trên trang chi tiết.
 *
 * ⚠️ `GET /projects` trả `roleInProject` của người gọi, nhưng `GET /projects/{id}` thì
 * **KHÔNG** — nó chỉ có `members[]` với vai trò của TỪNG người. Nên phải tự tìm mình
 * trong danh sách đó; đây là cách duy nhất, không phải lối tắt.
 *
 * `isResolving` tồn tại để không hiện nút rồi giấu đi ở render kế tiếp: trong lúc chưa
 * biết vai trò thì `role` là `null`, mà `null` trong `lib/tasks/permissions.ts` nghĩa là
 * "không được phép" — nếu render thẳng thì màn hình sẽ nháy từ trạng thái không-có-nút
 * sang có-nút.
 */
export function useMyProjectRole(projectId: string | null): {
  role: RoleInProject | null;
  isResolving: boolean;
  myEmployeeId: string | null;
} {
  const overview = useProjectOverview(projectId);
  const myEmployeeId = useAuthStore((state) => state.user?.id ?? null);

  const role = useMemo(() => {
    if (!overview.data || !myEmployeeId) return null;

    const me = overview.data.members.find((member) => member.employeeId === myEmployeeId);

    // `Pending`/`Declined` KHÔNG phải thành viên thật — họ nằm trong `members[]` nhưng
    // chưa có quyền gì cả. Bỏ qua bước kiểm này là cấp quyền PM cho người mới được mời.
    return me?.invitationStatus === 'Accepted' ? me.roleInProject : null;
  }, [overview.data, myEmployeeId]);

  return { role, isResolving: overview.isPending, myEmployeeId };
}

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  changeSystemRole,
  listAdminEmployees,
  listPermissions,
  listRolePermissions,
  lockEmployee,
  unlockEmployee,
  updateRolePermissions,
} from '@/lib/api/endpoints/admin';
import { adminEmployeeKeys, rolePermissionKeys, systemAuditKeys } from '@/lib/hooks/keys';
import type {
  ChangeSystemRoleRequest,
  LockAccountRequest,
  UpdateRolePermissionsRequest,
} from '@/types/admin';
import type { PagedRequest } from '@/types/common';
import type { SystemRole } from '@/types/enums';

// ---------- Nhân sự ----------

/** ⚠️ Cần quyền `employees:manage`; người khác nhận 403 chứ không phải danh sách rỗng. */
export function useAdminEmployees(request: PagedRequest) {
  return useQuery({
    queryKey: adminEmployeeKeys.list(request),
    queryFn: ({ signal }) => listAdminEmployees(request, signal),
    // Danh sách phân trang: giữ dữ liệu cũ khi đổi trang/từ khóa để bảng không nháy về
    // skeleton ở mỗi phím gõ.
    placeholderData: (previous) => previous,
  });
}

/**
 * Ba mutation dưới đây đều làm mới **cả nhật ký hệ thống**: khóa/mở/đổi vai trò đều sinh
 * một dòng `ActivityLog` cấp hệ thống, nên hai màn quản trị phải khớp nhau ngay.
 */
function useAdminEmployeeMutation<TVars>(mutationFn: (vars: TVars) => Promise<void>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminEmployeeKeys.all });
      void queryClient.invalidateQueries({ queryKey: systemAuditKeys.all });
    },
  });
}

export function useLockEmployee() {
  return useAdminEmployeeMutation(({ id, body }: { id: string; body: LockAccountRequest }) =>
    lockEmployee(id, body),
  );
}

export function useUnlockEmployee() {
  return useAdminEmployeeMutation(({ id }: { id: string }) => unlockEmployee(id));
}

/** ⚠️ Thu hồi toàn bộ refresh token của người bị đổi vai trò — họ phải đăng nhập lại. */
export function useChangeSystemRole() {
  return useAdminEmployeeMutation(({ id, body }: { id: string; body: ChangeSystemRoleRequest }) =>
    changeSystemRole(id, body),
  );
}

// ---------- Phân quyền vai trò (ADR-045) ----------

/** Danh mục quyền — mã + mô tả tiếng Việt do backend cấp. */
export function usePermissionCatalog() {
  return useQuery({
    queryKey: rolePermissionKeys.catalog(),
    queryFn: ({ signal }) => listPermissions(signal),
    // Danh mục là ĐÓNG (đổi được chỉ bằng migration) nên gần như bất biến trong một phiên.
    staleTime: 30 * 60 * 1000,
  });
}

export function useRolePermissions() {
  return useQuery({
    queryKey: rolePermissionKeys.matrix(),
    queryFn: ({ signal }) => listRolePermissions(signal),
  });
}

/**
 * Ghi đè tập quyền của một vai trò.
 *
 * ⚠️ KHÔNG cập nhật lạc quan: lệnh này có hai đường từ chối ở server (400 mã ngoài danh
 * mục, 409 gỡ `roles:manage` khỏi SystemAdmin), và hiện ô tích đã đổi rồi mới bật lại là
 * cách nhanh nhất để người quản trị tin sai về trạng thái phân quyền thật.
 *
 * ⚠️ Cũng KHÔNG đụng tới phiên hiện tại: quyền của chính người đang thao tác chỉ đổi ở
 * access token kế tiếp. Màn hình phải nói rõ điều đó thay vì tự ý đăng xuất họ.
 */
export function useUpdateRolePermissions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ role, body }: { role: SystemRole; body: UpdateRolePermissionsRequest }) =>
      updateRolePermissions(role, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: rolePermissionKeys.all });
      void queryClient.invalidateQueries({ queryKey: systemAuditKeys.all });
    },
  });
}

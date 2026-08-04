'use client';

import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { FormError } from '@/components/form/form-error';
import { WarningBanner } from '@/components/common/warning-banner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import { useChangeSystemRole } from '@/lib/hooks/use-admin';
import type { EmployeeAdminResponse } from '@/types/admin';
import { SYSTEM_ROLE_LABEL, type SystemRole } from '@/types/enums';

const ROLES: SystemRole[] = ['User', 'SystemAdmin'];

/**
 * Đổi vai trò hệ thống của một người.
 *
 * 📌 Sau ADR-045, vai trò không còn là trục phân quyền — nó là cái NHÃN quyết định người
 * này nhận tập quyền nào từ bảng `RolePermissions`. Vì vậy dialog nói rõ "xem/sửa tập quyền
 * ở tab Phân quyền" thay vì để người quản trị đoán vai trò này có nghĩa gì.
 */
export function ChangeRoleDialog({
  employee,
  onClose,
}: {
  employee: EmployeeAdminResponse | null;
  onClose: () => void;
}) {
  const [role, setRole] = useState<SystemRole>('User');
  const [error, setError] = useState<string | null>(null);
  const changeRole = useChangeSystemRole();

  useEffect(() => {
    if (employee) {
      setRole(employee.systemRole);
      setError(null);
    }
  }, [employee]);

  const unchanged = employee?.systemRole === role;

  const submit = async () => {
    if (!employee || unchanged) return;
    setError(null);

    try {
      await changeRole.mutateAsync({ id: employee.id, body: { role } });
      toast.success(
        `${employee.email} nay là ${SYSTEM_ROLE_LABEL[role]}. Họ cần đăng nhập lại để quyền mới có hiệu lực.`,
      );
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  return (
    <Dialog open={employee !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Đổi vai trò hệ thống</DialogTitle>
          <DialogDescription>
            {employee?.name} ({employee?.email})
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          <Label htmlFor="system-role">Vai trò</Label>
          <Select value={role} onValueChange={(value) => setRole(value as SystemRole)}>
            <SelectTrigger id="system-role">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ROLES.map((r) => (
                <SelectItem key={r} value={r}>
                  {SYSTEM_ROLE_LABEL[r]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-muted-foreground text-xs">
            Vai trò quyết định người này nhận tập quyền nào. Xem và sửa tập quyền ở tab{' '}
            <strong className="text-foreground">Phân quyền</strong>.
          </p>
        </div>

        <WarningBanner title="Thao tác này đăng xuất người đó khỏi mọi thiết bị.">
          Vai trò đi trong access token, nên toàn bộ refresh token của họ bị thu hồi để tập
          quyền cũ không sống thêm 7 ngày nữa.
        </WarningBanner>

        <FormError message={error} />

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={changeRole.isPending}>
            Hủy
          </Button>
          <Button
            disabled={unchanged || changeRole.isPending}
            onClick={() => void submit()}
          >
            {changeRole.isPending ? 'Đang lưu…' : 'Lưu vai trò'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

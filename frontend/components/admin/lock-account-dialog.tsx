'use client';

import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { FormError } from '@/components/form/form-error';
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
import { Textarea } from '@/components/ui/textarea';
import { errorMessage } from '@/lib/api/problem';
import { useLockEmployee } from '@/lib/hooks/use-admin';
import type { EmployeeAdminResponse } from '@/types/admin';

const MAX_REASON = 256;

/**
 * Khóa một tài khoản. Lý do là BẮT BUỘC — backend trả 400 nếu rỗng, và nó được ghi thẳng
 * vào nhật ký hệ thống nên là thứ duy nhất giải thích được hành động này về sau.
 */
export function LockAccountDialog({
  employee,
  onClose,
}: {
  employee: EmployeeAdminResponse | null;
  onClose: () => void;
}) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const lock = useLockEmployee();

  useEffect(() => {
    if (employee) {
      setReason('');
      setError(null);
    }
  }, [employee]);

  const submit = async () => {
    if (!employee) return;
    setError(null);

    try {
      await lock.mutateAsync({ id: employee.id, body: { reason: reason.trim() } });
      toast.success(`Đã khóa tài khoản ${employee.email}. Mọi phiên đăng nhập đã bị thu hồi.`);
      onClose();
    } catch (err) {
      // Giữ dialog mở và hiện thông điệp của backend tại chỗ. Ba nguyên nhân 409/400 ở
      // đây có ba cách khắc phục khác hẳn nhau ("đây là admin cuối cùng" / "đã khóa rồi" /
      // "không tự khóa mình được"), gộp thành một toast đỏ là bỏ phí sự phân biệt đó.
      setError(errorMessage(err));
    }
  };

  return (
    <Dialog open={employee !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Khóa tài khoản</DialogTitle>
          <DialogDescription>
            {employee?.name} ({employee?.email}) sẽ bị đăng xuất khỏi mọi thiết bị và không
            đăng nhập lại được cho tới khi được mở khóa.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          <Label htmlFor="lock-reason">Lý do khóa</Label>
          <Textarea
            id="lock-reason"
            autoFocus
            rows={3}
            maxLength={MAX_REASON}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Ví dụ: nhân sự đã nghỉ việc từ 01/08/2026."
          />
          <p className="text-muted-foreground text-xs">
            Bắt buộc, tối đa {MAX_REASON} ký tự. Lý do này được ghi vào nhật ký hệ thống.
          </p>
        </div>

        <FormError message={error} />

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={lock.isPending}>
            Hủy
          </Button>
          <Button
            variant="destructive"
            disabled={!reason.trim() || lock.isPending}
            onClick={() => void submit()}
          >
            {lock.isPending ? 'Đang khóa…' : 'Khóa tài khoản'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRoundIcon } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useAuth } from '@/lib/hooks/use-auth';
import {
  changePasswordSchema,
  type ChangePasswordValues,
} from '@/lib/validation/auth-schema';

/** ⚠️ Phải khớp ĐÚNG tên property của `ChangePasswordRequest` phía backend. */
const FIELDS = ['currentPassword', 'newPassword', 'confirmPassword'] as const;

/**
 * Đổi mật khẩu khi ĐANG đăng nhập (ADR-049) — khác luồng `/forgot-password` ở chỗ xác minh
 * bằng mật khẩu hiện tại, không cần rời khỏi trang.
 *
 * Thành công thì mọi phiên KHÁC bị thu hồi (cùng tiền lệ đặt-lại-mật-khẩu-qua-email), nhưng
 * tab này VẪN đăng nhập — `useAuth().changePassword` đã tự `setSession` lại bằng token mới
 * server phát kèm phản hồi, nên không cần điều hướng về `/login`.
 */
export function ChangePasswordDialog() {
  const [open, setOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const { changePassword } = useAuth();

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next) {
      reset();
      setFormError(null);
    }
  };

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);
      try {
        await changePassword(values);
        toast.success('Đã đổi mật khẩu. Các phiên đăng nhập khác (nếu có) đã bị đăng xuất.');
        handleOpenChange(false);
      } catch (error) {
        if (!applyServerErrors(error, setError, FIELDS)) {
          setFormError(errorMessage(error));
        }
      }
    },
    () => setFormError(null),
  );

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger
        render={
          <Button variant="outline" size="sm">
            <KeyRoundIcon className="size-4" />
            Đổi mật khẩu
          </Button>
        }
      />
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Đổi mật khẩu</DialogTitle>
          <DialogDescription>
            Các phiên đăng nhập khác của bạn (nếu có, ví dụ trên thiết bị khác) sẽ bị đăng
            xuất. Phiên trên tab này vẫn tiếp tục.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Mật khẩu hiện tại"
            type="password"
            autoFocus
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            {...register('currentPassword')}
          />

          <Field
            label="Mật khẩu mới"
            type="password"
            autoComplete="new-password"
            hint="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số."
            error={errors.newPassword?.message}
            {...register('newPassword')}
          />

          <Field
            label="Nhập lại mật khẩu mới"
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />

          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Đang lưu…' : 'Đổi mật khẩu'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

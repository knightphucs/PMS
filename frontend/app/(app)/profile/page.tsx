'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { CheckIcon, InfoIcon, PencilIcon, XIcon } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { PageHeader } from '@/components/common/page-header';
import { UserAvatar } from '@/components/common/user-avatar';
import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { ChangePasswordDialog } from '@/components/profile/change-password-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useAuth } from '@/lib/hooks/use-auth';
import { updateProfileSchema, type UpdateProfileValues } from '@/lib/validation/auth-schema';
import { SYSTEM_ROLE_LABEL } from '@/types/enums';

/** ⚠️ Phải khớp ĐÚNG tên property của `UpdateProfileRequest` phía backend. */
const FIELDS = ['name'] as const;

/**
 * Hồ sơ cá nhân (ADR-049).
 *
 * 🔴 Đã có đường ghi từ 2026-08-06 — trước đó màn này CỐ Ý chỉ đọc, vì `GET /auth/me` dựng
 * DTO từ JWT claim (ADR-045) chứ không đọc DB: một nút "Lưu" ngây thơ sẽ báo thành công rồi
 * vẫn hiện tên cũ tới 15 phút. Giải bằng cách cho `PUT /auth/profile` VÀ
 * `POST /auth/change-password` trả về `AuthenticatedResponse` mới — `useAuth().updateProfile`
 * / `changePassword` gọi `setSession(...)` lại ngay với response đó, nên tab này thấy tên/
 * phiên mới NGAY LẬP TỨC, không cần chờ refresh. Các tab KHÁC (nếu có) vẫn giữ claim cũ tới
 * lần refresh kế tiếp của chúng — đánh đổi đã ghi trong ADR-049, không phải bug.
 *
 * Email vẫn không đổi được từ đây: đổi email là đổi định danh đăng nhập, cần luồng xác minh
 * riêng ngoài phạm vi hôm nay.
 *
 * 📌 Cố ý KHÔNG liệt kê `permissions` — đó là màn hình cho người viết code, không phải người
 * dùng cuối. Ai cần xem ma trận quyền thì vào `/admin/roles`.
 */
export default function ProfilePage() {
  const { user, updateProfile } = useAuth();
  const [editingName, setEditingName] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UpdateProfileValues>({
    resolver: zodResolver(updateProfileSchema),
    defaultValues: { name: user?.name ?? '' },
  });

  // `AuthGuard` ở `app/(app)/layout.tsx` đã chặn người chưa đăng nhập; nhánh này chỉ để
  // khoảnh khắc giữa hai lần render không nổ.
  if (!user) return null;

  const startEditing = () => {
    reset({ name: user.name });
    setFormError(null);
    setEditingName(true);
  };

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);
      try {
        await updateProfile(values);
        toast.success('Đã cập nhật tên.');
        setEditingName(false);
      } catch (error) {
        if (!applyServerErrors(error, setError, FIELDS)) {
          setFormError(errorMessage(error));
        }
      }
    },
    () => setFormError(null),
  );

  return (
    <div className="grid min-w-0 gap-5">
      <PageHeader title="Hồ sơ của tôi" description="Thông tin tài khoản của bạn." />

      <Card>
        <CardContent className="flex flex-wrap items-start gap-4">
          <UserAvatar id={user.id} name={user.name} className="size-14 shrink-0 text-lg" />

          <div className="grid min-w-0 flex-1 gap-1">
            {editingName ? (
              <form onSubmit={onSubmit} noValidate className="grid max-w-sm min-w-0 gap-3">
                <FormError message={formError} />
                <Field
                  label="Tên hiển thị"
                  autoFocus
                  error={errors.name?.message}
                  {...register('name')}
                />
                <div className="flex gap-2">
                  <Button type="submit" size="sm" disabled={isSubmitting}>
                    <CheckIcon className="size-4" />
                    {isSubmitting ? 'Đang lưu…' : 'Lưu'}
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => setEditingName(false)}
                  >
                    <XIcon className="size-4" />
                    Hủy
                  </Button>
                </div>
              </form>
            ) : (
              <>
                <div className="flex min-w-0 items-center gap-1">
                  <span className="truncate text-base font-semibold">{user.name}</span>
                  <Button
                    type="button"
                    size="icon-sm"
                    variant="ghost"
                    aria-label="Sửa tên"
                    onClick={startEditing}
                  >
                    <PencilIcon className="size-3.5" />
                  </Button>
                </div>
                <span className="text-muted-foreground truncate text-sm">{user.email}</span>
                <Badge variant="secondary" className="mt-1 w-fit">
                  {SYSTEM_ROLE_LABEL[user.systemRole]}
                </Badge>
              </>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3">
          <div className="grid min-w-0 gap-1">
            <span className="text-sm font-medium">Mật khẩu</span>
            <span className="text-muted-foreground text-sm">
              Đổi mật khẩu đăng nhập của tài khoản này.
            </span>
          </div>
          <ChangePasswordDialog />
        </CardContent>
      </Card>

      <Card>
        <CardContent className="text-muted-foreground flex gap-3 text-sm">
          <InfoIcon className="mt-0.5 size-4 shrink-0" />
          <span>Email không đổi được từ đây — liên hệ quản trị viên nếu cần cập nhật.</span>
        </CardContent>
      </Card>
    </div>
  );
}

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRoundIcon } from 'lucide-react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { Button, buttonVariants } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { resetPassword } from '@/lib/api/endpoints/auth';
import { errorMessage } from '@/lib/api/problem';
import { cn } from '@/lib/utils';
import { resetPasswordSchema, type ResetPasswordValues } from '@/lib/validation/auth-schema';

function ResetPasswordForm() {
  const router = useRouter();
  const token = useSearchParams().get('token') ?? '';
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await resetPassword({ token, ...values });

      // 🔴 LUÔN về /login, không tự đăng nhập hộ: đặt lại mật khẩu thành công sẽ thu hồi
      // MỌI phiên (kể cả trên thiết bị khác), nên không còn phiên nào để tiếp tục.
      toast.success('Đã đổi mật khẩu. Hãy đăng nhập lại bằng mật khẩu mới.');
      router.replace('/login');
    } catch (error) {
      // Token sai / hết hạn / đã dùng đều là CÙNG một 400 với cùng một thông điệp — thông
      // tin phân biệt ba trường hợp cố tình không được trả về, đừng cố đoán để hiện thông
      // báo "thông minh" hơn.
      setFormError(errorMessage(error));
    }
  });

  // Không có token trong URL thì form vô nghĩa — nói thẳng thay vì để người dùng gõ xong
  // mật khẩu rồi mới nhận lỗi.
  if (!token) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Liên kết không hợp lệ</CardTitle>
          <CardDescription>
            Đường dẫn này thiếu mã đặt lại mật khẩu. Hãy mở lại liên kết trong email, hoặc
            yêu cầu gửi một liên kết mới.
          </CardDescription>
        </CardHeader>

        <CardFooter className="mt-6 flex-col gap-3">
          {/* Base UI (shadcn v4) không có `asChild` — xem chú thích ở forgot-password. */}
          <Link href="/forgot-password" className={cn(buttonVariants(), 'w-full')}>
            Yêu cầu liên kết mới
          </Link>
          <Link
            href="/login"
            className="text-muted-foreground text-sm underline-offset-4 hover:underline"
          >
            Về trang đăng nhập
          </Link>
        </CardFooter>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <KeyRoundIcon className="text-primary size-5" />
          Đặt mật khẩu mới
        </CardTitle>
        <CardDescription>
          Sau khi đổi, mọi phiên đăng nhập hiện tại của bạn sẽ bị đăng xuất — kể cả trên
          thiết bị khác.
        </CardDescription>
      </CardHeader>

      <form onSubmit={onSubmit} noValidate>
        <CardContent className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Mật khẩu mới"
            type="password"
            autoComplete="new-password"
            autoFocus
            error={errors.newPassword?.message}
            hint="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số."
            {...register('newPassword')}
          />

          <Field
            label="Nhập lại mật khẩu mới"
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />
        </CardContent>

        <CardFooter className="mt-6 flex-col gap-4">
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Đang đổi mật khẩu…' : 'Đổi mật khẩu'}
          </Button>
          <Link
            href="/login"
            className="text-muted-foreground text-sm underline-offset-4 hover:underline"
          >
            Về trang đăng nhập
          </Link>
        </CardFooter>
      </form>
    </Card>
  );
}

export default function ResetPasswordPage() {
  // `useSearchParams` cần Suspense bao ngoài, nếu không `next build` đỏ ở bước prerender —
  // lỗi chỉ hiện ở production build chứ `next dev` chạy im (cùng bẫy với trang đăng nhập).
  return (
    <Suspense fallback={null}>
      <ResetPasswordForm />
    </Suspense>
  );
}

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense, useState } from 'react';
import { useForm } from 'react-hook-form';

import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { errorMessage } from '@/lib/api/problem';
import { applyServerErrors } from '@/lib/form';
import { useAuth } from '@/lib/hooks/use-auth';
import { registerSchema, type RegisterValues } from '@/lib/validation/auth-schema';

const FIELDS = ['name', 'email', 'password', 'confirmPassword'] as const;

function RegisterForm() {
  const { register: registerAccount } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: '', email: '', password: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await registerAccount(values);
      // Đăng ký trả về phiên luôn, không phải đăng nhập lại. `next` cho luồng chấp nhận
      // lời mời project (`/invitations/{token}?...`) quay lại đúng chỗ, giống /login.
      router.replace(searchParams.get('next') || '/projects');
    } catch (error) {
      // Email trùng trả 409 (không phải lỗi field) nên rơi xuống FormError.
      if (!applyServerErrors(error, setError, FIELDS)) {
        setFormError(errorMessage(error));
      }
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Tạo tài khoản</CardTitle>
        <CardDescription>
          Tài khoản mới có quyền tạo dự án và tự trở thành quản lý của dự án mình tạo.
        </CardDescription>
      </CardHeader>

      <form onSubmit={onSubmit} noValidate>
        <CardContent className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Họ và tên"
            autoComplete="name"
            autoFocus
            placeholder="Nguyễn Văn A"
            error={errors.name?.message}
            {...register('name')}
          />

          <Field
            label="Email"
            type="email"
            autoComplete="email"
            placeholder="ban@congty.com"
            error={errors.email?.message}
            {...register('email')}
          />

          <Field
            label="Mật khẩu"
            type="password"
            autoComplete="new-password"
            hint="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số."
            error={errors.password?.message}
            {...register('password')}
          />

          <Field
            label="Nhập lại mật khẩu"
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />
        </CardContent>

        <CardFooter className="mt-6 flex-col gap-4">
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Đang tạo tài khoản…' : 'Đăng ký'}
          </Button>
          <p className="text-muted-foreground text-sm">
            Đã có tài khoản?{' '}
            <Link href="/login" className="text-foreground font-medium underline-offset-4 hover:underline">
              Đăng nhập
            </Link>
          </p>
        </CardFooter>
      </form>
    </Card>
  );
}

export default function RegisterPage() {
  // `useSearchParams` cần Suspense bao ngoài, nếu không `next build` sẽ đỏ ở bước
  // prerender — lỗi chỉ hiện ở production build chứ `next dev` chạy im (giống /login).
  return (
    <Suspense fallback={null}>
      <RegisterForm />
    </Suspense>
  );
}

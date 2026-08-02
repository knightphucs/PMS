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
import { loginSchema, type LoginValues } from '@/lib/validation/auth-schema';

const FIELDS = ['email', 'password'] as const;

function LoginForm() {
  const { login } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await login(values);
      // AuthGuard nhét đích đến vào ?next= khi đá người dùng ra, nên quay lại đúng chỗ.
      router.replace(searchParams.get('next') || '/projects');
    } catch (error) {
      // ⚠️ Đăng nhập bằng mật khẩu KHÔNG ĐỦ ĐỘ PHỨC TẠP trả 400 kèm lỗi field, không
      // phải 401 — backend áp cùng bộ luật mật khẩu cho cả LoginRequest. Không gắn lỗi
      // ngược vào form thì người dùng thấy "sai email hoặc mật khẩu" cho một lỗi định dạng.
      if (!applyServerErrors(error, setError, FIELDS)) {
        setFormError(errorMessage(error));
      }
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>Đăng nhập</CardTitle>
        <CardDescription>Nhập email và mật khẩu để vào hệ thống.</CardDescription>
      </CardHeader>

      <form onSubmit={onSubmit} noValidate>
        <CardContent className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Email"
            type="email"
            autoComplete="email"
            autoFocus
            placeholder="ban@congty.com"
            error={errors.email?.message}
            {...register('email')}
          />

          <Field
            label="Mật khẩu"
            type="password"
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
        </CardContent>

        <CardFooter className="mt-6 flex-col gap-4">
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Đang đăng nhập…' : 'Đăng nhập'}
          </Button>
          <p className="text-muted-foreground text-sm">
            Chưa có tài khoản?{' '}
            <Link href="/register" className="text-foreground font-medium underline-offset-4 hover:underline">
              Đăng ký
            </Link>
          </p>
        </CardFooter>
      </form>
    </Card>
  );
}

export default function LoginPage() {
  // `useSearchParams` cần Suspense bao ngoài, nếu không `next build` sẽ đỏ ở bước
  // prerender — lỗi chỉ hiện ở production build chứ `next dev` chạy im.
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}

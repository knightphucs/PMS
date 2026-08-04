'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { MailCheckIcon } from 'lucide-react';
import Link from 'next/link';
import { useState } from 'react';
import { useForm } from 'react-hook-form';

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
import { forgotPassword } from '@/lib/api/endpoints/auth';
import { errorMessage } from '@/lib/api/problem';
import { cn } from '@/lib/utils';
import {
  forgotPasswordSchema,
  type ForgotPasswordValues,
} from '@/lib/validation/auth-schema';

/**
 * 🔴 **MỘT thông điệp duy nhất cho MỌI kết quả thành công** — đây là toàn bộ điểm mấu chốt
 * của màn này (ADR-041).
 *
 * Backend trả 204 kể cả khi email không tồn tại, cố ý, để endpoint này không trở thành công
 * cụ dò xem ai đã đăng ký hệ thống. Nếu UI hiện "email không tồn tại" — hay chỉ cần hiện hai
 * thông điệp khác nhau cho hai trường hợp — thì kênh rò rỉ đó được dựng lại nguyên vẹn ở
 * phía client, và toàn bộ công backend bỏ ra thành vô nghĩa.
 *
 * Hệ quả kéo theo: cũng KHÔNG được có trạng thái "đang gửi lại…" hay đếm ngược khác nhau
 * theo email, và không log gì ra console phân biệt hai nhánh.
 */
const SENT_MESSAGE =
  'Nếu email này đã đăng ký, chúng tôi vừa gửi hướng dẫn đặt lại mật khẩu tới đó. Hãy kiểm tra cả hộp thư rác.';

export default function ForgotPasswordPage() {
  const [sent, setSent] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await forgotPassword(values);
      setSent(true);
    } catch (error) {
      // Chỉ lỗi THẬT mới tới đây: 429 (quá 3 lần/phút theo IP) hoặc mất kết nối. Email
      // không tồn tại KHÔNG rơi vào nhánh này — nó trả 204 như mọi email khác.
      setFormError(errorMessage(error));
    }
  });

  if (sent) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MailCheckIcon className="text-primary size-5" />
            Đã gửi yêu cầu
          </CardTitle>
          <CardDescription>{SENT_MESSAGE}</CardDescription>
        </CardHeader>

        <CardFooter className="mt-6 flex-col gap-3">
          {/* Base UI (shadcn v4) không có `asChild`; nút-là-liên-kết thì gắn thẳng
              `buttonVariants()` lên <Link> để giữ đúng ngữ nghĩa thẻ <a>. */}
          <Link href="/login" className={cn(buttonVariants(), 'w-full')}>
            Về trang đăng nhập
          </Link>
          <p className="text-muted-foreground text-center text-xs">
            Liên kết trong email có hiệu lực 30 phút và chỉ dùng được một lần.
          </p>
        </CardFooter>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Quên mật khẩu</CardTitle>
        <CardDescription>
          Nhập email của bạn. Chúng tôi sẽ gửi một liên kết để đặt lại mật khẩu.
        </CardDescription>
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
        </CardContent>

        <CardFooter className="mt-6 flex-col gap-4">
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? 'Đang gửi…' : 'Gửi hướng dẫn đặt lại'}
          </Button>
          <p className="text-muted-foreground text-sm">
            Nhớ ra rồi?{' '}
            <Link
              href="/login"
              className="text-foreground font-medium underline-offset-4 hover:underline"
            >
              Đăng nhập
            </Link>
          </p>
        </CardFooter>
      </form>
    </Card>
  );
}

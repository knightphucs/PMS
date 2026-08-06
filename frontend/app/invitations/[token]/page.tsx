'use client';

import { useParams, useRouter } from 'next/navigation';

import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ApiError, errorMessage } from '@/lib/api/problem';
import { useSessionBootstrap } from '@/lib/hooks/use-auth';
import { useAcceptInvitation, useInvitationPreview } from '@/lib/hooks/use-invitation';
import { useAuthStore } from '@/store/auth-store';
import { ROLE_IN_PROJECT_LABEL } from '@/types/enums';

/**
 * Trang public chấp nhận lời mời project qua email — CỐ Ý nằm ngoài `(app)` (cần đăng
 * nhập) và `(auth)` (chỉ cho khách) vì trang này phải render được ở CẢ HAI trạng thái:
 * khách xem trước lời mời rồi tự chọn đăng nhập/đăng ký, người đã đăng nhập bấm chấp
 * nhận thẳng. Tự kiểm tra phiên bằng `useSessionBootstrap` thay vì đi qua guard nào.
 */
export default function InvitationPage() {
  const { token } = useParams<{ token: string }>();
  const router = useRouter();
  const status = useSessionBootstrap();
  const me = useAuthStore((s) => s.user);

  const preview = useInvitationPreview(token);
  const accept = useAcceptInvitation();

  const next = `/invitations/${token}`;

  if (preview.isPending || status === 'unknown') {
    return (
      <CenteredCard>
        <CardContent className="grid gap-3">
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-64" />
        </CardContent>
      </CenteredCard>
    );
  }

  if (preview.isError) {
    const message =
      preview.error instanceof ApiError
        ? preview.error.message
        : errorMessage(preview.error);

    return (
      <CenteredCard>
        <CardHeader>
          <CardTitle>Không mở được lời mời</CardTitle>
          <CardDescription>{message}</CardDescription>
        </CardHeader>
        <CardFooter>
          <Button variant="outline" onClick={() => router.replace('/')}>
            Về trang chủ
          </Button>
        </CardFooter>
      </CenteredCard>
    );
  }

  const invitation = preview.data;
  const roleLabel = ROLE_IN_PROJECT_LABEL[invitation.role];

  const handleAccept = async () => {
    await accept.mutateAsync(token);
    router.replace(`/projects/${invitation.projectId}`);
  };

  return (
    <CenteredCard>
      <CardHeader>
        <CardTitle>Lời mời tham gia project</CardTitle>
        <CardDescription>
          Bạn được mời tham gia <strong className="text-foreground">{invitation.projectName}</strong>{' '}
          với vai trò <strong className="text-foreground">{roleLabel}</strong>, dành cho email{' '}
          <strong className="text-foreground">{invitation.email}</strong>.
        </CardDescription>
      </CardHeader>

      <CardContent className="grid gap-3">
        {accept.isError ? (
          <p className="text-destructive text-sm">{errorMessage(accept.error)}</p>
        ) : null}

        {status === 'anonymous' ? (
          <div className="grid gap-2 sm:grid-cols-2">
            <Button onClick={() => router.push(`/login?next=${encodeURIComponent(next)}`)}>
              Đăng nhập để tham gia
            </Button>
            <Button
              variant="outline"
              onClick={() => router.push(`/register?next=${encodeURIComponent(next)}`)}
            >
              Đăng ký tài khoản mới
            </Button>
          </div>
        ) : me && me.email.toLowerCase() !== invitation.email.toLowerCase() ? (
          <p className="text-muted-foreground text-sm">
            Bạn đang đăng nhập bằng <strong className="text-foreground">{me.email}</strong>, nhưng
            lời mời này dành cho <strong className="text-foreground">{invitation.email}</strong>.
            Hãy đăng xuất rồi đăng nhập hoặc đăng ký đúng email được mời.
          </p>
        ) : (
          <Button onClick={() => void handleAccept()} disabled={accept.isPending}>
            {accept.isPending ? 'Đang tham gia…' : 'Tham gia project'}
          </Button>
        )}
      </CardContent>
    </CenteredCard>
  );
}

function CenteredCard({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-muted/30 flex min-h-svh flex-col items-center justify-center gap-6 p-6">
      <div className="flex items-center gap-2 font-semibold">
        <span className="bg-primary text-primary-foreground grid size-8 place-items-center rounded text-sm font-bold">
          PMS
        </span>
        <span className="text-lg">Quản lý dự án</span>
      </div>
      <Card className="w-full max-w-md">{children}</Card>
    </div>
  );
}

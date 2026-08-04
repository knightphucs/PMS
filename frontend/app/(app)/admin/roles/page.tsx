'use client';

import { ShieldCheckIcon } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { WarningBanner } from '@/components/common/warning-banner';
import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { SYSTEM_PERMISSIONS } from '@/lib/auth/system-permissions';
import {
  usePermissionCatalog,
  useRolePermissions,
  useUpdateRolePermissions,
} from '@/lib/hooks/use-admin';
import { cn } from '@/lib/utils';
import { SYSTEM_ROLE_LABEL, type SystemRole } from '@/types/enums';

/**
 * Quyền TỰ PHỤC HỒI duy nhất: còn nó thì cấp lại được mọi quyền khác qua chính màn này.
 * Backend trả 409 khi gỡ nó khỏi SystemAdmin — ô tích tương ứng bị vô hiệu hóa ở đây kèm
 * giải thích, thay vì để người dùng bấm rồi ăn lỗi đỏ và không hiểu vì sao.
 */
const LOCKED: { role: SystemRole; code: string } = {
  role: 'SystemAdmin',
  code: SYSTEM_PERMISSIONS.rolesManage,
};

export default function AdminRolesPage() {
  const catalog = usePermissionCatalog();
  const matrix = useRolePermissions();
  const save = useUpdateRolePermissions();

  /** Bản nháp: `role -> Set<code>`. `null` = chưa tải xong. */
  const [draft, setDraft] = useState<Record<string, string[]> | null>(null);
  const [savingRole, setSavingRole] = useState<SystemRole | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Nạp lại bản nháp mỗi khi dữ liệu server đổi (kể cả sau khi lưu thành công) — bản nháp
  // không được sống lâu hơn sự thật mà nó đang phản chiếu.
  useEffect(() => {
    if (!matrix.data) return;
    setDraft(Object.fromEntries(matrix.data.map((r) => [r.role, [...r.permissions]])));
  }, [matrix.data]);

  const serverState = useMemo(
    () => Object.fromEntries((matrix.data ?? []).map((r) => [r.role, [...r.permissions].sort()])),
    [matrix.data],
  );

  const isDirty = (role: SystemRole) =>
    JSON.stringify([...(draft?.[role] ?? [])].sort()) !== JSON.stringify(serverState[role] ?? []);

  const toggle = (role: SystemRole, code: string) => {
    setError(null);
    setDraft((current) => {
      if (!current) return current;
      const has = current[role]?.includes(code);
      return {
        ...current,
        [role]: has
          ? current[role].filter((c) => c !== code)
          : [...(current[role] ?? []), code],
      };
    });
  };

  const submit = async (role: SystemRole) => {
    if (!draft) return;
    setError(null);
    setSavingRole(role);

    try {
      await save.mutateAsync({ role, body: { permissions: draft[role] ?? [] } });
      toast.success(
        `Đã lưu quyền của ${SYSTEM_ROLE_LABEL[role]}. Có hiệu lực sau khi người mang vai trò này đăng nhập lại (tối đa 15 phút).`,
      );
    } catch (err) {
      // KHÔNG hoàn nguyên bản nháp: người dùng vừa mất công tích, và hai lỗi có thể xảy ra
      // (400 mã lạ, 409 gỡ roles:manage) đều sửa được ngay tại chỗ.
      setError(errorMessage(err));
    } finally {
      setSavingRole(null);
    }
  };

  const isError = catalog.isError || matrix.isError;
  const isPending = catalog.isPending || matrix.isPending || draft === null;

  return (
    <div className="grid gap-5">
      <PageHeader
        title="Phân quyền vai trò"
        description="Quyền cấp hệ thống của từng vai trò. Danh mục quyền là cố định — ở đây chỉ đổi được ai có quyền gì."
      />

      <WarningBanner title="Lưu xong bạn sẽ phải đăng nhập lại.">
        Quyền đi trong access token, nên đổi ở đây chỉ có hiệu lực ở lần lấy token kế tiếp —{' '}
        <strong className="text-foreground">tối đa 15 phút</strong>. Để cửa sổ đó không kéo
        dài hơn, thao tác lưu thu hồi mọi phiên của <em>mọi người mang vai trò đó</em>,{' '}
        <strong className="text-foreground">kể cả phiên của chính bạn</strong> nếu bạn cũng
        thuộc vai trò vừa sửa.
      </WarningBanner>

      {isError ? (
        <QueryError
          title="Không tải được cấu hình phân quyền"
          error={catalog.error ?? matrix.error}
          onRetry={() => {
            void catalog.refetch();
            void matrix.refetch();
          }}
          isRetrying={catalog.isFetching || matrix.isFetching}
        />
      ) : isPending ? (
        <RolesSkeleton />
      ) : catalog.data.length === 0 ? (
        <EmptyState
          icon={<ShieldCheckIcon className="size-8" />}
          title="Danh mục quyền đang trống"
          description="Danh mục được seed bằng migration. Nếu bảng Permissions rỗng thì database chưa chạy migration mới nhất."
        />
      ) : (
        <div className="grid gap-4">
          <FormError message={error} />

          {matrix.data.map((row) => {
            const dirty = isDirty(row.role);
            const busy = savingRole === row.role;

            return (
              <section key={row.role} className="bg-card grid gap-3 rounded-lg border p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h2 className="text-sm font-semibold">{SYSTEM_ROLE_LABEL[row.role]}</h2>
                    <p className="text-muted-foreground text-xs">
                      {(draft[row.role] ?? []).length}/{catalog.data.length} quyền
                    </p>
                  </div>

                  <Button
                    size="sm"
                    disabled={!dirty || busy}
                    onClick={() => void submit(row.role)}
                  >
                    {busy ? 'Đang lưu…' : dirty ? 'Lưu thay đổi' : 'Chưa có thay đổi'}
                  </Button>
                </div>

                <ul className="grid gap-2 sm:grid-cols-2">
                  {catalog.data.map((permission) => {
                    const checked = draft[row.role]?.includes(permission.code) ?? false;
                    const locked =
                      row.role === LOCKED.role && permission.code === LOCKED.code;

                    return (
                      <li key={permission.code}>
                        <label
                          className={cn(
                            'flex min-w-0 gap-2.5 rounded-md border p-2.5 transition-colors',
                            locked
                              ? 'cursor-not-allowed opacity-70'
                              : 'hover:bg-muted/50 cursor-pointer',
                          )}
                        >
                          <input
                            type="checkbox"
                            className="accent-primary mt-0.5 size-4 shrink-0"
                            checked={checked}
                            disabled={locked || busy}
                            onChange={() => toggle(row.role, permission.code)}
                          />
                          <div className="min-w-0">
                            <p className="text-[13px] font-medium break-words">
                              {permission.description}
                            </p>
                            <code className="text-muted-foreground text-xs break-words">
                              {permission.code}
                            </code>
                            {locked ? (
                              <p className="text-muted-foreground mt-1 text-xs">
                                Không gỡ được: đây là quyền duy nhất mở lại được màn hình
                                này. Gỡ nó là khóa vĩnh viễn mọi lối vào quản trị.
                              </p>
                            ) : null}
                          </div>
                        </label>
                      </li>
                    );
                  })}
                </ul>
              </section>
            );
          })}
        </div>
      )}
    </div>
  );
}

function RolesSkeleton() {
  return (
    <div className="grid gap-4" aria-busy="true">
      <span className="sr-only">Đang tải cấu hình phân quyền…</span>
      {Array.from({ length: 2 }, (_, i) => (
        <div key={i} className="bg-card grid gap-3 rounded-lg border p-4">
          <div className="flex items-center justify-between">
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-8 w-32" />
          </div>
          <div className="grid gap-2 sm:grid-cols-2">
            {Array.from({ length: 5 }, (_, j) => (
              <Skeleton key={j} className="h-14" />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

'use client';

import { SearchIcon, SearchXIcon, UsersIcon } from 'lucide-react';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { ChangeRoleDialog } from '@/components/admin/change-role-dialog';
import { EmployeeTable, EmployeeTableSkeleton } from '@/components/admin/employee-table';
import { LockAccountDialog } from '@/components/admin/lock-account-dialog';
import { ConfirmDialog } from '@/components/common/confirm-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { ProjectPagination } from '@/components/projects/project-pagination';
import { Input } from '@/components/ui/input';
import { errorMessage } from '@/lib/api/problem';
import { useAdminEmployees, useUnlockEmployee } from '@/lib/hooks/use-admin';
import { useDebounced } from '@/lib/hooks/use-debounced';
import { useAuthStore } from '@/store/auth-store';
import type { EmployeeAdminResponse } from '@/types/admin';
import { DEFAULT_PAGE_SIZE } from '@/types/common';

export default function AdminEmployeesPage() {
  const currentEmployeeId = useAuthStore((s) => s.user?.id);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebounced(searchInput);

  const [locking, setLocking] = useState<EmployeeAdminResponse | null>(null);
  const [unlocking, setUnlocking] = useState<EmployeeAdminResponse | null>(null);
  const [unlockError, setUnlockError] = useState<string | null>(null);
  const [changingRole, setChangingRole] = useState<EmployeeAdminResponse | null>(null);

  // Đổi từ khóa mà giữ số trang sẽ rơi vào trang trống khi kết quả mới ít hơn.
  useEffect(() => {
    setPage(1);
  }, [search, pageSize]);

  // ✅ `search` ở endpoint này THỰC SỰ chạy (khớp tên hoặc email) — một trong hai chỗ hiếm
  // hoi như vậy trong toàn bộ API. Đừng chép khuôn này sang project/task/sprint: ở đó
  // `?search=` bị nhận rồi bỏ qua im lặng.
  const query = useAdminEmployees({
    page,
    pageSize,
    search: search || undefined,
    sortBy: 'name',
  });

  const unlock = useUnlockEmployee();

  const handleUnlock = async () => {
    if (!unlocking) return;
    setUnlockError(null);

    try {
      await unlock.mutateAsync({ id: unlocking.id });
      toast.success(`Đã mở khóa tài khoản ${unlocking.email}.`);
      setUnlocking(null);
    } catch (error) {
      setUnlockError(errorMessage(error));
    }
  };

  const hasResults = (query.data?.items.length ?? 0) > 0;

  return (
    <div className="grid gap-5">
      <PageHeader
        title="Nhân sự"
        count={query.data?.totalCount}
        description="Toàn bộ tài khoản trong hệ thống. Khóa tài khoản sẽ thu hồi mọi phiên đăng nhập của người đó ngay lập tức."
      />

      <div className="bg-card flex flex-wrap items-center gap-3 rounded-lg border p-3">
        <div className="relative min-w-56 flex-1">
          <SearchIcon className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Tìm theo tên hoặc email…"
            aria-label="Tìm nhân sự theo tên hoặc email"
            className="pl-9"
          />
        </div>
        {search ? (
          <span className="text-muted-foreground text-sm" aria-live="polite">
            {query.data?.totalCount ?? 0} kết quả cho “{search}”
          </span>
        ) : null}
      </div>

      {query.isError ? (
        <QueryError
          title="Không tải được danh sách nhân sự"
          error={query.error}
          onRetry={() => void query.refetch()}
          isRetrying={query.isFetching}
        />
      ) : query.isPending ? (
        <EmployeeTableSkeleton rows={pageSize > 10 ? 10 : pageSize} />
      ) : hasResults ? (
        <>
          <EmployeeTable
            employees={query.data.items}
            currentEmployeeId={currentEmployeeId}
            dimmed={query.isFetching}
            onLock={setLocking}
            onUnlock={setUnlocking}
            onChangeRole={setChangingRole}
          />
          <ProjectPagination
            page={query.data}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            disabled={query.isFetching}
            unitLabel="tài khoản"
          />
        </>
      ) : search ? (
        <EmptyState
          icon={<SearchXIcon className="size-8" />}
          title={`Không có tài khoản nào khớp “${search}”`}
          description="Thử tìm bằng một phần của tên hoặc địa chỉ email."
        />
      ) : (
        <EmptyState
          icon={<UsersIcon className="size-8" />}
          title="Chưa có tài khoản nào"
          description="Người dùng tự đăng ký qua trang Đăng ký; hệ thống không tạo tài khoản hộ."
        />
      )}

      <LockAccountDialog employee={locking} onClose={() => setLocking(null)} />

      <ChangeRoleDialog employee={changingRole} onClose={() => setChangingRole(null)} />

      <ConfirmDialog
        open={unlocking !== null}
        title="Mở khóa tài khoản?"
        description={
          <>
            <strong className="text-foreground">{unlocking?.email}</strong> sẽ đăng nhập lại
            được ngay. Các phiên cũ đã bị thu hồi lúc khóa nên họ vẫn phải nhập mật khẩu.
          </>
        }
        confirmLabel="Mở khóa"
        pendingLabel="Đang mở khóa…"
        error={unlockError}
        isPending={unlock.isPending}
        onConfirm={handleUnlock}
        onClose={() => {
          setUnlocking(null);
          setUnlockError(null);
        }}
      />
    </div>
  );
}

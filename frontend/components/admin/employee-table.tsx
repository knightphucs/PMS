'use client';

import { LockIcon, LockOpenIcon, MoreHorizontalIcon, ShieldIcon } from 'lucide-react';

import { UserAvatar } from '@/components/common/user-avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { formatDate, formatDateTime } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { EmployeeAdminResponse } from '@/types/admin';
import { SYSTEM_ROLE_LABEL } from '@/types/enums';

export function EmployeeTable({
  employees,
  currentEmployeeId,
  dimmed,
  onLock,
  onUnlock,
  onChangeRole,
}: {
  employees: EmployeeAdminResponse[];
  /** Để vô hiệu hóa hành động lên CHÍNH MÌNH — backend trả 400, chặn trước cho gọn. */
  currentEmployeeId: string | undefined;
  dimmed?: boolean;
  onLock: (employee: EmployeeAdminResponse) => void;
  onUnlock: (employee: EmployeeAdminResponse) => void;
  onChangeRole: (employee: EmployeeAdminResponse) => void;
}) {
  return (
    <div
      className={cn(
        'bg-card overflow-x-auto rounded-lg border transition-opacity',
        dimmed && 'pointer-events-none opacity-60',
      )}
      aria-busy={dimmed}
    >
      <Table className="[&_td]:px-3 [&_td]:py-2 [&_td]:text-[13px] [&_th]:h-9 [&_th]:px-3">
        <TableHeader className="bg-muted/40">
          <TableRow>
            <TableHead>Nhân sự</TableHead>
            <TableHead className="w-40">Vai trò hệ thống</TableHead>
            <TableHead className="w-56">Trạng thái</TableHead>
            <TableHead className="w-36">Ngày tạo</TableHead>
            <TableHead className="w-16 text-right">Thao tác</TableHead>
          </TableRow>
        </TableHeader>

        <TableBody>
          {employees.map((employee) => {
            const isSelf = employee.id === currentEmployeeId;

            return (
              <TableRow key={employee.id}>
                <TableCell>
                  <div className="flex min-w-0 items-center gap-2.5">
                    <UserAvatar id={employee.id} name={employee.name} size="sm" />
                    <div className="min-w-0">
                      <p className="truncate font-medium">
                        {employee.name}
                        {isSelf ? (
                          <span className="text-muted-foreground ml-1.5 text-xs font-normal">
                            (bạn)
                          </span>
                        ) : null}
                      </p>
                      <p className="text-muted-foreground truncate text-xs">{employee.email}</p>
                    </div>
                  </div>
                </TableCell>

                <TableCell>
                  <Badge variant={employee.systemRole === 'SystemAdmin' ? 'default' : 'secondary'}>
                    {SYSTEM_ROLE_LABEL[employee.systemRole]}
                  </Badge>
                </TableCell>

                <TableCell>
                  {employee.isLocked ? (
                    <div className="min-w-0">
                      <Badge variant="destructive">Đã khóa</Badge>
                      {employee.lockReason ? (
                        <p
                          className="text-muted-foreground mt-1 truncate text-xs"
                          title={employee.lockReason}
                        >
                          {employee.lockReason}
                        </p>
                      ) : null}
                      {employee.lockedAt ? (
                        <p className="text-muted-foreground text-xs">
                          {formatDateTime(employee.lockedAt)}
                        </p>
                      ) : null}
                    </div>
                  ) : (
                    <span className="text-muted-foreground">Đang hoạt động</span>
                  )}
                </TableCell>

                <TableCell className="text-muted-foreground tabular-nums">
                  {formatDate(employee.createdAt)}
                </TableCell>

                <TableCell className="text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger
                      render={
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          aria-label={`Thao tác với ${employee.name}`}
                        >
                          <MoreHorizontalIcon className="size-4" />
                        </Button>
                      }
                    />
                    <DropdownMenuContent align="end">
                      {employee.isLocked ? (
                        <DropdownMenuItem onClick={() => onUnlock(employee)}>
                          <LockOpenIcon className="size-4" />
                          Mở khóa tài khoản
                        </DropdownMenuItem>
                      ) : (
                        // Tự khóa mình trả 400 — chặn ở đây để không ai phải học điều đó
                        // bằng một thông báo lỗi đỏ.
                        <DropdownMenuItem disabled={isSelf} onClick={() => onLock(employee)}>
                          <LockIcon className="size-4" />
                          Khóa tài khoản
                        </DropdownMenuItem>
                      )}

                      <DropdownMenuItem
                        disabled={isSelf}
                        onClick={() => onChangeRole(employee)}
                      >
                        <ShieldIcon className="size-4" />
                        Đổi vai trò hệ thống
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

export function EmployeeTableSkeleton({ rows = 10 }: { rows?: number }) {
  return (
    <div className="bg-card overflow-hidden rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách nhân sự…</span>
      <div className="bg-muted/40 h-9 border-b" />
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex items-center gap-3 border-b px-3 py-2 last:border-b-0">
          <Skeleton className="size-7 shrink-0 rounded-full" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-4 w-24" />
        </div>
      ))}
    </div>
  );
}

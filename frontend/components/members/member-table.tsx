'use client';

import { LogOutIcon, MoreHorizontalIcon, ShieldIcon, UserMinusIcon } from 'lucide-react';

import { UserAvatar } from '@/components/common/user-avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
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
import { formatDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import {
  INVITATION_STATUS_LABEL,
  ROLE_IN_PROJECT_LABEL,
  type InvitationStatus,
} from '@/types/enums';
import type { ProjectMemberResponse } from '@/types/project';

const INVITATION_TONE: Record<InvitationStatus, string> = {
  Accepted: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
  Pending: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  Declined: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
};

export function MemberTable({
  members,
  canManage,
  myEmployeeId,
  onChangeRole,
  onRemove,
}: {
  members: ProjectMemberResponse[];
  canManage: boolean;
  myEmployeeId: string | null;
  onChangeRole: (member: ProjectMemberResponse) => void;
  onRemove: (member: ProjectMemberResponse) => void;
}) {
  return (
    <div className="bg-card overflow-x-auto rounded-lg border">
      <Table className="[&_td]:px-3 [&_td]:py-2 [&_td]:text-[13px] [&_th]:h-9 [&_th]:px-3">
        <TableHeader>
          <TableRow>
            <TableHead>Thành viên</TableHead>
            <TableHead>Vai trò</TableHead>
            <TableHead>Trạng thái</TableHead>
            <TableHead>Tham gia từ</TableHead>
            <TableHead className="w-12">
              <span className="sr-only">Thao tác</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {members.map((member) => {
            const isMe = member.employeeId === myEmployeeId;
            // Ai cũng tự rời được; gỡ NGƯỜI KHÁC thì phải là PM.
            const showMenu = canManage || isMe;

            return (
              <TableRow key={member.employeeId}>
                <TableCell>
                  <div className="flex items-center gap-2.5">
                    <UserAvatar
                      id={member.employeeId}
                      name={member.employeeName}
                      size="sm"
                    />
                    <span className="font-medium">{member.employeeName}</span>
                    {isMe ? (
                      <span className="text-muted-foreground text-xs">(bạn)</span>
                    ) : null}
                  </div>
                </TableCell>
                <TableCell>
                  <span className="inline-flex items-center gap-1.5">
                    {member.roleInProject === 'ProjectManager' ? (
                      <ShieldIcon className="text-primary size-3.5" />
                    ) : null}
                    {ROLE_IN_PROJECT_LABEL[member.roleInProject]}
                  </span>
                </TableCell>
                <TableCell>
                  <Badge
                    variant="secondary"
                    className={cn(
                      'border-0 font-medium',
                      INVITATION_TONE[member.invitationStatus],
                    )}
                  >
                    {INVITATION_STATUS_LABEL[member.invitationStatus]}
                  </Badge>
                </TableCell>
                <TableCell className="text-muted-foreground tabular-nums">
                  {member.joinedDate ? formatDate(member.joinedDate) : '—'}
                </TableCell>
                <TableCell className="text-right">
                  {showMenu ? (
                    <DropdownMenu>
                      <DropdownMenuTrigger
                        render={
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-label={`Thao tác với ${member.employeeName}`}
                          >
                            <MoreHorizontalIcon className="size-4" />
                          </Button>
                        }
                      />
                      <DropdownMenuContent align="end" className="w-52">
                        {canManage ? (
                          <>
                            <DropdownMenuItem onClick={() => onChangeRole(member)}>
                              <ShieldIcon className="size-4" />
                              Đổi vai trò
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                          </>
                        ) : null}
                        <DropdownMenuItem
                          variant="destructive"
                          onClick={() => onRemove(member)}
                        >
                          {isMe ? (
                            <>
                              <LogOutIcon className="size-4" />
                              Rời dự án
                            </>
                          ) : (
                            <>
                              <UserMinusIcon className="size-4" />
                              Gỡ khỏi dự án
                            </>
                          )}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  ) : null}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

export function MemberTableSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="bg-card rounded-lg border" aria-busy="true">
      <span className="sr-only">Đang tải danh sách thành viên…</span>
      <div className="divide-y">
        {Array.from({ length: rows }).map((_, index) => (
          <div key={index} className="flex items-center gap-3 px-3 py-2.5">
            <Skeleton className="size-6 shrink-0 rounded-full" />
            <Skeleton className="h-4 flex-1 max-w-48" />
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-4 w-20" />
          </div>
        ))}
      </div>
    </div>
  );
}

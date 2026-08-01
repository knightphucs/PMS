'use client';

import { UsersIcon } from 'lucide-react';
import { useParams } from 'next/navigation';
import { useState } from 'react';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { ChangeRoleDialog } from '@/components/members/change-role-dialog';
import { InviteMemberDialog } from '@/components/members/invite-member-dialog';
import { MemberTable, MemberTableSkeleton } from '@/components/members/member-table';
import { RemoveMemberDialog } from '@/components/members/remove-member-dialog';
import { useMembers } from '@/lib/hooks/use-members';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { canManageMembers } from '@/lib/tasks/permissions';
import type { ProjectMemberResponse } from '@/types/project';

export default function MembersPage() {
  const { id } = useParams<{ id: string }>();
  const members = useMembers(id);
  const { role, isResolving, myEmployeeId } = useMyProjectRole(id);

  const [changingRole, setChangingRole] = useState<ProjectMemberResponse | null>(null);
  const [removing, setRemoving] = useState<ProjectMemberResponse | null>(null);

  // `isResolving` để nút "Mời thành viên" không nháy hiện rồi biến mất.
  const canManage = !isResolving && canManageMembers(role);

  return (
    <div className="grid gap-4">
      <PageHeader
        title="Thành viên"
        count={members.data?.length}
        description="Ai đang tham gia dự án và với vai trò gì."
        actions={canManage ? <InviteMemberDialog projectId={id} /> : undefined}
      />

      {members.isError ? (
        <QueryError
          title="Không tải được danh sách thành viên"
          error={members.error}
          onRetry={() => void members.refetch()}
          isRetrying={members.isFetching}
        />
      ) : members.isPending ? (
        <MemberTableSkeleton />
      ) : members.data.length === 0 ? (
        <EmptyState
          icon={<UsersIcon className="size-8" />}
          title="Chưa có thành viên nào"
          description="Mời đồng nghiệp bằng email để cùng làm việc trên dự án này."
          action={canManage ? <InviteMemberDialog projectId={id} /> : undefined}
        />
      ) : (
        <MemberTable
          members={members.data}
          canManage={canManage}
          myEmployeeId={myEmployeeId}
          onChangeRole={setChangingRole}
          onRemove={setRemoving}
        />
      )}

      <ChangeRoleDialog
        projectId={id}
        member={changingRole}
        onClose={() => setChangingRole(null)}
      />
      <RemoveMemberDialog
        projectId={id}
        member={removing}
        isMe={removing?.employeeId === myEmployeeId}
        onClose={() => setRemoving(null)}
      />
    </div>
  );
}

'use client';

import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import { useChangeMemberRole } from '@/lib/hooks/use-members';
import { ROLE_IN_PROJECT_LABEL, type RoleInProject } from '@/types/enums';
import type { ProjectMemberResponse } from '@/types/project';

const ROLES: RoleInProject[] = ['ProjectManager', 'Member', 'Viewer'];

export function ChangeRoleDialog({
  projectId,
  member,
  onClose,
}: {
  projectId: string;
  member: ProjectMemberResponse | null;
  onClose: () => void;
}) {
  const open = member !== null;
  const changeRole = useChangeMemberRole(projectId);
  const [role, setRole] = useState<RoleInProject>('Member');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (member) setRole(member.roleInProject);
  }, [member]);

  const handleSubmit = async () => {
    if (!member) return;
    setError(null);

    try {
      await changeRole.mutateAsync({ employeeId: member.employeeId, role });
      toast.success(
        `${member.employeeName} nay là ${ROLE_IN_PROJECT_LABEL[role].toLowerCase()}.`,
      );
      handleOpenChange(false);
    } catch (err) {
      // 409 ở đây là câu trả lời nghiệp vụ có ích: hạ xuống Viewer khi người đó còn task
      // chưa xong, hoặc hạ vai trò của ProjectManager cuối cùng. Giữ dialog mở để đọc.
      setError(errorMessage(err));
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (next) return;
    setError(null);
    onClose();
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Đổi vai trò</DialogTitle>
          <DialogDescription>
            Vai trò của{' '}
            <strong className="text-foreground">{member?.employeeName}</strong> trong dự án
            này.
          </DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        <div className="grid gap-2">
          <Label htmlFor="change-role">Vai trò</Label>
          <Select value={role} onValueChange={(value) => setRole(value as RoleInProject)}>
            <SelectTrigger id="change-role" className="w-full">
              {/* `SelectValue` của Base UI hiện giá trị thô — phải tự định dạng. */}
              <SelectValue>
                {(current: RoleInProject) => ROLE_IN_PROJECT_LABEL[current]}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {ROLES.map((item) => (
                <SelectItem key={item} value={item}>
                  {ROLE_IN_PROJECT_LABEL[item]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {role === 'Viewer' ? (
            <p className="text-muted-foreground text-xs">
              Người xem không tự nhận việc và không bình luận được. Nếu người này còn task
              chưa hoàn thành, hệ thống sẽ từ chối.
            </p>
          ) : null}
        </div>

        <DialogFooter className="mt-2">
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button
            type="button"
            onClick={handleSubmit}
            disabled={changeRole.isPending || role === member?.roleInProject}
          >
            {changeRole.isPending ? 'Đang lưu…' : 'Lưu'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

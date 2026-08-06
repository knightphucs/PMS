'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { MailPlusIcon } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { Field } from '@/components/form/field';
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
  DialogTrigger,
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
import { applyServerErrors } from '@/lib/form';
import { useInviteExternalMember } from '@/lib/hooks/use-members';
import { inviteMemberSchema, type InviteMemberValues } from '@/lib/validation/member-schema';
import { ROLE_IN_PROJECT_LABEL, type RoleInProject } from '@/types/enums';

/** ⚠️ Phải khớp ĐÚNG tên property của `InviteExternalRequest` phía backend. */
const FIELDS = ['email', 'role'] as const;

const ROLES: RoleInProject[] = ['ProjectManager', 'Member', 'Viewer'];

/**
 * Mời qua LINK gửi bằng email — khác {@link InviteMemberDialog}, không đòi hỏi người được
 * mời đã có tài khoản trong hệ thống. Vì vậy KHÔNG có ô gợi ý nhân sự nội bộ: email ở đây
 * là tự do, không tra `useEmployeeSearch`.
 */
export function InviteExternalDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const inviteExternal = useInviteExternalMember(projectId);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<InviteMemberValues>({
    resolver: zodResolver(inviteMemberSchema),
    defaultValues: { email: '', role: 'Member' },
  });

  const role = watch('role');

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);

      try {
        await inviteExternal.mutateAsync(values);
        // Khác "Thêm thành viên": chưa vào project ngay, chỉ vừa gửi link.
        toast.success(`Đã gửi lời mời tới ${values.email}.`);
        handleOpenChange(false);
      } catch (error) {
        if (!applyServerErrors(error, setError, FIELDS)) {
          setFormError(errorMessage(error));
        }
      }
    },
    () => setFormError(null),
  );

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next) {
      reset();
      setFormError(null);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger
        render={
          <Button size="sm" variant="outline">
            <MailPlusIcon className="size-4" />
            Mời thành viên
          </Button>
        }
      />
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Mời thành viên qua email</DialogTitle>
          <DialogDescription>
            Gửi link mời tới email bất kỳ, kể cả người chưa có tài khoản. Họ vào project
            sau khi bấm link rồi đăng nhập hoặc đăng ký.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <FormError message={formError} />

          <Field
            label="Email"
            type="email"
            autoFocus
            autoComplete="off"
            placeholder="ai-do@ngoai-cong-ty.com"
            error={errors.email?.message}
            {...register('email')}
          />

          <div className="grid gap-2">
            <Label htmlFor="invite-external-role">Vai trò</Label>
            <Select
              value={role}
              onValueChange={(value) => setValue('role', value as RoleInProject)}
            >
              <SelectTrigger id="invite-external-role" className="w-full">
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
            <p className="text-muted-foreground text-xs">
              {role === 'ProjectManager'
                ? 'Toàn quyền: quản lý thành viên, sprint và task.'
                : role === 'Member'
                  ? 'Tự nhận việc, bình luận và tách subtask từ việc được giao. Không tạo/sửa task hay sprint.'
                  : 'Chỉ xem. Không tự nhận việc, không bình luận.'}
            </p>
          </div>

          <DialogFooter className="mt-2">
            <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Đang gửi…' : 'Gửi lời mời'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

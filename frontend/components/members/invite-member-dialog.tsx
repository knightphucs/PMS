'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { UserPlusIcon } from 'lucide-react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';

import { UserAvatar } from '@/components/common/user-avatar';
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
import { useDebounced } from '@/lib/hooks/use-debounced';
import { useEmployeeSearch } from '@/lib/hooks/use-employees';
import { useInviteMember, useMembers } from '@/lib/hooks/use-members';
import { inviteMemberSchema, type InviteMemberValues } from '@/lib/validation/member-schema';
import { EMPLOYEE_SEARCH_MIN_LENGTH } from '@/types/employee';
import { ROLE_IN_PROJECT_LABEL, type RoleInProject } from '@/types/enums';

/** ⚠️ Phải khớp ĐÚNG tên property của `InviteMemberRequest` phía backend. */
const FIELDS = ['email', 'role'] as const;

const ROLES: RoleInProject[] = ['ProjectManager', 'Member', 'Viewer'];

export function InviteMemberDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const inviteMember = useInviteMember(projectId);

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

  // Ô email vẫn là NGUỒN SỰ THẬT của form: schema, `applyServerErrors` và cả ba mã lỗi
  // nghiệp vụ giữ nguyên. Gợi ý chỉ là một cách điền nhanh vào chính ô đó, không phải một
  // trường mới — nên không có gì phải đồng bộ giữa hai nơi.
  const email = watch('email');
  const [suggesting, setSuggesting] = useState(false);
  const keyword = useDebounced(email);
  const suggestions = useEmployeeSearch(suggesting ? keyword : '');

  // Người đã ở trong dự án (kể cả đang chờ) chắc chắn nhận 409 — chặn ở đây thì nước đi đó
  // không tạo ra request nào, thay vì để người dùng bấm rồi đọc lỗi.
  // So bằng `employeeId`: `ProjectMemberResponse` KHÔNG mang email, và id là thứ duy nhất
  // hai DTO này có chung.
  const members = useMembers(projectId);
  const memberIds = new Set(members.data?.map((m) => m.employeeId) ?? []);

  const emailField = register('email');

  const onSubmit = handleSubmit(
    async (values) => {
      setFormError(null);

      try {
        await inviteMember.mutateAsync(values);
        toast.success(`Đã gửi lời mời tới ${values.email}.`);
        handleOpenChange(false);
      } catch (error) {
        // Ba lỗi nghiệp vụ đều có câu tiếng Việt sẵn ở `title`, và đều KHÁC nhau về cách
        // xử lý: 404 = email chưa có tài khoản (người này cần đăng ký trước), 400 = tự
        // mời mình, 409 = đã là thành viên. Viết lại chỉ làm mất thông tin đó.
        if (!applyServerErrors(error, setError, FIELDS)) {
          setFormError(errorMessage(error));
        }
      }
    },
    // Nhánh validate CLIENT hỏng. Không có nó thì nhánh thành công ở trên không chạy,
    // `setFormError(null)` không bao giờ gọi, và lỗi máy chủ của lần thử TRƯỚC vẫn nằm
    // đó bên cạnh lỗi field mới — người dùng đọc được hai thông báo mâu thuẫn nhau.
    () => setFormError(null),
  );

  const handleOpenChange = (next: boolean) => {
    setOpen(next);
    if (!next) {
      reset();
      setFormError(null);
      setSuggesting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger
        render={
          <Button size="sm">
            <UserPlusIcon className="size-4" />
            Mời thành viên
          </Button>
        }
      />
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Mời thành viên</DialogTitle>
          <DialogDescription>
            Người được mời phải đã có tài khoản trong hệ thống. Lời mời ở trạng thái chờ
            cho tới khi họ chấp nhận.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} noValidate className="grid gap-4">
          <FormError message={formError} />

          <div className="grid gap-2">
            <Field
              label="Email"
              type="email"
              autoFocus
              autoComplete="off"
              placeholder="Gõ tên hoặc email để tìm…"
              error={errors.email?.message}
              {...emailField}
              onChange={(event) => {
                void emailField.onChange(event);
                setSuggesting(true);
              }}
            />

            {suggesting && email.trim().length > 0 ? (
              email.trim().length < EMPLOYEE_SEARCH_MIN_LENGTH ? (
                // Không phải lỗi — chỉ là chưa đủ để tra. Server trả 400 dưới ngưỡng này nên
                // `useEmployeeSearch` cố ý chưa bắn request nào.
                <p className="text-muted-foreground text-xs">
                  Nhập tối thiểu {EMPLOYEE_SEARCH_MIN_LENGTH} ký tự để tìm nhân sự.
                </p>
              ) : suggestions.data && suggestions.data.length > 0 ? (
                <ul className="grid max-h-48 gap-0.5 overflow-y-auto rounded-lg border p-1">
                  {suggestions.data.map((employee) => {
                    const already = memberIds.has(employee.id);

                    return (
                      <li key={employee.id}>
                        <button
                          type="button"
                          disabled={already}
                          onClick={() => {
                            setValue('email', employee.email, { shouldValidate: true });
                            setSuggesting(false);
                          }}
                          className="hover:bg-accent flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left transition-colors disabled:pointer-events-none disabled:opacity-50"
                        >
                          <UserAvatar
                            id={employee.id}
                            name={employee.name}
                            className="size-6 text-[10px]"
                          />
                          <span className="grid min-w-0 flex-1">
                            <span className="truncate text-[13px] font-medium">
                              {employee.name}
                            </span>
                            <span className="text-muted-foreground truncate text-xs">
                              {employee.email}
                            </span>
                          </span>
                          {already ? (
                            <span className="text-muted-foreground shrink-0 text-xs">
                              Đã trong dự án
                            </span>
                          ) : null}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              ) : suggestions.isFetched ? (
                <p className="text-muted-foreground text-xs">
                  Không tìm thấy nhân sự nào khớp “{keyword.trim()}”.
                </p>
              ) : null
            ) : null}
          </div>

          <div className="grid gap-2">
            <Label htmlFor="invite-role">Vai trò</Label>
            <Select
              value={role}
              onValueChange={(value) => setValue('role', value as RoleInProject)}
            >
              <SelectTrigger id="invite-role" className="w-full">
                {/* `SelectValue` của Base UI hiện giá trị thô ("Member") chứ không phải
                    nhãn của item — phải tự định dạng. */}
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
                  ? 'Tự nhận việc và bình luận. Không tạo/sửa được task hay sprint.'
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

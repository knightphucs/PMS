'use client';

import { PencilIcon, PlusIcon, ShieldCheckIcon, Trash2Icon } from 'lucide-react';
import { useMemo, useState } from 'react';
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
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { errorMessage } from '@/lib/api/problem';
import {
  useApprovalPolicies,
  useCreateApprovalPolicy,
  useDeleteApprovalPolicy,
  useUpdateApprovalPolicy,
} from '@/lib/hooks/use-approvals';
import { useBoardColumns } from '@/lib/hooks/use-board-columns';
import { useMembers } from '@/lib/hooks/use-members';
import { useWorkItemTypes } from '@/lib/hooks/use-work-item-types';
import type { ApprovalPolicyResponse, ApproverMode } from '@/types/approval';

/**
 * Nhãn của `ApproverMode` — nguồn DUY NHẤT, nuôi cả ô chọn lẫn danh sách luật.
 *
 * 📌 Là `Record<ApproverMode, string>` chứ không phải hai `<SelectItem>` viết tay: thêm một
 * chế độ ở backend thì TypeScript đỏ ngay tại đây, thay vì ô chọn im lặng thiếu một mục.
 */
const APPROVER_MODE_LABEL: Record<ApproverMode, string> = {
  ProjectManagers: 'Mọi quản lý dự án',
  NamedApprovers: 'Người duyệt chỉ định',
};

/**
 * Quản lý luật duyệt của project (ADR-062).
 *
 * 🔴 Sống ở trang **Cấu hình**, không phải header trang Bảng — luật 5 của Doctrine (§0).
 * Đây chính là bề mặt mà ADR-061 dựng `/settings` để đón trước.
 *
 * 📌 Dialog này chỉ khai LUẬT. Việc *ký* một yêu cầu duyệt nằm ở khối duyệt trong chi tiết
 * task, vì đó là hai vai khác nhau: người cấu hình và người xử lý.
 */
export function ManageApprovalPoliciesDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const policies = useApprovalPolicies(projectId);
  const remove = useDeleteApprovalPolicy(projectId);

  const [editing, setEditing] = useState<ApprovalPolicyResponse | null>(null);
  const [creating, setCreating] = useState(false);

  const list = policies.data ?? [];

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger
          render={
            <Button variant="outline" size="sm">
              <ShieldCheckIcon className="size-4" />
              Luật duyệt
            </Button>
          }
        />
        <DialogContent showCloseButton={false} className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Luật duyệt</DialogTitle>
            <DialogDescription>
              Bắt một loại công việc phải có chữ ký mới vào được một cột. Yêu cầu duyệt được
              gửi <strong>tự động</strong> khi có người chuyển task sang cột đó — không ai
              phải nhớ bấm gì.
            </DialogDescription>
          </DialogHeader>

          <div className="grid min-w-0 gap-2">
            {list.length === 0 ? (
              <p className="text-muted-foreground rounded-md border border-dashed px-3 py-4 text-sm">
                Chưa có luật duyệt nào. Mọi task chuyển cột tự do như hiện tại.
              </p>
            ) : null}

            {list.map((policy) => (
              <div
                key={policy.id}
                className="flex min-w-0 flex-wrap items-center gap-2 rounded-md border p-2"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium">
                    {policy.workItemTypeName} → {policy.targetColumnName}
                  </p>
                  <p className="text-muted-foreground truncate text-xs">
                    Cần <span className="tabular-nums">{policy.minApprovals}</span> lượt duyệt ·{' '}
                    {policy.approverMode === 'ProjectManagers'
                      ? 'quản lý dự án ký'
                      : `${policy.approvers.map((a) => a.name).join(', ')} ký`}
                  </p>
                </div>

                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Sửa luật "${policy.workItemTypeName} → ${policy.targetColumnName}"`}
                  onClick={() => setEditing(policy)}
                >
                  <PencilIcon className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Xoá luật "${policy.workItemTypeName} → ${policy.targetColumnName}"`}
                  disabled={remove.isPending}
                  onClick={() =>
                    remove.mutate(policy.id, {
                      onSuccess: () => toast.success('Đã xoá luật duyệt.'),
                      onError: (error: unknown) => toast.error(errorMessage(error)),
                    })
                  }
                >
                  <Trash2Icon className="text-destructive size-4" />
                </Button>
              </div>
            ))}
          </div>

          <DialogFooter className="flex-wrap gap-2 sm:justify-between">
            <Button variant="outline" size="sm" onClick={() => setCreating(true)}>
              <PlusIcon className="size-4" />
              Thêm luật
            </Button>
            <DialogClose render={<Button variant="ghost">Đóng</Button>} />
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {creating ? (
        <PolicyFormDialog projectId={projectId} onClose={() => setCreating(false)} />
      ) : null}

      {editing ? (
        <PolicyFormDialog
          projectId={projectId}
          policy={editing}
          onClose={() => setEditing(null)}
        />
      ) : null}
    </>
  );
}

function PolicyFormDialog({
  projectId,
  policy,
  onClose,
}: {
  projectId: string;
  policy?: ApprovalPolicyResponse;
  onClose: () => void;
}) {
  const types = useWorkItemTypes(projectId);
  const columns = useBoardColumns(projectId);
  const members = useMembers(projectId);

  const create = useCreateApprovalPolicy(projectId);
  const update = useUpdateApprovalPolicy(projectId);

  const [workItemTypeId, setWorkItemTypeId] = useState(policy?.workItemTypeId ?? '');
  const [targetColumnId, setTargetColumnId] = useState(policy?.targetColumnId ?? '');
  const [approverMode, setApproverMode] = useState<ApproverMode>(
    policy?.approverMode ?? 'ProjectManagers',
  );
  const [minApprovals, setMinApprovals] = useState(String(policy?.minApprovals ?? 1));
  const [approverIds, setApproverIds] = useState<string[]>(
    policy?.approvers.map((a) => a.employeeId) ?? [],
  );
  const [error, setError] = useState<string | null>(null);

  // Chỉ thành viên đã chấp nhận mới ký được — backend cũng lọc như vậy và trả 400 nếu lệch.
  const candidates = useMemo(
    () => (members.data ?? []).filter((m) => m.invitationStatus === 'Accepted'),
    [members.data],
  );

  const isBusy = create.isPending || update.isPending;
  const isNamed = approverMode === 'NamedApprovers';

  const toggleApprover = (employeeId: string) =>
    setApproverIds((prev) =>
      prev.includes(employeeId)
        ? prev.filter((id) => id !== employeeId)
        : [...prev, employeeId],
    );

  const submit = async () => {
    setError(null);

    const body = {
      workItemTypeId,
      targetColumnId,
      approverMode,
      minApprovals: Number(minApprovals) || 1,
      approverIds: isNamed ? approverIds : [],
    };

    try {
      if (policy) await update.mutateAsync({ policyId: policy.id, body });
      else await create.mutateAsync(body);

      toast.success(policy ? 'Đã cập nhật luật duyệt.' : 'Đã thêm luật duyệt.');
      onClose();
    } catch (caught) {
      setError(errorMessage(caught));
    }
  };

  // Cảnh báo TẠI CHỖ thay vì để người dùng bấm Lưu rồi nhận 400: một cổng có quorum lớn hơn
  // số người ký sẽ không bao giờ mở được (luật 4 Doctrine — backend cũng chặn, đây là bản
  // nói trước cho tử tế).
  const quorumUnreachable =
    isNamed && approverIds.length > 0 && Number(minApprovals) > approverIds.length;

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{policy ? 'Sửa luật duyệt' : 'Thêm luật duyệt'}</DialogTitle>
          <DialogDescription>
            Ví dụ: “Change Request” cần 2 chữ ký mới vào được cột “Đang xử lý”.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="policy-type">Loại công việc</Label>
            <Select value={workItemTypeId} onValueChange={(v) => setWorkItemTypeId(v ?? '')}>
              <SelectTrigger id="policy-type" className="w-full">
                {/* 🔴 PHẢI có render prop. `SelectValue` trần của Base UI in ra chính GIÁ TRỊ
                    của ô — ở đây là một Guid — chứ không phải nhãn của mục đang chọn. Cùng
                    khuôn `task-form-dialog.tsx` và `view-bar.tsx`. */}
                <SelectValue placeholder="Chọn loại công việc">
                  {(current: string) =>
                    (types.data ?? []).find((t) => t.id === current)?.name ??
                    'Chọn loại công việc'
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(types.data ?? []).map((type) => (
                  <SelectItem key={type.id} value={type.id}>
                    {type.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="policy-column">Cột cần được duyệt</Label>
            <Select value={targetColumnId} onValueChange={(v) => setTargetColumnId(v ?? '')}>
              <SelectTrigger id="policy-column" className="w-full">
                <SelectValue placeholder="Chọn cột đích">
                  {(current: string) =>
                    (columns.data ?? []).find((c) => c.id === current)?.name ?? 'Chọn cột đích'
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(columns.data ?? []).map((column) => (
                  <SelectItem key={column.id} value={column.id}>
                    {column.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="policy-mode">Ai được duyệt</Label>
            <Select
              value={approverMode}
              onValueChange={(v) => setApproverMode((v ?? 'ProjectManagers') as ApproverMode)}
            >
              <SelectTrigger id="policy-mode" className="w-full">
                {/* Cùng lý do hai ô trên: không có render prop thì ô này in ra chính chuỗi
                    enum (`ProjectManagers`) chứ không phải nhãn tiếng Việt. */}
                <SelectValue>
                  {(current: string) =>
                    APPROVER_MODE_LABEL[current as ApproverMode] ??
                    APPROVER_MODE_LABEL.ProjectManagers
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(Object.keys(APPROVER_MODE_LABEL) as ApproverMode[]).map((mode) => (
                  <SelectItem key={mode} value={mode}>
                    {APPROVER_MODE_LABEL[mode]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {isNamed ? (
            <div className="grid gap-2">
              <Label>Người duyệt</Label>
              <div className="grid max-h-48 gap-1 overflow-y-auto rounded-md border p-2">
                {candidates.map((member) => (
                  <label
                    key={member.employeeId}
                    className="hover:bg-muted/50 flex min-w-0 cursor-pointer items-center gap-2 rounded px-1.5 py-1 text-sm"
                  >
                    {/* `<input type="checkbox">` trần, cùng khuôn `ManageWorkItemTypesDialog`
                        — bộ shadcn v4 của dự án không có component Checkbox. */}
                    <input
                      type="checkbox"
                      className="size-4 shrink-0"
                      checked={approverIds.includes(member.employeeId)}
                      onChange={() => toggleApprover(member.employeeId)}
                    />
                    <span className="min-w-0 truncate">{member.employeeName}</span>
                  </label>
                ))}
              </div>
              {/* ⚠️ Người duyệt chỉ định KHÔNG tự động gồm quản lý dự án — đó là điểm của chế
                  độ này ("chỉ trưởng phòng ký được"). Nói ra để không ai ngạc nhiên. */}
              <p className="text-muted-foreground text-xs">
                Quản lý dự án <strong>không</strong> tự động ký được ở chế độ này — chỉ những
                người bạn chọn ở trên.
              </p>
            </div>
          ) : null}

          <div className="grid gap-2">
            <Label htmlFor="policy-quorum">Số lượt duyệt cần có</Label>
            <Input
              id="policy-quorum"
              type="number"
              min={1}
              max={20}
              value={minApprovals}
              onChange={(event) => setMinApprovals(event.target.value)}
            />
            {quorumUnreachable ? (
              <p className="text-destructive text-xs">
                Bạn chọn {approverIds.length} người duyệt nhưng cần {minApprovals} lượt — cổng
                sẽ không bao giờ mở được.
              </p>
            ) : null}
          </div>
        </div>

        <FormError message={error} />

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={isBusy}>
            Huỷ
          </Button>
          <Button
            onClick={() => void submit()}
            disabled={
              isBusy ||
              !workItemTypeId ||
              !targetColumnId ||
              quorumUnreachable ||
              (isNamed && approverIds.length === 0)
            }
          >
            {policy ? 'Lưu' : 'Thêm'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

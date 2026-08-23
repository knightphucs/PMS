'use client';

import { LayersIcon, PencilIcon, PlusIcon, Trash2Icon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { FormError } from '@/components/form/form-error';
import { WorkItemTypeChip } from '@/components/tasks/work-item-type-chip';
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
import { useFieldDefinitions } from '@/lib/hooks/use-custom-fields';
import {
  useCreateWorkItemType,
  useDeleteWorkItemType,
  useUpdateWorkItemType,
  useWorkItemTypes,
} from '@/lib/hooks/use-work-item-types';
import type {
  WorkItemTypeFieldRequest,
  WorkItemTypeResponse,
} from '@/types/work-item-type';

/** Icon gợi ý — tên phải tồn tại trong `lucide-react`, xem `WorkItemTypeChip`. */
const ICONS = [
  'CircleDot',
  'CircleAlert',
  'Wrench',
  'ShieldCheck',
  'FileText',
  'Bug',
  'Rocket',
  'CalendarClock',
];

const SWATCHES = ['#6B7280', '#3B82F6', '#A855F7', '#22C55E', '#F59E0B', '#EF4444', '#14B8A6'];

/**
 * Quản lý loại công việc của project (ADR-060).
 *
 * 🔴 Luồng XOÁ là chỗ quan trọng nhất, y hệt dialog quản lý cột: loại còn task thì **bắt
 * buộc chọn loại đích**. Backend trả 400 kèm số task nếu thiếu, nhưng UI hỏi trước để người
 * dùng không phải gặp lỗi mới biết mình cần quyết định gì.
 */
export function ManageWorkItemTypesDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const types = useWorkItemTypes(projectId);
  const remove = useDeleteWorkItemType(projectId);

  const [editing, setEditing] = useState<WorkItemTypeResponse | null>(null);
  const [creating, setCreating] = useState(false);
  const [deleting, setDeleting] = useState<WorkItemTypeResponse | null>(null);
  const [targetId, setTargetId] = useState('');
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const list = types.data ?? [];
  const others = list.filter((t) => t.id !== deleting?.id);

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteError(null);
    try {
      await remove.mutateAsync({
        typeId: deleting.id,
        body: { targetTypeId: targetId || null },
      });
      toast.success(`Đã xoá loại "${deleting.name}".`);
      setDeleting(null);
      setTargetId('');
    } catch (error) {
      setDeleteError(errorMessage(error));
    }
  };

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger
          render={
            <Button variant="outline" size="sm">
              <LayersIcon className="size-4" />
              Loại công việc
            </Button>
          }
        />
        <DialogContent showCloseButton={false} className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Loại công việc</DialogTitle>
            <DialogDescription>
              Mỗi loại có tập trường tuỳ biến riêng — ví dụ “Change Request” bắt buộc có “Hệ
              thống ảnh hưởng”, còn “Task” thường thì không. Mọi task đều thuộc đúng một loại.
            </DialogDescription>
          </DialogHeader>

          <div className="grid min-w-0 gap-2">
            {list.map((type) => (
              <div
                key={type.id}
                className="flex min-w-0 flex-wrap items-center gap-2 rounded-md border p-2"
              >
                <WorkItemTypeChip
                  type={{
                    typeId: type.id,
                    name: type.name,
                    icon: type.icon,
                    color: type.color,
                  }}
                />
                <span className="text-muted-foreground min-w-0 flex-1 truncate text-xs">
                  {type.fields.length} trường
                  {type.fields.some((f) => f.isRequired)
                    ? ` · ${type.fields.filter((f) => f.isRequired).length} bắt buộc`
                    : ''}
                  {type.taskCount > 0 ? ` · ${type.taskCount} task` : ''}
                </span>

                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Sửa "${type.name}"`}
                  onClick={() => setEditing(type)}
                >
                  <PencilIcon className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Xoá "${type.name}"`}
                  disabled={list.length <= 1}
                  onClick={() => {
                    setDeleting(type);
                    setTargetId('');
                    setDeleteError(null);
                  }}
                >
                  <Trash2Icon className="text-destructive size-4" />
                </Button>
              </div>
            ))}
          </div>

          <DialogFooter className="flex-wrap gap-2 sm:justify-between">
            <Button variant="outline" size="sm" onClick={() => setCreating(true)}>
              <PlusIcon className="size-4" />
              Thêm loại
            </Button>
            <DialogClose render={<Button variant="ghost">Đóng</Button>} />
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {creating ? (
        <TypeFormDialog projectId={projectId} onClose={() => setCreating(false)} />
      ) : null}

      {editing ? (
        <TypeFormDialog
          projectId={projectId}
          type={editing}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <Dialog open={deleting !== null} onOpenChange={(next) => !next && setDeleting(null)}>
        <DialogContent showCloseButton={false} className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Xoá loại “{deleting?.name}”?</DialogTitle>
            <DialogDescription>
              {deleting && deleting.taskCount > 0
                ? `${deleting.taskCount} task đang thuộc loại này. Task không thể không có loại, nên hãy chọn loại đích để chuyển chúng sang.`
                : 'Loại này chưa có task nào.'}
            </DialogDescription>
          </DialogHeader>

          {deleting && deleting.taskCount > 0 ? (
            <div className="grid gap-2">
              <Label htmlFor="target-type">Chuyển task sang</Label>
              <Select value={targetId} onValueChange={(v) => setTargetId(v ?? '')}>
                <SelectTrigger id="target-type" className="w-full">
                  {/* 🔴 PHẢI có render prop — `SelectValue` trần của Base UI in ra chính GIÁ
                      TRỊ của ô, ở đây là một Guid. Lỗi có sẵn từ ADR-060, phát hiện 2026-08-23
                      khi cùng hình dạng lỗi lộ ra ở dialog luật duyệt. Ô này chỉ hiện khi loại
                      đang xoá CÒN task, nên nó nằm ngoài đường đi thường ngày — đúng lớp lỗi
                      "thứ cần kiểm chứng chưa có ai gọi tới" mà §15 đã đặt tên. */}
                  <SelectValue placeholder="Chọn loại đích">
                    {(current: string) =>
                      others.find((t) => t.id === current)?.name ?? 'Chọn loại đích'
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {others.map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}

          <FormError message={deleteError} />

          <DialogFooter>
            <Button variant="ghost" onClick={() => setDeleting(null)}>
              Huỷ
            </Button>
            <Button
              variant="destructive"
              disabled={
                remove.isPending || (deleting !== null && deleting.taskCount > 0 && !targetId)
              }
              onClick={() => void confirmDelete()}
            >
              Xoá loại
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** Form thêm/sửa một loại, kèm bảng chọn trường và cờ bắt buộc. */
function TypeFormDialog({
  projectId,
  type,
  onClose,
}: {
  projectId: string;
  type?: WorkItemTypeResponse;
  onClose: () => void;
}) {
  const isEdit = type !== undefined;
  const create = useCreateWorkItemType(projectId);
  const update = useUpdateWorkItemType(projectId);
  const fields = useFieldDefinitions(projectId);

  const [name, setName] = useState(type?.name ?? '');
  const [icon, setIcon] = useState(type?.icon ?? ICONS[0]);
  const [color, setColor] = useState(type?.color ?? SWATCHES[1]);
  const [selected, setSelected] = useState<WorkItemTypeFieldRequest[]>(
    type?.fields.map((f) => ({
      fieldDefinitionId: f.fieldDefinitionId,
      isRequired: f.isRequired,
    })) ?? [],
  );
  const [error, setError] = useState<string | null>(null);

  const isPending = create.isPending || update.isPending;

  const toggle = (fieldId: string) =>
    setSelected((prev) =>
      prev.some((f) => f.fieldDefinitionId === fieldId)
        ? prev.filter((f) => f.fieldDefinitionId !== fieldId)
        : [...prev, { fieldDefinitionId: fieldId, isRequired: false }],
    );

  const setRequired = (fieldId: string, isRequired: boolean) =>
    setSelected((prev) =>
      prev.map((f) => (f.fieldDefinitionId === fieldId ? { ...f, isRequired } : f)),
    );

  const submit = async () => {
    setError(null);
    const body = { name: name.trim(), icon, color, fields: selected };

    try {
      if (isEdit) await update.mutateAsync({ typeId: type.id, body });
      else await create.mutateAsync(body);
      toast.success(isEdit ? 'Đã lưu loại.' : 'Đã thêm loại.');
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa loại công việc' : 'Thêm loại công việc'}</DialogTitle>
          <DialogDescription>
            Chọn những trường tuỳ biến mà loại này dùng. Chỉ các trường được chọn mới hiện ra
            trên task thuộc loại đó.
          </DialogDescription>
        </DialogHeader>

        <div className="grid min-w-0 gap-4">
          <div className="grid gap-2">
            <Label htmlFor="type-name">Tên loại</Label>
            <Input
              id="type-name"
              value={name}
              maxLength={50}
              placeholder="Change Request"
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="type-icon">Icon</Label>
              <Select value={icon} onValueChange={(v) => setIcon(v ?? ICONS[0])}>
                <SelectTrigger id="type-icon" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ICONS.map((name_) => (
                    <SelectItem key={name_} value={name_}>
                      {name_}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="type-color">Màu</Label>
              <input
                id="type-color"
                type="color"
                value={color}
                className="h-9 w-full cursor-pointer rounded border bg-transparent"
                onChange={(e) => setColor(e.target.value)}
              />
            </div>
          </div>

          <div className="grid gap-2">
            <Label>Trường tuỳ biến của loại này</Label>

            {(fields.data?.length ?? 0) === 0 ? (
              <p className="text-muted-foreground text-sm">
                Project chưa có trường tuỳ biến nào. Tạo ở nút “Trường tuỳ biến”.
              </p>
            ) : (
              <div className="grid max-h-56 gap-1 overflow-y-auto">
                {(fields.data ?? []).map((field) => {
                  const picked = selected.find((f) => f.fieldDefinitionId === field.id);
                  return (
                    <div
                      key={field.id}
                      className="flex min-w-0 items-center gap-2 rounded border p-2"
                    >
                      <input
                        type="checkbox"
                        id={`f-${field.id}`}
                        className="size-4 shrink-0"
                        checked={picked !== undefined}
                        onChange={() => toggle(field.id)}
                      />
                      <label
                        htmlFor={`f-${field.id}`}
                        className="min-w-0 flex-1 truncate text-sm"
                      >
                        {field.label}
                      </label>

                      {picked ? (
                        <label className="text-muted-foreground flex shrink-0 items-center gap-1 text-xs">
                          <input
                            type="checkbox"
                            className="size-3.5"
                            checked={picked.isRequired}
                            onChange={(e) => setRequired(field.id, e.target.checked)}
                          />
                          Bắt buộc
                        </label>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            )}

            {selected.some((f) => f.isRequired) ? (
              // Nói trước phạm vi của "bắt buộc" — nếu không, người dùng sẽ tưởng nó chặn
              // cả những task đang có và hoảng khi thấy chúng vẫn trống.
              <p className="text-muted-foreground text-xs">
                ⚠️ “Bắt buộc” chặn việc <strong>xoá trắng</strong> giá trị trên task thuộc
                loại này. Task đã tạo từ trước mà đang để trống vẫn dùng bình thường — bật cờ
                này không làm hỏng dữ liệu cũ.
              </p>
            ) : null}
          </div>

          <FormError message={error} />
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={isPending}>
            Huỷ
          </Button>
          <Button onClick={() => void submit()} disabled={isPending || name.trim().length === 0}>
            {isEdit ? 'Lưu' : 'Thêm loại'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

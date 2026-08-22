'use client';

import {
  ChevronDownIcon,
  ChevronUpIcon,
  PencilIcon,
  PlusIcon,
  SlidersHorizontalIcon,
  Trash2Icon,
  XIcon,
} from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { ConfirmDialog } from '@/components/common/confirm-dialog';
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
  useCreateFieldDefinition,
  useDeleteFieldDefinition,
  useFieldDefinitions,
  useReorderFieldDefinitions,
  useUpdateFieldDefinition,
} from '@/lib/hooks/use-custom-fields';
import {
  FIELD_TYPE_LABEL,
  isSelectType,
  type FieldDefinitionResponse,
  type FieldOptionRequest,
  type FieldType,
} from '@/types/custom-field';

const TYPES: FieldType[] = [
  'Text',
  'Number',
  'Date',
  'Checkbox',
  'Url',
  'SingleSelect',
  'MultiSelect',
];

const SWATCHES = ['#6B7280', '#3B82F6', '#A855F7', '#22C55E', '#F59E0B', '#EF4444', '#14B8A6'];

/**
 * Quản lý trường tuỳ biến của project (ADR-059).
 *
 * 🔴 Hai chỗ UI phải nói thật thay vì để người dùng tự phát hiện:
 *  1. **Kiểu không đổi được sau khi tạo.** Form sửa không có ô chọn kiểu — bày ra rồi báo
 *     lỗi khi lưu là hứa một việc backend từ chối làm.
 *  2. **Đổi TÊN một lựa chọn = mất giá trị** ở các task đang chọn nó (backend khớp theo
 *     Label, xem ADR-059). Đổi màu/thứ tự thì không sao. Cảnh báo hiện ngay trong form sửa.
 */
export function ManageFieldsDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const fields = useFieldDefinitions(projectId);
  const reorder = useReorderFieldDefinitions(projectId);
  const remove = useDeleteFieldDefinition(projectId);

  const [editing, setEditing] = useState<FieldDefinitionResponse | null>(null);
  const [creating, setCreating] = useState(false);
  const [deleting, setDeleting] = useState<FieldDefinitionResponse | null>(null);

  const list = fields.data ?? [];

  const move = async (index: number, delta: number) => {
    const next = [...list];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];

    try {
      // Gửi TRỌN danh sách — server từ chối danh sách thiếu hoặc trùng.
      await reorder.mutateAsync({ fieldIds: next.map((f) => f.id) });
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogTrigger
          render={
            <Button variant="outline" size="sm">
              <SlidersHorizontalIcon className="size-4" />
              Trường tuỳ biến
            </Button>
          }
        />
        <DialogContent showCloseButton={false} className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Trường tuỳ biến</DialogTitle>
            <DialogDescription>
              Thêm những thông tin mà riêng đội bạn cần theo dõi trên mỗi task — ví dụ “Hệ
              thống ảnh hưởng”, “Cửa sổ bảo trì”, “Mức rủi ro”. Chúng hiện ở trang chi tiết
              task.
            </DialogDescription>
          </DialogHeader>

          <div className="grid min-w-0 gap-2">
            {list.length === 0 ? (
              <p className="text-muted-foreground py-6 text-center text-sm">
                Chưa có trường nào. Thêm trường đầu tiên bên dưới.
              </p>
            ) : null}

            {list.map((field, index) => (
              <div
                key={field.id}
                className="flex min-w-0 flex-wrap items-center gap-2 rounded-md border p-2"
              >
                <div className="grid min-w-0 flex-1 gap-0.5">
                  <span className="truncate text-sm font-medium">{field.label}</span>
                  <span className="text-muted-foreground text-xs">
                    {FIELD_TYPE_LABEL[field.type]}
                    {isSelectType(field.type) ? ` · ${field.options.length} lựa chọn` : ''}
                    {field.valueCount > 0 ? ` · ${field.valueCount} task đã điền` : ''}
                  </span>
                </div>

                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Đưa "${field.label}" lên trên`}
                  disabled={index === 0 || reorder.isPending}
                  onClick={() => void move(index, -1)}
                >
                  <ChevronUpIcon className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Đưa "${field.label}" xuống dưới`}
                  disabled={index === list.length - 1 || reorder.isPending}
                  onClick={() => void move(index, 1)}
                >
                  <ChevronDownIcon className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Sửa "${field.label}"`}
                  onClick={() => setEditing(field)}
                >
                  <PencilIcon className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Xoá "${field.label}"`}
                  onClick={() => setDeleting(field)}
                >
                  <Trash2Icon className="text-destructive size-4" />
                </Button>
              </div>
            ))}
          </div>

          <DialogFooter className="flex-wrap gap-2 sm:justify-between">
            <Button variant="outline" size="sm" onClick={() => setCreating(true)}>
              <PlusIcon className="size-4" />
              Thêm trường
            </Button>
            <DialogClose render={<Button variant="ghost">Đóng</Button>} />
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {creating ? (
        <FieldFormDialog projectId={projectId} onClose={() => setCreating(false)} />
      ) : null}

      {editing ? (
        <FieldFormDialog
          projectId={projectId}
          field={editing}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        error={null}
        variant="destructive"
        title={`Xoá trường "${deleting?.label}"?`}
        description={
          deleting && deleting.valueCount > 0
            ? `${deleting.valueCount} task đang có giá trị cho trường này. Xoá trường sẽ xoá luôn những giá trị đó và không khôi phục được.`
            : 'Trường này chưa có task nào điền giá trị.'
        }
        confirmLabel="Xoá trường"
        isPending={remove.isPending}
        onConfirm={async () => {
          if (!deleting) return;
          try {
            await remove.mutateAsync(deleting.id);
            toast.success(`Đã xoá trường "${deleting.label}".`);
            setDeleting(null);
          } catch (error) {
            toast.error(errorMessage(error));
          }
        }}
      />
    </>
  );
}

/** Form thêm/sửa một trường. `field` có giá trị = chế độ sửa. */
function FieldFormDialog({
  projectId,
  field,
  onClose,
}: {
  projectId: string;
  field?: FieldDefinitionResponse;
  onClose: () => void;
}) {
  const isEdit = field !== undefined;
  const create = useCreateFieldDefinition(projectId);
  const update = useUpdateFieldDefinition(projectId);

  const [label, setLabel] = useState(field?.label ?? '');
  const [type, setType] = useState<FieldType>(field?.type ?? 'Text');
  const [options, setOptions] = useState<FieldOptionRequest[]>(
    field?.options.map((o) => ({ label: o.label, color: o.color })) ?? [
      { label: '', color: SWATCHES[1] },
    ],
  );
  const [error, setError] = useState<string | null>(null);

  const needsOptions = isSelectType(type);
  const isPending = create.isPending || update.isPending;

  const submit = async () => {
    setError(null);

    const cleaned = options
      .map((o) => ({ label: o.label.trim(), color: o.color }))
      .filter((o) => o.label.length > 0);

    if (needsOptions && cleaned.length === 0) {
      setError('Trường kiểu chọn phải có ít nhất một lựa chọn.');
      return;
    }

    try {
      if (isEdit) {
        await update.mutateAsync({
          fieldId: field.id,
          body: { label: label.trim(), options: needsOptions ? cleaned : undefined },
        });
        toast.success('Đã lưu trường.');
      } else {
        await create.mutateAsync({
          label: label.trim(),
          type,
          options: needsOptions ? cleaned : undefined,
        });
        toast.success('Đã thêm trường.');
      }
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent showCloseButton={false} className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa trường' : 'Thêm trường'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Kiểu dữ liệu không đổi được sau khi tạo — mọi giá trị đã nhập sẽ nằm sai chỗ.'
              : 'Chọn kiểu cẩn thận: sau khi tạo thì không đổi được nữa.'}
          </DialogDescription>
        </DialogHeader>

        <div className="grid min-w-0 gap-4">
          <div className="grid gap-2">
            <Label htmlFor="field-label">Tên trường</Label>
            <Input
              id="field-label"
              value={label}
              maxLength={100}
              placeholder="Hệ thống ảnh hưởng"
              onChange={(e) => setLabel(e.target.value)}
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="field-type">Kiểu dữ liệu</Label>
            {isEdit ? (
              // Cố ý hiện dạng CHỈ ĐỌC thay vì ô chọn bị vô hiệu hoá: ô chọn xám vẫn gợi ý
              // rằng đâu đó có cách bật nó lên, còn dòng chữ này nói thẳng là không có.
              <p className="text-muted-foreground text-sm">
                {FIELD_TYPE_LABEL[type]} — không đổi được sau khi tạo.
              </p>
            ) : (
              <Select value={type} onValueChange={(v) => setType(v as FieldType)}>
                <SelectTrigger id="field-type">
                  {/* 🔴 Render prop bắt buộc: `SelectValue` trần in ra chính giá trị của ô —
                      ở đây là chuỗi enum `SingleSelect`, không phải nhãn "Chọn một". Lỗi có
                      sẵn từ ADR-059, phát hiện 2026-08-23. */}
                  <SelectValue>
                    {(current: string) =>
                      FIELD_TYPE_LABEL[current as FieldType] ?? current
                    }
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => (
                    <SelectItem key={t} value={t}>
                      {FIELD_TYPE_LABEL[t]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          {needsOptions ? (
            <div className="grid gap-2">
              <Label>Các lựa chọn</Label>

              {isEdit ? (
                <p className="text-muted-foreground text-xs">
                  ⚠️ Đổi <strong>màu</strong> hoặc <strong>thứ tự</strong> thì giá trị đã nhập
                  giữ nguyên. Nhưng đổi <strong>tên</strong> một lựa chọn sẽ làm các task đang
                  chọn nó mất giá trị — hệ thống coi đó là một lựa chọn khác.
                </p>
              ) : null}

              <div className="grid gap-2">
                {options.map((option, index) => (
                  <div key={index} className="flex min-w-0 items-center gap-2">
                    <input
                      type="color"
                      aria-label={`Màu lựa chọn ${index + 1}`}
                      value={option.color}
                      className="size-9 shrink-0 cursor-pointer rounded border bg-transparent"
                      onChange={(e) =>
                        setOptions((prev) =>
                          prev.map((o, i) =>
                            i === index ? { ...o, color: e.target.value } : o,
                          ),
                        )
                      }
                    />
                    <Input
                      value={option.label}
                      maxLength={100}
                      placeholder={`Lựa chọn ${index + 1}`}
                      onChange={(e) =>
                        setOptions((prev) =>
                          prev.map((o, i) =>
                            i === index ? { ...o, label: e.target.value } : o,
                          ),
                        )
                      }
                    />
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`Bỏ lựa chọn ${index + 1}`}
                      disabled={options.length === 1}
                      onClick={() =>
                        setOptions((prev) => prev.filter((_, i) => i !== index))
                      }
                    >
                      <XIcon className="size-4" />
                    </Button>
                  </div>
                ))}
              </div>

              <Button
                variant="outline"
                size="sm"
                className="justify-self-start"
                disabled={options.length >= 50}
                onClick={() =>
                  setOptions((prev) => [
                    ...prev,
                    { label: '', color: SWATCHES[prev.length % SWATCHES.length] },
                  ])
                }
              >
                <PlusIcon className="size-4" />
                Thêm lựa chọn
              </Button>
            </div>
          ) : null}

          <FormError message={error} />
        </div>

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={isPending}>
            Huỷ
          </Button>
          <Button onClick={() => void submit()} disabled={isPending || label.trim().length === 0}>
            {isEdit ? 'Lưu' : 'Thêm trường'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

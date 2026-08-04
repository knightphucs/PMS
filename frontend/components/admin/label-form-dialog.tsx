'use client';

import { useEffect, useState } from 'react';
import { toast } from 'sonner';

import { Field } from '@/components/form/field';
import { FormError } from '@/components/form/form-error';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { errorMessage } from '@/lib/api/problem';
import { useCreateLabel, useUpdateLabel } from '@/lib/hooks/use-labels';
import type { LabelResponse } from '@/types/label';

/** Backend mặc định màu này khi client không gửi. Giữ đồng bộ để bản xem trước không nói dối. */
const DEFAULT_COLOR = '#6B7280';

const HEX = /^#[0-9a-fA-F]{6}$/;

/**
 * Một dialog cho cả tạo lẫn sửa — `label === null` là chế độ tạo.
 *
 * Gộp được vì `Label` không có `rowVersion`: không có luồng 409, không phải nạp chi tiết
 * trước. Cùng lý do với `SprintFormDialog`.
 *
 * ⚠️ Bất đối xứng quyền có thật ở backend: **tạo** nhãn thì mọi user đăng nhập đều làm được,
 * còn **sửa/xóa** cần `labels:manage`. Màn này nằm sau tab đã gác `labels:manage` nên cả hai
 * đều an toàn — đừng đem component này ra dùng ở nơi chưa gác.
 */
export function LabelFormDialog({
  open,
  label,
  onClose,
}: {
  open: boolean;
  label: LabelResponse | null;
  onClose: () => void;
}) {
  const isEdit = label !== null;
  const [name, setName] = useState('');
  const [color, setColor] = useState(DEFAULT_COLOR);
  const [error, setError] = useState<string | null>(null);

  const createLabel = useCreateLabel();
  const updateLabel = useUpdateLabel();
  const isPending = createLabel.isPending || updateLabel.isPending;

  useEffect(() => {
    if (!open) return;
    setName(label?.name ?? '');
    setColor(label?.color ?? DEFAULT_COLOR);
    setError(null);
  }, [open, label]);

  const colorInvalid = !HEX.test(color);

  const submit = async () => {
    setError(null);

    try {
      if (isEdit) {
        await updateLabel.mutateAsync({ id: label.id, body: { name: name.trim(), color } });
        toast.success(`Đã cập nhật nhãn "${name.trim()}".`);
      } else {
        await createLabel.mutateAsync({ name: name.trim(), color });
        toast.success(`Đã tạo nhãn "${name.trim()}".`);
      }
      onClose();
    } catch (err) {
      // 409 trùng tên là lỗi thường gặp nhất ở đây — giữ dialog mở để sửa tên tại chỗ.
      setError(errorMessage(err));
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa nhãn' : 'Tạo nhãn'}</DialogTitle>
          <DialogDescription>
            {isEdit
              ? 'Đổi tên hoặc màu sẽ áp dụng ngay ở mọi dự án đang dùng nhãn này.'
              : 'Nhãn dùng chung cho mọi dự án. Tên nhãn phải là duy nhất trong toàn hệ thống.'}
          </DialogDescription>
        </DialogHeader>

        <Field
          label="Tên nhãn"
          autoFocus
          value={name}
          maxLength={50}
          onChange={(event) => setName(event.target.value)}
          placeholder="bug"
        />

        <div className="grid gap-2">
          <Label htmlFor="label-color">Màu</Label>
          <div className="flex items-center gap-3">
            <input
              id="label-color"
              type="color"
              value={HEX.test(color) ? color : DEFAULT_COLOR}
              onChange={(event) => setColor(event.target.value.toUpperCase())}
              className="border-input h-9 w-14 shrink-0 cursor-pointer rounded-md border bg-transparent p-1"
            />
            <input
              value={color}
              onChange={(event) => setColor(event.target.value.toUpperCase())}
              aria-label="Mã màu dạng hex"
              aria-invalid={colorInvalid || undefined}
              className="border-input h-9 w-32 rounded-md border px-3 text-sm tabular-nums"
            />
            <span
              className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium"
              style={{
                backgroundColor: `${HEX.test(color) ? color : DEFAULT_COLOR}20`,
                color: HEX.test(color) ? color : DEFAULT_COLOR,
              }}
            >
              {name.trim() || 'xem trước'}
            </span>
          </div>
          {colorInvalid ? (
            <p role="alert" className="text-destructive text-sm">
              Mã màu phải có dạng #RRGGBB.
            </p>
          ) : null}
        </div>

        <FormError message={error} />

        <DialogFooter>
          <Button variant="ghost" onClick={onClose} disabled={isPending}>
            Hủy
          </Button>
          <Button
            disabled={!name.trim() || colorInvalid || isPending}
            onClick={() => void submit()}
          >
            {isPending ? 'Đang lưu…' : isEdit ? 'Lưu nhãn' : 'Tạo nhãn'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

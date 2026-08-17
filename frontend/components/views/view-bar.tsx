'use client';

import { SaveIcon, Trash2Icon, UsersIcon } from 'lucide-react';
import { useState } from 'react';

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
import { useCreateSavedView, useDeleteSavedView, useUpdateSavedView } from '@/lib/hooks/use-saved-views';
import type { SavedViewResponse } from '@/types/saved-view';

/** Giá trị đại diện cho "chưa chọn view nào" — `Select` không nhận `null`. */
export const NO_VIEW = 'none';

/**
 * Thanh chọn / lưu view (ADR-061).
 *
 * 🔴 **Ba mức quyền hiện ra thành ba hành vi khác nhau, không phải ba nút bị vô hiệu hoá:**
 *  - Ai cũng lưu được view **riêng** của mình → nút "Lưu thành view mới" luôn có.
 *  - Chỉ PM tạo/sửa được view **chia sẻ** → ô "Chia sẻ với cả đội" chỉ hiện với PM.
 *  - Sửa/xoá một view có sẵn theo `view.canEdit` do **backend trả về** — không tự suy ở
 *    frontend, vì luật là "chủ sở hữu HOẶC PM" mà hai nơi cùng dựng một luật thì có lúc lệch.
 *
 * Nút xám vẫn gợi ý rằng đâu đó có cách bật lên (bài học ADR-059), nên thứ không dùng được
 * thì **không hiện**.
 */
export function ViewBar({
  projectId,
  views,
  activeViewId,
  isDirty,
  canShare,
  currentState,
  onSelect,
  onSaved,
}: {
  projectId: string;
  views: SavedViewResponse[];
  activeViewId: string | null;
  /** Trạng thái đang xem đã lệch khỏi view đã lưu chưa. */
  isDirty: boolean;
  /** Người dùng có quyền tạo/sửa view CHIA SẺ không (PM). */
  canShare: boolean;
  /** Bộ lọc + sắp xếp + cột đang áp dụng, để lưu xuống. */
  currentState: Pick<
    SavedViewResponse,
    'sortBy' | 'sortDescending' | 'groupBy' | 'filters' | 'columns'
  >;
  onSelect: (viewId: string | null) => void;
  onSaved: (view: SavedViewResponse) => void;
}) {
  const [saveOpen, setSaveOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [name, setName] = useState('');
  const [shared, setShared] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const create = useCreateSavedView(projectId);
  const update = useUpdateSavedView(projectId);
  const remove = useDeleteSavedView(projectId);

  const active = views.find((v) => v.id === activeViewId) ?? null;

  const handleCreate = async () => {
    setError(null);
    try {
      const created = await create.mutateAsync({
        name: name.trim(),
        isShared: shared,
        ...currentState,
      });
      setSaveOpen(false);
      setName('');
      setShared(false);
      onSaved(created);
    } catch (cause) {
      // Giữ dialog mở và hiện lỗi tại chỗ: 409 trùng tên và 403 chia-sẻ đều kèm câu tiếng
      // Việt nói đúng việc cần làm — đóng dialog là ném đi đúng câu đó (khuôn ConfirmDialog).
      setError(errorMessage(cause));
    }
  };

  const handleUpdate = async () => {
    if (!active) return;
    setError(null);
    try {
      const saved = await update.mutateAsync({
        viewId: active.id,
        body: { name: active.name, isShared: active.isShared, ...currentState },
      });
      onSaved(saved);
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  const handleDelete = async () => {
    if (!active) return;
    setError(null);
    try {
      await remove.mutateAsync(active.id);
      setDeleteOpen(false);
      onSelect(null);
    } catch (cause) {
      setError(errorMessage(cause));
    }
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Select
        value={activeViewId ?? NO_VIEW}
        onValueChange={(next: string | null) =>
          onSelect(next === null || next === NO_VIEW ? null : next)
        }
      >
        <SelectTrigger size="sm" className="w-56" aria-label="Chọn view">
          {/* ⚠️ `SelectValue` của Base UI hiện GIÁ TRỊ thô — phải tự dựng nhãn. */}
          <SelectValue>
            {(current: string) =>
              current === NO_VIEW
                ? 'Không dùng view'
                : (views.find((v) => v.id === current)?.name ?? 'Không dùng view')
            }
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={NO_VIEW}>Không dùng view</SelectItem>
          {views.map((view) => (
            <SelectItem key={view.id} value={view.id}>
              <span className="flex items-center gap-2">
                {view.name}
                {view.isShared ? (
                  <UsersIcon className="size-3.5 shrink-0" aria-label="View dùng chung" />
                ) : null}
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {isDirty ? (
        <span className="text-muted-foreground text-xs" role="status">
          Có thay đổi chưa lưu
        </span>
      ) : null}

      {/* Chỉ hiện khi có thứ để lưu VÀ view đó sửa được — nút "Lưu" trên một view chỉ đọc
          là một lời hứa backend sẽ từ chối. */}
      {active?.canEdit && isDirty ? (
        <Button size="sm" variant="outline" onClick={() => void handleUpdate()} disabled={update.isPending}>
          <SaveIcon className="size-4" />
          {update.isPending ? 'Đang lưu…' : 'Lưu'}
        </Button>
      ) : null}

      <Button size="sm" variant="outline" onClick={() => setSaveOpen(true)}>
        Lưu thành view mới
      </Button>

      {active?.canEdit ? (
        <Button
          size="sm"
          variant="ghost"
          aria-label={`Xoá view ${active.name}`}
          onClick={() => setDeleteOpen(true)}
        >
          <Trash2Icon className="size-4" />
        </Button>
      ) : null}

      {/* Lỗi của thao tác Lưu tại chỗ (không qua dialog) vẫn phải nhìn thấy được. */}
      {error && !saveOpen && !deleteOpen ? <FormError message={error} /> : null}

      <Dialog open={saveOpen} onOpenChange={setSaveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Lưu thành view mới</DialogTitle>
            <DialogDescription>
              Bộ lọc, sắp xếp và tập cột đang xem sẽ được lưu lại.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-3">
            <div className="grid gap-1.5">
              <Label htmlFor="view-name">Tên view</Label>
              <Input
                id="view-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Ví dụ: Change Request chờ duyệt"
                maxLength={100}
              />
            </div>

            {canShare ? (
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={shared}
                  onChange={(event) => setShared(event.target.checked)}
                />
                <span>
                  Chia sẻ với cả đội
                  <span className="text-muted-foreground block text-xs">
                    Mọi thành viên dự án sẽ thấy view này.
                  </span>
                </span>
              </label>
            ) : null}

            {error ? <FormError message={error} /> : null}
          </div>

          <DialogFooter>
            <DialogClose render={<Button variant="outline">Huỷ</Button>} />
            <Button
              onClick={() => void handleCreate()}
              disabled={name.trim().length === 0 || create.isPending}
            >
              {create.isPending ? 'Đang lưu…' : 'Lưu view'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={deleteOpen}
        title="Xoá view"
        description={
          active?.isShared
            ? `View "${active.name}" đang được chia sẻ — xoá thì cả đội mất góc nhìn này.`
            : `Xoá view "${active?.name}"? Task không bị ảnh hưởng.`
        }
        confirmLabel="Xoá view"
        pendingLabel="Đang xoá…"
        error={error}
        isPending={remove.isPending}
        onConfirm={() => void handleDelete()}
        onClose={() => {
          setDeleteOpen(false);
          setError(null);
        }}
      />
    </div>
  );
}

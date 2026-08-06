'use client';

import { GripVerticalIcon, PencilIcon, PlusIcon, SettingsIcon, Trash2Icon } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';

import { columnDotStyle } from '@/components/tasks/status-tone';
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
import { FormError } from '@/components/form/form-error';
import { errorMessage } from '@/lib/api/problem';
import {
  useBoardColumns,
  useCreateBoardColumn,
  useDeleteBoardColumn,
  useReorderBoardColumns,
  useUpdateBoardColumn,
} from '@/lib/hooks/use-board-columns';
import type { BoardColumnResponse, StatusCategory } from '@/types/task';

/** Nhóm là danh mục ĐÓNG — người dùng đặt tên cột tuỳ ý nhưng phải khai nó nghĩa là gì. */
const CATEGORIES: { value: StatusCategory; label: string; hint: string }[] = [
  { value: 'ToDo', label: 'Chưa bắt đầu', hint: 'Task ở đây mới tự nhận việc được.' },
  { value: 'InProgress', label: 'Đang làm', hint: 'Task bị chặn không chuyển vào được.' },
  { value: 'Done', label: 'Hoàn thành', hint: 'Tính là đã xong ở mọi thống kê và báo cáo.' },
];

/** Bảng màu gợi ý. Người dùng vẫn nhập hex tự do được. */
const SWATCHES = ['#6B7280', '#3B82F6', '#A855F7', '#22C55E', '#F59E0B', '#EF4444', '#14B8A6'];

/**
 * Quản lý cột board (ADR-052) — thêm, sửa, đổi thứ tự, xóa.
 *
 * 🔴 Điểm quan trọng nhất là luồng XÓA: cột còn task thì **bắt buộc chọn cột đích**. Backend
 * trả 400 kèm số task nếu thiếu, nhưng UI hỏi trước để người dùng không phải gặp lỗi mới
 * biết mình cần quyết định gì.
 */
export function ManageColumnsDialog({ projectId }: { projectId: string }) {
  const [open, setOpen] = useState(false);
  const columns = useBoardColumns(projectId);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button variant="outline" size="sm">
            <SettingsIcon className="size-4" />
            Quản lý cột
          </Button>
        }
      />
      <DialogContent showCloseButton={false} className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Cột trên bảng</DialogTitle>
          <DialogDescription>
            Mỗi cột phải khai thuộc nhóm nào. Tên cột là của bạn, còn nhóm là thứ hệ thống
            dùng để biết task đã xong hay chưa.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          {(columns.data ?? []).map((column, index) => (
            <ColumnRow
              key={column.id}
              projectId={projectId}
              column={column}
              siblings={columns.data ?? []}
              index={index}
            />
          ))}
        </div>

        <AddColumnForm projectId={projectId} />

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Đóng</DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ColumnRow({
  projectId,
  column,
  siblings,
  index,
}: {
  projectId: string;
  column: BoardColumnResponse;
  siblings: BoardColumnResponse[];
  index: number;
}) {
  const [editing, setEditing] = useState(false);
  const reorder = useReorderBoardColumns(projectId);

  const move = (delta: number) => {
    const next = [...siblings];
    const [taken] = next.splice(index, 1);
    next.splice(index + delta, 0, taken);
    reorder.mutate(
      { orderedColumnIds: next.map((c) => c.id) },
      { onError: (error) => toast.error(errorMessage(error)) },
    );
  };

  if (editing) {
    return (
      <ColumnForm
        projectId={projectId}
        column={column}
        onDone={() => setEditing(false)}
      />
    );
  }

  return (
    <div className="flex items-center gap-2 rounded-lg border px-3 py-2">
      <GripVerticalIcon className="text-muted-foreground size-4 shrink-0" />
      <span className="size-2.5 shrink-0 rounded-full" style={columnDotStyle(column.color)} />

      <span className="min-w-0 flex-1 truncate text-sm font-medium">{column.name}</span>

      <span className="text-muted-foreground shrink-0 text-xs">
        {CATEGORIES.find((c) => c.value === column.category)?.label}
      </span>
      <span className="bg-muted text-muted-foreground shrink-0 rounded px-1.5 py-0.5 text-[11px] tabular-nums">
        {column.taskCount}
      </span>

      <div className="flex shrink-0 items-center gap-0.5">
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Chuyển ${column.name} sang trái`}
          disabled={index === 0 || reorder.isPending}
          onClick={() => move(-1)}
        >
          ←
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Chuyển ${column.name} sang phải`}
          disabled={index === siblings.length - 1 || reorder.isPending}
          onClick={() => move(1)}
        >
          →
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`Sửa cột ${column.name}`}
          onClick={() => setEditing(true)}
        >
          <PencilIcon className="size-3.5" />
        </Button>
        <DeleteColumnButton projectId={projectId} column={column} siblings={siblings} />
      </div>
    </div>
  );
}

function AddColumnForm({ projectId }: { projectId: string }) {
  const [adding, setAdding] = useState(false);

  if (!adding) {
    return (
      <Button variant="outline" size="sm" className="w-fit" onClick={() => setAdding(true)}>
        <PlusIcon className="size-4" />
        Thêm cột
      </Button>
    );
  }

  return <ColumnForm projectId={projectId} onDone={() => setAdding(false)} />;
}

/** Form dùng chung cho thêm và sửa — hai luồng khác nhau đúng một lời gọi mutation. */
function ColumnForm({
  projectId,
  column,
  onDone,
}: {
  projectId: string;
  column?: BoardColumnResponse;
  onDone: () => void;
}) {
  const [name, setName] = useState(column?.name ?? '');
  const [color, setColor] = useState(column?.color ?? SWATCHES[0]);
  const [category, setCategory] = useState<StatusCategory>(column?.category ?? 'ToDo');
  const [error, setError] = useState<string | null>(null);

  const create = useCreateBoardColumn(projectId);
  const update = useUpdateBoardColumn(projectId);
  const isPending = create.isPending || update.isPending;

  const categoryChanged = column !== undefined && column.category !== category;

  const submit = async () => {
    setError(null);
    const body = { name: name.trim(), color, category };

    try {
      if (column) await update.mutateAsync({ columnId: column.id, body });
      else await create.mutateAsync(body);
      onDone();
    } catch (caught) {
      // Giữ form mở và hiện chữ của server: 409 trùng tên và 400 sai định dạng màu đều có
      // câu tiếng Việt riêng, và cả hai đều sửa được ngay tại chỗ.
      setError(errorMessage(caught));
    }
  };

  return (
    <div className="grid gap-3 rounded-lg border p-3">
      <FormError message={error} />

      <div className="grid gap-2">
        <Label htmlFor="column-name">Tên cột</Label>
        <Input
          id="column-name"
          autoFocus
          value={name}
          placeholder="Chờ QA"
          maxLength={50}
          onChange={(event) => setName(event.target.value)}
        />
      </div>

      <div className="grid gap-2">
        <Label>Màu</Label>
        <div className="flex flex-wrap items-center gap-1.5">
          {SWATCHES.map((swatch) => (
            <button
              key={swatch}
              type="button"
              aria-label={`Chọn màu ${swatch}`}
              aria-pressed={color === swatch}
              onClick={() => setColor(swatch)}
              className="size-6 rounded-full ring-offset-2 transition-shadow data-[selected=true]:ring-2"
              data-selected={color === swatch}
              style={{ backgroundColor: swatch, boxShadow: color === swatch ? `0 0 0 2px ${swatch}` : undefined }}
            />
          ))}
        </div>
      </div>

      <div className="grid gap-2">
        <Label htmlFor="column-category">Nhóm</Label>
        <Select value={category} onValueChange={(v) => setCategory(v as StatusCategory)}>
          <SelectTrigger id="column-category" className="w-full">
            {/* `SelectValue` của Base UI hiện giá trị thô — phải tự định dạng. */}
            <SelectValue>
              {(current: StatusCategory) =>
                CATEGORIES.find((c) => c.value === current)?.label ?? current
              }
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {CATEGORIES.map((item) => (
              <SelectItem key={item.value} value={item.value}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-muted-foreground text-xs">
          {CATEGORIES.find((c) => c.value === category)?.hint}
        </p>
      </div>

      {/* Đổi nhóm chạm tới MỌI task trong cột — nói trước thay vì để người dùng phát hiện
          bằng cách thấy con số thống kê nhảy. */}
      {categoryChanged && column.taskCount > 0 ? (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          {column.taskCount} task trong cột này sẽ được tính lại theo nhóm mới.
        </p>
      ) : null}

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" size="sm" onClick={onDone}>
          Hủy
        </Button>
        <Button
          type="button"
          size="sm"
          disabled={!name.trim() || isPending}
          onClick={() => void submit()}
        >
          {isPending ? 'Đang lưu…' : column ? 'Lưu' : 'Thêm cột'}
        </Button>
      </div>
    </div>
  );
}

/**
 * Xóa cột — luồng có chọn cột đích.
 *
 * 🔴 Đây là yêu cầu nghiệp vụ cốt lõi của ADR-052, không phải một tiện ích: **không có
 * đường xóa cuốn theo task**. Task là dữ liệu người dùng đã bỏ công tạo, một cú bấm đổi cấu
 * hình board không được phép làm mất chúng — mà cũng không được âm thầm dồn chúng vào một
 * cột do máy chọn, vì khi đó họ không biết chỗ nào mà tìm.
 */
function DeleteColumnButton({
  projectId,
  column,
  siblings,
}: {
  projectId: string;
  column: BoardColumnResponse;
  siblings: BoardColumnResponse[];
}) {
  const [open, setOpen] = useState(false);
  const [targetId, setTargetId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const remove = useDeleteBoardColumn(projectId);

  const targets = siblings.filter((c) => c.id !== column.id);
  const isLast = siblings.length <= 1;
  const needsTarget = column.taskCount > 0;

  const confirm = async () => {
    setError(null);
    try {
      await remove.mutateAsync({
        columnId: column.id,
        body: { targetColumnId: needsTarget ? targetId : null },
      });
      setOpen(false);
      toast.success(`Đã xóa cột "${column.name}".`);
    } catch (caught) {
      setError(errorMessage(caught));
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={`Xóa cột ${column.name}`}
            // Ẩn hẳn đường đi thay vì để người dùng bấm rồi ăn 409: board phải luôn còn ít
            // nhất một cột, vì task mới cần một cột để đứng.
            disabled={isLast}
          >
            <Trash2Icon className="size-3.5" />
          </Button>
        }
      />
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>Xóa cột &ldquo;{column.name}&rdquo;?</DialogTitle>
          <DialogDescription>
            {needsTarget
              ? `Cột này còn ${column.taskCount} task. Chọn cột để chuyển chúng sang — task sẽ không bị xóa.`
              : 'Cột này đang trống nên không có task nào bị ảnh hưởng.'}
          </DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        {needsTarget ? (
          <div className="grid gap-2">
            <Label htmlFor="target-column">Chuyển task sang</Label>
            <Select value={targetId ?? ''} onValueChange={(v) => setTargetId(v || null)}>
              <SelectTrigger id="target-column" className="w-full">
                <SelectValue>
                  {(current: string) =>
                    targets.find((c) => c.id === current)?.name ?? 'Chọn cột…'
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {targets.map((target) => (
                  <SelectItem key={target.id} value={target.id}>
                    {target.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        ) : null}

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button
            type="button"
            variant="destructive"
            // Chặn ở nút thay vì để backend trả 400: người dùng đang ở đúng chỗ để quyết
            // định, nên hỏi xong mà vẫn cho bấm là bắt họ đọc một lỗi không cần thiết.
            disabled={remove.isPending || (needsTarget && !targetId)}
            onClick={() => void confirm()}
          >
            {remove.isPending ? 'Đang xóa…' : 'Xóa cột'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

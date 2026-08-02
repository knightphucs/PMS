'use client';

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

/**
 * Xác nhận một thao tác phá hủy — theo đúng khuôn `DeleteProjectDialog`, nhưng dùng
 * chung được cho xóa sprint, gỡ thành viên, rời dự án, xóa task.
 *
 * 🔴 Điểm quan trọng nhất: khi thất bại thì **giữ dialog mở** và hiện lỗi tại chỗ. Rất
 * nhiều lỗi ở đây là **409 nghiệp vụ hợp lệ** kèm câu tiếng Việt giải thích chính xác lý
 * do ("còn 3 task chưa hoàn thành", "không thể gỡ quản lý dự án cuối cùng"). Đóng dialog
 * rồi bắn toast là ném đi đúng câu người dùng cần đọc để biết làm gì tiếp.
 *
 * Dùng `Dialog` chứ không phải `AlertDialog`: `AlertDialog` khóa việc đóng bằng Esc /
 * bấm ra ngoài, mà ở đây sau khi đọc thông báo 409 người dùng cần thoát ra dễ dàng.
 */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  pendingLabel,
  error,
  isPending = false,
  variant = 'destructive',
  onConfirm,
  onClose,
}: {
  open: boolean;
  title: string;
  description: React.ReactNode;
  confirmLabel: string;
  pendingLabel?: string;
  /** Thông điệp lỗi của lần thử gần nhất, `null` khi chưa có. */
  error: string | null;
  isPending?: boolean;
  variant?: 'destructive' | 'default';
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <FormError message={error} />

        <DialogFooter>
          <DialogClose render={<Button type="button" variant="outline" />}>Hủy</DialogClose>
          <Button type="button" variant={variant} onClick={onConfirm} disabled={isPending}>
            {isPending ? (pendingLabel ?? 'Đang xử lý…') : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

'use client';

import { useRouter } from 'next/navigation';

import { TaskDetailContent } from '@/components/tasks/task-detail-content';
import { Dialog, DialogContent } from '@/components/ui/dialog';

/**
 * Vỏ dialog cho chi tiết task — chỉ được mount qua route chặn
 * `@modal/(.)tasks/[taskId]`, tức là chỉ khi người dùng điều hướng từ trong ứng dụng.
 *
 * `open` truyền cứng `true`, KHÔNG dùng state cục bộ: trạng thái mở/đóng của dialog này
 * chính là URL. Đóng = `router.back()` → URL quay về board → slot `@modal` không còn khớp
 * → `default.tsx` trả `null` → dialog unmount. Nhờ vậy nút Back của trình duyệt, phím
 * Escape và bấm ra nền đều đi chung một đường, không cần đồng bộ gì thêm.
 */
export function TaskDetailModal({
  projectId,
  taskId,
}: {
  projectId: string;
  taskId: string;
}) {
  const router = useRouter();
  const close = () => router.back();

  return (
    <Dialog
      open
      onOpenChange={(next) => {
        if (!next) close();
      }}
    >
      {/*
        Ghi đè `sm:max-w-sm` mặc định TẠI ĐÂY chứ không sửa `components/ui/dialog.tsx` —
        file đó do shadcn sinh và sẽ bị ghi đè ở lần thêm component sau.

        `max-h-[85svh] overflow-y-auto` biến chính popup thành vùng cuộn, đó là mốc mà cột
        phải `sticky top-0` của `TaskDetailContent` bám vào. `svh` chứ không phải `vh`:
        trên trình duyệt di động, `vh` tính cả phần bị thanh địa chỉ che.
      */}
      <DialogContent
        showCloseButton={false}
        className="max-h-[85svh] overflow-y-auto p-5 sm:max-w-5xl"
      >
        <TaskDetailContent
          projectId={projectId}
          taskId={taskId}
          variant="modal"
          onRequestClose={close}
        />
      </DialogContent>
    </Dialog>
  );
}

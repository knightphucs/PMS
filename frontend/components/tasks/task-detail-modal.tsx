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

        🔴 `overflow-x-hidden` là CHỐT CHẶN CUỐI của một lớp lỗi bố cục, không phải trang trí.
        `DialogContent` là `display:grid`, mà grid item mặc định có `min-width:auto` — nghĩa
        là một chuỗi dài không ngắt được (URL dán vào mô tả, tên file đính kèm, tên task
        không có dấu cách) làm PHỒNG chính cái track, đẩy nội dung tràn ra ngoài popup. Cách
        sửa gốc là `min-w-0` + `break-words` ở từng chỗ hiển thị văn bản tự do (đã làm ở
        TaskDetailContent / TaskDescription / TaskComments / TaskDetailHeader); dòng này chặn
        nốt chỗ nào bị bỏ sót sau này.

        `sm:max-w-5xl` (64rem) dành đủ chỗ cho hai cột khi xem task trên màn hình desktop.
        Cột mô tả vẫn được giới hạn bởi layout nội dung, còn sidebar rộng hơn nên tên người
        đảm nhận và vai trò không bị ép tràn. Nhớ sửa `task-detail-skeleton.tsx` cùng lúc.
      */}
      <DialogContent
        showCloseButton={false}
        className="max-h-[85svh] overflow-x-hidden overflow-y-auto p-5 sm:max-w-5xl"
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

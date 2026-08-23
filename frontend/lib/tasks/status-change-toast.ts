import { toast } from 'sonner';

import { errorMessage, isApprovalRequested } from '@/lib/api/problem';

/**
 * Báo kết quả của một lần đổi cột thất bại — dùng chung cho **cả ba** bề mặt kéo/chọn cột
 * (board kéo–thả, ô chọn ở chi tiết task, bảng ở màn Danh sách).
 *
 * 🔴 Tồn tại vì một trong các "lỗi" ở đây **không phải lỗi**. Khi task chạm một cổng duyệt
 * lần đầu (ADR-062), backend trả 409 nhưng đã kịp **gửi yêu cầu duyệt giúp người dùng** và
 * báo cho những người ký. Hiện nó thành toast đỏ là nói dối về thứ vừa xảy ra — và hậu quả
 * cụ thể là người dùng kéo lại lần nữa vì tưởng chưa có gì.
 *
 * Ba bản chép tay ở ba bề mặt thì chắc chắn có lúc trôi khỏi nhau (ADR-034); một trong ba
 * sẽ giữ nguyên toast đỏ và không ai để ý.
 */
export function notifyStatusChangeFailure(error: unknown, targetName?: string) {
  const message = errorMessage(error);

  if (isApprovalRequested(error)) {
    // Không phải `toast.success` — task KHÔNG di chuyển, và nói "thành công" cũng sai y như
    // nói "lỗi". Toast trung tính là thứ đúng: có việc đã xảy ra, và nó không phải việc bạn
    // vừa yêu cầu.
    toast.info(message, { duration: 6000 });
    return;
  }

  toast.error(targetName ? `Không chuyển được sang "${targetName}": ${message}` : message);
}

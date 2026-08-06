import type { StatusCategory } from '@/types/task';

/**
 * Luật chuyển cột phía client (ADR-052 thay thế ADR-021).
 *
 * 🗑️ **`ALLOWED_TRANSITIONS` và `canTransition` đã bị GỠ.** Chúng là bản sao của
 * `TaskItem.CanTransitionTo`, mà method đó không còn tồn tại: khi cột do NGƯỜI DÙNG tạo,
 * hệ thống không còn cơ sở nào để nói cặp nào hợp lệ — nó không biết "Chờ QA" đứng trước
 * hay sau "Đang sửa".
 *
 * Hai hệ quả cho UI, cả hai đều nới lỏng:
 *
 * - **Mọi cột đều là đích hợp lệ.** Không còn `useDroppable disabled` cho cột "không kề" —
 *   trước đây board dùng chính nó để chặn 409 bằng cấu trúc.
 * - **Thả về đúng cột đang đứng nay trả 200**, không còn 409. UI vẫn nên chặn sớm để khỏi
 *   bắn một request không làm gì, nhưng nếu lọt thì cũng không hỏng.
 *
 * ⚠️ Đừng dựng lại một bảng luật ở client "cho chắc": nó sẽ chặn đúng những nước đi mà
 * backend cho phép, và người dùng không có cách nào biết vì sao.
 */

/**
 * ⚠️ Chuyển cột **có thể vẫn 409** dù client không đoán trước được.
 *
 * Còn đúng một trường hợp: task đang bị một `TaskLink` chặn bởi task khác chưa xong.
 * Đã đối chiếu `TaskStatusTransitionService`: nó gọi `EnsureNotBlockedAsync` khi **NHÓM**
 * của cột đích là `InProgress`.
 *
 * 📌 Đổi so với trước ADR-052: điều kiện là `category`, không phải một tên cột cụ thể. Nhờ
 * vậy một cột người dùng tự đặt tên "Chờ QA" thuộc nhóm InProgress cũng được tính đúng —
 * so tên thì sẽ trượt ngay khi ai đó đổi cấu hình board.
 *
 * Dùng cờ này để quyết định chỗ nào cần chuẩn bị sẵn đường lùi (rollback + toast).
 */
export function mayFailUnpredictably(targetCategory: StatusCategory): boolean {
  return targetCategory === 'InProgress';
}

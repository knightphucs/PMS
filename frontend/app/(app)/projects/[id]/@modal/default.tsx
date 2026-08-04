/**
 * Trạng thái "không có gì" của slot song song `@modal`.
 *
 * 🔴 File này BẮT BUỘC phải tồn tại. Với mọi URL dưới `projects/[id]` mà slot `@modal`
 * không khớp (tức là gần như mọi URL: board, backlog, sprint, thành viên), Next đi tìm
 * đúng file `@modal/default`. Không có nó, loader rơi về `PARALLEL_ROUTE_DEFAULT_PATH` và
 * gọi `notFound()` — kết quả là **404 cho cả trang**, không phải một slot rỗng. Hỏng theo
 * kiểu rất khó truy: board tự nhiên 404 vì một file *không* được tạo ở chỗ khác.
 *
 * Không phải server component có chủ đích — nó không render gì nên `'use client'` chỉ là
 * chi phí thừa (cùng lý do với `[id]/page.tsx`).
 */
export default function ModalSlotDefault() {
  return null;
}

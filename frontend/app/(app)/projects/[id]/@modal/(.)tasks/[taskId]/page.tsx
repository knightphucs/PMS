'use client';

import { useParams } from 'next/navigation';

import { TaskDetailModal } from '@/components/tasks/task-detail-modal';

/**
 * Route CHẶN (intercepting route) — bấm một thẻ task từ board/backlog thì URL đổi thành
 * `/projects/{id}/tasks/{taskId}` nhưng nội dung hiện ra trong một dialog đè lên board,
 * thay vì thay cả trang. Đúng hành vi của Jira.
 *
 * 🔑 Tiền tố là `(.)`, KHÔNG phải `(..)`. Đã đối chiếu
 * `next/dist/shared/lib/router/utils/interception-routes.js` của chính bản 15.5.22 đang
 * cài: `normalizeAppPath` bỏ **cả** segment nhóm `(…)` **lẫn** segment slot `@…`, nên
 * `projects/[id]/@modal/` chuẩn hóa về `projects/[id]` và `(.)tasks/[taskId]` ghép ra
 * đúng `projects/[id]/tasks/[taskId]`. Dùng `(..)` sẽ trỏ nhầm sang `projects/tasks/…`.
 *
 * Chặn CHỈ áp dụng cho điều hướng phía client. Tải cứng / F5 / mở tab mới đi thẳng vào
 * `tasks/[taskId]/page.tsx` — đó là lý do modal không cần tự xử lý trạng thái "mở bằng URL".
 */
export default function InterceptedTaskDetailPage() {
  const { id, taskId } = useParams<{ id: string; taskId: string }>();

  return <TaskDetailModal projectId={id} taskId={taskId} />;
}

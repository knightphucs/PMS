'use client';

import { useParams } from 'next/navigation';

import { TaskDetailContent } from '@/components/tasks/task-detail-content';

/**
 * Chi tiết task dưới dạng TRANG THẬT.
 *
 * Đây là thứ người dùng nhận khi mở link được chia sẻ, khi F5, hoặc khi mở URL trong tab
 * mới — mọi lối vào KHÔNG phải điều hướng phía client. Bấm một thẻ trên board thì
 * `@modal/(.)tasks/[taskId]` chặn trước và hiện dialog thay vì trang này; cả hai render
 * chung một `TaskDetailContent` nên không có chuyện hai vỏ lệch nội dung.
 *
 * Trang nằm dưới `[id]/layout.tsx` nên vẫn giữ được tiêu đề project; layout tự ẩn thanh
 * tab khi segment là `tasks` (task không phải tab thứ năm).
 */
export default function TaskDetailPage() {
  const { id, taskId } = useParams<{ id: string; taskId: string }>();

  return <TaskDetailContent projectId={id} taskId={taskId} variant="page" />;
}

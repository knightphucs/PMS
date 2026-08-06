'use client';

import { QueryError } from '@/components/common/query-error';
import { WarningBanner } from '@/components/common/warning-banner';
import { TaskAttachments } from '@/components/tasks/task-attachments';
import { TaskDescription } from '@/components/tasks/task-description';
import { TaskDetailHeader } from '@/components/tasks/task-detail-header';
import { TaskDetailSkeleton } from '@/components/tasks/task-detail-skeleton';
import { TaskDiscussion } from '@/components/tasks/task-discussion';
import { TaskLinks } from '@/components/tasks/task-links';
import { TaskSidebar } from '@/components/tasks/task-sidebar';
import { TaskSubtasks } from '@/components/tasks/task-subtasks';
import { useTaskFieldSave } from '@/components/tasks/use-task-field-save';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { useTask } from '@/lib/hooks/use-tasks';
import { canCreateSubtask, canManageTaskLinks, canManageTasks } from '@/lib/tasks/permissions';
import { cn } from '@/lib/utils';

/**
 * Nội dung chi tiết task — thân chung của CẢ HAI vỏ.
 *
 * Trang thật (`tasks/[taskId]/page.tsx`) và dialog chặn (`@modal/(.)tasks/[taskId]`) đều
 * render đúng component này, nên không có chuyện hai lối vào cùng một task lại hiện hai
 * thứ khác nhau. Hai vỏ không bao giờ mount cùng lúc: khi dialog đang mở, slot `children`
 * vẫn giữ board; khi tải cứng, slot `@modal` rơi về `default.tsx` → `null`.
 *
 * `variant` chỉ đổi bố cục (nơi cuộn và bề rộng cột phải), không đổi hành vi.
 */
export function TaskDetailContent({
  projectId,
  taskId,
  variant,
  onRequestClose,
}: {
  projectId: string;
  taskId: string;
  variant: 'page' | 'modal';
  onRequestClose?: () => void;
}) {
  const detail = useTask(projectId, taskId);
  const { role, myEmployeeId } = useMyProjectRole(projectId);
  const { save, isStale, isBusy } = useTaskFieldSave(projectId, taskId, detail);

  if (detail.isError) {
    return (
      <QueryError
        // ⚠️ KHÔNG viết "task không tồn tại": người ngoài project cũng nhận 404 một cách
        // CỐ Ý (ADR-006/019), nên 404 ở đây không phân biệt được "không có" với "không
        // được xem". `QueryError` hiện nguyên văn thông điệp của backend.
        title="Không mở được task"
        error={detail.error}
        onRetry={() => void detail.refetch()}
        isRetrying={detail.isFetching}
      />
    );
  }

  if (detail.isPending) return <TaskDetailSkeleton variant={variant} />;

  const task = detail.data;
  const canEdit = canManageTasks(role);

  return (
    // `min-w-0`: đây là grid item của `DialogContent` (cũng là grid). Không có nó thì
    // `min-width:auto` mặc định cho phép nội dung phồng track và tràn ra ngoài popup.
    <div className="grid min-w-0 gap-4">
      <TaskDetailHeader
        projectId={projectId}
        task={task}
        variant={variant}
        canEdit={canEdit}
        isBusy={isBusy}
        onSaveName={(name) => save({ name })}
        onRequestClose={onRequestClose}
      />

      {isStale ? (
        <WarningBanner title="Người khác vừa sửa task này.">
          Dữ liệu trên màn hình đã được tải lại theo bản mới nhất. Kiểm tra lại rồi thực
          hiện thay đổi của bạn một lần nữa nếu vẫn cần.
        </WarningBanner>
      ) : null}

      <div
        className={cn(
          'grid gap-6',
          variant === 'page'
            ? 'lg:grid-cols-[minmax(0,1fr)_24rem]'
            : 'lg:grid-cols-[minmax(0,1fr)_22rem]',
        )}
      >
        {/*
          Cột trái. `minmax(0,1fr)` phía trên chỉ ghìm được TRACK; các con của grid này lại
          có `min-width:auto` của riêng chúng, nên vẫn cần `min-w-0` ở đây.
        */}
        <div className="grid min-w-0 gap-6">
          <TaskDescription
            description={task.description}
            canEdit={canEdit}
            isBusy={isBusy}
            onSave={(description) => save({ description })}
          />

          <TaskSubtasks
            projectId={projectId}
            parentTask={task}
            canManage={canCreateSubtask(
              role,
              task.assignees.some((assignee) => assignee.employeeId === myEmployeeId),
            )}
          />

          <TaskAttachments
            projectId={projectId}
            taskId={taskId}
            role={role}
            myEmployeeId={myEmployeeId}
          />

          <TaskLinks
            projectId={projectId}
            taskId={taskId}
            canManage={canManageTaskLinks(role)}
          />

          {/* Trong DIALOG, thảo luận nằm trong cột trái như mọi khối khác — popup chỉ rộng
              64rem nên tách ra cũng chẳng thêm được chỗ nào. Ở TRANG thì nó xuống dưới,
              chiếm trọn bề ngang (xem khối sau `</div>` của lưới hai cột). */}
          {variant === 'modal' ? (
            <TaskDiscussion
              projectId={projectId}
              taskId={taskId}
              role={role}
              myEmployeeId={myEmployeeId}
            />
          ) : null}
        </div>

        {/*
          Cột phải DÍNH. Ở trang thật, thanh header của `AppShell` cao 56px và `sticky
          top-0`, nên phải trừ ra để cột không chui xuống dưới nó. Trong dialog thì vùng
          cuộn là chính `DialogContent`, nên mốc là 0.
        */}
        <div
          className={cn(
            'self-start',
            variant === 'page' ? 'lg:sticky lg:top-[4.5rem]' : 'lg:sticky lg:top-0',
          )}
        >
          <TaskSidebar
            projectId={projectId}
            task={task}
            role={role}
            myEmployeeId={myEmployeeId}
            isBusy={isBusy}
            onSaveField={save}
          />
        </div>
      </div>

      {/*
        🔑 Khác biệt CẤU TRÚC giữa hai vỏ, không phải trang trí.

        Bình luận và lịch sử là khối rộng nhất của màn này — mỗi dòng có avatar, tên, mốc
        thời gian rồi mới tới nội dung. Trong dialog, nó chỉ được ~40rem (64rem trừ cột phải
        và khe), nên câu ngắn cũng xuống dòng ba lần.

        Ở trang thật, sau khi hai cột kết thúc, khối này lấy TRỌN bề ngang — trên màn 1440px
        là hơn gấp đôi chỗ trong dialog. Đây chính là thứ nút "Mở trang riêng" hứa: cùng một
        task, nhưng phần đọc-và-viết nhiều nhất được thở.

        Vì sao đặt ở đây mà không phải trong cột trái với `lg:col-span-2`: cột phải là
        `sticky`, cho khối này nằm cùng lưới sẽ kéo dài track và làm mốc dính nhảy khi đoạn
        bình luận dài ra.
      */}
      {variant === 'page' ? (
        <div className="min-w-0">
          <TaskDiscussion
            projectId={projectId}
            taskId={taskId}
            role={role}
            myEmployeeId={myEmployeeId}
          />
        </div>
      ) : null}
    </div>
  );
}

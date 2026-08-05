/** Soi gương `MyWorkResponse` trong `PMS.Application/Features/Tasks/TaskDtos.cs`. */

import type { TaskSummaryResponse } from './task';

/**
 * Việc của người đang đăng nhập, gom theo dự án (ADR-053).
 *
 * Bộ lọc do SERVER áp: được gán cho tôi · chưa thuộc cột nhóm `Done` · có hạn ≤ hôm nay.
 * "≤" chứ không "=" là cố ý — việc trễ hạn phải nổi lên cùng việc hôm nay, giấu nó đi là
 * đúng cách để nó bị quên tiếp.
 */
export interface MyWorkResponse {
  /**
   * Mốc "hôm nay" mà SERVER dùng để lọc, ISO 8601 UTC.
   *
   * ⚠️ Hiện lại mốc này thay vì để client tự tính `new Date()`: hai bên ở hai múi giờ thì
   * "hôm nay" khác nhau, và người dùng cần biết phạm vi mình đang xem là gì (ADR-046b).
   */
  today: string;
  totalTasks: number;
  overdueTasks: number;
  groups: MyWorkGroup[];
}

export interface MyWorkGroup {
  projectId: string;
  projectName: string;
  /** Mã ngắn của project (`PMS`) — nửa đầu của `task.code`. */
  projectKey: string;
  tasks: TaskSummaryResponse[];
}

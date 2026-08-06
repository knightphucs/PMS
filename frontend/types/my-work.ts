/** Soi gương `MyWorkResponse` trong `PMS.Application/Features/Tasks/TaskDtos.cs`. */

import type { TaskSummaryResponse } from './task';

/**
 * Việc của người đang đăng nhập, gom theo dự án (ADR-053).
 *
 * Bộ lọc do SERVER áp: được gán cho tôi · chưa thuộc cột nhóm `Done`. Bao gồm task quá hạn,
 * hôm nay, tương lai và chưa đặt hạn; danh sách được xếp hạn gần trước, không hạn ở cuối.
 */
export interface MyWorkResponse {
  /** Mốc "hôm nay" của server, ISO 8601 UTC, để client hiển thị nhất quán (ADR-046b). */
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

/** Soi gương `PMS.Application/Features/Reports/ReportsDtos.cs`. */

import type { PriorityCount } from './statistics';
import type { SprintStatus } from './sprint';

/**
 * Backlog insight — nhóm báo cáo kiểu Jira. `byPriority` cùng kiểu với Thống kê
 * (`PriorityCount`) nhưng đếm tập task KHÁC: đây là "còn mở" (chưa ở cột nhóm Done), Thống
 * kê là toàn bộ.
 */
export interface BacklogInsightResponse {
  projectId: string;
  totalOpen: number;
  overdue: number;
  dueSoon: number;
  noDueDate: number;
  /** Luôn đủ 5 phần tử — server zero-fill, giống `ProjectStatisticsResponse.byPriority`. */
  byPriority: PriorityCount[];
}

/** Một điểm trên biểu đồ velocity — một sprint đã đóng sổ. */
export interface SprintVelocityPoint {
  sprintId: string;
  name: string;
  completedAt: string;
  doneCount: number;
  totalCount: number;
}

/**
 * Velocity — CHỈ sprint đã `Completed` xuất hiện ở đây (khác `SprintProgress` của Thống kê,
 * vốn liệt kê MỌI sprint). `averageVelocity` là 0 khi chưa có sprint nào đóng, không phải
 * giá trị thiếu.
 */
export interface VelocityResponse {
  projectId: string;
  sprints: SprintVelocityPoint[];
  averageVelocity: number;
}

/**
 * Một sprint trên trục thời gian — MỌI vòng đời có mặt (khác `SprintVelocityPoint`, chỉ
 * sprint đã đóng). `completedAt` là mốc đóng sổ thật, `null` nếu chưa đóng — dùng `status`
 * để tô màu, không suy diễn "đang chạy" từ ngày.
 */
export interface SprintTimelinePoint {
  sprintId: string;
  name: string;
  status: SprintStatus;
  startDate: string;
  endDate: string;
  completedAt: string | null;
  total: number;
  done: number;
  /**
   * Quá hạn THẬT — `status === 'Active'` và đã qua `endDate` mà chưa đóng sổ. KHÔNG suy
   * được bằng cách so `startDate`/`endDate` ở client: một sprint được bấm chạy SỚM hơn kế
   * hoạch cũng "chưa tới ngày" giống hệt bề ngoài nhưng không hề quá hạn — xem
   * `SprintResponse.isOverdue` (cùng field, cùng công thức, khác chỗ đứng).
   */
  isOverdue: boolean;
}

/** Timeline kiểu Jira roadmap — mọi sprint của project, đã sắp theo ngày bắt đầu. */
export interface TimelineResponse {
  projectId: string;
  sprints: SprintTimelinePoint[];
}

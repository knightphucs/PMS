/** Soi gương `PMS.Application/Features/Statistics/StatisticsDtos.cs`. */

import type { Priority, Status } from './enums';

export interface StatusCount {
  status: Status;
  count: number;
}

export interface PriorityCount {
  priority: Priority;
  count: number;
}

export interface AssigneeWorkload {
  employeeId: string;
  employeeName: string;
  total: number;
  done: number;
  overdue: number;
}

export interface SprintProgress {
  sprintId: string;
  name: string;
  isActive: boolean;
  startDate: string;
  endDate: string;
  total: number;
  done: number;
}

export interface ProjectStatisticsResponse {
  projectId: string;
  /** Gồm **cả subtask** — subtask là công việc thật (§5), board loại nó ra chỉ vì hiển thị. */
  totalTasks: number;
  overdueTasks: number;
  /** Phần trăm **0–100**, làm tròn 2 chữ số. `0` khi project chưa có task nào. */
  completionRate: number;
  /**
   * ⚠️ Backend LUÔN trả đủ **4** phần tử theo thứ tự khai báo enum, kể cả trạng thái không
   * có task nào — Recharts không phải tự bịa ra hạng mục thiếu.
   */
  byStatus: StatusCount[];
  /** Luôn đủ **5** phần tử, cùng lý do với `byStatus`. */
  byPriority: PriorityCount[];
  /** Chỉ gồm người thực sự được gán ít nhất một task; đã sắp giảm dần theo `total`. */
  byAssignee: AssigneeWorkload[];
  sprints: SprintProgress[];
}

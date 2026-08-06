/** Soi gương `PMS.Application/Features/Statistics/StatisticsDtos.cs`. */

import type { Priority } from './enums';
import type { StatusCategory } from './task';

/**
 * Một cột trên biểu đồ phân bố trạng thái.
 *
 * ⚠️ Sau ADR-052 đây là MỘT CỘT BOARD chứ không phải một giá trị enum: nó mang `columnId`,
 * tên và màu do người dùng đặt. Số phần tử vì vậy **thay đổi theo cấu hình từng project**.
 */
export interface StatusCount {
  columnId: string;
  name: string;
  /** `#RRGGBB` của chính cột — biểu đồ dùng nó để khớp màu với board. */
  color: string;
  order: number;
  category: StatusCategory;
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
   * ⚠️ Backend LUÔN trả đủ **mọi cột của project** theo `order`, kể cả cột không có task
   * nào — Recharts không phải tự bịa ra hạng mục thiếu.
   *
   * 📌 Số phần tử **không cố định 4** kể từ ADR-052. Đừng viết code dựa trên độ dài mảng.
   */
  byStatus: StatusCount[];
  /** Luôn đủ **5** phần tử, cùng lý do với `byStatus`. */
  byPriority: PriorityCount[];
  /** Chỉ gồm người thực sự được gán ít nhất một task; đã sắp giảm dần theo `total`. */
  byAssignee: AssigneeWorkload[];
  sprints: SprintProgress[];
}

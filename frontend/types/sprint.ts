/** Soi gương `PMS.Application/Features/Sprints/SprintDtos.cs`. */

export interface SprintResponse {
  id: string;
  projectId: string;
  name: string;
  /** Mục tiêu sprint. Backend mặc định `""`, không bao giờ `null`. */
  goal: string;
  startDate: string;
  endDate: string;
  /**
   * Tính sẵn phía server: `today ∈ [startDate, endDate]` theo UTC.
   *
   * ⚠️ KHÔNG phải "sprint duy nhất đang chạy" — hai sprint gối ngày nhau thì cả hai đều
   * `isActive`, và một sprint có thể active mà chưa có task nào.
   */
  isActive: boolean;
  /** Số task thuộc sprint. Bằng 0 ở phản hồi vừa TẠO (sprint mới chưa có task). */
  taskCount: number;
}

export interface CreateSprintRequest {
  name: string;
  goal: string;
  startDate: string;
  endDate: string;
}

/**
 * ⚠️ Sprint KHÔNG có `rowVersion` — không có optimistic concurrency.
 *
 * Hai người sửa cùng lúc là last-write-wins, không có tín hiệu nào phát ra. Nên đừng
 * dựng UI cảnh báo "dữ liệu đã cũ" cho sprint: không có gì để phát hiện cả.
 */
export type UpdateSprintRequest = CreateSprintRequest;

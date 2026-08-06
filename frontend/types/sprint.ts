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
  /**
   * Vòng đời do NGƯỜI DÙNG điều khiển (ADR-050).
   *
   * 🔴 **Khác hẳn `isActive`.** `isActive` suy từ NGÀY; trường này nói đội đã bấm bắt đầu
   * chưa và đã chốt sổ chưa. Hai thứ tách nhau trong thực tế, và chỗ chúng lệch nhau chính
   * là tín hiệu đáng hiện: `status === 'Active'` mà `isActive === false` nghĩa là sprint đã
   * quá hạn mà chưa ai đóng.
   */
  status: SprintStatus;
  /** ISO 8601 UTC, `null` khi chưa đóng. Mốc velocity đo theo. */
  completedAt: string | null;
  /** Số task trong sprint đã thuộc cột nhóm `Done` — nuôi thanh tiến độ. */
  doneCount: number;
}

export type SprintStatus = 'Planned' | 'Active' | 'Completed';

/**
 * Đóng sprint (ADR-050).
 *
 * 🔴 `targetSprintId: null` nghĩa là **đẩy về Backlog** — một lựa chọn hợp lệ, KHÔNG phải
 * "chưa chọn". Trường luôn phải có mặt trong body.
 */
export interface CompleteSprintRequest {
  targetSprintId: string | null;
}

/** Xem trước việc đóng sprint — nuôi dialog hỏi task chưa xong đi đâu. */
export interface SprintCompletionPreview {
  sprintId: string;
  sprintName: string;
  doneCount: number;
  unfinishedCount: number;
  /** Chỉ sprint CHƯA đóng và khác chính nó. Rỗng = chỉ còn đường đẩy về Backlog. */
  availableTargets: SprintOption[];
}

export interface SprintOption {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
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

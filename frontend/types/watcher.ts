/** Soi gương `PMS.Application/Features/Watchers/WatcherDtos.cs`. */

export interface WatcherResponse {
  employeeId: string;
  employeeName: string;
  createdAt: string;
}

/**
 * Trả về sau khi watch/unwatch — đủ để cập nhật nút và số đếm trong một lượt, không phải
 * gọi thêm `GET /watchers`.
 *
 * Cả hai endpoint đều **idempotent**: bấm Theo dõi hai lần trả 200 với cùng trạng thái, chứ
 * không phải 409. UI không cần dò trạng thái trước khi gọi.
 */
export interface WatchStateResponse {
  isWatching: boolean;
  watcherCount: number;
}

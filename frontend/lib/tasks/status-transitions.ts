import type { Status } from '@/types/enums';

/**
 * Bản sao của `TaskItem.CanTransitionTo` (PMS.Domain/Entities/TaskItem.cs:77).
 *
 * ⚠️ ĐÂY KHÔNG PHẢI QUY TẮC "CỘT KỀ". `docs/frontend-next-session.md` §6 ghi
 * "chỉ cho thả sang cột kề" — SAI, và làm theo là ship bug. Đối chiếu state machine thật:
 *
 *   • `Done → Review` và `Review → InProgress` là bước LÙI và HỢP LỆ (task bị reject).
 *   • `ToDo → Review` trông như cột kề nhưng KHÔNG hợp lệ.
 *   • `ToDo → Done` và `InProgress → Done` cũng không.
 *
 * Khai bằng `Record<Status, …>` để thêm một giá trị vào enum `Status` là lỗi BIÊN DỊCH
 * tại đây, không phải một cột lặng lẽ không thả được vào.
 */
export const ALLOWED_TRANSITIONS: Record<Status, readonly Status[]> = {
  ToDo: ['InProgress'],
  InProgress: ['ToDo', 'Review'],
  Review: ['InProgress', 'Done'],
  Done: ['Review'],
};

/**
 * Có được chuyển từ `from` sang `to` không.
 *
 * `from === to` luôn false: state machine phía backend từ chối cả việc "đứng yên", nên
 * thả thẻ về ĐÚNG CỘT NÓ ĐANG ĐỨNG cũng nhận 409. UI phải chặn trước chứ không được bắn
 * request rồi hiện toast đỏ khi người dùng đổi ý giữa chừng.
 */
export function canTransition(from: Status, to: Status): boolean {
  return from !== to && ALLOWED_TRANSITIONS[from].includes(to);
}

/**
 * ⚠️ `canTransition` trả `true` KHÔNG bảo đảm request sẽ thành công.
 *
 * Còn đúng một trường hợp 409 mà client không đoán trước được: task đang bị một
 * `TaskLink` loại `IsBlockedBy` chặn bởi task khác chưa `Done`. Đã đối chiếu
 * `TaskStatusTransitionService`: nó CHỈ gọi `EnsureNotBlockedAsync` khi đích là
 * `InProgress`, nên mọi cột khác đều đoán được trọn vẹn.
 *
 * Dùng cờ này để quyết định chỗ nào cần chuẩn bị sẵn đường lùi (rollback + toast).
 */
export function mayFailUnpredictably(to: Status): boolean {
  return to === 'InProgress';
}

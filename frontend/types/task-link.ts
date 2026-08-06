/** Soi gương `PMS.Application/Features/TaskLinks/TaskLinkDtos.cs`. */

import type { LinkType } from './enums';
import type { TaskStatusRef } from './task';

/**
 * Một liên kết nhìn từ phía task đang mở.
 *
 * ⚠️ `linkType` ở đây là **hướng đã diễn giải cho người xem**, không phải giá trị thô trong
 * DB: cùng một hàng `Blocks(A,B)` trả về `Blocks` khi hỏi từ A và `IsBlockedBy` khi hỏi từ B
 * (ADR-038). Đừng ngạc nhiên khi thấy `IsBlockedBy` ở đây mà không bao giờ thấy nó trong DB.
 */
export interface TaskLinkResponse {
  id: string;
  linkType: LinkType;
  /** Task ở ĐẦU KIA của liên kết, không phải task đang mở. */
  relatedTaskId: string;
  /** Mã `PMS-12` của task đối diện, backend đã ghép sẵn. */
  relatedTaskCode: string;
  relatedTaskName: string;
  /** Cột hiện tại của task liên quan (ADR-052), không còn là enum cố định. */
  relatedTaskStatus: TaskStatusRef;
}

/**
 * ⚠️ Gửi `IsBlockedBy` là hợp lệ và tiện (UI có thể cho chọn "task này bị chặn bởi..."),
 * nhưng backend sẽ **chuẩn hóa** nó thành `Blocks` đảo chiều trước khi lưu. Hệ quả thực tế:
 * tạo `Blocks(A→B)` rồi tạo `IsBlockedBy(B→A)` sẽ nhận **409** vì đó là cùng một sự thật.
 */
export interface CreateTaskLinkRequest {
  targetTaskId: string;
  linkType: LinkType;
}

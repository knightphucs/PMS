import type {
  BoardColumnResponse,
  CreateBoardColumnRequest,
  DeleteBoardColumnRequest,
  ReorderBoardColumnsRequest,
  UpdateBoardColumnRequest,
} from '@/types/task';

import { apiFetch } from '../http';

/**
 * Cấu hình cột board của một project (ADR-052).
 *
 * Đọc mở cho mọi thành viên; mọi thao tác GHI đều PM-only. Người ngoài project nhận **404**
 * chứ không phải 403 (ADR-019).
 */
export function listBoardColumns(projectId: string, signal?: AbortSignal) {
  return apiFetch<BoardColumnResponse[]>(`/projects/${projectId}/columns`, { signal });
}

/** **409** khi trùng tên cột trong cùng project · **400** khi màu sai định dạng `#RRGGBB`. */
export function createBoardColumn(projectId: string, body: CreateBoardColumnRequest) {
  return apiFetch<BoardColumnResponse>(`/projects/${projectId}/columns`, {
    method: 'POST',
    body,
  });
}

/**
 * ⚠️ Đổi `category` sẽ kéo theo việc backend đồng bộ lại nhóm cho MỌI task trong cột. Đó là
 * thao tác có thể chạm hàng nghìn dòng — UI nên nói rõ trước khi lưu.
 */
export function updateBoardColumn(columnId: string, body: UpdateBoardColumnRequest) {
  return apiFetch<BoardColumnResponse>(`/columns/${columnId}`, { method: 'PUT', body });
}

/**
 * Xóa cột.
 *
 * ⚠️ **DELETE có thân request** — khác thường nhưng cố ý: cột còn task thì bắt buộc kèm
 * `targetColumnId`. Đưa lên query string sẽ khiến một thao tác phá hủy phụ thuộc vào chuỗi
 * URL, thứ dễ sao chép nhầm và nằm lại trong log máy chủ.
 *
 * **400** khi còn task mà không chọn đích (thông điệp có kèm số task) · **409** khi đó là
 * cột cuối cùng · **404** khi cột đích không thuộc project.
 */
export function deleteBoardColumn(columnId: string, body: DeleteBoardColumnRequest) {
  return apiFetch<void>(`/columns/${columnId}`, { method: 'DELETE', body });
}

/** Gửi TRỌN danh sách theo thứ tự mới — server từ chối danh sách thiếu hoặc trùng. */
export function reorderBoardColumns(projectId: string, body: ReorderBoardColumnsRequest) {
  return apiFetch<BoardColumnResponse[]>(`/projects/${projectId}/columns/order`, {
    method: 'PUT',
    body,
  });
}

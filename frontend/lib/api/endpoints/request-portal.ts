import type { PagedResult } from '@/types/common';
import type {
  MyRequestDetailResponse,
  MyRequestResponse,
  RequestPortalFormResponse,
  RequestPortalProjectResponse,
  SubmitRequestRequest,
} from '@/types/request-portal';

import { apiFetch } from '../http';

/**
 * Cổng yêu cầu (ADR-063).
 *
 * 🔴 **Không một endpoint nào ở đây nằm dưới `/projects/{id}`**, và đó là chủ đích: tiền tố
 * đó mang lời hứa ngầm "bạn là thành viên project này", còn người dùng cổng thì không.
 * Đặt chúng cùng chỗ sẽ khiến phiên sau gắn thêm một lượt kiểm quyền "cho nhất quán" và
 * phá đúng thứ ADR-063 dựng lên.
 */

/** Project đang mở cổng. Rỗng = chưa đội nào bật — UI phải TỰ ẨN lối vào (luật 3 Doctrine). */
export function listRequestPortals(signal?: AbortSignal) {
  return apiFetch<RequestPortalProjectResponse[]>('/request-portal/projects', { signal });
}

/** **404** khi project không tồn tại, đã xoá, **hoặc** chưa mở cổng — cùng một câu trả lời. */
export function getRequestForm(projectId: string, signal?: AbortSignal) {
  return apiFetch<RequestPortalFormResponse>(`/request-portal/projects/${projectId}/form`, {
    signal,
  });
}

/**
 * Gửi một yêu cầu.
 *
 * **400** thiếu trường bắt buộc của loại — điểm cưỡng chế `IsRequired` lúc TẠO, thứ
 * `POST /tasks` cố ý KHÔNG có (xem ADR-063 guard G2).
 * **404** loại không tồn tại, thuộc project khác, hoặc không nhận yêu cầu từ ngoài.
 */
export function submitRequest(projectId: string, body: SubmitRequestRequest) {
  return apiFetch<MyRequestResponse>(`/request-portal/projects/${projectId}/requests`, {
    method: 'POST',
    body,
  });
}

/** Yêu cầu do CHÍNH người gọi gửi, xuyên dự án, mới nhất trước. */
export function listMyRequests(page: number, pageSize: number, signal?: AbortSignal) {
  return apiFetch<PagedResult<MyRequestResponse>>(
    `/request-portal/requests?page=${page}&pageSize=${pageSize}`,
    { signal },
  );
}

/** ⚠️ **404 chứ không 403** khi yêu cầu thuộc người khác — 403 xác nhận id đó có tồn tại. */
export function getMyRequest(taskId: string, signal?: AbortSignal) {
  return apiFetch<MyRequestDetailResponse>(`/request-portal/requests/${taskId}`, { signal });
}

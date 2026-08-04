import type { AttachmentResponse } from '@/types/attachment';

import { API_BASE_URL } from '../config';
import { apiFetch } from '../http';

export function getTaskAttachments(taskId: string, signal?: AbortSignal) {
  return apiFetch<AttachmentResponse[]>(`/tasks/${taskId}/attachments`, { signal });
}

export function getProjectAttachments(projectId: string, signal?: AbortSignal) {
  return apiFetch<AttachmentResponse[]>(`/projects/${projectId}/attachments`, { signal });
}

/**
 * Tải file lên task.
 *
 * ⚠️ Tên trường **bắt buộc là `file`** — khớp tham số `IFormFile file` của controller. Đặt
 * tên khác thì model binder không tìm thấy gì và trả 400 với thông điệp không liên quan.
 *
 * `apiFetch` nhận `FormData` và **cố tình không đặt `Content-Type`** để trình duyệt tự sinh
 * `boundary`. Đừng tự thêm header đó.
 *
 * Mã lỗi cần phân biệt ở UI: **413** file quá lớn · **415** định dạng không hỗ trợ ·
 * **400** tên file có ý đồ / đuôi kép / nội dung không khớp đuôi đã khai (ADR-035).
 */
export function uploadTaskAttachment(taskId: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  return apiFetch<AttachmentResponse>(`/tasks/${taskId}/attachments`, {
    method: 'POST',
    body: form,
  });
}

export function uploadProjectAttachment(projectId: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  return apiFetch<AttachmentResponse>(`/projects/${projectId}/attachments`, {
    method: 'POST',
    body: form,
  });
}

/** Xóa — người tải lên HOẶC ProjectManager; người khác nhận 403 (khuôn ADR-026). */
export function deleteAttachment(attachmentId: string) {
  return apiFetch<void>(`/attachments/${attachmentId}`, { method: 'DELETE' });
}

/**
 * URL tải file về.
 *
 * 🔴 **KHÔNG dùng được với `<a href>` trực tiếp**: endpoint cần header
 * `Authorization: Bearer`, mà thẻ `<a>` không gắn được header. Access token cũng nằm trong
 * bộ nhớ chứ không phải cookie (ADR-027), nên trình duyệt không tự đính kèm gì cả.
 *
 * Cách đúng: gọi `downloadAttachment()` bên dưới rồi tạo object URL từ `Blob`.
 */
export function attachmentDownloadUrl(attachmentId: string): string {
  return `${API_BASE_URL}/attachments/${attachmentId}/download`;
}

/**
 * Tải nội dung file về dạng `Blob`.
 *
 * Không đi qua `apiFetch` vì hàm đó luôn `response.json()` — ở đây thân phản hồi là nhị
 * phân. Đổi lại phải tự làm phần gắn token. Chấp nhận một bản sao nhỏ thay vì thêm một
 * nhánh `responseType` vào `apiFetch` mà chỉ đúng một chỗ dùng tới.
 *
 * ⚠️ Không tự refresh khi 401 — nếu phiên vừa hết hạn thì gọi lại sau một thao tác bất kỳ
 * khác là được (thao tác đó sẽ đi qua single-flight refresh của `apiFetch`).
 */
export async function downloadAttachment(
  attachmentId: string,
  accessToken: string,
): Promise<Blob> {
  const response = await fetch(attachmentDownloadUrl(attachmentId), {
    headers: { Authorization: `Bearer ${accessToken}` },
    credentials: 'include',
  });

  if (!response.ok) throw new Error(`Tải file thất bại (HTTP ${response.status}).`);

  return await response.blob();
}

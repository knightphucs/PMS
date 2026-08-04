import { REFRESH_SKEW_MS } from '@/lib/api/config';
import { downloadAttachment } from '@/lib/api/endpoints/attachments';
import { refreshAccessToken } from '@/lib/api/refresh';
import { authStore } from '@/store/auth-store';

/**
 * Tải file đính kèm về máy.
 *
 * 🔴 Vì sao không phải một thẻ `<a href>`: endpoint download đòi header
 * `Authorization: Bearer`, mà `<a>` không gắn header được, và access token nằm trong bộ
 * nhớ chứ không phải cookie (ADR-027) nên trình duyệt cũng không tự đính kèm gì.
 *
 * `downloadAttachment` cố ý không tự refresh khi 401 (nó nằm ngoài `apiFetch`), nên phần
 * làm mới token chủ động nằm ở đây — dùng `refreshAccessToken()`, KHÔNG phải
 * `performRefresh()`: chỉ hàm public đó mới đi qua single-flight (ADR-030). Bỏ bước này
 * thì tải file ngay sau 15 phút không thao tác sẽ hỏng, còn mọi hành động khác vẫn chạy.
 */
export async function downloadAttachmentToDisk(
  attachmentId: string,
  fileName: string,
): Promise<void> {
  const { accessToken, accessTokenExpiresAt } = authStore.get();

  const needsRefresh =
    accessToken === null ||
    (accessTokenExpiresAt !== null && accessTokenExpiresAt - Date.now() < REFRESH_SKEW_MS);

  const token = needsRefresh ? await refreshAccessToken() : accessToken;

  const blob = await downloadAttachment(attachmentId, token);

  const url = URL.createObjectURL(blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    // `download` mới là thứ quyết định tên file trên đĩa: phản hồi luôn là
    // `application/octet-stream` + `Content-Disposition: attachment` (ADR-035), nhưng tên
    // gợi ý từ header không dùng được cho object URL.
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    // Thu hồi ngay sau khi click là an toàn — trình duyệt đã giữ tham chiếu tới blob cho
    // lượt tải đang chạy. Không thu hồi thì blob nằm lại trong bộ nhớ tới khi đóng tab.
    URL.revokeObjectURL(url);
  }
}

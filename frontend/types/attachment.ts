/** Soi gương `PMS.Application/Features/Attachments/AttachmentDtos.cs`. */

export interface AttachmentResponse {
  id: string;
  /** Tên gốc người dùng đã tải lên — chỉ để HIỂN THỊ, không dùng dựng đường dẫn. */
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploaderId: string;
  uploaderName: string;
  /** Đúng MỘT trong hai trường này khác `null` (CHECK constraint phía DB). */
  taskId: string | null;
  projectId: string | null;
  createdAt: string;
}

/**
 * Giới hạn và whitelist — **bản sao để hiển thị/chặn sớm ở UI, KHÔNG phải nguồn sự thật**.
 * Nguồn thật là section `FileStorage` trong `appsettings.json`; backend vẫn kiểm lại đầy đủ
 * (9 bước, gồm cả magic number) nên bảng này lệch thì cùng lắm là thông báo lỗi kém thân
 * thiện, không phải lỗ hổng.
 */
export const ATTACHMENT_MAX_BYTES = 20 * 1024 * 1024;

export const ATTACHMENT_ACCEPT = [
  '.png', '.jpg', '.jpeg', '.gif', '.webp',
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
  '.txt', '.csv', '.zip',
].join(',');

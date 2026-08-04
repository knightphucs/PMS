/**
 * Soi gương `PMS.Application/Features/Comments/CommentDtos.cs`.
 *
 * 📌 File này đáng lẽ phải có từ lâu: `CommentsController` xong từ phiên 2026-07-30 nhưng
 * frontend chưa từng có kiểu tương ứng, vì màn duy nhất dùng comment (chi tiết Task) bị
 * chặn bởi các API khác.
 */

export interface CommentResponse {
  id: string;
  content: string;
  authorId: string;
  authorName: string;
  createdAt: string;
  /** `null` nếu chưa từng sửa. */
  updatedAt: string | null;
}

export interface CreateCommentRequest {
  content: string;
}

/**
 * ⚠️ Quyền sửa HẸP HƠN quyền xóa, đúng ADR-026 — dễ làm ngược vì phản xạ là "PM quyền cao
 * hơn thì làm được nhiều hơn":
 * - **Sửa = CHỈ tác giả**, PM cũng không (viết lại lời người khác mà vẫn đứng tên họ)
 * - **Xóa = tác giả HOẶC PM** (kiểm duyệt là việc hợp lý của PM)
 *
 * Dùng `canEditComment` / `canDeleteComment` ở `lib/tasks/permissions.ts`, đừng tự suy.
 */
export interface UpdateCommentRequest {
  content: string;
}

/**
 * @mention trong bình luận — phần logic thuần, tách khỏi component để test được (ADR-048).
 *
 * 🔴 **Client gửi ID, server KHÔNG parse `@tên` từ nội dung.** Tên hiển thị không phải định
 * danh: hai người trùng tên là chuyện thường với tên tiếng Việt, người ta đổi tên, và `@abc`
 * rất có thể chỉ là một mẩu email. Nhưng chính vì thế mà **chữ và id có thể trôi khỏi nhau**:
 * người dùng chèn `@Nam` rồi xóa chữ đó đi trước khi gửi, mà id thì vẫn nằm trong state.
 *
 * `reconcileMentions` là chỗ đóng khe hở đó. Không có nó, một bình luận không hề nhắc tên ai
 * vẫn bắn thông báo "bạn được nhắc tới" — người nhận mở ra và không tìm thấy tên mình ở đâu.
 */

export interface MentionCandidate {
  id: string;
  name: string;
}

/** Chuỗi đại diện cho một lượt nhắc trong nội dung. Một nơi định nghĩa duy nhất. */
function mentionToken(name: string): string {
  return `@${name}`;
}

/**
 * Chèn `@Tên` vào cuối nháp, tự thêm khoảng trắng phân cách khi cần.
 *
 * Trả về nháp mới thay vì sửa tại chỗ để component giữ được luồng state một chiều.
 */
export function appendMention(draft: string, name: string): string {
  const token = mentionToken(name);

  if (draft.length === 0) return `${token} `;
  // `\s` gồm cả xuống dòng: người dùng vừa Enter xuống dòng mới thì không chèn thêm cách.
  return /\s$/.test(draft) ? `${draft}${token} ` : `${draft} ${token} `;
}

/**
 * Lọc danh sách người đã chọn xuống còn những ai **thật sự còn được nhắc trong nội dung**.
 *
 * Giữ nguyên thứ tự chọn, và khử trùng lặp theo id — chèn `@Nam` hai lần vẫn là một người,
 * gửi id trùng lên chỉ tổ để server tự lọc lại.
 *
 * ⚠️ So khớp theo chuỗi con chứ không theo ranh giới từ. Cố ý: tên tiếng Việt có dấu cách
 * ("Nguyễn Văn Nam") nên một biểu thức "ranh giới từ" sẽ hoặc là sai với dấu tiếng Việt,
 * hoặc là cắt tên thành nhiều mảnh. Hệ quả chấp nhận được: nếu tên người này là tiền tố của
 * tên người kia thì nhắc người tên dài vẫn tính là có nhắc người tên ngắn. Sai lệch đó
 * **về phía gửi thừa một thông báo**, còn server thì vẫn lọc lại theo thành viên dự án — chứ
 * không phải về phía nhắc mà không báo.
 */
export function reconcileMentions(content: string, picked: readonly MentionCandidate[]): string[] {
  const kept = new Set<string>();

  for (const candidate of picked) {
    if (content.includes(mentionToken(candidate.name))) kept.add(candidate.id);
  }

  return [...kept];
}

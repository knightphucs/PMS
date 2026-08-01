const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/**
 * ⚠️ KHÔNG dùng `Intl.DateTimeFormat('vi-VN', { day, month })` cho dạng ngắn.
 *
 * Với vi-VN, ICU trả về mẫu chỉ-ngày-tháng bằng dấu GẠCH NGANG (`29-07`) trong khi mẫu
 * đủ ba thành phần lại dùng dấu gạch chéo (`12/08/2026`). Đặt hai cái cạnh nhau trong
 * cùng một khoảng ngày cho ra `29-07 – 12/08/2026`, trông hệt như lỗi. Tự ghép để giữ
 * một kiểu dấu duy nhất.
 */
function dayMonth(date: Date): string {
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  return `${day}/${month}`;
}

/** Ngày từ API là chuỗi ISO. Hiển thị theo định dạng Việt Nam. */
export function formatDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return dateFormatter.format(date);
}

/**
 * Quá hạn so với hôm nay.
 *
 * ⚠️ Chỉ dùng cho `Project.ExpectedCompletionDate` — Project không có trường tính sẵn
 * nào cho việc này. Với Task thì PHẢI dùng `IsOverdue` do API trả về, đừng tự tính lại.
 */
export function isPastDue(iso: string): boolean {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return false;

  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return date < today;
}

/** Ngày ngắn `12/08` — cho thẻ Kanban, nơi mỗi ký tự đều phải trả tiền chỗ. */
export function formatShortDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return dayMonth(date);
}

/** Khoảng ngày của sprint: `12/08 – 26/08/2026`. */
export function formatDateRange(startIso: string, endIso: string): string {
  return `${formatShortDate(startIso)} – ${formatDate(endIso)}`;
}

/**
 * Chữ cái đầu cho avatar.
 *
 * Lấy HAI từ CUỐI: tên tiếng Việt đặt họ trước, tên gọi sau ("Nguyễn Văn An" → "VA"),
 * nên hai từ đầu sẽ cho ra cùng một cặp chữ cho rất nhiều người khác nhau.
 */
export function initials(name: string): string {
  return (
    name
      .trim()
      .split(/\s+/)
      .slice(-2)
      .map((part) => part.charAt(0).toUpperCase())
      .join('') || '?'
  );
}

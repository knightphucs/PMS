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

/**
 * `DateTime.MinValue` của .NET (`0001-01-01`) là **mốc canh**, không phải một ngày.
 *
 * Nó có thật trong DB: migration `20260728032237` thêm cột `CreatedAt` với
 * `defaultValue: 0001-01-01`, nên mọi hàng tạo TRƯỚC mốc đó mang giá trị này (ADR-033 ghi
 * lại hiện tượng cho `Tasks`; `ProjectMembers` cũng dính, và nó lộ ra ở dòng "Được mời
 * ngày …" của trang Lời mời). Hiển thị nguyên xi cho ra "01/01/1" — trông như lỗi định
 * dạng, trong khi thật ra là "không biết".
 *
 * Ngưỡng 1900 chứ không phải so bằng đúng `0001-01-01`: một mốc canh bị lệch múi giờ có
 * thể thành `0001-12-31`, và không nghiệp vụ nào ở đây có ngày trước thế kỷ 20.
 */
function isSentinelDate(date: Date): boolean {
  return date.getFullYear() < 1900;
}

/** Ngày từ API là chuỗi ISO. Hiển thị theo định dạng Việt Nam. */
export function formatDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime()) || isSentinelDate(date)) return '—';
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

const dateTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** Ngày + giờ — cho comment, lịch sử, thông báo, nơi thứ tự trong ngày là thông tin. */
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime()) || isSentinelDate(date)) return '—';
  return dateTimeFormatter.format(date);
}

const relativeFormatter = new Intl.RelativeTimeFormat('vi', { numeric: 'auto' });

/**
 * "5 phút trước", "hôm qua" — cho dòng thời gian của thông báo và comment.
 *
 * ⚠️ Backend trả mốc **UTC** nhưng chuỗi ISO của .NET `DateTime` (Kind=Utc) có hậu tố `Z`,
 * nên `new Date()` diễn giải đúng. Nếu về sau có endpoint nào trả `DateTime` không hậu tố
 * thì trình duyệt sẽ hiểu là giờ ĐỊA PHƯƠNG và mọi mốc lệch đi đúng bằng chênh múi giờ —
 * kiểm chuỗi thô trước khi nghi ngờ hàm này.
 *
 * Quá 7 ngày thì đổi sang ngày tuyệt đối: "3 tuần trước" bắt người đọc phải tự tính.
 */
export function formatRelativeTime(iso: string): string {
  const date = new Date(iso);
  // Không có nhánh này thì mốc canh ra "2025 năm trước" — sai một cách rất tự tin.
  if (Number.isNaN(date.getTime()) || isSentinelDate(date)) return '—';

  const diffMs = date.getTime() - Date.now();
  const diffMinutes = Math.round(diffMs / 60_000);

  if (Math.abs(diffMinutes) < 1) return 'vừa xong';
  if (Math.abs(diffMinutes) < 60) return relativeFormatter.format(diffMinutes, 'minute');

  const diffHours = Math.round(diffMinutes / 60);
  if (Math.abs(diffHours) < 24) return relativeFormatter.format(diffHours, 'hour');

  const diffDays = Math.round(diffHours / 24);
  if (Math.abs(diffDays) <= 7) return relativeFormatter.format(diffDays, 'day');

  return formatDate(iso);
}

/**
 * Cỡ file cho danh sách đính kèm. Dùng bội số 1024 và một chữ số thập phân — khớp với
 * cách Windows/macOS hiển thị, nên người dùng đối chiếu được với file trên máy họ.
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB'];
  let value = bytes / 1024;
  let unitIndex = 0;

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(1)} ${units[unitIndex]}`;
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

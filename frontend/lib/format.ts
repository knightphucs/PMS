const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

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

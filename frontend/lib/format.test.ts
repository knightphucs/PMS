import { describe, expect, it } from 'vitest';

import { formatDate, formatDateRange, formatShortDate, initials } from './format';

describe('formatShortDate / formatDateRange', () => {
  it('dạng ngắn dùng dấu GẠCH CHÉO, không phải gạch ngang', () => {
    // ICU của vi-VN trả `29-07` cho mẫu chỉ ngày-tháng nhưng `12/08/2026` cho mẫu đủ ba
    // thành phần. Ghép hai cái lại cho ra `29-07 – 12/08/2026`, trông hệt như lỗi.
    expect(formatShortDate('2026-07-29T00:00:00Z')).toMatch(/^\d{2}\/\d{2}$/);
  });

  it('hai đầu của một khoảng ngày dùng CÙNG một loại dấu phân cách', () => {
    const range = formatDateRange('2026-07-29T00:00:00Z', '2026-08-12T00:00:00Z');
    const [start, end] = range.split(' – ');

    expect(start).not.toContain('-');
    expect(start.split('/')).toHaveLength(2);
    expect(end.split('/')).toHaveLength(3);
  });

  it('chuỗi ngày hỏng trả về gạch ngang thay vì "Invalid Date"', () => {
    expect(formatDate('khong-phai-ngay')).toBe('—');
    expect(formatShortDate('')).toBe('—');
  });
});

describe('initials', () => {
  it('lấy HAI TỪ CUỐI — tiếng Việt đặt họ trước, tên gọi sau', () => {
    // "Nguyễn Văn An" -> "VA" chứ không phải "NV": hai từ đầu trùng nhau ở rất nhiều người.
    expect(initials('Nguyễn Văn An')).toBe('VA');
    expect(initials('Trần Thị Lan')).toBe('TL');
  });

  it('xử lý được tên một từ, khoảng trắng thừa và chuỗi rỗng', () => {
    expect(initials('Lan')).toBe('L');
    expect(initials('  Lê   Văn   Cường  ')).toBe('VC');
    expect(initials('')).toBe('?');
    expect(initials('   ')).toBe('?');
  });
});

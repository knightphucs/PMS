'use client';

import { useEffect, useState } from 'react';

/**
 * Hoãn giá trị lại một nhịp.
 *
 * Dùng cho ô tìm kiếm: gõ "dự án" mà bắn request theo từng phím là 5 lời gọi API, trong
 * đó 4 cái vô ích và tất cả đều đi qua tầng refresh token.
 */
export function useDebounced<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

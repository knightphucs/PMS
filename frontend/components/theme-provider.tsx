'use client';

import { ThemeProvider as NextThemesProvider } from 'next-themes';

/**
 * `next-themes` đã là phụ thuộc từ trước nhưng chưa từng được mount — `ui/sonner.tsx`
 * vẫn gọi `useTheme()` và lặng lẽ rơi về `"system"`. Thêm provider này sẽ đổi diện mạo
 * toast, đó là kết quả đúng chứ không phải lỗi.
 *
 * `attribute="class"` khớp với `@custom-variant dark (&:is(.dark *))` trong globals.css.
 */
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      // Không có cờ này thì mọi transition-colors trên trang cùng chạy một lượt lúc đổi
      // chủ đề — trông như trang bị lỗi chứ không phải như hiệu ứng.
      disableTransitionOnChange
    >
      {children}
    </NextThemesProvider>
  );
}

import type { Metadata } from 'next';
import { Be_Vietnam_Pro } from 'next/font/google';

import { Toaster } from '@/components/ui/sonner';
import { Providers } from '@/lib/providers';

import './globals.css';

// Geist mặc định của scaffold thiếu dấu tiếng Việt ở một số ký tự tổ hợp.
const beVietnamPro = Be_Vietnam_Pro({
  variable: '--font-sans',
  subsets: ['latin', 'vietnamese'],
  weight: ['400', '500', '600', '700'],
});

export const metadata: Metadata = {
  title: 'PMS — Quản lý dự án',
  description: 'Hệ thống quản lý dự án và công việc',
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    // suppressHydrationWarning là BẮT BUỘC với next-themes: script chống nháy của nó
    // gắn class `dark` lên <html> trước khi React hydrate, nên server và client luôn
    // khác nhau ở đúng thẻ này. Không có cờ này thì mỗi lần tải trang đều log cảnh báo.
    <html lang="vi" suppressHydrationWarning>
      <body className={`${beVietnamPro.variable} antialiased`}>
        <Providers>
          {children}
          <Toaster richColors position="top-right" />
        </Providers>
      </body>
    </html>
  );
}

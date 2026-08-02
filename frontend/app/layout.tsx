import type { Metadata } from 'next';
import { IBM_Plex_Sans } from 'next/font/google';

import { Toaster } from '@/components/ui/sonner';
import { Providers } from '@/lib/providers';

import './globals.css';

/**
 * ⚠️ Bộ `vietnamese` là BẮT BUỘC, không phải tùy chọn.
 *
 * Geist mặc định của scaffold đã bị loại vì thiếu dấu ở một số ký tự tổ hợp (ế, ệ, ỗ…) —
 * lỗi chỉ lộ ra ở vài từ nên rất dễ lọt. Trước khi đổi sang font khác, kiểm bằng
 * `next/dist/compiled/@next/font/dist/google/font-data.json` xem họ font đó có
 * `vietnamese` trong `subsets` không.
 *
 * Chỉ nạp 4 weight thật sự dùng tới (400 thân, 500 nhấn, 600 tiêu đề, 700 đậm) thay vì
 * cả họ 7 weight — mỗi weight là một file phải tải.
 */
const ibmPlexSans = IBM_Plex_Sans({
  variable: '--font-sans',
  subsets: ['latin', 'vietnamese'],
  weight: ['400', '500', '600', '700'],
  display: 'swap',
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
    // 🔴 Class `.variable` của next/font phải nằm trên <html>, KHÔNG phải <body>.
    //
    // Nó là thứ định nghĩa `--font-sans`, mà `globals.css` lại `@apply font-sans` ở tầng
    // `html`. Đặt ở <body> thì lúc <html> tính font-family, biến chưa tồn tại → giá trị
    // không hợp lệ → trình duyệt rơi về mặc định là **Times New Roman**, rồi <body> thừa
    // kế luôn cái đó. Không có lỗi, không có cảnh báo — chỉ là cả ứng dụng bỗng dùng font
    // serif. Đây chính là bug đã tồn tại từ phiên dựng scaffold tới 2026-08-02.
    <html lang="vi" className={ibmPlexSans.variable} suppressHydrationWarning>
      <body className="antialiased">
        <Providers>
          {children}
          <Toaster richColors position="top-right" />
        </Providers>
      </body>
    </html>
  );
}

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
      {/* 🔴 `suppressHydrationWarning` KHÔNG lan xuống thẻ con — nó chỉ áp cho đúng thẻ
          mang nó. Vì vậy cờ trên <html> ở trên không che được <body>, và cần cờ thứ hai.
          Đây không phải "dán băng dính cho cùng một lỗi hai lần".

          Lý do <body> cần: **tiện ích mở rộng của trình duyệt chèn thuộc tính vào <body>
          trước khi React hydrate** — Grammarly (`data-gr-ext-installed`,
          `data-new-gr-c-s-check-loaded`), ColorZilla (`cz-shortcut-listen`), trình quản lý
          mật khẩu… HTML từ server không có chúng, DOM ở client thì có, nên React báo lệch.

          Đã loại trừ nguyên nhân trong mã nguồn trước khi dán cờ: `next-themes` khai
          `attribute="class"` nên nó viết lên <html> chứ không phải <body>, và không file
          nào trong app ghi thuộc tính lên `document.body` (chỗ duy nhất chạm tới nó là
          `lib/attachments/download.ts`, chèn một thẻ <a> tạm SAU khi người dùng bấm tải).

          ⚠️ Cờ này chỉ tắt cảnh báo cho thuộc tính của CHÍNH <body>, không tắt cho cây bên
          trong — một lỗi hydrate thật trong ứng dụng vẫn được báo như thường. Kiểm chứng
          nhanh rằng thủ phạm là tiện ích: mở cùng trang ở cửa sổ ẩn danh (tiện ích tắt),
          cảnh báo sẽ biến mất kể cả khi chưa có cờ này. */}
      <body className="antialiased" suppressHydrationWarning>
        <Providers>
          {children}
          <Toaster richColors position="top-right" />
        </Providers>
      </body>
    </html>
  );
}

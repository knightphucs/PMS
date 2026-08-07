import path from 'node:path';

import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  turbopack: {
    // Có một package-lock.json lạc ở C:\Users\win10\ nên Turbopack suy ra thư mục gốc
    // workspace là HOME chứ không phải frontend/. Hệ quả: nó quét nhầm cây thư mục và
    // đường dẫn module có thể phân giải sai. Chỉ định tường minh cho hết mơ hồ.
    root: path.resolve(__dirname),
  },

  // 🔴 Proxy /api/* sang backend (Cloudflare Tunnel) qua chính domain Vercel.
  //
  // Backend (tunnel) và frontend (Vercel) là hai domain khác nhau -> mọi request từ
  // trình duyệt là cross-site. Cookie refresh dù đã SameSite=None; Secure vẫn bị Safari
  // chặn hoàn toàn vì Safari mặc định bật "Prevent Cross-Site Tracking", coi cookie do
  // domain B set khi trang đang mở là domain A là cookie bên-thứ-ba và không lưu/gửi nó
  // — bất kể thuộc tính SameSite là gì. Đây KHÔNG phải bug backend, mà là hành vi mặc
  // định của Safari (và tương lai có thể là các trình duyệt khác).
  //
  // Rewrite này khiến trình duyệt chỉ nói chuyện với `pms-six-gamma.vercel.app` (Vercel
  // là proxy trong suốt, forward request/response kể cả header Set-Cookie). Với trình
  // duyệt, request/response giờ same-origin thật sự -> không còn khái niệm "cross-site
  // cookie" để mà ITP hay SameSite chặn. Nhờ vậy dùng được cả SameSite=Strict.
  //
  // ⚠️ `BACKEND_ORIGIN` được đọc lúc `next build`, rewrite bị đóng băng vào bản build đó.
  // Đổi domain tunnel (Quick Tunnel của Cloudflare đổi domain mỗi lần restart) BẮT BUỘC
  // phải set lại biến môi trường này trên Vercel rồi redeploy — không tự nhận theo
  // runtime. Muốn hết cảnh phải redeploy mỗi lần cloudflared restart thì cần một
  // Cloudflare Tunnel đặt tên (named tunnel) gắn domain cố định, không dùng Quick Tunnel.
  async rewrites() {
    const backendOrigin = process.env.BACKEND_ORIGIN;
    if (!backendOrigin) return [];

    return [
      {
        source: '/api/:path*',
        destination: `${backendOrigin.replace(/\/+$/, '')}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;

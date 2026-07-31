import path from 'node:path';

import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  turbopack: {
    // Có một package-lock.json lạc ở C:\Users\win10\ nên Turbopack suy ra thư mục gốc
    // workspace là HOME chứ không phải frontend/. Hệ quả: nó quét nhầm cây thư mục và
    // đường dẫn module có thể phân giải sai. Chỉ định tường minh cho hết mơ hồ.
    root: path.resolve(__dirname),
  },
};

export default nextConfig;

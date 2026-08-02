import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { defineConfig } from 'vitest/config';

// ⚠️ Đuôi `.mts` là bắt buộc, không phải tùy chọn: package.json không có `"type":
// "module"` nên Vitest nạp `vitest.config.ts` bằng require() và chết với ERR_REQUIRE_ESM
// ở phụ thuộc `std-env`. Đuôi .mts ép nó đi đường ESM.
// Kéo theo: không có `__dirname` trong ESM, phải suy ra từ import.meta.url.
const rootDir = path.dirname(fileURLToPath(import.meta.url));

/**
 * Chỉ test tầng `lib/api/` — nơi logic thật sự khó và KHÔNG nhìn thấy được trên màn hình.
 * Component test chưa cần: lỗi bố cục thì mở trình duyệt ra là thấy, còn single-flight
 * refresh sai thì biểu hiện duy nhất là "thỉnh thoảng tự đăng xuất".
 */
export default defineConfig({
  // Một dòng alias rẻ hơn cài `vite-tsconfig-paths` chỉ để đọc đúng một đường dẫn.
  resolve: { alias: { '@': rootDir } },
  test: {
    // `node` chứ KHÔNG phải jsdom: lib/api chỉ đụng fetch/URL/Response/Headers, Node 18+
    // có sẵn đủ. Thêm jsdom là thêm một phụ thuộc nặng đổi lấy con số không.
    environment: 'node',
    include: ['lib/**/*.test.ts'],
    // ⚠️ BẮT BUỘC. `lib/api/config.ts` NÉM ngay lúc import nếu thiếu biến này, mà Vitest
    // không đọc `.env.local`. Không set ở đây thì mọi test đỏ trước khi chạy dòng nào,
    // với thông báo trông như code hỏng.
    env: { NEXT_PUBLIC_API_BASE_URL: 'https://localhost:7264/api/v1' },
    restoreMocks: true,
    setupFiles: ['./lib/api/__tests__/setup.ts'],
  },
});

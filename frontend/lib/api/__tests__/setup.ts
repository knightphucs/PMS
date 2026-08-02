import { beforeEach, vi } from 'vitest';

import { resetRefreshState } from '@/lib/api/refresh';
import { useAuthStore } from '@/store/auth-store';

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());

  // Zustand thuần, không cần `vi.mock` — `authStore.set` chính là `setState`.
  useAuthStore.setState({
    accessToken: null,
    accessTokenExpiresAt: null,
    user: null,
    status: 'unknown',
  });

  // ⚠️ BẮT BUỘC. `inFlight` trong refresh.ts là state cấp MODULE, sống xuyên suốt mọi
  // test trong cùng một file. Không reset thì một test để lại promise đang chờ và test
  // kế tiếp lặng lẽ bám vào nó — đổi thứ tự test là đổi kết quả.
  resetRefreshState();
});

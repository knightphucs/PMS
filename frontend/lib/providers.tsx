'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { useState } from 'react';

import { ThemeProvider } from '@/components/theme-provider';
import { ApiError } from '@/lib/api/problem';

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Mặc định của TanStack Query là retry 3 lần cho MỌI lỗi. Sai với API này:
        // 401/403/404/409 đều là câu trả lời dứt khoát của backend, thử lại chỉ làm
        // người dùng chờ lâu hơn để nhận đúng lỗi đó. Riêng 401 còn tệ hơn — mỗi lần
        // retry lại kéo theo một lượt refresh.
        retry: (failureCount, error) => {
          if (error instanceof ApiError && error.status < 500) return false;
          return failureCount < 2;
        },
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export function Providers({ children }: { children: React.ReactNode }) {
  // useState để QueryClient chỉ tạo một lần cho mỗi lần mount — tạo thẳng trong thân
  // component sẽ vứt sạch cache ở mỗi lần render lại.
  const [queryClient] = useState(makeQueryClient);

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        {children}
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
    </ThemeProvider>
  );
}

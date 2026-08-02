'use client';

import { RefreshCwIcon } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { errorMessage } from '@/lib/api/problem';

/**
 * Nhánh lỗi của một truy vấn — trích từ `projects/page.tsx`, sắp có mặt trên cả năm màn
 * hình mới.
 *
 * Luôn hiện `errorMessage(error)`: với lỗi nghiệp vụ, backend đã trả sẵn câu tiếng Việt
 * giải thích đúng lý do trong `title`. Thay nó bằng một câu chung chung của frontend là
 * vứt đi thông tin duy nhất hữu ích.
 */
export function QueryError({
  title,
  error,
  onRetry,
  isRetrying = false,
}: {
  title: string;
  error: unknown;
  onRetry?: () => void;
  isRetrying?: boolean;
}) {
  return (
    <Alert variant="destructive">
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription className="grid gap-3">
        <span>{errorMessage(error)}</span>
        {onRetry ? (
          <Button
            variant="outline"
            size="sm"
            className="w-fit"
            onClick={onRetry}
            disabled={isRetrying}
          >
            <RefreshCwIcon className="size-4" />
            {isRetrying ? 'Đang thử lại…' : 'Thử lại'}
          </Button>
        ) : null}
      </AlertDescription>
    </Alert>
  );
}

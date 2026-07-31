'use client';

import { AlertCircleIcon } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/alert';

/** Lỗi cấp form (401, 409, mất mạng) — lỗi cấp field đã hiện ngay dưới ô nhập. */
export function FormError({ message }: { message?: string | null }) {
  if (!message) return null;

  return (
    <Alert variant="destructive" role="alert">
      <AlertCircleIcon className="size-4" />
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}

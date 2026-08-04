'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
} from '@/lib/api/endpoints/notifications';
import { notificationKeys } from '@/lib/hooks/keys';
import type { NotificationListRequest } from '@/types/notification';

export function useNotifications(request: NotificationListRequest) {
  return useQuery({
    queryKey: notificationKeys.list(request),
    queryFn: ({ signal }) => listNotifications(request, signal),
  });
}

/**
 * Badge chưa đọc trên chuông.
 *
 * Thông báo được sinh ra bởi **người khác** và bởi cả job quét hạn chạy nền (ADR-040), nên
 * không có mutation nào ở client báo hiệu "có cái mới". Hỏi lại theo nhịp là cách duy nhất
 * cho tới khi có SignalR (§6) — 60 giây đủ để cảm giác sống mà không thành polling ồn ào.
 *
 * `refetchIntervalInBackground` để mặc định (false): tab bị ẩn thì ngừng hỏi.
 */
export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: ({ signal }) => getUnreadCount(signal),
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  });
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    },
  });
}

/**
 * ⚠️ `markedCount === 0` KHÔNG phải lỗi — nó chỉ nghĩa là không còn gì chưa đọc (thao tác
 * idempotent, ADR-024). Nơi gọi đừng hiện toast đỏ cho giá trị 0.
 */
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => markAllNotificationsRead(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
    },
  });
}

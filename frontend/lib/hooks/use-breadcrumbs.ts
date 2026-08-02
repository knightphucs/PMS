'use client';

import { usePathname } from 'next/navigation';

import { useProjectOverview } from '@/lib/hooks/use-projects';

export interface Crumb {
  label: string | null;
  href?: string;
  /** `true` khi nhãn đang chờ tải — hiện skeleton chứ đừng hiện guid trần. */
  loading?: boolean;
}

const TAB_LABEL: Record<string, string> = {
  board: 'Bảng',
  backlog: 'Backlog',
  sprints: 'Sprint',
  members: 'Thành viên',
};

/**
 * Breadcrumb cho thanh header, vốn nằm trong `AppShell` — tức là **trên** segment `[id]`
 * nên không bao giờ nhận được project qua prop.
 *
 * 🔑 Cách giải: cache của TanStack CHÍNH LÀ state dùng chung. Hook này gọi `useQuery` trên
 * **đúng khóa** mà layout của `[id]` đã mount, nên:
 *   • điều hướng nội bộ: dữ liệu còn trong `staleTime` → có ngay ở render đầu, 0 request
 *   • mở thẳng URL sâu: hai observer mount cùng lúc → TanStack gộp thành MỘT request
 *
 * Đã loại hai phương án khác:
 *   • `queryClient.getQueryData()` — không reactive. Mở deep link thì tên project về sau
 *     khi breadcrumb đã render xong, và nó không bao giờ render lại.
 *   • Context + `useEffect(() => setTitle(name))` từ trang con — thêm một lượt render và
 *     nháy rỗng→đầy ở MỌI lần điều hướng, cộng thêm ràng buộc "ai sở hữu tiêu đề".
 */
export function useBreadcrumbs(): Crumb[] {
  const pathname = usePathname();
  const segments = pathname.split('/').filter(Boolean);

  const isProjectRoute = segments[0] === 'projects';
  const projectId = isProjectRoute && segments[1] ? segments[1] : null;
  const tab = segments[2];

  const overview = useProjectOverview(projectId);

  if (!isProjectRoute) return [];

  const crumbs: Crumb[] = [{ label: 'Dự án', href: '/projects' }];
  if (!projectId) return crumbs;

  crumbs.push({
    label: overview.data?.name ?? null,
    href: `/projects/${projectId}`,
    loading: overview.data === undefined,
  });

  if (tab && TAB_LABEL[tab]) {
    crumbs.push({ label: TAB_LABEL[tab] });
  }

  return crumbs;
}

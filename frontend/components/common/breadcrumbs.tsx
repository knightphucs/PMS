'use client';

import Link from 'next/link';
import { Fragment } from 'react';

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb';
import { Skeleton } from '@/components/ui/skeleton';
import { useBreadcrumbs } from '@/lib/hooks/use-breadcrumbs';

/**
 * Thanh header lâu nay gần như trống. Ứng dụng nhiều tầng mà không có breadcrumb thì
 * luôn có cảm giác lạc — nhất là khi trang chi tiết project có tới bốn tab.
 */
export function Breadcrumbs() {
  const crumbs = useBreadcrumbs();

  if (crumbs.length === 0) return null;

  return (
    <Breadcrumb className="min-w-0">
      <BreadcrumbList className="flex-nowrap">
        {crumbs.map((crumb, index) => {
          const last = index === crumbs.length - 1;

          return (
            <Fragment key={`${crumb.href ?? 'page'}-${index}`}>
              {index > 0 ? <BreadcrumbSeparator /> : null}
              <BreadcrumbItem className="min-w-0">
                {crumb.loading ? (
                  // Hiện guid trần trong lúc chờ thì vừa xấu vừa vô nghĩa với người đọc.
                  <Skeleton className="h-4 w-32" />
                ) : last || !crumb.href ? (
                  <BreadcrumbPage className="truncate font-medium">
                    {crumb.label}
                  </BreadcrumbPage>
                ) : (
                  <BreadcrumbLink
                    render={<Link href={crumb.href} />}
                    className="truncate"
                  >
                    {crumb.label}
                  </BreadcrumbLink>
                )}
              </BreadcrumbItem>
            </Fragment>
          );
        })}
      </BreadcrumbList>
    </Breadcrumb>
  );
}

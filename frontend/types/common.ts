/** Soi gương `PMS.Application/Common/Models/PagedResult.cs` và `PagedRequest.cs`. */

/**
 * Mọi endpoint danh sách đều trả shape này (§7).
 *
 * Bốn trường cuối là giá trị server TÍNH SẴN — đừng tính lại ở frontend, và đừng gửi
 * ngược lên. Cùng nguyên tắc với `IsOverdue`/`SubtaskProgress` của Task.
 */
export interface PagedResult<T> {
  readonly items: T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export type SortDirection = 'asc' | 'desc';

/**
 * Query param của mọi endpoint danh sách.
 *
 * Backend clamp sẵn: `page < 1` -> 1, `pageSize` ngoài [1,100] -> 20 hoặc 100. Không cần
 * chặn trùng ở client, nhưng cũng đừng trông đợi giá trị gửi lên được giữ nguyên.
 */
export interface PagedRequest {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: SortDirection;
  search?: string;
}

export const DEFAULT_PAGE_SIZE = 10;
export const PAGE_SIZE_OPTIONS = [10, 20, 50] as const;

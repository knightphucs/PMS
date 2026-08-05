import type { EmployeeLookupResponse } from '@/types/employee';

import { apiFetch } from '../http';

/**
 * `GET /employees?search=` — tra nhân sự cho ô gợi ý (ADR-048).
 *
 * ⚠️ Trả **mảng trần**, KHÔNG phải `PagedResult<T>`: không có `page`, không có `totalCount`.
 * Trần kết quả nằm ở server (10) và cố ý không nhận tham số từ client.
 *
 * ⚠️ Từ khóa `< 2` ký tự — kể cả rỗng — trả **400**, không phải mảng rỗng. Người gọi phải
 * tự chặn trước; xem `useEmployeeSearch`.
 */
export function searchEmployees(search: string, signal?: AbortSignal) {
  return apiFetch<EmployeeLookupResponse[]>('/employees', { query: { search }, signal });
}

'use client';

import { useQuery } from '@tanstack/react-query';

import { searchEmployees } from '@/lib/api/endpoints/employees';
import { EMPLOYEE_SEARCH_MIN_LENGTH } from '@/types/employee';

import { employeeLookupKeys } from './keys';

/**
 * Gợi ý nhân sự theo từ khóa (ADR-048).
 *
 * 🔴 `enabled` soi gương đúng luật của server: dưới `EMPLOYEE_SEARCH_MIN_LENGTH` ký tự thì
 * **không bắn request nào**. Server trả 400 chứ không phải mảng rỗng cho trường hợp đó, nên
 * gọi rồi bắt lỗi sẽ đổ một chuỗi 400 vào devtools và một thông điệp lỗi vô nghĩa lên màn
 * hình cho một hành vi hoàn toàn bình thường — người dùng mới gõ được một chữ cái.
 *
 * `staleTime` dài vì danh bạ nhân sự gần như không đổi trong một phiên, và cùng một từ khóa
 * hay được gõ lại khi người dùng xóa rồi sửa.
 */
export function useEmployeeSearch(keyword: string) {
  const trimmed = keyword.trim();

  return useQuery({
    queryKey: employeeLookupKeys.search(trimmed),
    queryFn: ({ signal }) => searchEmployees(trimmed, signal),
    enabled: trimmed.length >= EMPLOYEE_SEARCH_MIN_LENGTH,
    staleTime: 5 * 60_000,
  });
}

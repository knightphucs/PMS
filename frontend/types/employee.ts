/** Soi gương `PMS.Application/Features/Employees/EmployeeLookupDtos.cs`. */

/**
 * Kết quả tra nhân sự cho ô gợi ý — `GET /employees?search=` (ADR-048).
 *
 * 🔴 **Đúng ba trường, và đó là ranh giới an ninh chứ không phải sự thiếu sót.** Endpoint
 * này mở cho **mọi người đã đăng nhập** (khác `GET /admin/employees` vốn nằm sau quyền
 * `employees:manage`), nên nó cố ý KHÔNG trả `systemRole`, `isLocked`, `createdAt` — biết ai
 * là quản trị viên và ai đang bị khóa là thông tin dò quét được. Backend có test khẳng định
 * trên **JSON thô** chính vì deserialize vào record sẽ âm thầm bỏ qua trường thừa và test
 * vẫn xanh. Đừng "bổ sung cho đủ" ở đây.
 *
 * Hai ràng buộc còn lại cũng thuộc cùng lớp lý do: từ khóa phải **≥ 2 ký tự** (một ký tự
 * khớp phần lớn danh bạ, lặp 26 lần là có toàn bộ), và **trần 10 kết quả cứng ở server**
 * (không nhận từ client). Chỉ trả người chưa bị khóa.
 */
export interface EmployeeLookupResponse {
  id: string;
  name: string;
  email: string;
}

/** Ngưỡng của server (`EmployeeLookupService.MinKeywordLength`) — dưới mức này là **400**. */
export const EMPLOYEE_SEARCH_MIN_LENGTH = 2;

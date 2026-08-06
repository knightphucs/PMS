import type { Status } from '@/types/enums';
import type { StatusCategory } from '@/types/task';

/**
 * Màu trạng thái của **PROJECT** — vẫn là bốn giá trị enum cố định.
 *
 * 🔴 **KHÔNG dùng cho task nữa** (ADR-052). Trạng thái task nay là một CỘT do người dùng
 * tạo, mang màu hex riêng, nên tra bảng này bằng `task.status` sẽ ra `undefined` —
 * chính là lỗi `STATUS_TONE[status].badge` đã gặp lúc backend đổi hợp đồng còn frontend
 * chưa theo kịp. Dùng {@link columnChipStyle} cho task.
 *
 * Giữ bảng màu Tailwind ở đây thay vì biến CSS: bốn màu này đã có sẵn biến thể `dark:` và
 * đọc tốt ở cả hai chế độ, mà chúng mang nghĩa NGỮ NGHĨA cố định (xám = chưa bắt đầu, xanh
 * lá = xong) chứ không phải màu thương hiệu — đổi theo `--primary` là làm mất thông tin đó.
 */
export const STATUS_TONE: Record<Status, { badge: string; dot: string }> = {
  ToDo: {
    badge: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
    dot: 'bg-slate-400 dark:bg-slate-500',
  },
  InProgress: {
    badge: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
    dot: 'bg-blue-500',
  },
  Review: {
    badge: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
    dot: 'bg-amber-500',
  },
  Done: {
    badge: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
    dot: 'bg-emerald-500',
  },
};

/**
 * Màu cho chip trạng thái của **TASK** — dựng từ mã hex của cột (ADR-052).
 *
 * <p>Không tra bảng được nữa: người dùng đặt màu, nên không có tập màu hữu hạn nào để lập
 * bảng. Trả về inline style thay vì class Tailwind vì cùng lý do — Tailwind cần biết trước
 * giá trị lúc build.</p>
 *
 * 🔐 An toàn với `style`: server validate `^#[0-9A-Fa-f]{6}$` (`BoardColumnValidators`),
 * nên chuỗi này không mang được dấu `;` hay `)` để thoát ra ngoài khai báo CSS. Vẫn lọc
 * lại ở đây một lần nữa — dữ liệu cũ trong DB có thể vào trước khi validator tồn tại.
 */
export function columnChipStyle(color: string): React.CSSProperties {
  const safe = /^#[0-9A-Fa-f]{6}$/.test(color) ? color : FALLBACK_COLOR;

  return {
    color: safe,
    // `1F` = ~12% alpha. Nền mờ của chính màu chữ giữ được tương phản ở CẢ hai chế độ sáng
    // /tối mà không phải sinh hai bảng màu — nền tối thì lớp phủ 12% gần như trong suốt,
    // nền sáng thì nó thành một mảng nhạt.
    backgroundColor: `${safe}1F`,
  };
}

/** Chấm tròn cùng màu cột — dùng ở header cột Kanban và ô chọn trạng thái. */
export function columnDotStyle(color: string): React.CSSProperties {
  return { backgroundColor: /^#[0-9A-Fa-f]{6}$/.test(color) ? color : FALLBACK_COLOR };
}

/** Xám trung tính, khớp cột "Cần làm" mặc định của backend. */
const FALLBACK_COLOR = '#6B7280';

/**
 * Task đã kết thúc chưa — đọc NHÓM, không so tên cột.
 *
 * Một project đặt cột tên "Đã ship" hay "Hủy bỏ" thì tên không nói lên điều gì; `category`
 * là hợp đồng duy nhất giữa tên do người dùng đặt và ngữ nghĩa mà mã nguồn cần.
 */
export function isDoneCategory(category: StatusCategory): boolean {
  return category === 'Done';
}

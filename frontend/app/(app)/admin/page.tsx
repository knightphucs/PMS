import { redirect } from 'next/navigation';

/**
 * `/admin` không phải một trang — nó là tiền tố của bốn tab. Đưa thẳng về tab đầu tiên thay
 * vì để người gõ tay URL đó rơi vào 404.
 *
 * Là server component (không `'use client'`) nên chuyển hướng xảy ra trước khi gửi HTML —
 * không có một nhịp nháy nào. Quyền vẫn được gác ở `/admin/employees` bởi `AdminLayout`.
 */
export default function AdminIndexPage() {
  redirect('/admin/employees');
}

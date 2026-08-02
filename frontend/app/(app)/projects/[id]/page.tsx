import { redirect } from 'next/navigation';

/**
 * ⚠️ File này CỐ Ý không có `'use client'` — ngoại lệ duy nhất của ADR-028 trong nhánh
 * `[id]`, và có lý do cụ thể.
 *
 * Nó không render gì và không đọc state nào của trình duyệt, chỉ chuyển hướng. Để là
 * server component thì Next trả một redirect thật ngay trong phản hồi HTTP; làm bằng
 * `useRouter().replace()` trong `useEffect` sẽ phải dựng xong cả cây client rồi mới nhảy,
 * tức người dùng nhìn thấy một khung trắng nháy qua.
 *
 * Mọi file KHÁC dưới `[id]/` đều là client component và dùng `useParams()`.
 */
export default async function ProjectIndexPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  redirect(`/projects/${id}/board`);
}

'use client';

import { useParams } from 'next/navigation';

import { ManageApprovalPoliciesDialog } from '@/components/approvals/manage-approval-policies-dialog';
import { ManageColumnsDialog } from '@/components/board/manage-columns-dialog';
import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { ManageFieldsDialog } from '@/components/fields/manage-fields-dialog';
import { ManageWorkItemTypesDialog } from '@/components/fields/manage-work-item-types-dialog';
import { useMyProjectRole } from '@/lib/hooks/use-my-project-role';
import { canManageTasks } from '@/lib/tasks/permissions';

/**
 * Cấu hình dự án (ADR-061) — **bề mặt thứ ba**, tách khỏi bề mặt làm việc.
 *
 * 🔴 Lý do trang này tồn tại nằm ở luật 5 của Doctrine chống rối (`ARCHITECTURE.md` §0):
 * ba dialog dưới đây trước đó treo trên header trang **Bảng** — tức là màn người ta mở ra
 * để *nhìn công việc*. Tầng "bộ máy quy trình" (ADR-062/063/064) sẽ đổ thêm luật duyệt và
 * khuôn dự án lên đó, thành **sáu nút cấu hình** trên một màn làm việc.
 *
 * Ba vai, ba bề mặt: người gửi yêu cầu thấy form · người xử lý thấy bảng và hàng đợi ·
 * người cấu hình thấy trang này.
 *
 * 📌 Mỗi mục vẫn là một **dialog** chứ không phải một trang con: chúng đã có sẵn, đã được
 * kiểm, và biến chúng thành trang riêng là viết lại ba thứ đang chạy đúng để đổi lấy một
 * URL sâu hơn mà chưa ai cần.
 */
export default function ProjectSettingsPage() {
  const { id } = useParams<{ id: string }>();
  const { role, isResolving } = useMyProjectRole(id);

  // Tab và sidebar đã ẩn lối vào với người không có quyền, nhưng URL thì gõ thẳng được —
  // nên vẫn phải gác ở đây. Backend là chốt chặn thật; đây chỉ là để màn hình nói thật.
  if (!isResolving && !canManageTasks(role))
    return (
      <EmptyState
        title="Không có quyền cấu hình dự án"
        description="Chỉ quản lý dự án mới sửa được cột, trường tuỳ biến, loại công việc và luật duyệt."
      />
    );

  return (
    <div className="grid min-w-0 gap-4">
      <PageHeader
        title="Cấu hình dự án"
        description="Cột, trường tuỳ biến, loại công việc và luật duyệt — những thứ định hình cách cả đội làm việc."
      />

      <div className="grid gap-3">
        <SettingRow
          title="Cột trên bảng"
          description="Các bước trong quy trình của đội. Mỗi cột khai một nhóm trạng thái để hệ thống biết việc đã xong hay chưa."
          action={<ManageColumnsDialog projectId={id} />}
        />
        <SettingRow
          title="Trường tuỳ biến"
          description="Dữ liệu riêng của đội — hệ thống ảnh hưởng, mức rủi ro, cửa sổ bảo trì… Lọc và hiển thị được ở màn Danh sách."
          action={<ManageFieldsDialog projectId={id} />}
        />
        <SettingRow
          title="Loại công việc"
          description="Sự cố, Yêu cầu, Change Request… Mỗi loại lộ ra một tập trường khác nhau, và đánh dấu trường nào bắt buộc."
          action={<ManageWorkItemTypesDialog projectId={id} />}
        />
        <SettingRow
          title="Luật duyệt"
          description="Bắt một loại công việc phải có chữ ký mới vào được một cột. Yêu cầu duyệt gửi tự động khi có người chuyển task sang cột đó."
          action={<ManageApprovalPoliciesDialog projectId={id} />}
        />
      </div>
    </div>
  );
}

function SettingRow({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action: React.ReactNode;
}) {
  return (
    <div className="bg-card flex flex-col gap-3 rounded-lg border p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="grid gap-1">
        <h2 className="text-sm font-semibold">{title}</h2>
        <p className="text-muted-foreground text-sm">{description}</p>
      </div>
      <div className="shrink-0">{action}</div>
    </div>
  );
}

'use client';

import { ArrowLeftIcon } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { ApprovalStateBadge } from '@/components/requests/approval-state-badge';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { columnChipStyle, columnDotStyle } from '@/components/tasks/status-tone';
import { WorkItemTypeChip } from '@/components/tasks/work-item-type-chip';
import { Skeleton } from '@/components/ui/skeleton';
import { ApiError } from '@/lib/api/problem';
import { formatDate, formatDateTime } from '@/lib/format';
import { useMyRequest } from '@/lib/hooks/use-request-portal';
import type { RequestFieldValue } from '@/types/request-portal';

/**
 * Chi tiết một yêu cầu của CHÍNH người gửi (ADR-063) — **chỉ đọc**.
 *
 * 🔴 Chỉ-đọc là một quyết định, không phải một giai đoạn còn dở. Yêu cầu đã gửi thuộc về
 * hàng đợi của đội xử lý; cho người gửi sửa tiêu đề hoặc trường sau khi người ta đã bắt đầu
 * làm là mở một đường thay đổi phạm vi mà không ai được báo.
 *
 * ⚠️ **404 ở đây không phải lỗi hệ thống** — backend nhét vị từ `ReporterId == me` vào cùng
 * mệnh đề với `Id == taskId` (guard G3), nên "không tồn tại" và "không phải của bạn" trả về
 * đúng một câu trả lời. Đó là chủ đích: 403 sẽ xác nhận rằng id đó có thật.
 *
 * 📌 **Cố ý chưa có bình luận.** `CommentService` đi qua `ProjectAction.CreateComment` nên
 * người ngoài project nhận 404. Mở đường hồi đáp cần một quyết định sản phẩm riêng — ghi rõ
 * ở ADR-063 chứ không bỏ quên.
 */
export default function MyRequestDetailPage() {
  const { id } = useParams<{ id: string }>();
  const request = useMyRequest(id);

  if (request.isPending) {
    return (
      <div className="grid gap-4">
        <BackLink />
        <Skeleton className="h-9 w-80" />
        <Skeleton className="h-64 w-full rounded-lg" />
      </div>
    );
  }

  if (request.isError) {
    const notFound = request.error instanceof ApiError && request.error.status === 404;

    return (
      <div className="grid gap-4">
        <BackLink />
        {notFound ? (
          <EmptyState
            title="Không tìm thấy yêu cầu này"
            description="Yêu cầu không tồn tại, hoặc nó không phải do bạn gửi. Cổng tiếp nhận chỉ hiển thị yêu cầu của chính bạn."
          />
        ) : (
          <QueryError
            title="Không tải được yêu cầu"
            error={request.error}
            onRetry={() => void request.refetch()}
            isRetrying={request.isRefetching}
          />
        )}
      </div>
    );
  }

  const data = request.data;

  return (
    <div className="grid min-w-0 gap-4">
      <BackLink />

      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <span className="text-muted-foreground shrink-0 font-mono text-xs">{data.code}</span>
        <WorkItemTypeChip
          type={{
            typeId: data.taskId,
            name: data.typeName,
            icon: data.typeIcon,
            color: data.typeColor,
          }}
        />
        <ApprovalStateBadge state={data.approvalState} />
      </div>

      <PageHeader
        title={data.name}
        description={`Gửi tới ${data.projectName} lúc ${formatDateTime(data.createdAt)}.`}
      />

      <div className="bg-card grid gap-4 rounded-lg border p-4">
        <Row label="Trạng thái">
          <span
            className="inline-flex items-center gap-1.5 rounded px-2 py-0.5 text-xs font-medium"
            style={columnChipStyle(data.statusColor)}
          >
            <span
              className="size-1.5 shrink-0 rounded-full"
              style={columnDotStyle(data.statusColor)}
            />
            {data.statusName}
          </span>
        </Row>

        <Row label="Độ ưu tiên">
          <PriorityLabel priority={data.priority} />
        </Row>

        <Row label="Mong muốn xong trước">
          {data.dueDate ? formatDate(data.dueDate) : <Muted>Không đặt</Muted>}
        </Row>

        <Row label="Mô tả" align="start">
          {data.description ? (
            <p className="text-sm whitespace-pre-wrap">{data.description}</p>
          ) : (
            <Muted>Không có mô tả</Muted>
          )}
        </Row>
      </div>

      {/* Khối trường TỰ ẨN khi loại không khai trường nào — luật 3 Doctrine (§0). */}
      {data.fieldValues.length > 0 ? (
        <div className="bg-card grid gap-3 rounded-lg border p-4">
          <h2 className="text-sm font-semibold">Thông tin đã điền</h2>
          {data.fieldValues.map((value) => (
            <Row key={value.fieldDefinitionId} label={value.label} align="start">
              <FieldValueText value={value} />
            </Row>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function FieldValueText({ value }: { value: RequestFieldValue }) {
  if (value.selectedOptions.length > 0) {
    return (
      <div className="flex min-w-0 flex-wrap gap-1.5">
        {value.selectedOptions.map((option) => (
          <span
            key={option.id}
            className="rounded-full px-2 py-0.5 text-xs font-medium text-white"
            style={{ backgroundColor: option.color }}
          >
            {option.label}
          </span>
        ))}
      </div>
    );
  }

  if (value.valueDate) return <span className="text-sm">{formatDate(value.valueDate)}</span>;
  if (value.valueNumber !== null) return <span className="text-sm">{value.valueNumber}</span>;
  if (value.valueBoolean !== null)
    return <span className="text-sm">{value.valueBoolean ? 'Có' : 'Không'}</span>;
  if (value.valueText) return <span className="text-sm break-words">{value.valueText}</span>;

  return <Muted>Chưa điền</Muted>;
}

function Row({
  label,
  align = 'center',
  children,
}: {
  label: string;
  align?: 'start' | 'center';
  children: React.ReactNode;
}) {
  return (
    <div
      className={`grid min-w-0 gap-1.5 sm:grid-cols-[minmax(0,11rem)_minmax(0,1fr)] sm:gap-3 ${
        align === 'center' ? 'sm:items-center' : 'sm:items-start'
      }`}
    >
      <span className="text-muted-foreground text-sm">{label}</span>
      <div className="min-w-0">{children}</div>
    </div>
  );
}

function Muted({ children }: { children: React.ReactNode }) {
  return <span className="text-muted-foreground text-sm">{children}</span>;
}

function BackLink() {
  return (
    <Link
      href="/requests"
      className="text-muted-foreground hover:text-foreground inline-flex w-fit items-center gap-1.5 text-sm"
    >
      <ArrowLeftIcon className="size-4" />
      Yêu cầu của tôi
    </Link>
  );
}

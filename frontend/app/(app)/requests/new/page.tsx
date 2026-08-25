'use client';

import { ArrowLeftIcon, InboxIcon, SendIcon } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';

import { EmptyState } from '@/components/common/empty-state';
import { PageHeader } from '@/components/common/page-header';
import { QueryError } from '@/components/common/query-error';
import { RequestFieldInput } from '@/components/requests/request-field-input';
import { PriorityLabel } from '@/components/tasks/priority-icon';
import { WorkItemTypeChip } from '@/components/tasks/work-item-type-chip';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { errorMessage } from '@/lib/api/problem';
import { useRequestForm, useRequestPortals, useSubmitRequest } from '@/lib/hooks/use-request-portal';
import { cn } from '@/lib/utils';
import { PRIORITY_ORDER, type Priority } from '@/types/enums';
import type { SetRequestFieldValue } from '@/types/request-portal';

/**
 * Gửi yêu cầu (ADR-063) — form tiếp nhận **dựng thẳng từ lược đồ đã có**.
 *
 * 🔑 Không có bảng "form" nào trong CSDL, và đó là toàn bộ điểm của ADR-063: một loại công
 * việc (`WorkItemType`) cộng tập trường của nó (`WorkItemTypeFields` + `IsRequired`, ADR-060)
 * **đã là** một request type đầy đủ. Màn này chỉ render thứ đội xử lý đã khai ở
 * `/projects/{id}/settings` — thêm một khái niệm "form" song song sẽ là hai thứ cùng nghĩa
 * phải giữ đồng bộ mãi mãi.
 *
 * 🔴 **Người dùng màn này thường không thuộc project nào.** Vì vậy không có breadcrumb dự
 * án, không liên kết vào board, và ô chọn project chỉ liệt kê những nơi đã **mở cổng** —
 * chứ không phải "dự án của bạn".
 */
export default function NewRequestPage() {
  const router = useRouter();
  const portals = useRequestPortals();

  const [projectId, setProjectId] = useState<string | null>(null);
  const [typeId, setTypeId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<Priority>('Medium');
  const [dueDate, setDueDate] = useState('');
  const [values, setValues] = useState<Record<string, SetRequestFieldValue>>({});

  const form = useRequestForm(projectId);
  const submit = useSubmitRequest(projectId ?? '');

  const selectedType = useMemo(
    () => form.data?.types.find((t) => t.workItemTypeId === typeId) ?? null,
    [form.data, typeId],
  );

  // Đổi project hoặc đổi loại thì XOÁ giá trị đã điền. Giữ lại là giữ id của những trường
  // thuộc lược đồ khác — backend sẽ bỏ qua chúng im lặng, còn người dùng thì tưởng đã điền.
  // ⚠️ Base UI `Select` gọi lại với `string | null` (null = bỏ chọn), không phải `string`.
  const pickProject = (id: string | null) => {
    setProjectId(id);
    setTypeId(null);
    setValues({});
  };

  const pickType = (id: string | null) => {
    setTypeId(id);
    setValues({});
  };

  const canSubmit =
    Boolean(projectId) && Boolean(typeId) && name.trim().length > 0 && !submit.isPending;

  const send = async () => {
    if (!projectId || !typeId) return;

    try {
      const created = await submit.mutateAsync({
        workItemTypeId: typeId,
        name: name.trim(),
        description: description.trim() === '' ? null : description.trim(),
        priority,
        // Cùng luật ghép `T00:00:00Z` như RequestFieldInput — xem chú thích ở đó.
        dueDate: dueDate === '' ? null : `${dueDate}T00:00:00Z`,
        fieldValues: Object.values(values),
      });

      toast.success(`Đã gửi yêu cầu ${created.code}.`);
      router.push(`/requests/${created.taskId}`);
    } catch (error) {
      // 400 thiếu trường bắt buộc là ca thường gặp nhất, và thông điệp của backend đã nêu
      // ĐÚNG TÊN trường — hiện nguyên văn thay vì gói lại thành "có lỗi xảy ra".
      toast.error(errorMessage(error));
    }
  };

  if (portals.isPending) {
    return (
      <div className="grid gap-4">
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-64 w-full rounded-lg" />
      </div>
    );
  }

  if (portals.isError) {
    return (
      <QueryError
        title="Không tải được danh sách cổng tiếp nhận"
        error={portals.error}
        onRetry={() => void portals.refetch()}
        isRetrying={portals.isRefetching}
      />
    );
  }

  const available = portals.data ?? [];

  if (available.length === 0) {
    return (
      <div className="grid gap-4">
        <BackLink />
        <EmptyState
          icon={<InboxIcon className="size-6" />}
          title="Chưa phòng ban nào mở cổng tiếp nhận"
          description="Quản lý dự án bật cổng bằng cách đánh dấu một loại công việc là “nhận yêu cầu từ bên ngoài” ở màn Cấu hình."
        />
      </div>
    );
  }

  return (
    <div className="grid min-w-0 gap-4">
      <BackLink />
      <PageHeader
        title="Gửi yêu cầu"
        description="Chọn nơi tiếp nhận và loại yêu cầu; các ô bên dưới do chính đội xử lý khai báo."
      />

      <div className="bg-card grid gap-4 rounded-lg border p-4">
        <div className="grid items-start gap-4 sm:grid-cols-2">
          <div className="grid gap-2">
            <Label htmlFor="request-project">Gửi tới</Label>
            <Select value={projectId ?? ''} onValueChange={pickProject}>
              <SelectTrigger id="request-project" className="w-full">
                {/* 🔴 Phải truyền hàm render. `<SelectValue placeholder=… />` trần hiện
                    **giá trị thô** khi value là một id — tức người dùng nhìn thấy một GUID
                    thay vì tên dự án. Đã trả giá đúng một lần trong phiên ADR-063; khuôn
                    đúng có sẵn ở ô chọn Độ ưu tiên ngay dưới và ở dialog xoá loại việc. */}
                <SelectValue placeholder="Chọn nơi tiếp nhận">
                  {(current: string | null) =>
                    available.find((p) => p.projectId === current)?.projectName ??
                    'Chọn nơi tiếp nhận'
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {available.map((portal) => (
                  <SelectItem key={portal.projectId} value={portal.projectId}>
                    {portal.projectName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="request-type">Loại yêu cầu</Label>
            <Select
              value={typeId ?? ''}
              onValueChange={pickType}
              disabled={!projectId || form.isPending}
            >
              <SelectTrigger id="request-type" className="w-full">
                <SelectValue
                  placeholder={projectId ? 'Chọn loại yêu cầu' : 'Chọn nơi tiếp nhận trước'}
                >
                  {(current: string | null) =>
                    form.data?.types.find((t) => t.workItemTypeId === current)?.name ??
                    (projectId ? 'Chọn loại yêu cầu' : 'Chọn nơi tiếp nhận trước')
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {(form.data?.types ?? []).map((type) => (
                  <SelectItem key={type.workItemTypeId} value={type.workItemTypeId}>
                    {type.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {form.isError ? (
          <QueryError
            title="Không tải được form của nơi tiếp nhận này"
            error={form.error}
            onRetry={() => void form.refetch()}
          />
        ) : null}

        {selectedType ? (
          <>
            <div className="flex items-center gap-2">
              <WorkItemTypeChip
                type={{
                  typeId: selectedType.workItemTypeId,
                  name: selectedType.name,
                  icon: selectedType.icon,
                  color: selectedType.color,
                }}
              />
            </div>

            {/* Chỉ dẫn TỰ ẨN khi đội xử lý không viết gì — luật 3 Doctrine (§0). */}
            {selectedType.requestInstructions ? (
              <p className="bg-muted/50 text-muted-foreground rounded-md border px-3 py-2 text-sm whitespace-pre-wrap">
                {selectedType.requestInstructions}
              </p>
            ) : null}

            <div className="grid gap-2">
              <Label htmlFor="request-name">
                Tiêu đề <span className="text-destructive">*</span>
              </Label>
              <Input
                id="request-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Nêu ngắn gọn việc cần xử lý"
                maxLength={200}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="request-description">Mô tả</Label>
              <Textarea
                id="request-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Bối cảnh, mức độ ảnh hưởng, thời điểm mong muốn…"
                rows={4}
              />
            </div>

            <div className="grid items-start gap-4 sm:grid-cols-2">
              <div className="grid gap-2">
                <Label htmlFor="request-priority">Độ ưu tiên</Label>
                <Select value={priority} onValueChange={(v) => setPriority((v ?? 'Medium') as Priority)}>
                  <SelectTrigger id="request-priority" className="w-full">
                    <SelectValue>
                      {(current: Priority) => <PriorityLabel priority={current} />}
                    </SelectValue>
                  </SelectTrigger>
                  <SelectContent>
                    {PRIORITY_ORDER.map((item) => (
                      <SelectItem key={item} value={item}>
                        <PriorityLabel priority={item} />
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-2">
                <Label htmlFor="request-due">Mong muốn hoàn thành trước</Label>
                <Input
                  id="request-due"
                  type="date"
                  value={dueDate}
                  onChange={(e) => setDueDate(e.target.value)}
                />
              </div>
            </div>

            {/* Khối trường riêng của loại — TỰ ẨN khi loại không khai trường nào. */}
            {selectedType.fields.length > 0 ? (
              <div className="grid gap-3 rounded-md border p-3">
                {selectedType.fields.map((field) => (
                  <div
                    key={field.fieldDefinitionId}
                    className="grid min-w-0 gap-1.5 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:items-center sm:gap-3"
                  >
                    <Label className="text-muted-foreground text-sm">
                      {field.label}
                      {field.isRequired ? <span className="text-destructive"> *</span> : null}
                    </Label>
                    <RequestFieldInput
                      field={field}
                      value={values[field.fieldDefinitionId] ?? {
                        fieldDefinitionId: field.fieldDefinitionId,
                      }}
                      disabled={submit.isPending}
                      onChange={(next) =>
                        setValues((prev) => ({ ...prev, [field.fieldDefinitionId]: next }))
                      }
                    />
                  </div>
                ))}
              </div>
            ) : null}

            <div className="flex flex-wrap justify-end gap-2">
              <Button onClick={() => void send()} disabled={!canSubmit}>
                <SendIcon className="size-4" />
                {submit.isPending ? 'Đang gửi…' : 'Gửi yêu cầu'}
              </Button>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}

function BackLink() {
  return (
    <Link
      href="/requests"
      className={cn('text-muted-foreground hover:text-foreground inline-flex w-fit items-center gap-1.5 text-sm')}
    >
      <ArrowLeftIcon className="size-4" />
      Yêu cầu của tôi
    </Link>
  );
}

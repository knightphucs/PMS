'use client';

import { useState } from 'react';
import { toast } from 'sonner';

import { QueryError } from '@/components/common/query-error';
import { TaskFieldRow, TaskSection } from '@/components/tasks/task-section';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { errorMessage } from '@/lib/api/problem';
import { useFieldDefinitions, useFieldValues, useSetFieldValues } from '@/lib/hooks/use-custom-fields';
import { cn } from '@/lib/utils';
import type {
  FieldOptionResponse,
  FieldValueResponse,
  SetFieldValueRequest,
} from '@/types/custom-field';

/**
 * Khối "Trường tuỳ biến" ở chi tiết task (ADR-059).
 *
 * 📌 Ghi kiểu **PATCH, từng trường một** khi người dùng rời ô (blur) — giống `TaskDescription`.
 * Gửi trọn bộ giá trị mỗi lần lưu sẽ biến mỗi lần gõ thành một cơ hội ghi đè công của người
 * khác đang sửa một trường khác trên cùng task.
 *
 * 🔴 Khối này TỰ ẨN khi project chưa khai trường nào. Một khối trống mang tiêu đề "Trường
 * tuỳ biến" trên mọi task của mọi project chưa dùng tính năng này là nhiễu thuần tuý —
 * người dùng không hành động được gì với nó.
 */
export function TaskCustomFields({
  projectId,
  taskId,
  canEdit,
}: {
  projectId: string;
  taskId: string;
  canEdit: boolean;
}) {
  const values = useFieldValues(projectId, taskId);
  // 🔴 Cần CẢ lược đồ, không chỉ giá trị: response giá trị chỉ mang những lựa chọn ĐANG
  // được chọn, nên nếu chỉ dựa vào nó thì ô chọn không bao giờ hiện ra lựa chọn chưa chọn
  // — tức người dùng không có cách nào chọn thêm. Hook này dùng chung cache với dialog quản
  // lý trường (staleTime 5 phút) nên không tốn request thừa.
  const definitions = useFieldDefinitions(projectId);
  const save = useSetFieldValues(projectId, taskId);

  if (values.isPending) {
    return (
      <TaskSection title="Trường tuỳ biến">
        <Skeleton className="h-16 w-full" />
      </TaskSection>
    );
  }

  if (values.isError) {
    return (
      <TaskSection title="Trường tuỳ biến">
        <QueryError
          title="Không tải được trường tuỳ biến"
          error={values.error}
          onRetry={() => void values.refetch()}
        />
      </TaskSection>
    );
  }

  const fields = values.data ?? [];
  if (fields.length === 0) return null;

  const commit = async (patch: SetFieldValueRequest) => {
    try {
      await save.mutateAsync({ values: [patch] });
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <TaskSection title="Trường tuỳ biến" count={fields.length}>
      <div className="grid gap-3 rounded-md border p-3">
        {fields.map((field) => (
          <TaskFieldRow
            key={field.fieldDefinitionId}
            label={field.label}
            align={field.type === 'MultiSelect' ? 'start' : 'center'}
          >
            <FieldInput
              field={field}
              options={
                definitions.data?.find((d) => d.id === field.fieldDefinitionId)?.options ?? []
              }
              canEdit={canEdit}
              isBusy={save.isPending}
              onCommit={commit}
            />
          </TaskFieldRow>
        ))}
      </div>
    </TaskSection>
  );
}

function FieldInput({
  field,
  options,
  canEdit,
  isBusy,
  onCommit,
}: {
  field: FieldValueResponse;
  /** MỌI lựa chọn của trường, lấy từ lược đồ — không phải chỉ những cái đang chọn. */
  options: FieldOptionResponse[];
  canEdit: boolean;
  isBusy: boolean;
  onCommit: (patch: SetFieldValueRequest) => void | Promise<void>;
}) {
  const id = field.fieldDefinitionId;

  switch (field.type) {
    case 'Checkbox':
      return (
        <input
          type="checkbox"
          aria-label={field.label}
          className="size-4"
          disabled={!canEdit || isBusy}
          checked={field.valueBoolean ?? false}
          // Checkbox không có khái niệm "rời ô" — ghi ngay khi đổi.
          onChange={(e) =>
            void onCommit({ fieldDefinitionId: id, valueBoolean: e.target.checked })
          }
        />
      );

    case 'SingleSelect':
    case 'MultiSelect':
      return (
        <OptionPicker
          field={field}
          options={options}
          canEdit={canEdit}
          isBusy={isBusy}
          onCommit={onCommit}
        />
      );

    case 'Number':
      return (
        <BlurInput
          type="number"
          label={field.label}
          canEdit={canEdit}
          isBusy={isBusy}
          initial={field.valueNumber === null ? '' : String(field.valueNumber)}
          onCommit={(raw) =>
            onCommit({
              fieldDefinitionId: id,
              // Chuỗi rỗng -> null (xoá giá trị), KHÔNG phải 0. Number('') là 0, và gửi 0
              // đi sẽ biến "chưa điền" thành "bằng không" — hai thứ khác hẳn nhau khi
              // trường là "Giờ downtime".
              valueNumber: raw.trim() === '' ? null : Number(raw),
            })
          }
        />
      );

    case 'Date':
      return (
        <BlurInput
          type="date"
          label={field.label}
          canEdit={canEdit}
          isBusy={isBusy}
          // <input type="date"> chỉ nhận yyyy-MM-dd; backend trả ISO đầy đủ.
          initial={field.valueDate ? field.valueDate.slice(0, 10) : ''}
          onCommit={(raw) =>
            onCommit({
              fieldDefinitionId: id,
              valueDate: raw === '' ? null : new Date(`${raw}T00:00:00Z`).toISOString(),
            })
          }
        />
      );

    default:
      return (
        <BlurInput
          type={field.type === 'Url' ? 'url' : 'text'}
          label={field.label}
          canEdit={canEdit}
          isBusy={isBusy}
          initial={field.valueText ?? ''}
          onCommit={(raw) =>
            onCommit({ fieldDefinitionId: id, valueText: raw.trim() === '' ? null : raw })
          }
        />
      );
  }
}

/**
 * Ô nhập lưu khi RỜI ô, và chỉ khi giá trị thật sự đổi.
 *
 * `key` bên ngoài không đổi giữa các lần render nên state nháp sống qua mọi lần refetch —
 * đó là chủ ý: gõ dở mà cache làm mới thì chữ đang gõ không được phép biến mất.
 */
function BlurInput({
  type,
  label,
  initial,
  canEdit,
  isBusy,
  onCommit,
}: {
  type: string;
  label: string;
  initial: string;
  canEdit: boolean;
  isBusy: boolean;
  onCommit: (raw: string) => void | Promise<void>;
}) {
  const [draft, setDraft] = useState<string | null>(null);
  const shown = draft ?? initial;

  return (
    <Input
      type={type}
      aria-label={label}
      value={shown}
      disabled={!canEdit || isBusy}
      className={cn('h-8', !canEdit && 'disabled:opacity-100')}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={() => {
        setDraft(null);
        if (shown === initial) return;   // không đổi -> không request
        void onCommit(shown);
      }}
    />
  );
}

/** Chip chọn lựa chọn. SingleSelect bấm lại chip đang chọn = bỏ chọn. */
function OptionPicker({
  field,
  options,
  canEdit,
  isBusy,
  onCommit,
}: {
  field: FieldValueResponse;
  options: FieldOptionResponse[];
  canEdit: boolean;
  isBusy: boolean;
  onCommit: (patch: SetFieldValueRequest) => void | Promise<void>;
}) {
  const selected = new Set(field.selectedOptions.map((o) => o.id));

  // Danh sách đầy đủ đến từ LƯỢC ĐỒ. Nếu lược đồ chưa tải xong thì tạm hiện những cái đang
  // chọn — hiện đúng dữ liệu đã có còn hơn để trống rồi nháy sang đầy đủ.
  const all = options.length > 0 ? options : field.selectedOptions;

  const toggle = (optionId: string) => {
    const next = new Set(selected);
    if (next.has(optionId)) next.delete(optionId);
    else if (field.type === 'SingleSelect') {
      next.clear();
      next.add(optionId);
    } else next.add(optionId);

    void onCommit({
      fieldDefinitionId: field.fieldDefinitionId,
      selectedOptionIds: [...next],
    });
  };

  if (all.length === 0 && !canEdit) {
    return <span className="text-muted-foreground text-sm">—</span>;
  }

  return (
    <div className="flex min-w-0 flex-wrap gap-1.5">
      {all.map((option) => (
        <button
          key={option.id}
          type="button"
          disabled={!canEdit || isBusy}
          onClick={() => toggle(option.id)}
          className={cn(
            'rounded-full border px-2 py-0.5 text-xs font-medium transition',
            selected.has(option.id) ? 'text-white' : 'text-muted-foreground',
          )}
          style={
            selected.has(option.id)
              ? { backgroundColor: option.color, borderColor: option.color }
              : undefined
          }
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

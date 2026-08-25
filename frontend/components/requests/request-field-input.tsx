'use client';

import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import type { FieldOptionResponse } from '@/types/custom-field';
import type { RequestPortalFieldSchema, SetRequestFieldValue } from '@/types/request-portal';

/**
 * Một ô trên form tiếp nhận (ADR-063).
 *
 * 🔴 **Vì sao đây là component riêng chứ không tái dùng `FieldInput` của
 * `task-custom-fields.tsx`** — ghi rõ vì "sao không dùng lại?" là câu hỏi đúng phải hỏi:
 *
 * Hai thứ có cùng *cách vẽ* nhưng khác hẳn *ngữ nghĩa ghi*. Ô ở chi tiết task lưu kiểu
 * **PATCH khi rời ô** (mỗi trường một request, ghi ngay), vì task đã tồn tại và nhiều
 * người sửa song song. Ô ở đây là **bản nháp** — không có gì được ghi cho tới khi bấm Gửi,
 * vì cái task còn chưa ra đời. Ép chung một component sẽ phải nhét một cờ `mode` chảy vào
 * mọi nhánh, và đó là cách một component tử tế biến thành hai component mắc kẹt trong một
 * cái vỏ.
 *
 * Thứ *thật sự* dùng chung là các luật hiển thị theo `FieldType`, và chúng được giữ chung
 * bằng cách dùng lại `FieldOptionResponse` + cùng một bảng nhánh — không phải bằng cách
 * dùng chung code khung.
 */
export function RequestFieldInput({
  field,
  value,
  onChange,
  disabled,
}: {
  field: RequestPortalFieldSchema;
  value: SetRequestFieldValue;
  onChange: (next: SetRequestFieldValue) => void;
  disabled?: boolean;
}) {
  const id = field.fieldDefinitionId;
  const patch = (partial: Partial<SetRequestFieldValue>) =>
    onChange({ ...value, fieldDefinitionId: id, ...partial });

  switch (field.type) {
    case 'Checkbox':
      return (
        <input
          type="checkbox"
          aria-label={field.label}
          className="size-4"
          disabled={disabled}
          checked={value.valueBoolean ?? false}
          onChange={(e) => patch({ valueBoolean: e.target.checked })}
        />
      );

    case 'SingleSelect':
    case 'MultiSelect':
      return (
        <OptionPicker
          options={field.options}
          multiple={field.type === 'MultiSelect'}
          selected={value.selectedOptionIds ?? []}
          disabled={disabled}
          onChange={(ids) => patch({ selectedOptionIds: ids })}
        />
      );

    case 'Number':
      return (
        <Input
          type="number"
          aria-label={field.label}
          disabled={disabled}
          value={value.valueNumber === null || value.valueNumber === undefined ? '' : String(value.valueNumber)}
          onChange={(e) =>
            // Chuỗi rỗng → null (chưa điền), KHÔNG phải 0. `Number('')` là 0, và gửi 0 đi sẽ
            // biến "chưa điền" thành "bằng không" — hai thứ khác hẳn khi trường là "Giờ downtime".
            // Và nó còn qua mặt được cả phép kiểm bắt buộc ở backend.
            patch({ valueNumber: e.target.value.trim() === '' ? null : Number(e.target.value) })
          }
        />
      );

    case 'Date':
      return (
        <Input
          type="date"
          aria-label={field.label}
          disabled={disabled}
          value={value.valueDate ? value.valueDate.slice(0, 10) : ''}
          onChange={(e) =>
            patch({
              // 🔴 Ghép `T00:00:00Z` TƯỜNG MINH. `new Date('2026-09-15')` đã là UTC, nhưng
              // `new Date('2026-09-15T00:00:00')` thì là giờ ĐỊA PHƯƠNG — ở UTC+7 nó lùi
              // thành ngày 14. Mọi cột DateTime phía backend đóng dấu Kind=Utc (ADR-046b),
              // nên một chuỗi không có hậu tố Z là một lỗi lệch ngày đang chờ xảy ra.
              valueDate: e.target.value === '' ? null : `${e.target.value}T00:00:00Z`,
            })
          }
        />
      );

    default:
      return (
        <Input
          type={field.type === 'Url' ? 'url' : 'text'}
          aria-label={field.label}
          disabled={disabled}
          value={value.valueText ?? ''}
          onChange={(e) => patch({ valueText: e.target.value === '' ? null : e.target.value })}
        />
      );
  }
}

function OptionPicker({
  options,
  multiple,
  selected,
  disabled,
  onChange,
}: {
  options: FieldOptionResponse[];
  multiple: boolean;
  selected: string[];
  disabled?: boolean;
  onChange: (ids: string[]) => void;
}) {
  const set = new Set(selected);

  const toggle = (optionId: string) => {
    const next = new Set(set);
    if (next.has(optionId)) next.delete(optionId);
    else if (!multiple) {
      next.clear();
      next.add(optionId);
    } else next.add(optionId);
    onChange([...next]);
  };

  return (
    <div className="flex min-w-0 flex-wrap gap-1.5">
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          disabled={disabled}
          aria-pressed={set.has(option.id)}
          onClick={() => toggle(option.id)}
          className={cn(
            'rounded-full border px-2 py-0.5 text-xs font-medium transition',
            set.has(option.id) ? 'text-white' : 'text-muted-foreground',
          )}
          style={
            set.has(option.id)
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

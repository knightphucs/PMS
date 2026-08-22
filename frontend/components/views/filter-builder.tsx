'use client';

import { PlusIcon, XIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useBoardColumns } from '@/lib/hooks/use-board-columns';
import { useFieldDefinitions } from '@/lib/hooks/use-custom-fields';
import { useMembers } from '@/lib/hooks/use-members';
import { useSprints } from '@/lib/hooks/use-sprints';
import { useWorkItemTypes } from '@/lib/hooks/use-work-item-types';
import type { FieldDefinitionResponse } from '@/types/custom-field';
import {
  CATEGORY_VALUES,
  FILTER_OPERATOR_LABEL,
  PRIORITY_VALUES,
  TASK_FIELD_LABEL,
  isUnaryOperator,
  kindOfFieldType,
  kindOfTaskField,
  operatorsFor,
  type FilterOperator,
  type FilterValueKind,
  type SavedViewFilterDto,
  type TaskField,
} from '@/types/saved-view';

/**
 * Trình dựng bộ lọc (ADR-061).
 *
 * 🔴 **Chỉ bày ra những gì backend chấp nhận.** Danh sách toán tử được lọc theo
 * `FilterValueKind` của trường đang chọn, và ô nhập giá trị đổi hình dạng theo đúng kiểu
 * đó. Bày một ô chữ cho một trường Số rồi báo lỗi lúc Lưu là hứa một việc backend từ chối
 * làm — cùng lý lẽ đã dùng khi bỏ ô chọn kiểu ở form sửa trường (ADR-059).
 *
 * Backend vẫn là chốt chặn thật: mọi thứ ở đây được kiểm lại ở `TaskFilterCatalog`.
 *
 * ⚠️ Nhiều điều kiện nối bằng **AND**. Nói thẳng trên giao diện thay vì để người dùng tự
 * đoán — chưa hỗ trợ OR, và một người tưởng có OR sẽ đọc sai kết quả chứ không thấy lỗi.
 */

/** Khoá tổng hợp cho ô chọn trường: trường dựng sẵn dùng tên enum, trường tuỳ biến dùng id. */
const CUSTOM_PREFIX = 'custom:';

const BUILT_IN_FIELDS: TaskField[] = [
  'Name',
  'BoardColumn',
  'Category',
  'Priority',
  'WorkItemType',
  'Sprint',
  'Assignee',
  'Reporter',
  'DueDate',
  'StoryPoints',
  'CreatedAt',
];

function encodeField(filter: SavedViewFilterDto): string {
  return filter.fieldDefinitionId
    ? `${CUSTOM_PREFIX}${filter.fieldDefinitionId}`
    : (filter.field ?? '');
}

function kindOf(
  filter: SavedViewFilterDto,
  definitions: FieldDefinitionResponse[],
): FilterValueKind {
  if (filter.fieldDefinitionId) {
    const definition = definitions.find((d) => d.id === filter.fieldDefinitionId);
    // Trường đã bị xoá khỏi project nhưng điều kiện còn nằm trong state đang sửa dở.
    // Rơi về Text để ô nhập vẫn vẽ được — người dùng cần thấy nó để còn xoá đi.
    return definition ? kindOfFieldType(definition.type) : 'Text';
  }
  return filter.field ? kindOfTaskField(filter.field) : 'Text';
}

export function FilterBuilder({
  projectId,
  filters,
  onChange,
}: {
  projectId: string;
  filters: SavedViewFilterDto[];
  onChange: (next: SavedViewFilterDto[]) => void;
}) {
  const definitions = useFieldDefinitions(projectId);
  const custom = definitions.data ?? [];

  const update = (index: number, patch: Partial<SavedViewFilterDto>) =>
    onChange(filters.map((f, i) => (i === index ? { ...f, ...patch } : f)));

  const handleFieldChange = (index: number, encoded: string) => {
    const isCustom = encoded.startsWith(CUSTOM_PREFIX);
    const fieldDefinitionId = isCustom ? encoded.slice(CUSTOM_PREFIX.length) : null;
    const field = isCustom ? null : (encoded as TaskField);

    const definition = custom.find((d) => d.id === fieldDefinitionId);
    const kind = isCustom
      ? definition
        ? kindOfFieldType(definition.type)
        : 'Text'
      : kindOfTaskField(field!);

    // Đổi trường thì toán tử cũ có thể không còn hợp lệ (vd. `Contains` trên một trường Số)
    // và giá trị cũ chắc chắn vô nghĩa. Đặt lại cả hai thay vì để người dùng gửi đi một
    // tổ hợp mà backend sẽ từ chối.
    update(index, {
      field,
      fieldDefinitionId,
      operator: operatorsFor(kind)[0],
      value: null,
    });
  };

  return (
    <div className="grid gap-2">
      {filters.length === 0 ? (
        <p className="text-muted-foreground text-sm">
          Chưa có điều kiện nào — danh sách đang hiện mọi task của dự án.
        </p>
      ) : null}

      {filters.map((filter, index) => {
        const kind = kindOf(filter, custom);
        const allowed = operatorsFor(kind);

        return (
          <div
            key={index}
            className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,9rem)_minmax(0,1fr)_auto]"
          >
            {/* ⚠️ `onValueChange` của Base UI khai `string | null` (null = bỏ chọn). Ở đây
                không bao giờ có nhánh bỏ chọn, nhưng phải nhận đúng chữ ký chứ không ép
                kiểu — ép kiểu sẽ giấu mất một null thật nếu về sau bật `clearable`. */}
            <Select
              value={encodeField(filter)}
              onValueChange={(next: string | null) => {
                if (next !== null) handleFieldChange(index, next);
              }}
            >
              <SelectTrigger size="sm" aria-label={`Trường của điều kiện ${index + 1}`}>
                {/* ⚠️ `SelectValue` của Base UI hiện GIÁ TRỊ thô — phải tự dựng nhãn. */}
                <SelectValue>
                  {(current: string) =>
                    current.startsWith(CUSTOM_PREFIX)
                      ? (custom.find((d) => d.id === current.slice(CUSTOM_PREFIX.length))
                          ?.label ?? 'Trường đã xoá')
                      : (TASK_FIELD_LABEL[current as TaskField] ?? 'Chọn trường')
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {BUILT_IN_FIELDS.map((field) => (
                  <SelectItem key={field} value={field}>
                    {TASK_FIELD_LABEL[field]}
                  </SelectItem>
                ))}
                {custom.map((definition) => (
                  <SelectItem key={definition.id} value={`${CUSTOM_PREFIX}${definition.id}`}>
                    {definition.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select
              value={filter.operator}
              onValueChange={(next: string | null) => {
                if (next === null) return;
                const operator = next as FilterOperator;
                update(index, {
                  operator,
                  // Chuyển sang toán tử một ngôi thì giá trị cũ phải biến mất: backend từ
                  // chối một `IsEmpty` kèm giá trị, và giữ lại sẽ làm người dùng tưởng nó
                  // vẫn có tác dụng.
                  value: isUnaryOperator(operator) ? null : filter.value,
                });
              }}
            >
              <SelectTrigger size="sm" aria-label={`Toán tử của điều kiện ${index + 1}`}>
                <SelectValue>
                  {(current: string) => FILTER_OPERATOR_LABEL[current as FilterOperator]}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {allowed.map((operator) => (
                  <SelectItem key={operator} value={operator}>
                    {FILTER_OPERATOR_LABEL[operator]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {isUnaryOperator(filter.operator) ? (
              <span className="text-muted-foreground hidden text-sm sm:block">giá trị</span>
            ) : (
              <FilterValueInput
                projectId={projectId}
                kind={kind}
                filter={filter}
                definitions={custom}
                onChange={(value) => update(index, { value })}
              />
            )}

            <Button
              type="button"
              variant="ghost"
              size="icon"
              aria-label={`Xoá điều kiện ${index + 1}`}
              onClick={() => onChange(filters.filter((_, i) => i !== index))}
            >
              <XIcon className="size-4" />
            </Button>
          </div>
        );
      })}

      <div className="flex items-center justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="justify-self-start"
          onClick={() =>
            onChange([
              ...filters,
              { field: 'Name', fieldDefinitionId: null, operator: 'Contains', value: '' },
            ])
          }
        >
          <PlusIcon className="size-4" />
          Thêm điều kiện
        </Button>

        {filters.length > 1 ? (
          <p className="text-muted-foreground text-xs">
            Các điều kiện nối với nhau bằng <strong>VÀ</strong>.
          </p>
        ) : null}
      </div>
    </div>
  );
}

/** Ô nhập giá trị — hình dạng đổi theo kiểu của trường đang chọn. */
function FilterValueInput({
  projectId,
  kind,
  filter,
  definitions,
  onChange,
}: {
  projectId: string;
  kind: FilterValueKind;
  filter: SavedViewFilterDto;
  definitions: FieldDefinitionResponse[];
  onChange: (value: string) => void;
}) {
  const value = filter.value ?? '';

  if (kind === 'Text')
    return (
      <Input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="Giá trị"
        className="h-8"
        aria-label="Giá trị so sánh"
      />
    );

  if (kind === 'Number')
    return (
      <Input
        type="number"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder="0"
        className="h-8"
        aria-label="Giá trị so sánh"
      />
    );

  if (kind === 'Date')
    return (
      <Input
        type="date"
        value={value.slice(0, 10)}
        onChange={(event) => onChange(event.target.value)}
        className="h-8"
        aria-label="Mốc thời gian so sánh"
      />
    );

  if (kind === 'Boolean')
    return (
      <SimpleSelect
        value={value || 'true'}
        onChange={onChange}
        label="Giá trị so sánh"
        options={[
          { value: 'true', label: 'Có' },
          { value: 'false', label: 'Không' },
        ]}
      />
    );

  if (kind === 'Enum') {
    // Gửi TÊN chứ không phải số — backend khớp theo tên vì số của một enum không ổn định
    // qua các phiên bản (bài học ADR-052).
    const values =
      filter.field === 'Priority'
        ? PRIORITY_VALUES.map((v) => ({ value: v, label: PRIORITY_LABEL[v] }))
        : CATEGORY_VALUES.map((v) => ({ value: v, label: CATEGORY_LABEL[v] }));

    return (
      <SimpleSelect
        value={value || values[0].value}
        onChange={onChange}
        label="Giá trị so sánh"
        options={values}
      />
    );
  }

  return (
    <ReferenceValueInput
      projectId={projectId}
      filter={filter}
      definitions={definitions}
      value={value}
      onChange={onChange}
    />
  );
}

const PRIORITY_LABEL: Record<string, string> = {
  Highest: 'Cao nhất',
  High: 'Cao',
  Medium: 'Trung bình',
  Low: 'Thấp',
  Lowest: 'Thấp nhất',
};

const CATEGORY_LABEL: Record<string, string> = {
  ToDo: 'Cần làm',
  InProgress: 'Đang làm',
  Done: 'Hoàn thành',
};

/**
 * Giá trị là một THAM CHIẾU — id của cột / loại việc / sprint / người / lựa chọn.
 *
 * 🔴 Gửi **id**, hiện **tên**. Gửi tên sẽ vỡ ngay lần đầu người dùng đổi tên một cột hay
 * một lựa chọn, và vỡ im lặng: bộ lọc vẫn chạy, chỉ là không khớp gì nữa.
 */
function ReferenceValueInput({
  projectId,
  filter,
  definitions,
  value,
  onChange,
}: {
  projectId: string;
  filter: SavedViewFilterDto;
  definitions: FieldDefinitionResponse[];
  value: string;
  onChange: (value: string) => void;
}) {
  const columns = useBoardColumns(filter.field === 'BoardColumn' ? projectId : null);
  const types = useWorkItemTypes(filter.field === 'WorkItemType' ? projectId : null);
  const sprints = useSprints(filter.field === 'Sprint' ? projectId : null);
  // `useMembers` không nhận null nên gọi luôn; nó dùng chung cache với tab Thành viên.
  const members = useMembers(projectId);

  let options: { value: string; label: string }[] = [];

  if (filter.fieldDefinitionId) {
    const definition = definitions.find((d) => d.id === filter.fieldDefinitionId);
    options = (definition?.options ?? []).map((o) => ({ value: o.id, label: o.label }));
  } else if (filter.field === 'BoardColumn') {
    options = (columns.data ?? []).map((c) => ({ value: c.id, label: c.name }));
  } else if (filter.field === 'WorkItemType') {
    options = (types.data ?? []).map((t) => ({ value: t.id, label: t.name }));
  } else if (filter.field === 'Sprint') {
    options = (sprints.data ?? []).map((s) => ({ value: s.id, label: s.name }));
  } else {
    options = (members.data ?? [])
      .filter((m) => m.invitationStatus === 'Accepted')
      .map((m) => ({ value: m.employeeId, label: m.employeeName }));
  }

  if (options.length === 0)
    return (
      <p className="text-muted-foreground truncate text-sm" role="status">
        Chưa có lựa chọn nào
      </p>
    );

  return (
    <SimpleSelect
      value={value || options[0].value}
      onChange={onChange}
      label="Giá trị so sánh"
      options={options}
    />
  );
}

function SimpleSelect({
  value,
  onChange,
  options,
  label,
}: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  label: string;
}) {
  return (
    <Select
      value={value}
      onValueChange={(next: string | null) => {
        if (next !== null) onChange(next);
      }}
    >
      <SelectTrigger size="sm" aria-label={label}>
        <SelectValue>
          {(current: string) => options.find((o) => o.value === current)?.label ?? '—'}
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

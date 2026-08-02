'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import type { SprintResponse } from '@/types/sprint';

/** Giá trị đại diện cho "không lọc theo sprint" — `Select` không nhận `null`. */
export const ALL_SPRINTS = 'all';

export function SprintSwitcher({
  sprints,
  isLoading,
  value,
  onChange,
}: {
  sprints: SprintResponse[] | undefined;
  isLoading: boolean;
  value: string | null;
  onChange: (sprintId: string | null) => void;
}) {
  if (isLoading) return <Skeleton className="h-8 w-56" />;

  return (
    <Select
      value={value ?? ALL_SPRINTS}
      onValueChange={(next) => onChange(next === ALL_SPRINTS ? null : next)}
    >
      <SelectTrigger size="sm" className="w-56" aria-label="Chọn sprint để xem">
        {/* ⚠️ `SelectValue` của Base UI hiện GIÁ TRỊ thô chứ không phải nhãn của item.
            Không truyền hàm định dạng thì ô này hiện "all" hoặc nguyên một guid. */}
        <SelectValue>
          {(current: string) =>
            current === ALL_SPRINTS
              ? 'Tất cả task'
              : ((sprints ?? []).find((s) => s.id === current)?.name ?? 'Tất cả task')
          }
        </SelectValue>
      </SelectTrigger>
      <SelectContent>
        {/* ⚠️ Nhãn là "Tất cả task", KHÔNG phải "Backlog".
            Bỏ `sprintId` thì backend rơi xuống `GetRootTasksByProjectAsync(projectId)`,
            tức TẤT CẢ task gốc — kể cả task đang thuộc sprint khác. Gọi nó là "Backlog"
            là hứa một bộ lọc mà nó không thực hiện. */}
        <SelectItem value={ALL_SPRINTS}>Tất cả task</SelectItem>
        {(sprints ?? []).map((sprint) => (
          <SelectItem key={sprint.id} value={sprint.id}>
            {sprint.name}
            {sprint.isActive ? ' • đang diễn ra' : ''}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

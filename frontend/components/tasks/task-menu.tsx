'use client';

import {
  ArrowRightLeftIcon,
  MoreHorizontalIcon,
  PencilIcon,
  Trash2Icon,
  UsersIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import type { SprintResponse } from '@/types/sprint';
import type { TaskSummaryResponse } from '@/types/task';

/** Token cho "Backlog" — `DropdownMenuRadioItem` không nhận `null`. */
const BACKLOG = 'backlog';

export interface TaskActions {
  onEdit: (task: TaskSummaryResponse) => void;
  onAssign: (task: TaskSummaryResponse) => void;
  onDelete: (task: TaskSummaryResponse) => void;
  onMoveToSprint: (task: TaskSummaryResponse, sprintId: string | null) => void;
}

export function TaskMenu({
  task,
  sprints,
  canManage,
  canAssign,
  isMoving,
  actions,
  size = 'icon-xs',
}: {
  task: TaskSummaryResponse;
  sprints: SprintResponse[];
  /** PM — sửa, xóa, chuyển sprint. */
  canManage: boolean;
  /** PM hoặc Member — mở được danh sách người đảm nhận. */
  canAssign: boolean;
  isMoving?: boolean;
  actions: TaskActions;
  size?: 'icon-xs' | 'icon-sm';
}) {
  if (!canManage && !canAssign) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size={size}
            aria-label={`Thao tác với ${task.name}`}
            disabled={isMoving}
            // Ngăn cú bấm chậm trên thẻ Kanban biến thành thao tác kéo. Ràng buộc
            // distance 6px của PointerSensor không đỡ được click giữ lâu.
            onPointerDown={(event) => event.stopPropagation()}
          />
        }
      >
        <MoreHorizontalIcon className="size-3.5" />
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-52">
        {canAssign ? (
          <DropdownMenuItem onClick={() => actions.onAssign(task)}>
            <UsersIcon className="size-4" />
            Người đảm nhận
          </DropdownMenuItem>
        ) : null}

        {canManage ? (
          <>
            <DropdownMenuItem onClick={() => actions.onEdit(task)}>
              <PencilIcon className="size-4" />
              Sửa task
            </DropdownMenuItem>

            <DropdownMenuSub>
              <DropdownMenuSubTrigger>
                <ArrowRightLeftIcon className="size-4" />
                Chuyển sprint
              </DropdownMenuSubTrigger>
              <DropdownMenuSubContent className="w-56">
                <DropdownMenuRadioGroup
                  value={task.sprintId ?? BACKLOG}
                  onValueChange={(value) =>
                    actions.onMoveToSprint(task, value === BACKLOG ? null : value)
                  }
                >
                  <DropdownMenuRadioItem value={BACKLOG}>Backlog</DropdownMenuRadioItem>
                  {sprints.map((sprint) => (
                    <DropdownMenuRadioItem key={sprint.id} value={sprint.id}>
                      {sprint.name}
                      {sprint.isActive ? ' • đang diễn ra' : ''}
                    </DropdownMenuRadioItem>
                  ))}
                </DropdownMenuRadioGroup>
              </DropdownMenuSubContent>
            </DropdownMenuSub>

            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={() => actions.onDelete(task)}>
              <Trash2Icon className="size-4" />
              Xóa task
            </DropdownMenuItem>
          </>
        ) : null}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

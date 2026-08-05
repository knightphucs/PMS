import { describe, expect, it } from 'vitest';

import { findTaskInBoard, moveTaskInBoard, patchTaskInBoard } from './board-cache';

import type { BoardResponse, TaskStatusRef, TaskSummaryResponse } from '@/types/task';

let nextNumber = 1;
const statuses: Record<string, TaskStatusRef> = {
  todo: { columnId: 'todo', name: 'Cần làm', color: '#64748b', category: 'ToDo' },
  progress: { columnId: 'progress', name: 'Đang làm', color: '#3b82f6', category: 'InProgress' },
  review: { columnId: 'review', name: 'Đang duyệt', color: '#f59e0b', category: 'InProgress' },
  done: { columnId: 'done', name: 'Hoàn thành', color: '#10b981', category: 'Done' },
};

const task = (id: string, status: TaskStatusRef, over = false): TaskSummaryResponse => {
  const number = nextNumber++;
  return {
    id, number, code: `PMS-${number}`, name: `Task ${id}`, status, priority: 'Medium',
    dueDate: '2026-01-01T00:00:00Z', isOverdue: over, sprintId: null, parentTaskId: null,
    subtaskProgress: 40, assignees: [], labels: [],
  };
};

const board = (): BoardResponse => ({
  projectId: 'p1', sprintId: null,
  columns: Object.values(statuses).map((status, index) => ({
    column: { id: status.columnId, name: status.name, color: status.color, order: index, category: status.category, taskCount: 0 },
    tasks: status.columnId === 'todo'
      ? [task('a', status), task('b', status, true)]
      : status.columnId === 'progress' ? [task('c', status)] : [],
  })),
});

const idsIn = (b: BoardResponse, columnId: string) =>
  b.columns.find((group) => group.column.id === columnId)!.tasks.map((t) => t.id);

describe('moveTaskInBoard', () => {
  it('gỡ khỏi cột nguồn và thêm vào cuối cột đích', () => {
    const next = moveTaskInBoard(board(), 'a', 'progress');
    expect(idsIn(next, 'todo')).toEqual(['b']);
    expect(idsIn(next, 'progress')).toEqual(['c', 'a']);
    expect(findTaskInBoard(next, 'a')!.status.columnId).toBe('progress');
  });

  it('giữ nguyên board cũ để rollback', () => {
    const previous = board();
    expect(moveTaskInBoard(previous, 'a', 'progress')).not.toBe(previous);
    expect(idsIn(previous, 'todo')).toEqual(['a', 'b']);
  });

  it('không đổi cache với task/cột không có hoặc cột hiện tại', () => {
    const previous = board();
    expect(moveTaskInBoard(previous, 'missing', 'done')).toBe(previous);
    expect(moveTaskInBoard(previous, 'a', 'todo')).toBe(previous);
    expect(moveTaskInBoard(previous, 'a', 'missing')).toBe(previous);
  });
});

describe('patchTaskInBoard', () => {
  it('thay thẻ tại chỗ và chuyển thẻ khi server trả về cột khác', () => {
    const locallyPatched = patchTaskInBoard(board(), { ...task('b', statuses.todo), name: 'Tên mới' });
    expect(findTaskInBoard(locallyPatched, 'b')!.name).toBe('Tên mới');

    const moved = patchTaskInBoard(board(), task('a', statuses.review));
    expect(idsIn(moved, 'todo')).toEqual(['b']);
    expect(idsIn(moved, 'review')).toEqual(['a']);
  });
});

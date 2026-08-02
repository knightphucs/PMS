import { describe, expect, it } from 'vitest';

import { findTaskInBoard, moveTaskInBoard, patchTaskInBoard } from './board-cache';

import type { Status } from '@/types/enums';
import type { BoardResponse, TaskSummaryResponse } from '@/types/task';

const task = (id: string, status: Status, over = false): TaskSummaryResponse => ({
  id,
  name: `Task ${id}`,
  status,
  priority: 'Medium',
  dueDate: '2026-01-01T00:00:00Z',
  isOverdue: over,
  sprintId: null,
  parentTaskId: null,
  subtaskProgress: 40,
  assignees: [],
});

/** Board luôn đủ 4 cột kể cả cột rỗng — giống hệt backend. */
const board = (): BoardResponse => ({
  projectId: 'p1',
  sprintId: null,
  columns: [
    { status: 'ToDo', tasks: [task('a', 'ToDo'), task('b', 'ToDo', true)] },
    { status: 'InProgress', tasks: [task('c', 'InProgress')] },
    { status: 'Review', tasks: [] },
    { status: 'Done', tasks: [] },
  ],
});

const idsIn = (b: BoardResponse, status: Status) =>
  b.columns.find((c) => c.status === status)!.tasks.map((t) => t.id);

describe('moveTaskInBoard', () => {
  it('gỡ khỏi cột nguồn và thêm vào CUỐI cột đích', () => {
    const next = moveTaskInBoard(board(), 'a', 'InProgress');

    expect(idsIn(next, 'ToDo')).toEqual(['b']);
    expect(idsIn(next, 'InProgress')).toEqual(['c', 'a']);
    expect(findTaskInBoard(next, 'a')!.status).toBe('InProgress');
  });

  it('giữ nguyên board cũ (không đột biến) — TanStack cần tham chiếu mới', () => {
    const truoc = board();
    const sau = moveTaskInBoard(truoc, 'a', 'InProgress');

    expect(sau).not.toBe(truoc);
    expect(idsIn(truoc, 'ToDo')).toEqual(['a', 'b']); // bản gốc còn nguyên để rollback
  });

  it('luôn giữ đủ 4 cột', () => {
    expect(moveTaskInBoard(board(), 'a', 'InProgress').columns).toHaveLength(4);
  });

  it('chuyển sang Done thì isOverdue thành false', () => {
    // TaskItem.IsOverdue có `&& Status != Status.Done` — đây là trường tính sẵn DUY NHẤT
    // được phép suy lại ở client.
    const next = moveTaskInBoard(board(), 'b', 'InProgress');
    expect(findTaskInBoard(next, 'b')!.isOverdue).toBe(true); // chưa Done thì giữ nguyên

    const done = moveTaskInBoard(next, 'b', 'Review');
    expect(findTaskInBoard(moveTaskInBoard(done, 'b', 'Done'), 'b')!.isOverdue).toBe(false);
  });

  it('KHÔNG tự tính lại subtaskProgress', () => {
    // Không có luật nào cho phép suy ra giá trị mới ở client; onSettled sẽ chữa lành.
    const next = moveTaskInBoard(board(), 'a', 'InProgress');
    expect(findTaskInBoard(next, 'a')!.subtaskProgress).toBe(40);
  });

  it('task không tồn tại hoặc thả về đúng cột đang đứng -> trả về CHÍNH board cũ', () => {
    const truoc = board();
    expect(moveTaskInBoard(truoc, 'khong-co', 'Done')).toBe(truoc);
    expect(moveTaskInBoard(truoc, 'a', 'ToDo')).toBe(truoc);
  });
});

describe('patchTaskInBoard', () => {
  it('thay thẻ tại chỗ, GIỮ NGUYÊN vị trí trong cột', () => {
    const server = { ...task('b', 'ToDo'), subtaskProgress: 100, name: 'Tên mới từ server' };
    const next = patchTaskInBoard(board(), server);

    expect(idsIn(next, 'ToDo')).toEqual(['a', 'b']); // thứ tự không đổi
    expect(findTaskInBoard(next, 'b')!.subtaskProgress).toBe(100);
    expect(findTaskInBoard(next, 'b')!.name).toBe('Tên mới từ server');
  });

  it('server báo status khác chỗ thẻ đang nằm -> chuyển về đúng cột', () => {
    // Xảy ra khi người khác vừa đổi status task đó giữa chừng.
    const server = task('a', 'Review');
    const next = patchTaskInBoard(board(), server);

    expect(idsIn(next, 'ToDo')).toEqual(['b']);
    expect(idsIn(next, 'Review')).toEqual(['a']);
  });

  it('task không có trên board -> không đụng gì', () => {
    const truoc = board();
    expect(patchTaskInBoard(truoc, task('z', 'Done'))).toBe(truoc);
  });
});

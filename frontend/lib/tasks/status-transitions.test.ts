import { describe, expect, it } from 'vitest';

import { ALLOWED_TRANSITIONS, canTransition, mayFailUnpredictably } from './status-transitions';

import type { Status } from '@/types/enums';

const ALL: Status[] = ['ToDo', 'InProgress', 'Review', 'Done'];

/**
 * Bảng này soi gương `TaskItem.CanTransitionTo` (PMS.Domain/Entities/TaskItem.cs:77).
 * Nếu backend đổi state machine mà quên đồng bộ sang đây, board sẽ chặn nhầm (mất chức
 * năng) hoặc cho thả rồi ăn 409 (toast đỏ vô cớ) — cả hai đều không có gì báo động.
 */
describe('canTransition — sáu bước hợp lệ, đúng bằng backend', () => {
  it.each([
    ['ToDo', 'InProgress'],
    ['InProgress', 'Review'],
    ['InProgress', 'ToDo'],
    ['Review', 'Done'],
    ['Review', 'InProgress'],
    ['Done', 'Review'],
  ] as const)('%s -> %s hợp lệ', (from, to) => {
    expect(canTransition(from, to)).toBe(true);
  });

  it('CHÍNH XÁC sáu cặp hợp lệ, không hơn không kém', () => {
    const hopLe = ALL.flatMap((from) =>
      ALL.filter((to) => canTransition(from, to)).map((to) => `${from}->${to}`),
    );

    expect(hopLe).toHaveLength(6);
  });
});

describe('canTransition — những bước KHÔNG hợp lệ dễ làm sai nhất', () => {
  it('thả về ĐÚNG CỘT ĐANG ĐỨNG luôn bị chặn (state machine từ chối "đứng yên")', () => {
    // Không chặn ở client thì mỗi lần người dùng đổi ý giữa chừng là một toast đỏ.
    for (const status of ALL) {
      expect(canTransition(status, status)).toBe(false);
    }
  });

  it.each([
    ['ToDo', 'Done'],
    ['ToDo', 'Review'],
    ['InProgress', 'Done'],
    ['Done', 'ToDo'],
    ['Done', 'InProgress'],
    ['Review', 'ToDo'],
  ] as const)('%s -> %s bị chặn', (from, to) => {
    expect(canTransition(from, to)).toBe(false);
  });

  it('KHÔNG phải quy tắc "cột kề" — docs/frontend-next-session.md §6 ghi sai', () => {
    const thuTu: Status[] = ['ToDo', 'InProgress', 'Review', 'Done'];
    const laKe = (a: Status, b: Status) => Math.abs(thuTu.indexOf(a) - thuTu.indexOf(b)) === 1;

    // Hai phản ví dụ theo hai chiều ngược nhau. Cài theo "cột kề" là hỏng cả hai.
    expect(laKe('ToDo', 'Review')).toBe(false);
    expect(canTransition('ToDo', 'Review')).toBe(false);

    expect(laKe('Done', 'Review')).toBe(true);
    expect(canTransition('Done', 'Review')).toBe(true); // bước LÙI, vẫn hợp lệ
  });

  it('mọi trạng thái đều có ít nhất một đường đi ra (không có ngõ cụt)', () => {
    for (const status of ALL) {
      expect(ALLOWED_TRANSITIONS[status].length).toBeGreaterThan(0);
    }
  });
});

describe('mayFailUnpredictably', () => {
  it('chỉ đích InProgress mới có thể ăn 409 do bị TaskLink chặn', () => {
    // TaskStatusTransitionService chỉ gọi EnsureNotBlockedAsync cho nhánh InProgress.
    expect(mayFailUnpredictably('InProgress')).toBe(true);
    expect(mayFailUnpredictably('ToDo')).toBe(false);
    expect(mayFailUnpredictably('Review')).toBe(false);
    expect(mayFailUnpredictably('Done')).toBe(false);
  });
});

import { describe, expect, it } from 'vitest';

import { mayFailUnpredictably } from './status-transitions';

import type { StatusCategory } from '@/types/task';

/**
 * 🗑️ Hai khối `describe` về `canTransition` đã XÓA cùng ADR-052 — không phải vì chúng
 * hỏng, mà vì thứ chúng khóa đã không còn tồn tại.
 *
 * Chúng soi gương `TaskItem.CanTransitionTo`, một ma trận sáu-cặp-hợp-lệ. Với cột do NGƯỜI
 * DÙNG tạo thì không còn cơ sở nào để nói cặp nào hợp lệ: hệ thống không biết "Chờ QA"
 * đứng trước hay sau "Đang sửa". Giữ lại một bảng luật ở client sẽ chặn đúng những nước đi
 * mà backend cho phép, và người dùng không có cách nào biết vì sao.
 *
 * Trong đó có một test đáng tiếc phải bỏ: *"KHÔNG phải quy tắc cột kề"* — nó tồn tại để
 * chống lại một câu SAI trong tài liệu. Câu đó nay cũng hết hiệu lực theo.
 */
describe('mayFailUnpredictably — cờ duy nhất còn lại của state machine cũ', () => {
  it('chỉ NHÓM InProgress mới có thể ăn 409 do bị TaskLink chặn', () => {
    // `TaskStatusTransitionService` gọi `EnsureNotBlockedAsync` khi Category của cột đích
    // là InProgress. Mọi nhóm khác đoán trước được trọn vẹn.
    expect(mayFailUnpredictably('InProgress')).toBe(true);
    expect(mayFailUnpredictably('ToDo')).toBe(false);
    expect(mayFailUnpredictably('Done')).toBe(false);
  });

  it('🔴 kiểm theo NHÓM chứ không theo TÊN cột', () => {
    // Điểm dễ sai nhất sau ADR-052: một cột người dùng đặt tên "Chờ QA" hay "Đang sửa" đều
    // thuộc nhóm InProgress và đều phải được guard này bảo vệ. Cài theo tên thì sẽ trượt
    // ngay lần đầu ai đó đổi cấu hình board — mà đó chính là tính năng vừa dựng.
    const cacNhom: StatusCategory[] = ['ToDo', 'InProgress', 'Done'];
    const canDeDo = cacNhom.filter(mayFailUnpredictably);

    expect(canDeDo).toEqual(['InProgress']);
  });
});

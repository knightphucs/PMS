import { describe, expect, it } from 'vitest';

import {
  canChangeTaskStatus,
  canComment,
  canDeleteComment,
  canEditComment,
  canManageMembers,
  canManageSprints,
  canManageTasks,
  canSelfAssign,
} from './permissions';

import type { RoleInProject } from '@/types/enums';

const VAI_TRO: (RoleInProject | null)[] = ['ProjectManager', 'Member', 'Viewer', null];

describe('ma trận quyền — soi gương ProjectPermissions.cs', () => {
  it('chỉ ProjectManager quản lý được project, thành viên, sprint và task', () => {
    for (const guard of [canManageMembers, canManageSprints, canManageTasks]) {
      expect(guard('ProjectManager')).toBe(true);
      expect(guard('Member')).toBe(false);
      expect(guard('Viewer')).toBe(false);
      expect(guard(null)).toBe(false);
    }
  });

  it('Member tự nhận việc và bình luận được, Viewer thì không', () => {
    for (const guard of [canSelfAssign, canComment]) {
      expect(guard('ProjectManager')).toBe(true);
      expect(guard('Member')).toBe(true);
      expect(guard('Viewer')).toBe(false);
    }
  });

  it('role null (đang tải HOẶC không phải thành viên) không được cấp quyền gì', () => {
    // Trả `true` khi chưa biết vai trò là hiện nút rồi giấu đi ở render kế tiếp —
    // tệ hơn là để người dùng bấm vào một thứ chắc chắn sẽ 403.
    for (const guard of [canManageTasks, canManageMembers, canSelfAssign, canComment]) {
      expect(guard(null)).toBe(false);
    }
    expect(canChangeTaskStatus(null, true)).toBe(false);
  });
});

describe('canChangeTaskStatus — ADR-017, luật per-row', () => {
  it('ProjectManager đổi được status của MỌI task, kể cả task không do mình làm', () => {
    expect(canChangeTaskStatus('ProjectManager', false)).toBe(true);
    expect(canChangeTaskStatus('ProjectManager', true)).toBe(true);
  });

  it('Member CHỈ đổi được status task mình được giao', () => {
    // Đây là quyền mà cách làm "chỉ PM kéo-thả được" sẽ âm thầm lấy mất.
    expect(canChangeTaskStatus('Member', true)).toBe(true);
    expect(canChangeTaskStatus('Member', false)).toBe(false);
  });

  it('Viewer không bao giờ được, kể cả khi lọt vào danh sách assignee', () => {
    expect(canChangeTaskStatus('Viewer', true)).toBe(false);
  });
});

describe('quyền comment — ADR-026, chỗ dễ nhầm nhất', () => {
  it('SỬA comment chỉ tác giả — ProjectManager cũng KHÔNG', () => {
    // Mọi quyền khác PM đều override được, riêng cái này thì không.
    expect(canEditComment(true)).toBe(true);
    expect(canEditComment(false)).toBe(false);
  });

  it('XÓA comment thì tác giả HOẶC ProjectManager', () => {
    expect(canDeleteComment('Member', true)).toBe(true);
    expect(canDeleteComment('ProjectManager', false)).toBe(true);
    expect(canDeleteComment('Member', false)).toBe(false);
    expect(canDeleteComment('Viewer', false)).toBe(false);
  });

  it('sửa và xóa KHÔNG cùng một luật', () => {
    // Nếu ai đó gộp hai hàm này làm một thì test này đỏ.
    const pmKhongPhaiTacGia = VAI_TRO.filter((r) => r === 'ProjectManager');
    for (const role of pmKhongPhaiTacGia) {
      expect(canEditComment(false)).toBe(false);
      expect(canDeleteComment(role, false)).toBe(true);
    }
  });
});

import { describe, expect, it } from 'vitest';

import { appendMention, reconcileMentions, type MentionCandidate } from './mentions';

const NAM: MentionCandidate = { id: 'id-nam', name: 'Nguyễn Văn Nam' };
const LAN: MentionCandidate = { id: 'id-lan', name: 'Trần Thị Lan' };

describe('appendMention', () => {
  it('chèn vào nháp rỗng kèm khoảng trắng cuối', () => {
    expect(appendMention('', NAM.name)).toBe('@Nguyễn Văn Nam ');
  });

  it('tự thêm dấu cách khi nháp đang kết thúc bằng ký tự thường', () => {
    expect(appendMention('Nhờ', NAM.name)).toBe('Nhờ @Nguyễn Văn Nam ');
  });

  it('không thêm dấu cách thừa khi nháp đã kết thúc bằng khoảng trắng', () => {
    expect(appendMention('Nhờ ', NAM.name)).toBe('Nhờ @Nguyễn Văn Nam ');
  });

  it('coi xuống dòng là khoảng trắng', () => {
    expect(appendMention('Dòng một\n', NAM.name)).toBe('Dòng một\n@Nguyễn Văn Nam ');
  });
});

describe('reconcileMentions', () => {
  it('giữ id của người còn được nhắc trong nội dung', () => {
    expect(reconcileMentions('@Nguyễn Văn Nam xem giúp nhé', [NAM])).toEqual([NAM.id]);
  });

  it('🔴 BỎ id khi người dùng đã xóa chữ @Tên trước lúc gửi', () => {
    // Đây là lý do tồn tại của cả module: không có bước này thì một bình luận không nhắc ai
    // vẫn bắn thông báo "bạn được nhắc tới".
    expect(reconcileMentions('xem giúp nhé', [NAM])).toEqual([]);
  });

  it('lọc đúng người khi chọn nhiều mà chỉ giữ lại một', () => {
    expect(reconcileMentions('@Trần Thị Lan xem giúp', [NAM, LAN])).toEqual([LAN.id]);
  });

  it('khử trùng lặp khi cùng một người được nhắc hai lần', () => {
    expect(reconcileMentions('@Nguyễn Văn Nam và @Nguyễn Văn Nam', [NAM, NAM])).toEqual([NAM.id]);
  });

  it('trả mảng rỗng khi chưa chọn ai, kể cả nội dung có chứa ký tự @', () => {
    // Server KHÔNG parse `@tên` — `@abc` ở đây chỉ là một mẩu email, không phải lượt nhắc.
    expect(reconcileMentions('gửi tới abc@congty.com nhé', [])).toEqual([]);
  });

  it('giữ nguyên thứ tự đã chọn', () => {
    expect(reconcileMentions('@Trần Thị Lan @Nguyễn Văn Nam', [NAM, LAN])).toEqual([
      NAM.id,
      LAN.id,
    ]);
  });
});

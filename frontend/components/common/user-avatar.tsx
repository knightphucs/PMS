import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { initials } from '@/lib/format';
import { cn } from '@/lib/utils';

/**
 * Tám tông màu, đủ tương phản ở cả hai chế độ.
 *
 * Viết bằng giá trị oklch tùy ý thay vì `bg-blue-100` để chúng nằm cùng một không gian
 * màu với token thương hiệu — bảng màu mặc định của Tailwind trộn với oklch tự đặt sẽ
 * lệch độ bão hòa thấy rõ khi các avatar xếp cạnh nhau.
 */
const TONES = [
  'bg-[oklch(0.93_0.05_25)] text-[oklch(0.42_0.15_25)] dark:bg-[oklch(0.32_0.07_25)] dark:text-[oklch(0.86_0.09_25)]',
  'bg-[oklch(0.93_0.05_60)] text-[oklch(0.42_0.13_60)] dark:bg-[oklch(0.32_0.07_60)] dark:text-[oklch(0.86_0.09_60)]',
  'bg-[oklch(0.93_0.05_145)] text-[oklch(0.40_0.12_145)] dark:bg-[oklch(0.31_0.07_145)] dark:text-[oklch(0.85_0.09_145)]',
  'bg-[oklch(0.93_0.05_195)] text-[oklch(0.40_0.10_195)] dark:bg-[oklch(0.31_0.06_195)] dark:text-[oklch(0.85_0.08_195)]',
  'bg-[oklch(0.93_0.05_235)] text-[oklch(0.42_0.14_235)] dark:bg-[oklch(0.32_0.07_235)] dark:text-[oklch(0.86_0.09_235)]',
  'bg-[oklch(0.93_0.05_275)] text-[oklch(0.42_0.15_275)] dark:bg-[oklch(0.32_0.08_275)] dark:text-[oklch(0.86_0.10_275)]',
  'bg-[oklch(0.93_0.05_320)] text-[oklch(0.42_0.14_320)] dark:bg-[oklch(0.32_0.07_320)] dark:text-[oklch(0.86_0.09_320)]',
  'bg-[oklch(0.93_0.05_350)] text-[oklch(0.42_0.14_350)] dark:bg-[oklch(0.32_0.07_350)] dark:text-[oklch(0.86_0.09_350)]',
] as const;

/**
 * Băm theo **id**, KHÔNG theo tên.
 *
 * Hai lý do, cả hai đều gặp thật: đổi tên hiển thị không được làm avatar đổi màu, và hai
 * người trùng tên (rất thường gặp với tên tiếng Việt) phải phân biệt được bằng màu.
 *
 * `Math.imul` để phép nhân giữ nguyên ngữ nghĩa 32-bit — nhân thường sẽ tràn sang số
 * thực và làm phân phối bị dồn cục.
 */
function toneIndex(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i += 1) {
    hash = (Math.imul(hash, 31) + id.charCodeAt(i)) >>> 0;
  }
  return hash % TONES.length;
}

export function UserAvatar({
  id,
  name,
  size = 'default',
  className,
}: {
  id: string;
  name: string;
  size?: 'sm' | 'default' | 'lg';
  className?: string;
}) {
  return (
    <Avatar size={size} className={className} title={name}>
      <AvatarFallback className={cn('font-medium', TONES[toneIndex(id)])}>
        {initials(name)}
      </AvatarFallback>
    </Avatar>
  );
}

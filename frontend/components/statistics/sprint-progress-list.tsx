'use client';

import { Badge } from '@/components/ui/badge';
import { formatDateRange } from '@/lib/format';
import type { SprintProgress } from '@/types/statistics';

/**
 * Tiến độ từng sprint — danh sách **thanh mức**, không phải biểu đồ.
 *
 * Mỗi dòng là một tỷ lệ so với mốc của chính nó (bao nhiêu task đã xong trên tổng của sprint
 * đó), và các sprint không so được với nhau vì phạm vi khác nhau. Vẽ chúng thành một biểu đồ
 * cột chung sẽ ngầm mời người đọc so sánh chiều cao — tức là mời họ đọc sai.
 */
export function SprintProgressList({ sprints }: { sprints: SprintProgress[] }) {
  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div>
        <h2 className="text-sm font-semibold">Tiến độ sprint</h2>
        <p className="text-muted-foreground text-xs">
          Mỗi sprint tính theo phạm vi của riêng nó, nên các thanh không dùng để so với nhau.
        </p>
      </div>

      <ul className="grid gap-3">
        {sprints.map((sprint) => {
          const percent = sprint.total === 0 ? 0 : (sprint.done / sprint.total) * 100;

          return (
            <li key={sprint.sprintId} className="grid gap-1.5">
              <div className="flex min-w-0 flex-wrap items-center gap-2">
                <span className="min-w-0 flex-1 truncate text-[13px] font-medium">
                  {sprint.name}
                </span>
                {sprint.isActive ? <Badge>Đang chạy</Badge> : null}
                <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                  {sprint.done}/{sprint.total}
                </span>
              </div>

              <div
                role="meter"
                aria-valuenow={Math.round(percent)}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label={`Tiến độ ${sprint.name}`}
                // 🔴 Rãnh nền TRUNG TÍNH, không phải một bậc của thang màu. Đã thử
                // `--viz-seq-1` và nhìn trên màn hình thì sai hẳn nghĩa: ở chế độ tối bậc đó
                // là một màu xanh khá đậm, nên sprint "0/2" hiện ra một thanh xanh ĐẦY
                // chiều ngang — đọc thành đã hoàn thành 100%. Chỉ phần đã đầy mới được mang màu.
                className="bg-muted h-2 overflow-hidden rounded-full"
              >
                <div
                  className="h-full rounded-full"
                  style={{
                    width: `${percent}%`,
                    // Cùng sắc với rãnh nền (thang tuần tự), không đổi màu theo phần trăm:
                    // đổi màu sẽ ngụ ý một ngưỡng "tốt/xấu" mà dữ liệu này không hề có.
                    backgroundColor: 'var(--viz-seq-4)',
                  }}
                />
              </div>

              <p className="text-muted-foreground text-xs">
                {formatDateRange(sprint.startDate, sprint.endDate)}
              </p>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

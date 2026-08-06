'use client';

import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { formatDateRange, formatShortDate } from '@/lib/format';
import { cn } from '@/lib/utils';
import type { SprintTimelinePoint } from '@/types/report';

const DAY_MS = 24 * 60 * 60 * 1000;

/** Số mốc trên trục ngày — đủ để đọc nhịp thời gian mà không dày đặc trên khung hẹp (375px). */
const AXIS_TICK_COUNT = 5;

/**
 * Năm trạng thái hiển thị — KHÔNG phải bốn (bằng đúng số giá trị của `SprintStatus`), vì
 * `Active` và `Completed` mỗi cái còn tách đôi theo một điều kiện phụ:
 *
 * - `Active` tách theo `isOverdue` (server tính — xem `SprintDtos.cs`): đang chạy đúng hạn
 *   hay đã quá `endDate` mà chưa đóng sổ.
 * - `Completed` tách theo `done === total`: đóng sổ trọn vẹn, hay đóng sổ mà vẫn còn task
 *   chưa xong (ADR-050 cho phép đóng sprint dù còn việc dở — task đó bị đẩy đi nơi khác
 *   hoặc ở lại tuỳ lựa chọn lúc đóng).
 *
 * 🔴 **Không suy "quá hạn" bằng cách so `startDate`/`endDate` ở đây** — đã thử và sai
 * (2026-08-06): một sprint được bấm chạy SỚM hơn kế hoạch cũng "chưa qua ngày" giống hệt
 * bề ngoài nhưng không hề quá hạn. Bắt buộc đọc `sprint.isOverdue` do server tính.
 */
function timelineTone(sprint: SprintTimelinePoint): { tone: string; label: string } {
  if (sprint.status === 'Planned') {
    return { tone: 'var(--viz-status-todo)', label: 'Chưa bắt đầu' };
  }

  if (sprint.status === 'Active') {
    return sprint.isOverdue
      ? { tone: 'var(--viz-load-overdue)', label: 'Quá hạn, chưa đóng' }
      : { tone: 'var(--viz-status-inprogress)', label: 'Đang chạy' };
  }

  return sprint.done < sprint.total
    ? { tone: 'var(--viz-status-review)', label: 'Đã đóng — còn task chưa xong' }
    : { tone: 'var(--viz-status-done)', label: 'Đã đóng' };
}

const LEGEND: { tone: string; label: string }[] = [
  { tone: 'var(--viz-status-todo)', label: 'Chưa bắt đầu' },
  { tone: 'var(--viz-status-inprogress)', label: 'Đang chạy' },
  { tone: 'var(--viz-status-done)', label: 'Đã đóng' },
  { tone: 'var(--viz-load-overdue)', label: 'Quá hạn, chưa đóng' },
  { tone: 'var(--viz-status-review)', label: 'Đã đóng, còn task chưa xong' },
];

/**
 * Timeline kiểu Jira roadmap — MỌI sprint (kể cả chưa bắt đầu) trên MỘT trục thời gian
 * chung, khác hẳn `SprintProgressList` của tab Thống kê: ở đó mỗi thanh tự co theo phạm vi
 * của chính nó nên KHÔNG so được sprint này với sprint khác; ở đây vị trí và độ dài của
 * thanh phản ánh đúng ngày thật, nên xem được sprint nào chồng lịch, sprint nào cách quãng.
 *
 * 📌 Năm màu, không phải ba: xem {@link timelineTone} để biết vì sao `Active`/`Completed`
 * mỗi cái tách đôi theo một điều kiện phụ (quá hạn / còn task dở). Bốn trong năm màu mượn
 * lại đúng token `--viz-status-*`/`--viz-load-overdue` đã có (cùng nghĩa hệt board và các
 * biểu đồ khác); chỉ "còn task dở" mượn `--viz-status-review` (vốn chưa ai dùng tới).
 *
 * 📌 Trục ngày + lưới dọc dùng CHUNG hệ toạ độ phần trăm với các thanh sprint bên dưới: cả
 * hai đều là con trực tiếp của cùng một khối `relative` nên một mốc ở `left: 30%` luôn thẳng
 * hàng ở mọi hàng, bất kể hàng đó có bao nhiêu sprint.
 */
export function SprintTimelineChart({ sprints }: { sprints: SprintTimelinePoint[] }) {
  const starts = sprints.map((s) => new Date(s.startDate).getTime());
  const ends = sprints.map((s) => new Date(s.endDate).getTime());
  const rangeStart = Math.min(...starts);
  // Tối thiểu 1 ngày: một sprint bắt đầu và kết thúc cùng lúc (dữ liệu lỗi/test) không
  // được làm mẫu số bằng 0 rồi chia cho 0 ra NaN%.
  const rangeEnd = Math.max(...ends, rangeStart + DAY_MS);
  const span = rangeEnd - rangeStart;

  const today = Date.now();
  const todayPercent = today >= rangeStart && today <= rangeEnd
    ? ((today - rangeStart) / span) * 100
    : null;

  const ticks = Array.from({ length: AXIS_TICK_COUNT }, (_, i) => {
    const percent = (i / (AXIS_TICK_COUNT - 1)) * 100;
    const time = rangeStart + (span * i) / (AXIS_TICK_COUNT - 1);
    return { key: time, percent, label: formatShortDate(new Date(time).toISOString()) };
  });

  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div className="grid gap-2">
        <div>
          <h2 className="text-sm font-semibold">Timeline</h2>
          <p className="text-muted-foreground text-xs">
            Vị trí và độ dài mỗi thanh đúng theo ngày thật — so được sprint này với sprint
            khác, khác thanh tự co ở tab Thống kê. Chĩa chuột vào một thanh để xem chi tiết.
          </p>
        </div>

        {/* Chú giải màu — BẮT BUỘC ở đây, khác các biểu đồ đơn sắc khác trong dự án: màu ở
            đó chỉ lặp lại nhãn trục, còn ở đây màu tải một thông tin THẬT không có ở đâu
            khác trên thanh (trạng thái vòng đời + có quá hạn/còn dở hay không). */}
        <ul className="flex flex-wrap gap-x-3 gap-y-1">
          {LEGEND.map((item) => (
            <li key={item.label} className="text-muted-foreground flex items-center gap-1.5 text-[11px]">
              <span className="size-2 shrink-0 rounded-full" style={{ backgroundColor: item.tone }} />
              {item.label}
            </li>
          ))}
        </ul>
      </div>

      <div className="grid min-w-0 gap-1">
        {/* Trục ngày tháng — mốc đầu/cuối neo theo mép để không tràn ra ngoài khung. */}
        <div className="text-muted-foreground relative h-4 text-[11px] tabular-nums">
          {ticks.map((tick, i) => (
            <span
              key={tick.key}
              className={cn(
                'absolute',
                i === 0 ? 'left-0' : i === ticks.length - 1 ? 'right-0' : '-translate-x-1/2',
              )}
              style={i > 0 && i < ticks.length - 1 ? { left: `${tick.percent}%` } : undefined}
            >
              {tick.label}
            </span>
          ))}
        </div>

        {/* Lưới dọc + danh sách sprint dùng CHUNG một khối `relative`: lưới cao bằng đúng
            nội dung `<ul>` (bám theo `inset-0`), không phải một con số đo tay. */}
        <div className="relative min-w-0">
          <div className="pointer-events-none absolute inset-0" aria-hidden>
            {ticks.slice(1, -1).map((tick) => (
              <div
                key={tick.key}
                className="absolute top-0 bottom-0 w-px"
                style={{ left: `${tick.percent}%`, backgroundColor: 'var(--viz-grid)' }}
              />
            ))}
            {todayPercent !== null ? (
              <div
                className="bg-foreground/40 absolute top-0 bottom-0 w-px"
                style={{ left: `${todayPercent}%` }}
              />
            ) : null}
          </div>

          <ul className="relative grid min-w-0 gap-3">
            {sprints.map((sprint) => {
              const start = new Date(sprint.startDate).getTime();
              const end = new Date(sprint.endDate).getTime();
              const left = ((start - rangeStart) / span) * 100;
              // Tối thiểu 2%: một sprint ngắn so với toàn trục vẫn phải còn nhìn thấy được,
              // không teo thành một sợi chỉ không bấm/hover nổi.
              const width = Math.max(((end - start) / span) * 100, 2);
              const donePercent = sprint.total === 0 ? 0 : (sprint.done / sprint.total) * 100;
              const { tone, label } = timelineTone(sprint);

              return (
                <li key={sprint.sprintId} className="grid min-w-0 gap-1">
                  <div className="flex min-w-0 items-baseline justify-between gap-2">
                    <span className="min-w-0 truncate text-[13px] font-medium">{sprint.name}</span>
                    <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                      {formatDateRange(sprint.startDate, sprint.endDate)}
                    </span>
                  </div>

                  <div className="relative h-6 min-w-0">
                    <Tooltip>
                      <TooltipTrigger
                        render={
                          <div
                            role="meter"
                            aria-valuenow={Math.round(donePercent)}
                            aria-valuemin={0}
                            aria-valuemax={100}
                            aria-label={`${sprint.name} — ${label}`}
                            className="absolute top-0 h-full rounded-md"
                            style={{
                              left: `${left}%`,
                              width: `${width}%`,
                              backgroundColor: tone,
                            }}
                          />
                        }
                      />
                      <TooltipContent>
                        <div className="grid gap-0.5">
                          <p className="font-medium">{sprint.name}</p>
                          <p className="text-background/80">
                            {label} · {formatDateRange(sprint.startDate, sprint.endDate)}
                          </p>
                          <p className="text-background/80 tabular-nums">
                            {sprint.done}/{sprint.total} task Xong
                            {sprint.total > 0 ? ` (${Math.round(donePercent)}%)` : ''}
                          </p>
                        </div>
                      </TooltipContent>
                    </Tooltip>
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      </div>

      {todayPercent !== null ? (
        <p className="text-muted-foreground text-xs">
          <span aria-hidden>┃</span> Vạch đứng là hôm nay.
        </p>
      ) : null}
    </section>
  );
}

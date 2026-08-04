'use client';

import { UserAvatar } from '@/components/common/user-avatar';
import type { AssigneeWorkload } from '@/types/statistics';

const SEGMENTS = [
  { key: 'done', label: 'Xong', color: 'var(--viz-load-done)' },
  { key: 'active', label: 'Đang làm', color: 'var(--viz-load-active)' },
  { key: 'overdue', label: 'Quá hạn', color: 'var(--viz-load-overdue)' },
] as const;

/**
 * Khối lượng theo người — thanh chồng lớp NGANG.
 *
 * 📌 Nằm ngang chứ không dọc: tên người dài và số người thì thay đổi, cột dọc sẽ xoay nhãn
 * hoặc cắt chữ. Ngang thì tên đọc thẳng và danh sách dài chỉ việc cuộn xuống.
 *
 * 📌 Dựng bằng HTML thuần chứ không phải Recharts. Mỗi hàng là một thanh phần-trên-tổng có
 * avatar và số ở hai đầu — thứ đó là BỐ CỤC, và một thư viện biểu đồ ở đây chỉ thêm một
 * lớp trung gian mà không giải quyết gì.
 *
 * 🔴 Đây là bộ màu DUY NHẤT trong cả dashboard mà người đọc buộc phải phân biệt bằng màu,
 * nên nó đã chạy qua validator ở cả hai chế độ (xem chú thích ở `globals.css`). Chế độ sáng
 * có một cảnh báo tương phản dưới 3:1 → bắt buộc phải có **nhãn số hiện rõ**, và đó là lý do
 * mỗi hàng luôn in "x/y" bên phải thay vì chỉ tô màu.
 */
export function AssigneeWorkloadChart({ data }: { data: AssigneeWorkload[] }) {
  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div>
        <h2 className="text-sm font-semibold">Khối lượng theo người</h2>
        <p className="text-muted-foreground text-xs">
          Một task giao cho nhiều người sẽ được tính cho từng người. Đã sắp giảm dần theo
          tổng số việc.
        </p>
      </div>

      <ul aria-label="Chú giải" className="flex flex-wrap gap-x-4 gap-y-1">
        {SEGMENTS.map((s) => (
          <li key={s.key} className="text-muted-foreground flex items-center gap-1.5 text-xs">
            <span
              aria-hidden
              className="size-2.5 shrink-0 rounded-[2px]"
              style={{ backgroundColor: s.color }}
            />
            {s.label}
          </li>
        ))}
      </ul>

      <ul className="grid gap-2.5">
        {data.map((row) => {
          // `overdue` là TẬP CON của phần chưa xong, không phải một nhóm rời. Trừ cả hai ra
          // để ba phân đoạn cộng lại đúng bằng tổng; `Math.max(0, …)` phòng trường hợp dữ
          // liệu biên khiến tổng âm.
          const active = Math.max(0, row.total - row.done - row.overdue);
          const width = (n: number) => (row.total === 0 ? 0 : (n / row.total) * 100);

          return (
            <li key={row.employeeId} className="grid gap-1.5">
              <div className="flex min-w-0 items-center gap-2">
                <UserAvatar id={row.employeeId} name={row.employeeName} size="sm" />
                <span className="min-w-0 flex-1 truncate text-[13px] font-medium">
                  {row.employeeName}
                </span>
                <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                  {row.done}/{row.total} xong
                  {row.overdue > 0 ? ` · ${row.overdue} quá hạn` : ''}
                </span>
              </div>

              {/* gap-[2px] giữa các phân đoạn: khe nền mỏng làm ranh giới đọc được kể cả khi
                  hai màu cạnh nhau khó phân biệt (in đen trắng, mù màu). */}
              <div
                className="flex h-2.5 gap-[2px] overflow-hidden rounded-full"
                role="img"
                aria-label={`${row.employeeName}: ${row.done} xong, ${active} đang làm, ${row.overdue} quá hạn trên tổng ${row.total}`}
              >
                {[
                  { key: 'done', value: row.done },
                  { key: 'active', value: active },
                  { key: 'overdue', value: row.overdue },
                ].map(({ key, value }) =>
                  value === 0 ? null : (
                    <span
                      key={key}
                      className="h-full first:rounded-l-full last:rounded-r-full"
                      style={{
                        width: `${width(value)}%`,
                        backgroundColor: SEGMENTS.find((s) => s.key === key)!.color,
                      }}
                    />
                  ),
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

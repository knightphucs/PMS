'use client';

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { PRIORITY_LABEL, STATUS_LABEL } from '@/types/enums';
import type { PriorityCount, StatusCount } from '@/types/statistics';

/** Thứ tự trạng thái theo luồng làm việc, không theo số lượng. */
const STATUS_COLOR: Record<string, string> = {
  ToDo: 'var(--viz-status-todo)',
  InProgress: 'var(--viz-status-inprogress)',
  Review: 'var(--viz-status-review)',
  Done: 'var(--viz-status-done)',
};

/** Thang TUẦN TỰ nhạt → đậm: Highest là mức cao nhất nên đậm nhất. */
const PRIORITY_COLOR: Record<string, string> = {
  Highest: 'var(--viz-seq-5)',
  High: 'var(--viz-seq-4)',
  Medium: 'var(--viz-seq-3)',
  Low: 'var(--viz-seq-2)',
  Lowest: 'var(--viz-seq-1)',
};

const AXIS_TICK = { fontSize: 12, fill: 'var(--muted-foreground)' };

/**
 * Chú thích chung cho cả hai biểu đồ dưới đây.
 *
 * 📌 **Một chuỗi dữ liệu duy nhất, nên KHÔNG có chú giải (legend).** Danh tính nằm ở nhãn
 * trục — thêm một hộp chú giải chỉ lặp lại đúng những chữ đã hiện ngay dưới mỗi cột.
 *
 * 📌 Màu ở đây KHÔNG mang thông tin mới: nó lặp lại thứ nhãn trục đã nói, và tồn tại để
 * biểu đồ khớp với màu trạng thái trên board (`status-tone.ts`). Vì vậy biểu đồ vẫn đọc
 * được trọn vẹn khi in đen trắng hoặc với người mù màu.
 *
 * 📌 `byStatus`/`byPriority` do server zero-fill đủ mọi giá trị enum, nên trục X có số cột
 * CỐ ĐỊNH — không nhảy hình mỗi lần tải, và không cần vá dữ liệu ở client.
 */
export function StatusDistributionChart({ data }: { data: StatusCount[] }) {
  const rows = data.map((d) => ({
    key: d.status,
    label: STATUS_LABEL[d.status],
    count: d.count,
  }));

  return (
    <ChartCard
      title="Task theo trạng thái"
      description="Đếm cả subtask. Cột giữ nguyên bốn trạng thái kể cả khi chưa có task nào."
      rows={rows}
      colorOf={(key) => STATUS_COLOR[key]}
    />
  );
}

export function PriorityDistributionChart({ data }: { data: PriorityCount[] }) {
  const rows = data.map((d) => ({
    key: d.priority,
    label: PRIORITY_LABEL[d.priority],
    count: d.count,
  }));

  return (
    <ChartCard
      title="Task theo độ ưu tiên"
      description="Độ ưu tiên là thang có thứ tự, nên màu đi từ nhạt tới đậm chứ không phải năm màu rời rạc."
      rows={rows}
      colorOf={(key) => PRIORITY_COLOR[key]}
    />
  );
}

function ChartCard({
  title,
  description,
  rows,
  colorOf,
}: {
  title: string;
  description: string;
  rows: { key: string; label: string; count: number }[];
  colorOf: (key: string) => string;
}) {
  const total = rows.reduce((sum, r) => sum + r.count, 0);

  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div>
        <h2 className="text-sm font-semibold">{title}</h2>
        <p className="text-muted-foreground text-xs">{description}</p>
      </div>

      <div className="h-56 w-full min-w-0">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
            {/* Lưới chỉ theo phương ngang và rất mờ: nó là công cụ đọc giá trị, không
                phải một phần của dữ liệu. */}
            <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
            <XAxis dataKey="label" tick={AXIS_TICK} tickLine={false} axisLine={false} />
            <YAxis
              tick={AXIS_TICK}
              tickLine={false}
              axisLine={false}
              allowDecimals={false}
              width={44}
            />
            <Tooltip
              cursor={{ fill: 'var(--muted)', opacity: 0.4 }}
              content={({ active, payload, label }) =>
                active && payload?.length ? (
                  <div className="bg-popover rounded-md border px-2.5 py-1.5 text-xs shadow-sm">
                    <p className="font-medium">{label}</p>
                    <p className="text-muted-foreground tabular-nums">
                      {payload[0].value} task
                      {total > 0
                        ? ` · ${Math.round((Number(payload[0].value) / total) * 100)}%`
                        : ''}
                    </p>
                  </div>
                ) : null
              }
            />
            {/* radius chỉ bo ĐẦU cột, chân cột bám sát trục — bo cả bốn góc làm cột trông
                như đang lơ lửng và đọc sai giá trị gốc. */}
            <Bar dataKey="count" radius={[4, 4, 0, 0]} maxBarSize={56} isAnimationActive={false}>
              {rows.map((r) => (
                <Cell key={r.key} fill={colorOf(r.key)} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}

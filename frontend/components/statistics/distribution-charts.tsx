'use client';

import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { PRIORITY_LABEL } from '@/types/enums';
import type { PriorityCount, StatusCount } from '@/types/statistics';

/** Thang TUẦN TỰ nhạt → đậm: Highest là mức cao nhất nên đậm nhất. */
const PRIORITY_COLOR: Record<string, string> = {
  // Donut cần các mảng phân biệt ngay bằng mắt; không dùng thang xanh tuần tự vốn phù hợp
  // hơn cho bar chart có thứ tự. Tái dùng palette trực quan đã có của hệ thống.
  Highest: 'var(--viz-load-overdue)',
  High: 'var(--viz-status-review)',
  Medium: 'var(--viz-status-inprogress)',
  Low: 'var(--viz-status-todo)',
  Lowest: 'var(--viz-status-done)',
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
 * 📌 Server zero-fill đủ mọi hạng mục (mọi cột board / mọi độ ưu tiên) kể cả hạng mục có 0
 * task, nên trục X ổn định giữa các lần tải và client không phải vá dữ liệu.
 */
export function StatusDistributionChart({ data }: { data: StatusCount[] }) {
  /**
   * 🔴 `key` là `columnId`, KHÔNG phải tên cột.
   *
   * Trước ADR-052 nó là `d.status` (giá trị enum). Khi backend đổi sang cột, trường đó biến
   * mất và mọi `<Cell key={undefined}>` — React cảnh báo *"Each child in a list should have
   * a unique key prop… passed a child from ChartCard"*. Dùng tên cột cũng không an toàn:
   * tên do người dùng đặt và tuy có unique index trong DB, nó vẫn đổi được bất cứ lúc nào,
   * còn id thì không.
   */
  const rows = data.map((d) => ({
    key: d.columnId,
    label: d.name,
    count: d.count,
    // Màu lấy từ CHÍNH cột, không tra bảng: đó là thứ giữ cho biểu đồ khớp với board sau
    // khi người dùng đổi màu cột.
    color: d.color,
  }));

  return (
    <ChartCard
      title="Task theo trạng thái"
      description="Đếm cả subtask. Mỗi cột trên bảng là một cột ở đây, kể cả cột chưa có task nào."
      rows={rows}
      colorOf={(key) => rows.find((r) => r.key === key)?.color ?? 'var(--viz-seq-3)'}
    />
  );
}

export function PriorityDistributionChart({ data }: { data: PriorityCount[] }) {
  const rows = data.map((d) => ({
    key: d.priority,
    label: PRIORITY_LABEL[d.priority],
    count: d.count,
  }));

  const total = rows.reduce((sum, row) => sum + row.count, 0);

  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div>
        <h2 className="text-sm font-semibold">Task theo độ ưu tiên</h2>
        <p className="text-muted-foreground text-xs">
          Phân bổ toàn bộ task dưới dạng donut; 0 task vẫn được giữ trong chú giải.
        </p>
      </div>
      <div className="h-56 w-full min-w-0">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={rows}
              dataKey="count"
              nameKey="label"
              cx="50%"
              cy="50%"
              innerRadius="52%"
              outerRadius="76%"
              paddingAngle={2}
              isAnimationActive={false}
            >
              {rows.map((row) => (
                <Cell key={row.key} fill={PRIORITY_COLOR[row.key]} />
              ))}
            </Pie>
            <Tooltip
              content={({ active, payload }) =>
                active && payload?.length ? (
                  <div className="bg-popover rounded-md border px-2.5 py-1.5 text-xs shadow-sm">
                    <p className="font-medium">{payload[0].name}</p>
                    <p className="text-muted-foreground tabular-nums">
                      {payload[0].value} task
                      {total ? ` · ${Math.round((Number(payload[0].value) / total) * 100)}%` : ''}
                    </p>
                  </div>
                ) : null
              }
            />
            <Legend
              iconType="circle"
              iconSize={8}
              formatter={(value) => <span className="text-muted-foreground text-xs">{value}</span>}
            />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </section>
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

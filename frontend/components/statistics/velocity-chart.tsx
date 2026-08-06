'use client';

import {
  Bar,
  BarChart,
  CartesianGrid,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { formatShortDate } from '@/lib/format';
import type { SprintVelocityPoint } from '@/types/report';

const AXIS_TICK = { fontSize: 12, fill: 'var(--muted-foreground)' };

/**
 * Velocity — số task Xong mỗi sprint đã đóng sổ, theo đúng thứ tự đóng (server đã sắp theo
 * `completedAt`, không sắp lại ở đây).
 *
 * 📌 Một chuỗi dữ liệu, không chú giải — cùng nguyên tắc `distribution-charts.tsx`. Đường
 * ngang đứt nét là trung bình cộng, giúp đọc nhanh sprint nào trên/dưới phong độ chung mà
 * không phải tự nhẩm.
 */
export function VelocityChart({
  points,
  average,
}: {
  points: SprintVelocityPoint[];
  average: number;
}) {
  const rows = points.map((p) => ({
    key: p.sprintId,
    label: p.name,
    date: formatShortDate(p.completedAt),
    done: p.doneCount,
    total: p.totalCount,
  }));

  return (
    <section className="bg-card grid min-w-0 gap-3 rounded-lg border p-4">
      <div>
        <h2 className="text-sm font-semibold">Velocity</h2>
        <p className="text-muted-foreground text-xs">
          Số task Xong mỗi sprint đã đóng sổ. Chỉ sprint đã đóng mới có mặt — sprint đang chạy
          hoặc chưa bắt đầu chưa có gì để đo tốc độ theo.
        </p>
      </div>

      <div className="h-56 w-full min-w-0">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
            <CartesianGrid vertical={false} stroke="var(--viz-grid)" />
            <XAxis dataKey="label" tick={AXIS_TICK} tickLine={false} axisLine={false} />
            <YAxis
              tick={AXIS_TICK}
              tickLine={false}
              axisLine={false}
              allowDecimals={false}
              width={32}
            />
            <Tooltip
              cursor={{ fill: 'var(--muted)', opacity: 0.4 }}
              content={({ active, payload }) =>
                active && payload?.length ? (
                  <div className="bg-popover rounded-md border px-2.5 py-1.5 text-xs shadow-sm">
                    <p className="font-medium">{payload[0].payload.label}</p>
                    <p className="text-muted-foreground tabular-nums">
                      {payload[0].payload.done}/{payload[0].payload.total} task Xong · đóng{' '}
                      {payload[0].payload.date}
                    </p>
                  </div>
                ) : null
              }
            />
            {average > 0 ? (
              <ReferenceLine
                y={average}
                stroke="var(--viz-seq-4)"
                strokeDasharray="4 4"
                label={{
                  value: `TB ${average}`,
                  position: 'insideTopRight',
                  fill: 'var(--muted-foreground)',
                  fontSize: 11,
                }}
              />
            ) : null}
            <Bar
              dataKey="done"
              radius={[4, 4, 0, 0]}
              maxBarSize={56}
              isAnimationActive={false}
              fill="var(--viz-load-done)"
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}

import { Skeleton } from '@/components/ui/skeleton';

/**
 * Skeleton đúng hình dạng nội dung thật: bốn cột, số thẻ khác nhau, chiều cao bằng thẻ
 * thật (~72px). Một hình chữ nhật xám chung chung thì người dùng không đoán được cái gì
 * sắp hiện ra, và lúc dữ liệu về thì bố cục nhảy.
 */
const CARDS_PER_COLUMN = [3, 2, 4, 1];

export function BoardSkeleton() {
  return (
    <div
      className="grid grid-cols-1 items-start gap-3 sm:grid-cols-2 xl:grid-cols-4"
      aria-busy="true"
    >
      <span className="sr-only">Đang tải bảng công việc…</span>
      {CARDS_PER_COLUMN.map((count, columnIndex) => (
        <section key={columnIndex} className="bg-muted/40 flex flex-col rounded-lg p-2">
          <header className="mb-2 flex items-center gap-2 px-1">
            <Skeleton className="size-2 rounded-full" />
            <Skeleton className="h-3 w-24 flex-1" />
            <Skeleton className="h-4 w-6 rounded" />
          </header>
          <div className="flex flex-col gap-2">
            {Array.from({ length: count }).map((_, cardIndex) => (
              <Skeleton key={cardIndex} className="h-[72px] rounded-lg" />
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

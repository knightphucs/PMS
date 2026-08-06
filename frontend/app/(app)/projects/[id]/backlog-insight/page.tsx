import { redirect } from 'next/navigation';

/** Backlog Insight đã trở thành panel Insights ngay trên màn Backlog. */
export default function BacklogInsightRedirect() {
  redirect('../backlog');
}

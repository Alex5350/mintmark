"use client";

/** Top series ranking from the portfolio rollup (by value share). */
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { formatMoney, formatNumber } from "@/lib/format";

export function TopSeriesList() {
  const rollupQuery = useQuery({
    queryKey: ["portfolio", "rollup"],
    queryFn: api.portfolio.rollup,
  });

  if (rollupQuery.isPending) return <Skeleton className="h-64 w-full" />;
  if (rollupQuery.isError) {
    return (
      <EmptyState
        title="Top series unavailable"
        description="Series leaders compute on the API once holdings exist."
        className="h-64"
      />
    );
  }

  const top = rollupQuery.data.bySeries;
  if (top.length === 0) {
    return (
      <EmptyState
        title="No series yet"
        description="Your most-held series rank here as the collection grows."
        className="h-64"
      />
    );
  }

  return (
    <ol className="flex flex-col gap-2">
      {top.map((entry, index) => (
        <li
          key={entry.seriesId}
          className="flex items-baseline gap-3 rounded-md bg-surface-raised/50 px-3 py-2"
        >
          <span className="tnum text-xs text-ink-muted">{index + 1}</span>
          <span className="truncate text-sm font-medium text-ink">{entry.seriesName}</span>
          <span className="tnum ml-auto text-xs text-ink-muted">
            {formatNumber(entry.weight * 100, 1)}% of value
          </span>
          <span className="tnum text-sm font-semibold text-ink">{formatMoney(entry.value)}</span>
        </li>
      ))}
    </ol>
  );
}

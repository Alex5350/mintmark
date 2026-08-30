"use client";

/** Portfolio rollup cards: current value (gold), cost basis, unrealized %, holdings count. */
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { formatMoney, formatNumber, formatPct } from "@/lib/format";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/cn";

export function RollupCards() {
  const rollupQuery = useQuery({
    queryKey: ["portfolio", "rollup"],
    queryFn: api.portfolio.rollup,
    // The dashboard copy promises 30-second refreshes while the tab is open.
    refetchInterval: 30_000,
  });

  if (rollupQuery.isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-28" />
        ))}
      </div>
    );
  }

  if (rollupQuery.isError) {
    return (
      <EmptyState
        title="Rollup unavailable"
        description="Portfolio totals load from the Mintmark API — it could not be reached."
        action={
          <button
            type="button"
            onClick={() => void rollupQuery.refetch()}
            className="text-sm font-medium text-ink underline underline-offset-4"
          >
            Retry
          </button>
        }
      />
    );
  }

  const rollup = rollupQuery.data;
  const cards = [
    {
      label: "Current value",
      value: formatMoney(rollup.currentValue),
      accent: "border-l-gold text-gold",
    },
    {
      label: "Cost basis",
      value: formatMoney(rollup.costBasis),
      accent: "border-l-border text-ink",
    },
    {
      label: "Unrealized",
      value: formatPct(rollup.unrealizedPct),
      accent:
        rollup.unrealizedPct >= 0
          ? "border-l-positive text-positive"
          : "border-l-negative text-negative",
    },
    {
      label: "Holdings",
      value: formatNumber(rollup.holdingCount, 0),
      accent: "border-l-silver text-silver",
    },
  ] as const;

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((card) => (
        <Card key={card.label} className={cn("border-l-2", card.accent.split(" ")[0])}>
          <CardHeader className="pb-0">
            <span className="text-xs font-medium tracking-wide text-ink-muted uppercase">
              {card.label}
            </span>
          </CardHeader>
          <CardContent>
            <p className={cn("tnum text-2xl font-semibold", card.accent.split(" ")[1])}>
              {card.value}
            </p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

"use client";

/** Allocation by metal — donut of value share (weight) with metal-semantic colors. */
import { useQuery } from "@tanstack/react-query";
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { api } from "@/lib/api";
import { metalLabel, type Metal } from "@/lib/enums";
import { formatMoney, formatNumber } from "@/lib/format";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";

const METAL_FILL: Record<Metal, string> = {
  Gold: "var(--mm-gold)",
  Silver: "var(--mm-silver)",
  Platinum: "var(--mm-platinum)",
  Palladium: "var(--mm-palladium)",
};

const FALLBACK_FILL = "var(--mm-focus)";

interface Slice {
  label: string;
  /** Value share in percent (rollup weight is a [0,1] fraction). */
  sharePct: number;
  value: number;
  currency: string;
  fill: string;
}

export function AllocationChart() {
  const rollupQuery = useQuery({
    queryKey: ["portfolio", "rollup"],
    queryFn: api.portfolio.rollup,
    // The dashboard copy promises 30-second refreshes while the tab is open.
    refetchInterval: 30_000,
  });

  const slices: Slice[] = (rollupQuery.data?.byMetal ?? []).map((entry) => {
    const label = metalLabel(entry.metal);
    return {
      label,
      sharePct: entry.weight * 100,
      value: entry.value.amount,
      currency: entry.value.currency,
      fill: label in METAL_FILL ? METAL_FILL[label as Metal] : FALLBACK_FILL,
    };
  });

  if (rollupQuery.isPending) return <Skeleton className="h-72 w-full" />;

  if (rollupQuery.isError) {
    return (
      <EmptyState
        title="Allocation unavailable"
        description="Metal allocation computes on the API once holdings exist."
        className="h-72"
      />
    );
  }

  if (slices.length === 0) {
    return (
      <EmptyState
        title="Nothing allocated yet"
        description="Add holdings and the value share per metal renders here."
        className="h-72"
      />
    );
  }

  return (
    <div className="flex flex-col items-center gap-4 sm:flex-row">
      <div
        role="img"
        aria-label={`Allocation by value share: ${slices
          .map((s) => `${s.label} ${s.sharePct.toFixed(0)}%`)
          .join(", ")}`}
        className="h-64 w-64 shrink-0"
      >
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={slices}
              dataKey="sharePct"
              nameKey="label"
              innerRadius="62%"
              outerRadius="92%"
              paddingAngle={2}
              strokeWidth={0}
              isAnimationActive={false}
            >
              {slices.map((slice) => (
                <Cell key={slice.label} fill={slice.fill} />
              ))}
            </Pie>
            <Tooltip
              formatter={(value, name) => [`${formatNumber(Number(value), 1)}%`, String(name)]}
              contentStyle={{
                background: "var(--mm-surface-raised)",
                border: "1px solid var(--mm-border)",
                borderRadius: "0.375rem",
                color: "var(--mm-text)",
                fontSize: "0.75rem",
              }}
            />
          </PieChart>
        </ResponsiveContainer>
      </div>
      <ul className="flex w-full flex-col gap-2">
        {slices.map((slice) => (
          <li key={slice.label} className="flex items-center gap-2 text-sm">
            <span
              aria-hidden="true"
              className="size-2.5 rounded-full"
              style={{ backgroundColor: slice.fill }}
            />
            <span className="font-medium text-ink">{slice.label}</span>
            <span className="tnum ml-auto text-ink-muted">
              {formatMoney({ amount: slice.value, currency: slice.currency })} ·{" "}
              {formatNumber(slice.sharePct, 1)}%
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

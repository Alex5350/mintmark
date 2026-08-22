"use client";

/** Allocation by metal — donut of valueSharePct with metal-semantic colors. */
import { useQuery } from "@tanstack/react-query";
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { api } from "@/lib/api";
import type { Metal } from "@/lib/api-types";
import { formatNumber } from "@/lib/format";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";

const METAL_FILL: Record<Metal, string> = {
  Gold: "var(--mm-gold)",
  Silver: "var(--mm-silver)",
  Platinum: "var(--mm-platinum)",
  Palladium: "var(--mm-palladium)",
};

export function AllocationChart() {
  const rollupQuery = useQuery({
    queryKey: ["portfolio", "rollup"],
    queryFn: api.portfolio.rollup,
  });
  const byMetal = rollupQuery.data?.byMetal ?? [];

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

  if (byMetal.length === 0) {
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
        aria-label={`Allocation by value share: ${byMetal
          .map((m) => `${m.metal} ${m.valueSharePct.toFixed(0)}%`)
          .join(", ")}`}
        className="h-64 w-64 shrink-0"
      >
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={byMetal}
              dataKey="valueSharePct"
              nameKey="metal"
              innerRadius="62%"
              outerRadius="92%"
              paddingAngle={2}
              strokeWidth={0}
              isAnimationActive={false}
            >
              {byMetal.map((entry) => (
                <Cell key={entry.metal} fill={METAL_FILL[entry.metal]} />
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
        {byMetal.map((entry) => (
          <li key={entry.metal} className="flex items-center gap-2 text-sm">
            <span
              aria-hidden="true"
              className="size-2.5 rounded-full"
              style={{ backgroundColor: METAL_FILL[entry.metal] }}
            />
            <span className="font-medium text-ink">{entry.metal}</span>
            <span className="tnum ml-auto text-ink-muted">
              {formatNumber(entry.troyOz, 2)} ozt · {formatNumber(entry.valueSharePct, 1)}%
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

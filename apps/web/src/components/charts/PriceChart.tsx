"use client";

/**
 * Spot price chart: Recharts area series from server-downsampled daily
 * closes, range selector (1D..MAX), and the derived gold/silver ratio toggle.
 * The API serves chart points as {date, close} and ratio points as
 * {date, ratio}; both normalize to {t, price} for the time axis. The figure
 * carries an aria-label summarizing trend + last price.
 */
import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { api } from "@/lib/api";
import type { ChartRange, ChartSeries, RatioPoint } from "@/lib/api-types";
import { downsampleLabel, type Metal } from "@/lib/enums";
import { formatAxisNumber, formatChartTick } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/cn";

const RANGES: readonly ChartRange[] = ["1D", "1W", "1M", "3M", "6M", "1Y", "5Y", "MAX"] as const;

const METAL_STROKE: Record<Metal, string> = {
  Gold: "var(--mm-gold)",
  Silver: "var(--mm-silver)",
  Platinum: "var(--mm-platinum)",
  Palladium: "var(--mm-palladium)",
};

/** Normalized chart point — epoch ms + decimal-as-number price. */
interface ChartPoint {
  t: number;
  price: number;
}

function chartPoints(series: ChartSeries): ChartPoint[] {
  return series.points.map((point) => ({
    t: Date.parse(`${point.date}T00:00:00Z`),
    price: point.close.amount,
  }));
}

function ratioPoints(points: RatioPoint[]): ChartPoint[] {
  return points.map((point) => ({
    t: Date.parse(`${point.date}T00:00:00Z`),
    price: point.ratio,
  }));
}

function trendSummary(points: ChartPoint[]): { last: number | null; changePct: number | null } {
  if (points.length === 0) return { last: null, changePct: null };
  const first = points[0]?.price ?? 0;
  const last = points[points.length - 1]?.price ?? null;
  if (last == null || !first) return { last, changePct: null };
  return { last, changePct: ((last - first) / first) * 100 };
}

interface TooltipPayloadItem {
  payload?: ChartPoint;
}

function ChartTooltip({
  active,
  payload,
  label,
  range,
  isRatio,
}: {
  active?: boolean;
  payload?: ReadonlyArray<TooltipPayloadItem>;
  label?: number | string;
  range: ChartRange;
  isRatio: boolean;
}) {
  const point = payload?.[0]?.payload;
  if (!active || !point || label == null) return null;
  return (
    <div className="rounded-md border border-border bg-surface-raised px-3 py-2 text-xs shadow-lg">
      <div className="tnum font-semibold text-ink">
        {isRatio ? `${point.price.toFixed(1)} ratio` : `${point.price.toFixed(2)} per troy oz`}
      </div>
      <div className="text-ink-muted">{formatChartTick(point.t, range)}</div>
    </div>
  );
}

export interface PriceChartProps {
  metal?: Metal;
  className?: string;
}

export function PriceChart({ metal = "Gold", className }: PriceChartProps) {
  const [range, setRange] = useState<ChartRange>("1M");
  const [showRatio, setShowRatio] = useState(false);

  const seriesQuery = useQuery<ChartSeries | RatioPoint[]>({
    queryKey: ["chart", showRatio ? "ratio" : metal, range],
    queryFn: () => (showRatio ? api.prices.ratio(range) : api.prices.chart(metal, range)),
  });

  const points = useMemo(
    () =>
      seriesQuery.data == null
        ? []
        : Array.isArray(seriesQuery.data)
          ? ratioPoints(seriesQuery.data)
          : chartPoints(seriesQuery.data),
    [seriesQuery.data],
  );

  const stroke = showRatio ? "var(--mm-focus)" : METAL_STROKE[metal];
  const subjectLabel = showRatio ? "Gold-to-silver ratio" : `${metal} spot price`;

  const summary = useMemo(() => trendSummary(points), [points]);

  const ariaLabel = useMemo(() => {
    if (summary.last == null) return `${subjectLabel}, ${range} range: no data yet.`;
    const trend =
      summary.changePct == null
        ? ""
        : `, ${(summary.changePct >= 0 ? "up " : "down ") +
            Math.abs(summary.changePct).toFixed(1)}% over the period`;
    const value = showRatio
      ? `${summary.last.toFixed(1)}`
      : `${summary.last.toFixed(2)} per troy ounce`;
    return `${subjectLabel}, ${range} range: latest ${value}${trend}.`;
  }, [subjectLabel, range, summary, showRatio]);

  const chartSeries = seriesQuery.data != null && !Array.isArray(seriesQuery.data) ? seriesQuery.data : null;
  const downsample = downsampleLabel(chartSeries?.downsampleMethod);

  return (
    <figure className={cn("flex flex-col gap-3", className)}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        {/* Range selector — touch-friendly targets */}
        <div
          role="group"
          aria-label="Chart range"
          className="flex flex-wrap items-center gap-1 rounded-lg bg-surface-raised p-1"
        >
          {RANGES.map((r) => (
            <Button
              key={r}
              size="sm"
              variant={range === r ? "secondary" : "ghost"}
              aria-pressed={range === r}
              className="min-h-8 min-w-10"
              onClick={() => setRange(r)}
            >
              {r}
            </Button>
          ))}
        </div>
        <Button
          variant={showRatio ? "goldAccent" : "secondary"}
          size="sm"
          aria-pressed={showRatio}
          onClick={() => setShowRatio((v) => !v)}
        >
          Gold ÷ Silver ratio
        </Button>
      </div>

      {seriesQuery.isPending ? (
        <Skeleton className="h-72 w-full md:h-96" aria-label="Loading chart" />
      ) : seriesQuery.isError ? (
        <EmptyState
          title="Chart unavailable"
          description="The Mintmark API could not be reached for price history."
          action={
            <Button
              variant="secondary"
              onClick={() => void seriesQuery.refetch()}
              disabled={seriesQuery.isRefetching}
            >
              Retry
            </Button>
          }
        />
      ) : points.length === 0 ? (
        <EmptyState
          title="No price history yet"
          description="Daily closes backfill on first API run — the chart fills in then."
        />
      ) : (
        <div
          role="img"
          aria-label={ariaLabel}
          className="h-72 w-full md:h-96"
        >
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id="spotFill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={stroke} stopOpacity={0.28} />
                  <stop offset="100%" stopColor={stroke} stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="var(--mm-border)" strokeDasharray="3 3" vertical={false} />
              <XAxis
                dataKey="t"
                scale="time"
                type="number"
                domain={["dataMin", "dataMax"]}
                tickFormatter={(t: number) => formatChartTick(t, range)}
                tickLine={false}
                axisLine={{ stroke: "var(--mm-border)" }}
                tick={{ fill: "var(--mm-text-muted)", fontSize: 12 }}
                minTickGap={48}
              />
              <YAxis
                domain={["auto", "auto"]}
                tickFormatter={(v: number) => formatAxisNumber(v)}
                tickLine={false}
                axisLine={false}
                tick={{ fill: "var(--mm-text-muted)", fontSize: 12 }}
                width={52}
              />
              <Tooltip
                cursor={{ stroke: "var(--mm-border)" }}
                content={<ChartTooltip range={range} isRatio={showRatio} />}
              />
              <Area
                type="monotone"
                dataKey="price"
                stroke={stroke}
                strokeWidth={2}
                fill="url(#spotFill)"
                dot={false}
                activeDot={{ r: 4, strokeWidth: 0 }}
                isAnimationActive={false}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}

      <figcaption className="flex flex-wrap items-center gap-2 text-xs text-ink-muted">
        {seriesQuery.data != null ? (
          <>
            <span className="tnum">{points.length} points</span>
            <span aria-hidden="true">·</span>
            <span>
              {chartSeries
                ? `${chartSeries.range.start} → ${chartSeries.range.end}`
                : showRatio
                  ? "gold ÷ silver, daily"
                  : null}
            </span>
            {downsample ? (
              <>
                <span aria-hidden="true">·</span>
                <span>{downsample}</span>
              </>
            ) : null}
          </>
        ) : null}
      </figcaption>
    </figure>
  );
}

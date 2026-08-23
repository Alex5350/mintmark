/** Presentation-edge formatting. All timestamps are timestamptz UTC server-side. */
import { GRAMS_PER_TROY_OUNCE } from "@mintmark/domain-types";
import type { ChartRange, Money } from "@/lib/api-types";

export function formatMoney(money: Money | null | undefined, maximumFractionDigits = 2): string {
  if (!money) return "—";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: money.currency,
    maximumFractionDigits,
    // Intl throws a RangeError when minimum > maximum, so clamp together.
    minimumFractionDigits: Math.min(2, maximumFractionDigits),
  }).format(money.amount);
}

export function formatPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "—";
  const formatted = new Intl.NumberFormat("en-US", {
    maximumFractionDigits: digits,
    minimumFractionDigits: digits,
  }).format(Math.abs(value));
  return `${value >= 0 ? "+" : "−"}${formatted}%`;
}

export function formatTroyOz(value: number | null | undefined, digits = 3): string {
  if (value == null || Number.isNaN(value)) return "—";
  return `${new Intl.NumberFormat("en-US", { maximumFractionDigits: digits }).format(value)} ozt`;
}

/** Grams alongside ozt, via the single shared conversion factor in domain-types. */
export function troyOzToGrams(value: number): number {
  return value * GRAMS_PER_TROY_OUNCE;
}

/** "14:32 UTC" — provenance timestamps stay in UTC by policy. */
export function formatUtcTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return `${new Intl.DateTimeFormat("en-US", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "UTC",
  }).format(date)} UTC`;
}

/** "Aug 27, 2026" (UTC). */
export function formatUtcDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(date);
}

export function formatUtcDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  return `${formatUtcDate(iso)}, ${formatUtcTime(iso)}`;
}

/** X-axis label for a chart point epoch, granularity by range. */
export function formatChartTick(epochMs: number, range: ChartRange): string {
  const date = new Date(epochMs);
  if (Number.isNaN(date.getTime())) return "";
  if (range === "1D" || range === "1W") {
    return new Intl.DateTimeFormat("en-US", {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
      timeZone: "UTC",
    }).format(date);
  }
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    timeZone: "UTC",
  }).format(date);
}

/** Compact axis formatting: 2314.5 → "2.31k". */
export function formatAxisNumber(value: number): string {
  return new Intl.NumberFormat("en-US", {
    notation: "compact",
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatNumber(value: number | null | undefined, digits = 2): string {
  if (value == null || Number.isNaN(value)) return "—";
  return new Intl.NumberFormat("en-US", { maximumFractionDigits: digits }).format(value);
}

/** Gain/loss of current value vs. cost basis, in percent. */
export function gainLossPct(current: number | null | undefined, cost: number | null): number | null {
  if (current == null || !cost) return null;
  return ((current - cost) / cost) * 100;
}

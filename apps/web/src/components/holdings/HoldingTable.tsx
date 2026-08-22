"use client";

/**
 * Dense sortable holdings table: series, year, form, qty, metal, AMW, melt,
 * collectible, gain/loss %, updated. Header buttons sort (aria-sort), header
 * sticks within the scroll container, numerals are tabular, rows link to detail.
 */
import Link from "next/link";
import { useMemo, useState } from "react";
import type { Holding } from "@/lib/api-types";
import {
  formatMoney,
  formatNumber,
  formatPct,
  formatUtcDateTime,
  gainLossPct,
} from "@/lib/format";
import { TBody, TD, TH, THead, TR, Table } from "@/components/ui/table";
import { EmptyState } from "@/components/ui/empty-state";
import { cn } from "@/lib/cn";

type SortKey =
  | "series"
  | "year"
  | "form"
  | "qty"
  | "metal"
  | "amw"
  | "melt"
  | "collectible"
  | "gain"
  | "updated";

type SortDir = "asc" | "desc";

interface Column {
  key: SortKey;
  label: string;
  numeric: boolean;
}

const COLUMNS: readonly Column[] = [
  { key: "series", label: "Series", numeric: false },
  { key: "year", label: "Year", numeric: true },
  { key: "form", label: "Form", numeric: false },
  { key: "qty", label: "Qty", numeric: true },
  { key: "metal", label: "Metal", numeric: false },
  { key: "amw", label: "AMW", numeric: true },
  { key: "melt", label: "Melt", numeric: true },
  { key: "collectible", label: "Collectible", numeric: true },
  { key: "gain", label: "Gain/Loss", numeric: true },
  { key: "updated", label: "Updated", numeric: false },
] as const;

function rowValue(holding: Holding, key: SortKey): string | number | null {
  const valuation = holding.currentValuation;
  switch (key) {
    case "series":
      return holding.coinType?.seriesName ?? holding.itemForm;
    case "year":
      return holding.coinType?.year ?? null;
    case "form":
      return holding.itemForm;
    case "qty":
      return holding.quantity;
    case "metal":
      return holding.coinType?.metal ?? null;
    case "amw":
      return holding.coinType?.actualMetalWeightTroyOz ?? null;
    case "melt":
      return valuation?.melt.amount.amount ?? null;
    case "collectible":
      return valuation?.collectible.amount.amount ?? null;
    case "gain": {
      if (!valuation) return null;
      const current = valuation.melt.amount.amount + valuation.collectible.amount.amount;
      return gainLossPct(current, holding.quantity * holding.purchasePricePerUnit.amount);
    }
    case "updated":
      return valuation ? new Date(valuation.melt.spot.sourceTimestamp).getTime() : null;
  }
}

function displayValue(holding: Holding, key: SortKey): string {
  const valuation = holding.currentValuation;
  switch (key) {
    case "series":
      return holding.coinType?.seriesName ?? holding.itemForm;
    case "year":
      return holding.coinType ? String(holding.coinType.year) : "—";
    case "form":
      return holding.itemForm;
    case "qty":
      return formatNumber(holding.quantity, 0);
    case "metal":
      return holding.coinType?.metal ?? "—";
    case "amw":
      return holding.coinType
        ? `${formatNumber(holding.coinType.actualMetalWeightTroyOz, 3)}`
        : "—";
    case "melt":
      return valuation ? formatMoney(valuation.melt.amount) : "—";
    case "collectible":
      return valuation ? formatMoney(valuation.collectible.amount) : "—";
    case "gain": {
      if (!valuation) return "—";
      const current = valuation.melt.amount.amount + valuation.collectible.amount.amount;
      return formatPct(gainLossPct(current, holding.quantity * holding.purchasePricePerUnit.amount));
    }
    case "updated":
      return valuation
        ? formatUtcDateTime(valuation.melt.spot.sourceTimestamp)
        : formatUtcDateTime(holding.createdAt);
  }
}

export interface HoldingTableProps {
  holdings: Holding[];
  className?: string;
}

export function HoldingTable({ holdings, className }: HoldingTableProps) {
  const [sortKey, setSortKey] = useState<SortKey>("series");
  const [sortDir, setSortDir] = useState<SortDir>("asc");

  const sorted = useMemo(() => {
    const factor = sortDir === "asc" ? 1 : -1;
    return [...holdings].sort((a, b) => {
      const av = rowValue(a, sortKey);
      const bv = rowValue(b, sortKey);
      // Nulls (unvalued / uncataloged) always sink, regardless of direction.
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      if (typeof av === "number" && typeof bv === "number") return (av - bv) * factor;
      return String(av).localeCompare(String(bv)) * factor;
    });
  }, [holdings, sortKey, sortDir]);

  function toggleSort(key: SortKey) {
    if (key === sortKey) {
      setSortDir((dir) => (dir === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDir("asc");
    }
  }

  if (holdings.length === 0) {
    return (
      <EmptyState
        title="No holdings yet"
        description="Your collection appears here once the API is live and you add coins."
      />
    );
  }

  return (
    <div className={cn("max-h-[36rem] overflow-auto rounded-lg border border-border bg-surface", className)}>
      <Table>
        <THead>
          <TR>
            {COLUMNS.map((column) => {
              const isSorted = sortKey === column.key;
              return (
                <TH
                  key={column.key}
                  scope="col"
                  aria-sort={isSorted ? (sortDir === "asc" ? "ascending" : "descending") : "none"}
                  className={column.numeric ? "text-right" : undefined}
                >
                  <button
                    type="button"
                    onClick={() => toggleSort(column.key)}
                    className={cn(
                      "flex w-full items-center gap-1 rounded text-xs font-medium tracking-wide uppercase transition-colors",
                      "hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
                      column.numeric && "justify-end",
                      isSorted ? "text-ink" : "text-ink-muted",
                    )}
                  >
                    {column.label}
                    <span aria-hidden="true" className="tnum text-[0.625rem]">
                      {isSorted ? (sortDir === "asc" ? "▲" : "▼") : ""}
                    </span>
                  </button>
                </TH>
              );
            })}
          </TR>
        </THead>
        <TBody>
          {sorted.map((holding) => {
            const gain = rowValue(holding, "gain");
            return (
              <TR key={holding.holdingId}>
                {COLUMNS.map((column) => (
                  <TD
                    key={column.key}
                    className={cn(
                      column.numeric && "tnum text-right",
                      column.key === "melt" && "text-silver",
                      column.key === "collectible" && "text-gold",
                      column.key === "gain" &&
                        (typeof gain !== "number"
                          ? "text-ink-muted"
                          : gain >= 0
                            ? "text-positive"
                            : "text-negative"),
                      column.key === "updated" && "whitespace-nowrap text-ink-muted",
                    )}
                  >
                    {column.key === "series" ? (
                      <Link
                        href={`/holdings/${holding.holdingId}`}
                        className="font-medium text-ink underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus"
                      >
                        {displayValue(holding, column.key)}
                      </Link>
                    ) : (
                      displayValue(holding, column.key)
                    )}
                  </TD>
                ))}
              </TR>
            );
          })}
        </TBody>
      </Table>
    </div>
  );
}

"use client";

/**
 * Dense sortable holdings table: item, form, qty, metal, cost per unit, total
 * cost, current value, gain/loss. Header buttons sort (aria-sort), header
 * sticks within the scroll container, numerals are tabular, rows link to
 * detail. Columns are exactly what the holdings list DTO carries — no
 * invented values.
 */
import Link from "next/link";
import { useMemo, useState } from "react";
import type { HoldingListItem } from "@/lib/api-types";
import { itemFormLabel, knownMetal } from "@/lib/enums";
import { formatMoney, formatNumber, formatPct, gainLossPct } from "@/lib/format";
import { TBody, TD, TH, THead, TR, Table } from "@/components/ui/table";
import { EmptyState } from "@/components/ui/empty-state";
import { cn } from "@/lib/cn";

type SortKey = "item" | "form" | "qty" | "metal" | "unitCost" | "totalCost" | "value" | "gain";

type SortDir = "asc" | "desc";

interface Column {
  key: SortKey;
  label: string;
  numeric: boolean;
}

const COLUMNS: readonly Column[] = [
  { key: "item", label: "Item", numeric: false },
  { key: "form", label: "Form", numeric: false },
  { key: "qty", label: "Qty", numeric: true },
  { key: "metal", label: "Metal", numeric: false },
  { key: "unitCost", label: "Cost/unit", numeric: true },
  { key: "totalCost", label: "Total cost", numeric: true },
  { key: "value", label: "Current value", numeric: true },
  { key: "gain", label: "Gain/Loss", numeric: true },
] as const;

function totalCost(holding: HoldingListItem): number | null {
  const price = holding.effectivePurchasePricePerUnit;
  if (!price) return null;
  return holding.effectiveQuantity * price.amount;
}

function rowValue(holding: HoldingListItem, key: SortKey): string | number | null {
  switch (key) {
    case "item":
      return holding.displayName;
    case "form":
      return itemFormLabel(holding.form);
    case "qty":
      return holding.effectiveQuantity;
    case "metal":
      return knownMetal(holding.metal);
    case "unitCost":
      return holding.effectivePurchasePricePerUnit?.amount ?? null;
    case "totalCost":
      return totalCost(holding);
    case "value":
      return holding.currentValue?.amount ?? null;
    case "gain":
      return holding.currentValue
        ? gainLossPct(holding.currentValue.amount, totalCost(holding))
        : null;
  }
}

function displayValue(holding: HoldingListItem, key: SortKey): string {
  switch (key) {
    case "item":
      return holding.displayName;
    case "form":
      return itemFormLabel(holding.form);
    case "qty":
      return formatNumber(holding.effectiveQuantity, 0);
    case "metal":
      return knownMetal(holding.metal) ?? "—";
    case "unitCost":
      return holding.effectivePurchasePricePerUnit
        ? formatMoney(holding.effectivePurchasePricePerUnit)
        : "—";
    case "totalCost": {
      const cost = totalCost(holding);
      return cost == null
        ? "—"
        : formatMoney({
            amount: cost,
            currency: holding.effectivePurchasePricePerUnit.currency,
          });
    }
    case "value":
      return holding.currentValue ? formatMoney(holding.currentValue) : "—";
    case "gain":
      return holding.currentValue
        ? formatPct(gainLossPct(holding.currentValue.amount, totalCost(holding)))
        : "—";
  }
}

export interface HoldingTableProps {
  holdings: HoldingListItem[];
  className?: string;
}

export function HoldingTable({ holdings, className }: HoldingTableProps) {
  const [sortKey, setSortKey] = useState<SortKey>("item");
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
              <TR key={holding.id}>
                {COLUMNS.map((column) => (
                  <TD
                    key={column.key}
                    className={cn(
                      column.numeric && "tnum text-right",
                      column.key === "value" && "text-gold",
                      column.key === "gain" &&
                        (typeof gain !== "number"
                          ? "text-ink-muted"
                          : gain >= 0
                            ? "text-positive"
                            : "text-negative"),
                      column.key === "item" && "max-w-72 truncate",
                    )}
                  >
                    {column.key === "item" ? (
                      <Link
                        href={`/holdings/${holding.id}`}
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

import Link from "next/link";
import type { HoldingListItem } from "@/lib/api-types";
import { itemFormLabel, knownMetal } from "@/lib/enums";
import { formatMoney, formatNumber } from "@/lib/format";
import { MetalBadge } from "@/components/ui/badge";
import { cn } from "@/lib/cn";

/**
 * Gallery-mode holding card. Circular coin crop (obverse) — until per-holding
 * presigned photos flow from the API the crop renders an honest placeholder
 * (display-name initials), never a fake coin.
 */
function CoinThumb({ holding }: { holding: HoldingListItem }) {
  const initials =
    holding.displayName
      .split(/\s+/)
      .map((word) => word[0])
      .filter(Boolean)
      .slice(0, 2)
      .join("")
      .toUpperCase() || "?";
  return (
    <div
      aria-hidden="true"
      className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-full border border-border bg-surface-raised"
    >
      <span className="font-heading text-xl font-semibold text-ink-muted">{initials}</span>
    </div>
  );
}

export interface HoldingCardProps {
  holding: HoldingListItem;
  className?: string;
}

export function HoldingCard({ holding, className }: HoldingCardProps) {
  const form = itemFormLabel(holding.form);
  const metal = knownMetal(holding.metal);
  const pricePerUnit = holding.effectivePurchasePricePerUnit;
  const totalCost = pricePerUnit
    ? { amount: holding.effectiveQuantity * pricePerUnit.amount, currency: pricePerUnit.currency }
    : null;

  return (
    <Link
      href={`/holdings/${holding.id}`}
      className={cn(
        "group flex gap-4 rounded-lg border border-border bg-surface p-4 transition-colors",
        "hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus focus-visible:ring-offset-2 focus-visible:ring-offset-base",
        className,
      )}
    >
      <CoinThumb holding={holding} />
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <h3 className="truncate font-heading text-base font-semibold text-ink">
              {holding.displayName}
            </h3>
            <p className="truncate text-xs text-ink-muted">
              {[
                `${formatNumber(holding.effectiveQuantity, 0)} × ${form}`,
                pricePerUnit ? `${formatMoney(pricePerUnit)}/unit` : null,
              ]
                .filter(Boolean)
                .join(" · ")}
            </p>
          </div>
          {metal ? <MetalBadge metal={metal} /> : null}
        </div>

        <div className="mt-auto grid grid-cols-2 gap-2">
          <div className="rounded-md border-l-2 border-border bg-surface-raised/50 px-2.5 py-1.5">
            <div className="text-[0.688rem] font-medium tracking-wide text-ink-muted uppercase">
              Cost basis
            </div>
            <div className="tnum text-sm font-semibold text-ink">
              {totalCost ? formatMoney(totalCost) : "—"}
            </div>
          </div>
          <div className="rounded-md border-l-2 border-gold/60 bg-surface-raised/50 px-2.5 py-1.5">
            <div className="text-[0.688rem] font-medium tracking-wide text-ink-muted uppercase">
              Current value
            </div>
            <div className="tnum text-sm font-semibold text-gold">
              {holding.currentValue ? formatMoney(holding.currentValue) : "—"}
            </div>
            {!holding.currentValue ? (
              <div className="text-[0.688rem] text-ink-muted">not valued yet</div>
            ) : null}
          </div>
        </div>
      </div>
    </Link>
  );
}

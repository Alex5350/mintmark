import Link from "next/link";
import type { Holding } from "@/lib/api-types";
import { formatMoney, formatNumber } from "@/lib/format";
import { MetalBadge } from "@/components/ui/badge";
import { cn } from "@/lib/cn";

/**
 * Gallery-mode holding card. Circular coin crop (obverse) — image keys are
 * object-storage keys, so until presigned URLs flow from the API the crop
 * renders an honest catalog placeholder (series initials), never a fake coin.
 */
function CoinThumb({ holding }: { holding: Holding }) {
  const series = holding.coinType?.seriesName ?? holding.itemForm;
  const initials = series
    .split(/\s+/)
    .map((word) => word[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
  return (
    <div
      aria-hidden="true"
      className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-full border border-border bg-surface-raised"
    >
      <span className="font-heading text-xl font-semibold text-ink-muted">{initials || "?"}</span>
    </div>
  );
}

export interface HoldingCardProps {
  holding: Holding;
  className?: string;
}

export function HoldingCard({ holding, className }: HoldingCardProps) {
  const { coinType, currentValuation } = holding;
  const collectible = currentValuation?.collectible;

  return (
    <Link
      href={`/holdings/${holding.holdingId}`}
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
              {coinType ? `${coinType.seriesName} · ${coinType.year}` : holding.itemForm}
            </h3>
            <p className="truncate text-xs text-ink-muted">
              {[coinType?.mintMark, coinType?.finishPrimary, `${holding.quantity} × ${holding.itemForm}`]
                .filter(Boolean)
                .join(" · ")}
            </p>
          </div>
          {coinType ? <MetalBadge metal={coinType.metal} /> : null}
        </div>

        <div className="mt-auto grid grid-cols-2 gap-2">
          <div className="rounded-md border-l-2 border-silver/60 bg-surface-raised/50 px-2.5 py-1.5">
            <div className="text-[0.688rem] font-medium tracking-wide text-ink-muted uppercase">
              Melt
            </div>
            <div className="tnum text-sm font-semibold text-silver">
              {currentValuation ? formatMoney(currentValuation.melt.amount) : "—"}
            </div>
          </div>
          <div className="rounded-md border-l-2 border-gold/60 bg-surface-raised/50 px-2.5 py-1.5">
            <div className="text-[0.688rem] font-medium tracking-wide text-ink-muted uppercase">
              Collectible
            </div>
            <div className="tnum text-sm font-semibold text-gold">
              {collectible ? formatMoney(collectible.amount) : "—"}
            </div>
            {collectible ? (
              <div className="tnum text-[0.688rem] text-ink-muted">
                est. {formatMoney({ amount: collectible.confidenceLow, currency: collectible.amount.currency }, 0)}–
                {formatMoney({ amount: collectible.confidenceHigh, currency: collectible.amount.currency }, 0)}
              </div>
            ) : (
              <div className="text-[0.688rem] text-ink-muted">not valued yet</div>
            )}
          </div>
        </div>
        {coinType ? (
          <p className="tnum text-[0.688rem] text-ink-muted">
            AMW {formatNumber(coinType.actualMetalWeightTroyOz, 3)} ozt
          </p>
        ) : null}
      </div>
    </Link>
  );
}

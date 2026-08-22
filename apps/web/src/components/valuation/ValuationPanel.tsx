import type { CurrentValuation } from "@/lib/api-types";
import { formatMoney, formatUtcDateTime } from "@/lib/format";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/cn";

/**
 * Detail-view valuation: melt (silver accent) and collectible (gold accent)
 * side by side, each with full provenance — spot price, provider, timestamp
 * travel with the number. "How was this calculated" expands to the itemized
 * premium factors; valuations stay explainable forever.
 */
export interface ValuationPanelProps {
  valuation: CurrentValuation;
  className?: string;
}

export function ValuationPanel({ valuation, className }: ValuationPanelProps) {
  const { melt, collectible } = valuation;

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Valuation</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-2">
          {/* Melt — silver accent, spot provenance inline */}
          <section
            aria-labelledby="valuation-melt"
            className="rounded-md border border-border border-l-2 border-l-silver bg-surface-raised/50 p-3"
          >
            <h4 id="valuation-melt" className="text-xs font-medium tracking-wide text-ink-muted uppercase">
              Melt
            </h4>
            <p className="tnum text-2xl font-semibold text-silver">
              {formatMoney(melt.amount)}
            </p>
            <p className="mt-1 text-xs text-ink-muted">
              Spot {formatMoney(melt.spot.price)}/oz · {melt.spot.provider} ·{" "}
              {formatUtcDateTime(melt.spot.sourceTimestamp)}
            </p>
            {melt.spot.stale ? (
              <Badge
                tone="warning"
                className="mt-1"
                title="Provider outage — valued against the last known good spot."
              >
                STALE
              </Badge>
            ) : null}
          </section>

          {/* Collectible — gold accent, confidence band + method version */}
          <section
            aria-labelledby="valuation-collectible"
            className="rounded-md border border-border border-l-2 border-l-gold bg-surface-raised/50 p-3"
          >
            <h4
              id="valuation-collectible"
              className="text-xs font-medium tracking-wide text-ink-muted uppercase"
            >
              Collectible
            </h4>
            <p className="tnum text-2xl font-semibold text-gold">
              {formatMoney(collectible.amount)}
            </p>
            <p className="tnum mt-1 text-xs text-ink-muted">
              Confidence{" "}
              {formatMoney({ amount: collectible.confidenceLow, currency: collectible.amount.currency }, 0)}–
              {formatMoney({ amount: collectible.confidenceHigh, currency: collectible.amount.currency }, 0)}{" "}
              · method v{collectible.methodVersion}
            </p>
          </section>
        </div>

        <details className="group rounded-md border border-border">
          <summary
            className={cn(
              "cursor-pointer select-none rounded-md px-3 py-2 text-sm font-medium text-ink-muted transition-colors",
              "hover:bg-surface-raised hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
            )}
          >
            How was this calculated?
          </summary>
          <div className="border-t border-border px-3 py-3">
            <p className="mb-2 text-xs text-ink-muted">
              Melt = actual metal weight × quantity × spot. Collectible = melt × premium factors
              (method v{collectible.methodVersion}):
            </p>
            {collectible.premiumFactors.length === 0 ? (
              <p className="text-sm text-ink-muted">No premium factors applied.</p>
            ) : (
              <dl className="flex flex-col gap-2">
                {collectible.premiumFactors.map((factor) => (
                  <div key={factor.name} className="flex flex-col gap-0.5 border-l-2 border-border pl-3">
                    <div className="flex items-baseline justify-between gap-2">
                      <dt className="text-sm font-medium text-ink">{factor.name}</dt>
                      <dd className="tnum text-sm font-semibold text-gold">
                        ×{factor.multiplier.toFixed(2)}
                      </dd>
                    </div>
                    <dd className="text-xs text-ink-muted">{factor.rationale}</dd>
                  </div>
                ))}
              </dl>
            )}
          </div>
        </details>
      </CardContent>
    </Card>
  );
}

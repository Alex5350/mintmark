"use client";

/** Holding detail: coin flip, valuation provenance, grading, purchase facts. */
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { CoinFlip } from "@/components/holdings/CoinFlip";
import { ValuationPanel } from "@/components/valuation/ValuationPanel";
import { GradingPanel } from "@/components/valuation/GradingPanel";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { buttonStyles } from "@/components/ui/button";
import { formatMoney, formatNumber, formatTroyOz, formatUtcDate, troyOzToGrams } from "@/lib/format";

function FactRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-border/60 py-2 last:border-b-0">
      <dt className="text-xs tracking-wide text-ink-muted uppercase">{label}</dt>
      <dd className="tnum text-right text-sm text-ink">{value}</dd>
    </div>
  );
}

export function HoldingDetail({ holdingId }: { holdingId: string }) {
  const holdingQuery = useQuery({
    queryKey: ["holdings", "detail", holdingId],
    queryFn: () => api.holdings.detail(holdingId),
  });

  if (holdingQuery.isPending) {
    return (
      <div className="grid gap-6 lg:grid-cols-[auto_1fr]">
        <Skeleton className="size-64 rounded-full" />
        <div className="flex flex-col gap-4">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      </div>
    );
  }

  if (holdingQuery.isError) {
    return (
      <EmptyState
        title="Holding unavailable"
        description="This holding could not be loaded — the API is unreachable or the id does not exist."
        action={
          <Link href="/collection" className={buttonStyles({ variant: "secondary" })}>
            Back to collection
          </Link>
        }
      />
    );
  }

  const holding = holdingQuery.data;
  const coinType = holding.coinType;
  const title = coinType ? `${coinType.seriesName} · ${coinType.year}` : holding.itemForm;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-baseline justify-between gap-2">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-ink">{title}</h1>
          {coinType ? (
            <p className="text-sm text-ink-muted">
              {[
                coinType.mintMark,
                coinType.finishPrimary,
                coinType.finishAttributes.join(", ") || null,
              ]
                .filter(Boolean)
                .join(" · ")}
            </p>
          ) : (
            <p className="text-sm text-ink-muted">Uncataloged {holding.itemForm.toLowerCase()}</p>
          )}
        </div>
        <Link
          href="/collection"
          className="text-sm text-ink-muted underline-offset-4 hover:text-ink hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus"
        >
          ← Collection
        </Link>
      </header>

      <div className="grid gap-6 lg:grid-cols-[auto_1fr]">
        <div className="flex flex-col items-center gap-4">
          <CoinFlip
            label={title}
            size="lg"
            // imageKeys are storage keys, not URLs — presigned URLs arrive with
            // the generated client; placeholders show until then.
            obverseSrc={null}
            reverseSrc={null}
          />
          {coinType ? (
            <Card className="w-full max-w-xs">
              <CardContent className="p-4">
                <dl>
                  <FactRow label="Metal" value={coinType.metal} />
                  <FactRow
                    label="Actual metal weight"
                    value={`${formatTroyOz(coinType.actualMetalWeightTroyOz)} (${formatNumber(
                      troyOzToGrams(coinType.actualMetalWeightTroyOz),
                      2,
                    )} g)`}
                  />
                  <FactRow label="Quantity" value={String(holding.quantity)} />
                  <FactRow label="Item form" value={holding.itemForm} />
                </dl>
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="flex flex-col gap-6">
          {holding.currentValuation ? (
            <ValuationPanel valuation={holding.currentValuation} />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>Valuation</CardTitle>
              </CardHeader>
              <CardContent>
                <EmptyState
                  title="Not valued yet"
                  description="Valuations compute against live spot once the price pipeline is serving."
                  className="py-8"
                />
              </CardContent>
            </Card>
          )}

          <GradingPanel grading={holding.grading} />

          <Card>
            <CardHeader>
              <CardTitle>Purchase</CardTitle>
            </CardHeader>
            <CardContent>
              <dl>
                <FactRow label="Date" value={formatUtcDate(holding.purchaseDate)} />
                <FactRow
                  label="Price per unit"
                  value={formatMoney(holding.purchasePricePerUnit)}
                />
                <FactRow
                  label="Total cost"
                  value={formatMoney({
                    amount: holding.purchasePricePerUnit.amount * holding.quantity,
                    currency: holding.purchasePricePerUnit.currency,
                  })}
                />
                <FactRow label="Dealer" value={holding.dealer ?? "—"} />
                <FactRow label="Serial number" value={holding.serialNumber ?? "—"} />
                <FactRow label="Storage" value={holding.storageLocation ?? "—"} />
              </dl>
              {holding.notes ? (
                <p className="mt-3 rounded-md bg-surface-raised/50 p-3 text-sm text-ink-muted">
                  {holding.notes}
                </p>
              ) : null}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

"use client";

/** Holding detail: coin flip (presigned catalog images when cataloged), valuation provenance, purchase facts. */
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
import { itemFormLabel, metalLabel } from "@/lib/enums";
import { presignedImageUrl } from "@/lib/images";

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

  const valuationQuery = useQuery({
    queryKey: ["holdings", "valuation", holdingId],
    // No data dependency on the detail row — fetch in parallel instead of
    // serializing behind it.
    queryFn: () => api.holdings.valuation(holdingId),
    // Generic holdings 422 (no cataloged coin type → no AMW); network blips retry once.
    retry: 1,
  });

  const coinTypeQuery = useQuery({
    queryKey: ["catalog", "coinType", holdingQuery.data?.coinTypeId],
    queryFn: () => api.catalog.coinType(holdingQuery.data?.coinTypeId as number),
    enabled: holdingQuery.data?.coinTypeId != null,
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
  const coinType = coinTypeQuery.data?.detail;
  const title = holding.displayName;
  const form = itemFormLabel(holding.form);
  const pricePerUnit = holding.effectivePurchasePricePerUnit;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-baseline justify-between gap-2">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-ink">{title}</h1>
          <p className="text-sm text-ink-muted">
            {coinType
              ? [coinType.mintName, String(coinType.year)].filter(Boolean).join(" · ")
              : `Uncataloged ${form.toLowerCase()}`}
          </p>
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
            // Presigned catalog reference images when the holding is cataloged.
            obverseSrc={presignedImageUrl(coinTypeQuery.data?.obverseImageUrl)}
            reverseSrc={presignedImageUrl(coinTypeQuery.data?.reverseImageUrl)}
          />
          <Card className="w-full max-w-xs">
            <CardContent className="p-4">
              <dl>
                {coinType ? <FactRow label="Metal" value={metalLabel(coinType.metal)} /> : null}
                {coinType ? (
                  <FactRow
                    label="Actual metal weight"
                    value={`${formatTroyOz(coinType.actualMetalWeightTroyOz)} (${formatNumber(
                      troyOzToGrams(coinType.actualMetalWeightTroyOz),
                      2,
                    )} g)`}
                  />
                ) : null}
                <FactRow label="Quantity" value={String(holding.effectiveQuantity)} />
                <FactRow label="Item form" value={form} />
              </dl>
            </CardContent>
          </Card>
        </div>

        <div className="flex flex-col gap-6">
          {valuationQuery.isSuccess ? (
            <ValuationPanel valuation={valuationQuery.data} />
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>Valuation</CardTitle>
              </CardHeader>
              <CardContent>
                <EmptyState
                  title="Not valued yet"
                  description={
                    holding.coinTypeId == null
                      ? "Valuation requires a cataloged coin type — generic holdings have no metal weight to melt."
                      : "Valuation computes against live spot; retry in a moment."
                  }
                  className="py-8"
                />
              </CardContent>
            </Card>
          )}

          <GradingPanel />

          <Card>
            <CardHeader>
              <CardTitle>Purchase</CardTitle>
            </CardHeader>
            <CardContent>
              <dl>
                <FactRow label="Date" value={formatUtcDate(holding.purchasedAtUtc)} />
                <FactRow label="Price per unit" value={formatMoney(pricePerUnit)} />
                <FactRow
                  label="Total cost"
                  value={formatMoney({
                    amount: pricePerUnit.amount * holding.effectiveQuantity,
                    currency: pricePerUnit.currency,
                  })}
                />
                <FactRow label="Revisions" value={String(holding.revisionCount)} />
              </dl>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

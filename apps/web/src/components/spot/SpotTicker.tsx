"use client";

/**
 * Header strip: live spot per metal (gold/silver), provider + timestamp, amber
 * STALE badge when quote.stale. Price ticks animate via a color transition
 * (no bounce). Honest unavailable/retry state — no fake prices.
 */
import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { SpotQuote } from "@/lib/api-types";
import { formatMoney, formatUtcTime } from "@/lib/format";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/cn";

const TICKER_METALS = new Set(["Gold", "Silver"]);

function TickerCell({ quote }: { quote: SpotQuote }) {
  const [direction, setDirection] = useState<"up" | "down" | null>(null);
  const previous = useRef<number | null>(null);

  useEffect(() => {
    const prev = previous.current;
    previous.current = quote.price.amount;
    if (prev == null || prev === quote.price.amount) return;
    setDirection(quote.price.amount > prev ? "up" : "down");
    const timer = setTimeout(() => setDirection(null), 1500);
    return () => clearTimeout(timer);
  }, [quote.price.amount]);

  return (
    <div className="flex items-baseline gap-2">
      <span className="text-xs font-medium text-ink-muted">{quote.metal}</span>
      <span
        className={cn(
          "tnum text-sm font-semibold text-ink transition-colors duration-700",
          direction === "up" && "text-positive",
          direction === "down" && "text-negative",
        )}
      >
        {formatMoney(quote.price)}
      </span>
      <span className="hidden text-xs text-ink-muted sm:inline">
        {quote.provider} · {formatUtcTime(quote.sourceTimestamp)}
      </span>
      {quote.stale ? (
        <Badge
          tone="warning"
          title="Provider outage — showing the last known good price. Stale is never silent."
        >
          STALE
        </Badge>
      ) : null}
    </div>
  );
}

export function SpotTicker() {
  const quotesQuery = useQuery({
    queryKey: ["spot", "current"],
    queryFn: api.prices.current,
    refetchInterval: 30_000,
    select: (quotes: SpotQuote[]) => quotes.filter((q) => TICKER_METALS.has(q.metal)),
  });

  return (
    <section
      aria-label="Live spot prices"
      className="border-b border-border bg-surface"
    >
      <div className="mx-auto flex min-h-11 w-full max-w-6xl flex-wrap items-center gap-x-6 gap-y-1 px-4 py-2">
        {quotesQuery.isPending ? (
          <div className="flex items-center gap-6" aria-label="Loading spot prices">
            <Skeleton className="h-4 w-36" />
            <Skeleton className="h-4 w-36" />
          </div>
        ) : quotesQuery.isError || quotesQuery.data.length === 0 ? (
          <div className="flex items-center gap-3 py-0.5">
            <span className="text-xs text-ink-muted">
              Spot prices unavailable — no live quotes from the API.
            </span>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => void quotesQuery.refetch()}
              disabled={quotesQuery.isRefetching}
            >
              Retry
            </Button>
          </div>
        ) : (
          quotesQuery.data.map((quote) => <TickerCell key={quote.metal} quote={quote} />)
        )}
      </div>
    </section>
  );
}

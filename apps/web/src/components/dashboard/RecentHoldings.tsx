"use client";

/** Most recent holdings — first five in API order (newest first), honest empty state. */
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { HoldingCard } from "@/components/holdings/HoldingCard";
import { buttonStyles } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";

export function RecentHoldings() {
  const holdingsQuery = useQuery({
    queryKey: ["holdings", "recent"],
    queryFn: () => api.holdings.list(5),
    select: (holdings) => holdings.slice(0, 5),
  });

  if (holdingsQuery.isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-36" />
        ))}
      </div>
    );
  }

  if (holdingsQuery.isError) {
    return (
      <EmptyState
        title="Holdings unavailable"
        description="Your collection loads from the Mintmark API — it could not be reached."
        action={
          <Link href="/collection" className={buttonStyles({ variant: "secondary" })}>
            Go to collection
          </Link>
        }
      />
    );
  }

  if (holdingsQuery.data.length === 0) {
    return (
      <EmptyState
        title="No holdings yet"
        description="Identify your first coin from photos, or add one from the catalog."
        action={
          <Link href="/identify" className={buttonStyles({ variant: "goldAccent" })}>
            Identify a coin
          </Link>
        }
      />
    );
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {holdingsQuery.data.map((holding) => (
        <HoldingCard key={holding.id} holding={holding} />
      ))}
    </div>
  );
}

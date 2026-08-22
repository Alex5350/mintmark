"use client";

/** Collection browser: gallery (cards) | table (dense sortable) tabs. */
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { HoldingCard } from "@/components/holdings/HoldingCard";
import { HoldingTable } from "@/components/holdings/HoldingTable";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

export function CollectionView() {
  const holdingsQuery = useQuery({
    queryKey: ["holdings", "list"],
    queryFn: api.holdings.list,
  });

  if (holdingsQuery.isPending) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }, (_, i) => (
          <Skeleton key={i} className="h-36" />
        ))}
      </div>
    );
  }

  if (holdingsQuery.isError) {
    return (
      <EmptyState
        title="Collection unavailable"
        description="Holdings load from the Mintmark API — it could not be reached. Nothing is faked here."
        action={
          <Button variant="secondary" onClick={() => void holdingsQuery.refetch()}>
            Retry
          </Button>
        }
      />
    );
  }

  if (holdingsQuery.data.length === 0) {
    return (
      <EmptyState
        title="No holdings yet"
        description="Photograph a coin to identify it against the catalog, and it lands here."
      />
    );
  }

  return (
    <Tabs defaultValue="gallery">
      <TabsList aria-label="Collection view">
        <TabsTrigger value="gallery">Gallery</TabsTrigger>
        <TabsTrigger value="table">Table</TabsTrigger>
      </TabsList>
      <TabsContent value="gallery">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {holdingsQuery.data.map((holding) => (
            <HoldingCard key={holding.holdingId} holding={holding} />
          ))}
        </div>
      </TabsContent>
      <TabsContent value="table">
        <HoldingTable holdings={holdingsQuery.data} />
      </TabsContent>
    </Tabs>
  );
}

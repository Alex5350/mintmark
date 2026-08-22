import type { Metadata } from "next";
import { CollectionView } from "@/components/collection/CollectionView";

export const metadata: Metadata = { title: "Collection" };

export default function CollectionPage() {
  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="font-heading text-2xl font-semibold text-ink">Collection</h1>
        <p className="text-sm text-ink-muted">
          Gallery for browsing, table for reckoning — every column sorts.
        </p>
      </header>
      <CollectionView />
    </div>
  );
}

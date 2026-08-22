import type { Metadata } from "next";
import { PricesView } from "@/components/prices/PricesView";

export const metadata: Metadata = { title: "Prices" };

export default function PricesPage() {
  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="font-heading text-2xl font-semibold text-ink">Spot prices</h1>
        <p className="text-sm text-ink-muted">
          Long ranges are downsampled server-side (LTTB); intraday uses bucketed averages. The
          gold-to-silver ratio is a first-class derived series.
        </p>
      </header>
      <PricesView />
    </div>
  );
}

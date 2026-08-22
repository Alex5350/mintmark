import type { Metadata } from "next";
import { IdentifyPanel } from "@/components/identification/IdentifyPanel";

export const metadata: Metadata = { title: "Identify" };

export default function IdentifyPage() {
  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="font-heading text-2xl font-semibold text-ink">Identify a coin</h1>
        <p className="text-sm text-ink-muted">
          Both sides are matched against the catalog — hybrid retrieval over embeddings, legends,
          and structured filters. You confirm the final call; every run is audited.
        </p>
      </header>
      <IdentifyPanel />
    </div>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { SpotTicker } from "@/components/spot/SpotTicker";
import { buttonStyles } from "@/components/ui/button";

export const metadata: Metadata = {
  title: "Mintmark — every coin, every grain, accounted for",
};

const PILLARS = [
  {
    title: "Melt, honestly",
    body: "Actual metal weight × quantity × live spot — with the provider and timestamp printed next to every number.",
  },
  {
    title: "Collectible, explainable",
    body: "Premium factors are itemized, versioned, and banded by confidence. No black-box estimates.",
  },
  {
    title: "Stale is never silent",
    body: "When a price provider blinks, the last known good price is served — and badged STALE everywhere it appears.",
  },
] as const;

export default function HomePage() {
  return (
    <div className="flex flex-col gap-12">
      <SpotTicker />

      <section className="mx-auto max-w-3xl py-8 text-center">
        <h1 className="font-heading text-3xl font-semibold tracking-tight text-ink sm:text-4xl">
          Every coin. Every grain. Accounted for.
        </h1>
        <p className="mx-auto mt-4 max-w-xl text-base text-ink-muted">
          Mintmark tracks your precious-metals collection with two numbers per coin — melt and
          collectible — each carrying its full provenance from spot feed to premium factor.
        </p>
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <Link href="/register" className={buttonStyles({ variant: "primary", size: "lg" })}>
            Start your collection
          </Link>
          <Link href="/prices" className={buttonStyles({ variant: "secondary", size: "lg" })}>
            See live prices
          </Link>
        </div>
      </section>

      <section aria-label="Why Mintmark" className="grid gap-4 sm:grid-cols-3">
        {PILLARS.map((pillar) => (
          <div key={pillar.title} className="rounded-lg border border-border bg-surface p-4">
            <h2 className="font-heading text-base font-semibold text-ink">{pillar.title}</h2>
            <p className="mt-1.5 text-sm text-ink-muted">{pillar.body}</p>
          </div>
        ))}
      </section>

      <section className="rounded-lg border border-dashed border-border bg-surface/50 p-6 text-center">
        <h2 className="font-heading text-lg font-semibold text-ink">Live from the Mintmark API</h2>
        <p className="mx-auto mt-1.5 max-w-xl text-sm text-ink-muted">
          Every screen loads real data — holdings, valuations, and spot prices straight from the
          API. When something is missing, you see an honest empty state; nothing here is mock data.
        </p>
      </section>
    </div>
  );
}

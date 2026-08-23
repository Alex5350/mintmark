"use client";

/** Prices page shell: metal switch + chart. */
import { useState } from "react";
import type { Metal } from "@/lib/enums";
import { PriceChart } from "@/components/charts/PriceChart";
import { Button } from "@/components/ui/button";

const METALS: readonly Metal[] = ["Gold", "Silver"] as const;

export function PricesView() {
  const [metal, setMetal] = useState<Metal>("Gold");

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div
          role="group"
          aria-label="Metal"
          className="flex items-center gap-1 rounded-lg bg-surface-raised p-1"
        >
          {METALS.map((m) => (
            <Button
              key={m}
              size="sm"
              variant={metal === m ? "secondary" : "ghost"}
              aria-pressed={metal === m}
              className="min-h-9 min-w-16"
              onClick={() => setMetal(m)}
            >
              {m}
            </Button>
          ))}
        </div>
      </div>
      <PriceChart metal={metal} />
    </div>
  );
}

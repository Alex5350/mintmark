"use client";

/**
 * Obverse/reverse flip. CSS 3D flip on click or Enter/Space; reduced motion is
 * honored in globals.css (prefers-reduced-motion → opacity crossfade).
 */
import { useState } from "react";
import Image from "next/image";
import { cn } from "@/lib/cn";

export interface CoinFlipProps {
  /** Resolvable image URLs (presigned). Storage keys alone are not URLs. */
  obverseSrc?: string | null;
  reverseSrc?: string | null;
  /** What the coin is, for screen-reader labels. */
  label: string;
  size?: "md" | "lg";
  className?: string;
}

function CoinFace({
  src,
  side,
  label,
}: {
  src?: string | null;
  side: "obverse" | "reverse";
  label: string;
}) {
  if (src) {
    return (
      <Image
        src={src}
        alt={`${label} — ${side}`}
        fill
        unoptimized
        sizes="192px"
        className="object-cover"
      />
    );
  }
  return (
    <div className="flex size-full flex-col items-center justify-center gap-1 rounded-full bg-surface-raised">
      <span aria-hidden="true" className="font-heading text-2xl font-semibold text-ink-muted">
        {side === "obverse" ? "O" : "R"}
      </span>
      <span aria-hidden="true" className="text-[0.625rem] tracking-wide text-ink-muted uppercase">
        {side}
      </span>
    </div>
  );
}

export function CoinFlip({ obverseSrc, reverseSrc, label, size = "md", className }: CoinFlipProps) {
  const [flipped, setFlipped] = useState(false);
  const shown = flipped ? "reverse" : "obverse";

  return (
    <div className={cn("flex flex-col items-center gap-2", className)}>
      <button
        type="button"
        onClick={() => setFlipped((f) => !f)}
        className={cn(
          "coin-flip cursor-pointer rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus focus-visible:ring-offset-2 focus-visible:ring-offset-base",
          size === "lg" ? "size-48" : "size-32",
        )}
        aria-pressed={flipped}
        aria-label={`${label} — showing ${shown}. Activate to flip.`}
      >
        <span className="coin-flip-inner block size-full">
          <span className="coin-flip-face block size-full overflow-hidden rounded-full border border-border">
            <CoinFace src={obverseSrc} side="obverse" label={label} />
          </span>
          <span className="coin-flip-back overflow-hidden rounded-full border border-border">
            <CoinFace src={reverseSrc} side="reverse" label={label} />
          </span>
        </span>
      </button>
      <p className="text-xs text-ink-muted" aria-live="polite">
        Showing {shown} — flip to {flipped ? "obverse" : "reverse"}
      </p>
    </div>
  );
}

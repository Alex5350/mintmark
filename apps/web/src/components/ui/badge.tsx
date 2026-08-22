import type { HTMLAttributes } from "react";
import { metalAccent } from "@mintmark/ui-tokens";
import type { Metal as ApiMetal } from "@/lib/api-types";
import { cn } from "@/lib/cn";

export type BadgeTone =
  | "neutral"
  | "gold"
  | "silver"
  | "platinum"
  | "palladium"
  | "positive"
  | "negative"
  | "warning";

const toneStyles: Record<BadgeTone, string> = {
  neutral: "border-border bg-surface-raised text-ink-muted",
  // Metal tones are SEMANTIC — gold only for gold, never decoration.
  gold: "border-gold/50 bg-gold/10 text-gold",
  silver: "border-silver/50 bg-silver/10 text-silver",
  platinum: "border-platinum/50 bg-platinum/10 text-platinum",
  palladium: "border-palladium/50 bg-palladium/10 text-palladium",
  positive: "border-positive/50 bg-positive/10 text-positive",
  negative: "border-negative/50 bg-negative/10 text-negative",
  warning: "border-warning/50 bg-warning/10 text-warning",
};

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
}

export function Badge({ tone = "neutral", className, ...props }: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium whitespace-nowrap",
        toneStyles[tone],
        className,
      )}
      {...props}
    />
  );
}

/** Metal-accented badge — tone resolved through the ui-tokens `metalAccent` map. */
export function MetalBadge({ metal, ...props }: { metal: ApiMetal } & BadgeProps) {
  const tone = metalAccent[metal] as BadgeTone;
  return (
    <Badge tone={tone} {...props}>
      {metal}
    </Badge>
  );
}

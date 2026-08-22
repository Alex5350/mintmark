import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

function CoinGlyph({ className }: { className?: string }) {
  return (
    <svg
      width="40"
      height="40"
      viewBox="0 0 40 40"
      fill="none"
      aria-hidden="true"
      className={className}
    >
      <circle cx="20" cy="20" r="17" stroke="currentColor" strokeWidth="2" />
      <circle cx="20" cy="20" r="11.5" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M20 13.5l1.9 4 4.4.5-3.3 3 1 4.4-4-2.2-4 2.2 1-4.4-3.3-3 4.4-.5 1.9-4z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export interface EmptyStateProps {
  title: string;
  description?: string;
  /** Action slot (e.g. a Button or Link). */
  action?: ReactNode;
  className?: string;
}

/** Honest empty/loading-failure state — never fake data. */
export function EmptyState({ title, description, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border px-6 py-12 text-center",
        className,
      )}
    >
      <CoinGlyph className="text-ink-muted/60" />
      <div className="space-y-1">
        <p className="font-heading text-base font-semibold text-ink">{title}</p>
        {description ? <p className="max-w-sm text-sm text-ink-muted">{description}</p> : null}
      </div>
      {action ? <div className="mt-1">{action}</div> : null}
    </div>
  );
}

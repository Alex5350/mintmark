/** Minimal class-name join (no clsx/tailwind-merge dependency needed). */
export function cn(...parts: ReadonlyArray<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(" ");
}

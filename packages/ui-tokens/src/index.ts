/**
 * Mintmark design tokens — the single source of palette, type, spacing, and
 * radii shared by web and mobile. Dark-first (the collector's tool default),
 * light mode equally first-class. Metallic accents carry SEMANTIC meaning
 * (gold on gold, silver on silver) and are never decorative gradients.
 */

export const palette = {
  dark: {
    base: "#0E1116", // charcoal — page background
    surface: "#161B22", // cards, tables
    surfaceRaised: "#1D232C",
    border: "#2A323D",
    text: "#E6E9EE",
    textMuted: "#9BA4B0",
    gold: "#C9A227", // muted, desaturated gold — WCAG AA on base
    goldSoft: "#E3C55C",
    silver: "#B9C0C9",
    platinum: "#C8D0DC",
    palladium: "#9FB3C8",
    positive: "#3FB68B",
    negative: "#E5484D",
    warning: "#E0A32E",
    focus: "#7AB8FF",
  },
  light: {
    base: "#F7F8FA",
    surface: "#FFFFFF",
    surfaceRaised: "#F0F2F5",
    border: "#D9DEE5",
    text: "#1A2028",
    textMuted: "#5A6472",
    gold: "#8A6D14", // darkened for AA contrast on light base
    goldSoft: "#A5841E",
    silver: "#6B7480",
    platinum: "#5D6B7C",
    palladium: "#4E6076",
    positive: "#187A57",
    negative: "#C22127",
    warning: "#9A6B00",
    focus: "#1D5FAE",
  },
} as const;

/** Type scale. Headings: engraved-legend serif; data/UI: legible sans. */
export const typography = {
  fontFamily: {
    heading: "var(--font-heading)", // refined transitional serif (web wires it)
    body: "var(--font-body)", // clean sans
  },
  size: {
    xs: "0.75rem",
    sm: "0.875rem",
    base: "1rem",
    lg: "1.125rem",
    xl: "1.375rem",
    "2xl": "1.75rem",
    "3xl": "2.25rem",
  },
  /** Every price, weight, and quantity renders with tabular figures so
   * columns align and digits do not jitter as prices tick. */
  numeric: { fontVariantNumeric: "tabular-nums", fontFeatureSettings: "'tnum'" },
} as const;

export const spacing = {
  1: "0.25rem",
  2: "0.5rem",
  3: "0.75rem",
  4: "1rem",
  6: "1.5rem",
  8: "2rem",
  12: "3rem",
} as const;

export const radii = { sm: "0.375rem", md: "0.5rem", lg: "0.75rem", full: "9999px" } as const;

/** Semantic metal → accent token (holdings, charts, badges). */
export const metalAccent = {
  Gold: "gold",
  Silver: "silver",
  Platinum: "platinum",
  Palladium: "palladium",
} as const;

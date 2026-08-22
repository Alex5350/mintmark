/**
 * Token adapter: consumes @mintmark/ui-tokens (the shared source of truth)
 * and converts its CSS-flavored values into React Native numbers/styles.
 *
 * Dark-first: this scaffold pins the dark palette (the collector's tool
 * default per the token docs); light-mode wiring lands with theme switching.
 */
import {
  metalAccent,
  palette,
  radii,
  spacing,
  typography,
} from '@mintmark/ui-tokens';
import type { TextStyle } from 'react-native';

export const colors = palette.dark;

export type PaletteToken = keyof typeof colors;
export type Metal = 'Gold' | 'Silver' | 'Platinum' | 'Palladium';

/** Semantic metal -> accent color (holdings rows, badges, charts). */
export function metalColor(metal: string): string {
  const token = (metalAccent as Record<string, PaletteToken | undefined>)[metal];
  return token ? colors[token] : colors.textMuted;
}

/** 1rem in the token source maps to 16dp on mobile. */
const REM = 16;
const remToNumber = (value: string): number => Number.parseFloat(value) * REM;

const remEntries = (source: Record<string, string>): Record<string, number> =>
  Object.fromEntries(
    Object.entries(source).map(([key, value]) => [key, remToNumber(value)]),
  );

export const space = remEntries(spacing) as {
  [K in keyof typeof spacing]: number;
};

export const radius: { [K in keyof typeof radii]: number } = {
  ...(remEntries(radii) as { [K in keyof typeof radii]: number }),
  full: 9999, // pills: any value at-or-beyond half-height rounds fully
};

export const fontSize = remEntries(typography.size) as {
  [K in keyof typeof typography['size']]: number;
};

/**
 * Tabular figures for every price, weight, and quantity — the mobile
 * equivalent of the token package's `numeric` (font-variant-numeric:
 * tabular-nums / font-feature-settings 'tnum').
 */
export const tabular: TextStyle = {
  fontVariant: ['tabular-nums'],
};

export const fontWeight = {
  regular: '400',
  medium: '500',
  semibold: '600',
  bold: '700',
} as const;

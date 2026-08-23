/**
 * Weight units and conversion for precious metals. One conversion site,
 * the exact factor; everything else flows through here. Companion to the
 * C# `Mintmark.Domain.ValueObjects.Weight` — the two must agree (cross-
 * checked by a shared golden test file in both test suites).
 */

export type WeightUnit = "grams" | "troyOunces";

/** 1 troy ounce = 31.1034768 grams — exact, by international definition. */
export const GRAMS_PER_TROY_OUNCE = 31.1034768;

export interface Weight {
  readonly magnitude: number;
  readonly unit: WeightUnit;
}

export function toGrams(w: Weight): number {
  return w.unit === "grams" ? w.magnitude : w.magnitude * GRAMS_PER_TROY_OUNCE;
}

export function toTroyOunces(w: Weight): number {
  return w.unit === "troyOunces" ? w.magnitude : w.magnitude / GRAMS_PER_TROY_OUNCE;
}

export function grams(magnitude: number): Weight {
  return { magnitude, unit: "grams" };
}

export function troyOunces(magnitude: number): Weight {
  return { magnitude, unit: "troyOunces" };
}

/** Melt value uses ACTUAL metal weight (ASW/AGW), never gross weight. */
export function meltValue(
  actualMetalWeightTroyOz: number,
  quantity: number,
  spotPricePerTroyOz: number,
): number {
  return actualMetalWeightTroyOz * quantity * spotPricePerTroyOz;
}

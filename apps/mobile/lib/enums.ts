/**
 * Int-enum → label mappers. The API serializes enums as numbers, so
 * presentation code resolves labels through these maps instead of trusting
 * magic numbers. Values mirror Mintmark.Domain/Enums.cs and the web client's
 * apps/web/src/lib/enums.ts.
 */

export type Metal = "Gold" | "Silver" | "Platinum" | "Palladium";

const METAL_BY_CODE: Record<number, Metal> = {
  0: "Gold",
  1: "Silver",
  2: "Platinum",
  3: "Palladium",
};

/** 0=Gold 1=Silver 2=Platinum 3=Palladium; null (generic item) → "Unspecified". */
export function metalLabel(metal: number | null | undefined): Metal | "Unspecified" {
  if (metal == null) return "Unspecified";
  return METAL_BY_CODE[metal] ?? "Unspecified";
}

/** Known metal label or null — for components that render metal-driven UI only
 * when the API actually sent one (generic holdings have metal: null). */
export function knownMetal(metal: number | null | undefined): Metal | null {
  if (metal == null) return null;
  return METAL_BY_CODE[metal] ?? null;
}

const ITEM_FORM_BY_CODE: Record<number, string> = {
  0: "Coin",
  1: "Round",
  2: "Bar",
  3: "Ingot",
  4: "Junk silver",
  5: "Scrap",
  6: "Jewelry",
};

export function itemFormLabel(form: number): string {
  return ITEM_FORM_BY_CODE[form] ?? "Unknown form";
}

/** IdentificationJobStatus ints that mean "keep polling". 0=Queued. */
export function identificationStatusPolling(status: number): boolean {
  return status === 0;
}

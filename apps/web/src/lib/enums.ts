/**
 * Int-enum → label mappers. The API serializes enums as numbers (JSON.NET
 * policy), so presentation code resolves labels through these maps instead of
 * trusting magic numbers. Values mirror Mintmark.Domain/Enums.cs.
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

/** IdentificationJobStatus: 0=Queued, 1=AwaitingConfirmation, 2=Confirmed, 3=Failed. */
export function identificationStatusLabel(status: number): string {
  switch (status) {
    case 0:
      return "queued";
    case 1:
      return "awaiting confirmation";
    case 2:
      return "confirmed";
    case 3:
      return "failed";
    default:
      return "unknown";
  }
}

/** IdentificationJobStatus ints that mean "keep polling". */
export function identificationStatusPolling(status: number | undefined): boolean {
  return status === 0;
}

const DOWNSAMPLE_BY_CODE: Record<number, string> = {
  0: "raw closes",
  1: "LTTB downsampled",
  2: "daily aggregates",
  3: "weekly aggregates",
  4: "monthly aggregates",
};

export function downsampleLabel(method: number | undefined): string | null {
  if (method == null) return null;
  return DOWNSAMPLE_BY_CODE[method] ?? "downsampled";
}

/** The chart endpoint takes lowercase metal names as its `metal` query param. */
export function chartMetalParam(metal: Metal): string {
  return metal.toLowerCase();
}

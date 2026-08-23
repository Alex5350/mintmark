/**
 * Response DTO mirrors for the live Mintmark API. The generated
 * `@mintmark/api-client` types the paths, parameters, and request bodies from
 * the committed OpenAPI doc, but that doc does not type response payloads —
 * so these mirrors carry the response side. Every field here was verified
 * against the running API; keep in sync with Mintmark.Application.Dtos.
 *
 * Server enums serialize as ints — lib/enums.ts maps them to labels.
 */

// ---------------------------------------------------------------------------
// Shared value shapes
// ---------------------------------------------------------------------------

/** Decimal amount + ISO currency (the wire object also carries isZero). */
export interface Money {
  amount: number;
  currency: string;
}

/** POST /api/v1/auth/login | /register | /refresh. */
export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  tokenType: string;
}

// ---------------------------------------------------------------------------
// Holdings
// ---------------------------------------------------------------------------

/** Row of GET /api/v1/holdings (cursor-paginated list). */
export interface HoldingListItem {
  id: number;
  displayName: string;
  /** MetalKind int (0=Gold, 1=Silver, 2=Platinum, 3=Palladium); null for generic items. */
  metal: number | null;
  /** ItemForm int (0=Coin … 6=Jewelry). */
  form: number;
  effectiveQuantity: number;
  effectivePurchasePricePerUnit: Money;
  currentValue: Money | null;
}

export interface HoldingListResponse {
  items: HoldingListItem[];
  nextCursor: string | null;
}

/** GET /api/v1/holdings/{id}. */
export interface HoldingDetail {
  id: number;
  coinTypeId: number | null;
  displayName: string;
  form: number;
  originalQuantity: number;
  effectiveQuantity: number;
  originalPurchasePricePerUnit: Money;
  effectivePurchasePricePerUnit: Money;
  revisionCount: number;
  currentMelt: Money | null;
  currentCollectible: Money | null;
  purchasedAtUtc: string;
  isDeleted: boolean;
}

export interface PremiumFactor {
  factorName: string;
  multiplier: number;
  rationale: string;
}

/** GET /api/v1/holdings/{id}/valuation — explainable valuation with provenance. */
export interface HoldingValuation {
  holdingId: number;
  melt: Money;
  collectible: Money;
  premium: Money;
  premiumMultiplier: number;
  premiumFactors: PremiumFactor[];
  confidenceBand: {
    lowFraction: number;
    highFraction: number;
    lowValue: Money;
    highValue: Money;
  };
  provenance: {
    spotPricePerTroyOunce: Money;
    source: string;
    sourceTimestampUtc: string;
    method: string;
    methodVersion: string;
  };
  computedAtUtc: string;
}

// ---------------------------------------------------------------------------
// Portfolio
// ---------------------------------------------------------------------------

export interface RollupByMetal {
  metal: number;
  value: Money;
  /** Share of portfolio value in [0, 1]. */
  weight: number;
}

export interface RollupBySeries {
  seriesId: number;
  seriesName: string;
  value: Money;
  /** Share of portfolio value in [0, 1]. */
  weight: number;
}

/** GET /api/v1/portfolio/rollup. */
export interface PortfolioRollup {
  holdingCount: number;
  costBasis: Money;
  currentValue: Money;
  unrealizedPct: number;
  byMetal: RollupByMetal[];
  bySeries: RollupBySeries[];
}

// ---------------------------------------------------------------------------
// Market data
// ---------------------------------------------------------------------------

/** Row of GET /api/v1/prices/current. */
export interface SpotQuote {
  metal: number;
  currency: string;
  price: number;
  bid: number;
  ask: number;
  provider: string;
  sourceTimestampUtc: string;
  isStale: boolean;
  staleSince: string | null;
}

export type ChartRange = "1D" | "1W" | "1M" | "3M" | "6M" | "1Y" | "5Y" | "MAX";

/** GET /api/v1/prices/chart — daily closes, downsampled server-side for long ranges. */
export interface ChartSeries {
  metal: number;
  currency: string;
  range: { start: string; end: string; dayCount: number };
  points: Array<{ date: string; close: Money }>;
  /** ChartDownsampleMethod int (see lib/enums.ts). */
  downsampleMethod?: number;
}

/** GET /api/v1/prices/ratio — gold ÷ silver per day. */
export interface RatioPoint {
  date: string;
  ratio: number;
}

// ---------------------------------------------------------------------------
// Catalog
// ---------------------------------------------------------------------------

/** GET /api/v1/catalog/coin-types/{id} — detail plus presigned reference images. */
export interface CoinTypeDetail {
  detail: {
    id: number;
    seriesId: number;
    seriesName: string;
    mintId: number;
    mintName: string;
    year: number;
    name: string;
    metal: number;
    fineness: number;
    grossWeightGrams: number;
    actualMetalWeightTroyOz: number;
    diameterMillimeters: number | null;
    thicknessMillimeters: number | null;
    edge: number;
    finish: number;
    finishAttributes: number;
    mintage: number | null;
    sourceUrl: string | null;
    kmNumber: string | null;
    redBookReference: string | null;
  };
  obverseImageUrl: string | null;
  reverseImageUrl: string | null;
}

// ---------------------------------------------------------------------------
// Identification
// ---------------------------------------------------------------------------

/** 202 from POST /api/v1/identification/submit. */
export interface IdentificationSubmitResult {
  jobId: number;
  deduplicated: boolean;
}

export interface IdentificationCandidate {
  coinTypeId: number;
  score: number;
}

/** GET /api/v1/identification/{jobId}/status. */
export interface IdentificationStatusResponse {
  jobId: number;
  /** IdentificationJobStatus int (0=Queued, 1=AwaitingConfirmation, 2=Confirmed, 3=Failed). */
  status: number;
  providerLabel: string;
  promptTemplateVersion: string;
  createdAtUtc: string;
  /** Confidence in [0, 1] keyed by contract field name (series, year, mint, …). */
  perFieldConfidences: Record<string, number>;
  candidates: IdentificationCandidate[];
  confirmedCoinTypeId: number | null;
}

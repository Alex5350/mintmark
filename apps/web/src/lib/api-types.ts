/**
 * API DTO mirror (camelCase JSON). Hand-written against the committed API
 * contract; the generated `@mintmark/api-client` replaces `lib/api.ts` in a
 * later phase and these types move with it. Keep field-for-field in sync.
 */

// ---------------------------------------------------------------------------
// Enums (string-literal unions)
// ---------------------------------------------------------------------------

export type Metal = "Gold" | "Silver" | "Platinum" | "Palladium";

export type ItemForm = "Coin" | "Round" | "Bar" | "Ingot" | "JunkSilver" | "Scrap" | "Jewelry";

export type FinishPrimary =
  | "BusinessStrike"
  | "BullionUncirculated"
  | "Proof"
  | "ReverseProof"
  | "Burnished"
  | "MatteProof"
  | "Unknown";

export type FinishAttribute = "HighRelief" | "Enhanced" | "Colorized" | "Antiqued" | "FirstStrike";

export type GradingService = "NGC" | "PCGS" | "ANACS" | "ICG" | "Raw";

export type ChartRange = "1D" | "1W" | "1M" | "3M" | "1Y" | "5Y" | "MAX";

/** Server-side chart downsampling strategy (see docs/architecture.md). */
export type DownsampleMethod = "lttb" | "bucketedAverage";

export type IdentificationStatus = "pending" | "running" | "complete" | "failed";

export type IdentificationProvider = "offline" | "openai" | "gemini";

// ---------------------------------------------------------------------------
// Shared value shapes
// ---------------------------------------------------------------------------

/** Decimal amount + ISO currency code. Never float arithmetic client-side. */
export interface Money {
  amount: number;
  currency: string;
}

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

export interface User {
  userId: string;
  email: string;
  displayName?: string | null;
  createdAt: string;
}

/** Spot row a melt valuation derived from — provenance travels with the number. */
export interface SpotProvenance {
  price: Money;
  provider: string;
  sourceTimestamp: string;
  stale: boolean;
}

export interface ImageKeys {
  obverse: string | null;
  reverse: string | null;
}

/** Catalog row referenced by a holding (nullable for generic bars/rounds). */
export interface CoinTypeRef {
  coinTypeId: string;
  seriesName: string;
  year: number;
  mintMark?: string | null;
  finishPrimary: FinishPrimary;
  finishAttributes: FinishAttribute[];
  metal: Metal;
  actualMetalWeightTroyOz: number;
  imageKeys?: ImageKeys | null;
}

// ---------------------------------------------------------------------------
// Valuation
// ---------------------------------------------------------------------------

export interface MeltValuation {
  amount: Money;
  spot: SpotProvenance;
}

export interface PremiumFactor {
  name: string;
  multiplier: number;
  rationale: string;
}

export interface CollectibleValuation {
  amount: Money;
  confidenceLow: number;
  confidenceHigh: number;
  methodVersion: string;
  premiumFactors: PremiumFactor[];
}

export interface CurrentValuation {
  melt: MeltValuation;
  collectible: CollectibleValuation;
}

// ---------------------------------------------------------------------------
// Holdings
// ---------------------------------------------------------------------------

export interface Grading {
  service: GradingService;
  grade: number;
  designation?: string | null;
  certNumber?: string | null;
}

export interface Holding {
  holdingId: string;
  coinType?: CoinTypeRef | null;
  itemForm: ItemForm;
  quantity: number;
  purchaseDate: string;
  purchasePricePerUnit: Money;
  dealer?: string | null;
  storageLocation?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
  grading?: Grading | null;
  currentValuation?: CurrentValuation | null;
  createdAt: string;
}

/** Writable subset for POST /holdings (create sends an Idempotency-Key header). */
export interface HoldingCreateInput {
  coinTypeId?: string | null;
  itemForm: ItemForm;
  quantity: number;
  purchaseDate: string;
  purchasePricePerUnit: Money;
  dealer?: string | null;
  storageLocation?: string | null;
  serialNumber?: string | null;
  notes?: string | null;
}

export type HoldingUpdateInput = Partial<HoldingCreateInput>;

// ---------------------------------------------------------------------------
// Market data
// ---------------------------------------------------------------------------

export interface SpotQuote {
  metal: Metal;
  price: Money;
  provider: string;
  sourceTimestamp: string;
  stale: boolean;
}

export interface ChartPoint {
  /** Epoch milliseconds. */
  t: number;
  /** Decimal serialized as a JSON number. */
  price: number;
}

/** Series subject — a metal, or the first-class derived gold/silver ratio. */
export type ChartSeriesSubject = Metal | "GoldSilverRatio";

export interface ChartSeries {
  metal: ChartSeriesSubject;
  range: ChartRange;
  points: ChartPoint[];
  downsampleMethod: DownsampleMethod;
  stale: boolean;
}

// ---------------------------------------------------------------------------
// Portfolio
// ---------------------------------------------------------------------------

export interface RollupByMetal {
  metal: Metal;
  troyOz: number;
  valueSharePct: number;
}

export interface RollupTopSeries {
  series: string;
  units: number;
  value: Money;
}

export interface PortfolioRollup {
  totalMelt: Money;
  totalCollectible: Money;
  costBasis: Money;
  unrealizedPct: number;
  byMetal: RollupByMetal[];
  topSeries: RollupTopSeries[];
}

// ---------------------------------------------------------------------------
// Identification
// ---------------------------------------------------------------------------

export interface IdentifiedField {
  value: string | number;
  confidence: number;
  evidence?: string | null;
}

export type IdentificationFieldName = "series" | "year" | "mintMark" | "finishPrimary" | "metal";

export interface IdentificationCandidate {
  coinTypeId: string;
  seriesName: string;
  year: number;
  score: number;
}

export interface IdentificationJob {
  jobId: string;
  status: IdentificationStatus;
  provider: IdentificationProvider;
  fields: Partial<Record<IdentificationFieldName, IdentifiedField>>;
  candidates: IdentificationCandidate[];
  confirmedCoinTypeId?: string | null;
}

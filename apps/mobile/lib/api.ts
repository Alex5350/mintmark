/**
 * Typed API client for the Mintmark API.
 *
 * Wire contracts mirror the committed OpenAPI document (docs/openapi.json)
 * and the generated web client: enums arrive as ints (label maps live in
 * lib/enums.ts), money as {amount, currency, isZero}, dates as ISO strings.
 *
 * Transport behavior:
 * - Base URL: expo-constants `expoConfig.extra.apiBaseUrl`
 *   (default http://127.0.0.1:5100 — IPv4 loopback deliberately, because
 *   the iOS simulator sandbox resolves `localhost` to IPv6 `::1` first
 *   and cannot open IPv6 loopback sockets, so every request fails with
 *   an opaque "Failed to fetch"; plain 127.0.0.1 sidesteps resolution).
 * - JSON is camelCase.
 * - 401 -> refresh once via rotating refresh token, retry the original
 *   request; if refresh fails the session is cleared and UnauthorizedError
 *   is thrown so the app can return to login.
 * - Mutations may carry an Idempotency-Key header (flaky-network double
 *   submits must not create duplicate holdings).
 */
import Constants from 'expo-constants';
import { Platform } from 'react-native';
import { clearTokens, getTokens, setTokens } from './tokens';

const extra = (Constants.expoConfig?.extra ?? {}) as { apiBaseUrl?: string };

const DEFAULT_BASE_URL =
  Platform.OS === 'ios' ? 'http://127.0.0.1:5100' : 'http://10.0.2.2:5100';

export const API_BASE_URL: string = extra.apiBaseUrl ?? DEFAULT_BASE_URL;

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, body: unknown, message?: string) {
    super(message ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.body = body;
  }
}

/** Fetch-level transport failure (offline, DNS, server unreachable). */
export class NetworkError extends Error {
  constructor(message = 'Network request failed') {
    super(message);
    this.name = 'NetworkError';
  }
}

/** Thrown when a 401 survives the single refresh-and-retry. */
export class UnauthorizedError extends ApiError {
  constructor() {
    super(401, null, 'Session expired');
    this.name = 'UnauthorizedError';
  }
}

export const isNetworkError = (error: unknown): error is NetworkError =>
  error instanceof NetworkError;

export const isUnauthorizedError = (error: unknown): error is UnauthorizedError =>
  error instanceof UnauthorizedError;

// ---------------------------------------------------------------------------
// Wire types (docs/openapi.json / live API)
// ---------------------------------------------------------------------------

export interface Money {
  amount: number;
  currency: string;
  isZero: boolean;
}

export interface User {
  id: string;
  email: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc?: string;
  tokenType?: string;
}

export interface HoldingListItem {
  id: number;
  displayName: string;
  metal: number | null;
  form: number;
  effectiveQuantity: number;
  effectivePurchasePricePerUnit: Money | null;
  currentValue: Money | null;
}

export interface HoldingsPage {
  items: HoldingListItem[];
  nextCursor: string | null;
}

export interface HoldingDetail {
  id: number;
  coinTypeId: number | null;
  displayName: string;
  form: number;
  originalQuantity: number;
  effectiveQuantity: number;
  originalPurchasePricePerUnit: Money | null;
  effectivePurchasePricePerUnit: Money | null;
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

export interface Valuation {
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

export interface PortfolioRollup {
  holdingCount: number;
  costBasis: Money | null;
  currentValue: Money | null;
  unrealizedPct: number | null;
  byMetal: { metal: number; value: Money; weight: number }[];
  bySeries: { seriesId: number; seriesName: string; value: Money; weight: number }[];
}

export interface SpotQuote {
  /** MetalKind int: 0 Gold, 1 Silver, 2 Platinum, 3 Palladium. */
  metal: number;
  currency: string;
  price: number;
  bid: number;
  ask: number;
  provider?: string | null;
  sourceTimestampUtc: string;
  /** Stale is never silent (architecture doc): served from last known good
   * price while a provider outage resolves. */
  isStale: boolean;
  staleSince?: string | null;
}

export interface IdentificationSubmitResult {
  jobId: number;
  deduplicated: boolean;
}

export interface IdentificationCandidateWire {
  coinTypeId: number;
  /** Blended hybrid-search match score, 0..1. */
  score: number;
}

/** IdentificationJobStatus: 0 Queued, 1 AwaitingConfirmation, 2 Confirmed, 3 Failed. */
export interface IdentificationStatus {
  jobId: number;
  status: number;
  providerLabel: string;
  promptTemplateVersion: string;
  createdAtUtc: string;
  perFieldConfidences: Record<string, number>;
  candidates: IdentificationCandidateWire[];
  confirmedCoinTypeId: number | null;
}

export interface CoinTypeDetail {
  detail: {
    id: number;
    seriesId: number;
    seriesName: string;
    mintId: number;
    mintName: string;
    year: number;
    name: string;
    metal: number | null;
    fineness: number;
    grossWeightGrams: number;
    actualMetalWeightTroyOz: number;
    diameterMillimeters: number | null;
    mintage?: number | null;
  };
  obverseImageUrl?: string | null;
  reverseImageUrl?: string | null;
}

/** A picked/captured photo ready for multipart upload. */
export interface ImagePart {
  uri: string;
  name: string;
  type: string;
}

/** JWT claims the API puts in access tokens (sub, email). */
export function claimsFromToken(token: string): { sub?: string; email?: string } {
  const payload = token.split('.')[1];
  if (!payload) return {};
  try {
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const json = atob(normalized);
    return JSON.parse(json) as { sub?: string; email?: string };
  } catch {
    return {};
  }
}

// ---------------------------------------------------------------------------
// Transport
// ---------------------------------------------------------------------------

export interface RequestOptions {
  query?: Record<string, string | number | boolean | undefined | null>;
  /** JSON body (camelCase). */
  body?: unknown;
  /** Multipart body (identification images). Mutually exclusive with body. */
  formData?: FormData;
  idempotencyKey?: string;
  /** Skip attaching the access token (auth endpoints). */
  skipAuth?: boolean;
}

async function rawFetch(
  method: Method,
  path: string,
  options: RequestOptions,
  accessToken: string | null,
): Promise<Response> {
  const url = new URL(path, API_BASE_URL);
  for (const [key, value] of Object.entries(options.query ?? {})) {
    if (value !== undefined && value !== null) {
      url.searchParams.set(key, String(value));
    }
  }

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.idempotencyKey) headers['Idempotency-Key'] = options.idempotencyKey;
  if (accessToken && !options.skipAuth) {
    headers.Authorization = `Bearer ${accessToken}`;
  }

  let body: BodyInit | undefined;
  if (options.formData) {
    body = options.formData; // fetch sets the multipart boundary itself
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(options.body);
  }

  try {
    return await fetch(url.toString(), { method, headers, body });
  } catch (error) {
    throw new NetworkError(
      error instanceof Error ? error.message : 'Network request failed',
    );
  }
}

async function parseBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function errorFor(response: Response, body: unknown): ApiError {
  const detail =
    typeof body === 'object' && body !== null && 'error' in body
      ? String((body as { error: unknown }).error)
      : undefined;
  return new ApiError(response.status, body, detail);
}

// --- refresh-once retry -----------------------------------------------------

let refreshInFlight: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      const tokens = await getTokens();
      if (!tokens) return false;
      const response = await rawFetch(
        'POST',
        '/api/v1/auth/refresh',
        { body: { refreshToken: tokens.refreshToken }, skipAuth: true },
        null,
      );
      if (!response.ok) return false;
      const rotated = (await parseBody(response)) as AuthResponse;
      if (!rotated?.accessToken || !rotated?.refreshToken) return false;
      await setTokens({
        accessToken: rotated.accessToken,
        refreshToken: rotated.refreshToken,
      });
      return true;
    } catch {
      return false;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

/**
 * Set by the session layer: called when a request 401s even after the
 * refresh-once retry, so the app can drop back to the login screen.
 */
export let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

async function request<T>(
  method: Method,
  path: string,
  options: RequestOptions = {},
  isRetry = false,
): Promise<T> {
  let tokens = await getTokens();
  let response = await rawFetch(method, path, options, tokens?.accessToken ?? null);

  if (
    response.status === 401 &&
    !options.skipAuth &&
    !isRetry &&
    tokens?.refreshToken
  ) {
    const refreshed = await refreshTokens();
    if (refreshed) {
      tokens = await getTokens();
      response = await rawFetch(
        method,
        path,
        options,
        tokens?.accessToken ?? null,
      );
      if (response.ok) {
        return (await parseBody(response)) as T;
      }
    }
    await clearTokens();
    onUnauthorized?.();
    throw new UnauthorizedError();
  }

  if (!response.ok) {
    throw errorFor(response, await parseBody(response));
  }
  return (await parseBody(response)) as T;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

function imagePartFormData(parts: {
  obverse: ImagePart;
  reverse: ImagePart;
  edge?: ImagePart | null;
}): FormData {
  const formData = new FormData();
  // RN FormData accepts {uri, name, type} parts at runtime; the DOM lib
  // signature only knows Blob, hence the cast.
  formData.append('obverse', parts.obverse as unknown as Blob);
  formData.append('reverse', parts.reverse as unknown as Blob);
  if (parts.edge) formData.append('edge', parts.edge as unknown as Blob);
  return formData;
}

export const api = {
  auth: {
    register(input: { email: string; password: string }): Promise<AuthResponse> {
      return request('POST', '/api/v1/auth/register', {
        body: input,
        skipAuth: true,
      });
    },
    login(input: { email: string; password: string }): Promise<AuthResponse> {
      return request('POST', '/api/v1/auth/login', {
        body: input,
        skipAuth: true,
      });
    },
  },

  holdings: {
    /** `limit` is required by the list endpoint (page size). */
    list(cursor?: string | null, limit = 50): Promise<HoldingsPage> {
      return request('GET', '/api/v1/holdings', { query: { cursor, limit } });
    },
    get(id: number): Promise<HoldingDetail> {
      return request('GET', `/api/v1/holdings/${id}`);
    },
    valuation(id: number): Promise<Valuation> {
      return request('GET', `/api/v1/holdings/${id}/valuation`);
    },
  },

  portfolio: {
    rollup(): Promise<PortfolioRollup> {
      return request('GET', '/api/v1/portfolio/rollup');
    },
  },

  prices: {
    current(): Promise<SpotQuote[]> {
      return request('GET', '/api/v1/prices/current');
    },
  },

  identification: {
    submit(images: {
      obverse: ImagePart;
      reverse: ImagePart;
      edge?: ImagePart | null;
    }): Promise<IdentificationSubmitResult> {
      return request('POST', '/api/v1/identification/submit', {
        formData: imagePartFormData(images),
        idempotencyKey: newIdempotencyKey('identification'),
      });
    },
    status(jobId: number): Promise<IdentificationStatus> {
      return request('GET', `/api/v1/identification/${jobId}/status`);
    },
    confirm(
      jobId: number,
      coinTypeId: number,
      idempotencyKey = newIdempotencyKey('confirm'),
    ): Promise<unknown> {
      return request('POST', `/api/v1/identification/${jobId}/confirm`, {
        body: { coinTypeId },
        idempotencyKey,
      });
    },
  },

  catalog: {
    coinType(id: number): Promise<CoinTypeDetail> {
      return request('GET', `/api/v1/catalog/coin-types/${id}`);
    },
  },
};

export function newIdempotencyKey(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random()
    .toString(36)
    .slice(2, 10)}`;
}

/** Generic request escape hatch for the offline queue (durable replays). */
export function queuedRequest(
  method: Method,
  path: string,
  options: RequestOptions,
): Promise<unknown> {
  return request(method, path, options);
}

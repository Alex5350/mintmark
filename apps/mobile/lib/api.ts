/**
 * Typed API client for the Mintmark API.
 *
 * Response shapes are provisional — the architecture plan routes both
 * clients through a generated `packages/api-client` built from the
 * committed OpenAPI document; once that exists this module's types get
 * replaced by it while keeping the transport behavior below (refresh-once
 * retry, multipart identification, idempotency keys).
 *
 * - Base URL: expo-constants `expoConfig.extra.apiBaseUrl`
 *   (default http://localhost:5100).
 * - JSON is camelCase.
 * - 401 -> refresh once via rotating refresh token, retry the original
 *   request; if refresh fails the session is cleared and UnauthorizedError
 *   is thrown so the app can return to login.
 * - Mutations may carry an Idempotency-Key header (flaky-network double
 *   submits must not create duplicate holdings).
 */
import Constants from 'expo-constants';
import { clearTokens, getTokens, setTokens } from './tokens';

const extra = (Constants.expoConfig?.extra ?? {}) as { apiBaseUrl?: string };

export const API_BASE_URL: string = extra.apiBaseUrl ?? 'http://localhost:5100';

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
// Provisional domain types (camelCase JSON)
// ---------------------------------------------------------------------------

export interface User {
  id: string;
  email: string;
}

export interface AuthResponse {
  user: User;
  accessToken: string;
  refreshToken: string;
}

export type Metal = 'Gold' | 'Silver' | 'Platinum' | 'Palladium';

export interface Holding {
  id: string;
  series: string;
  metal: Metal;
  year?: number | null;
  mintMark?: string | null;
  quantity: number;
  meltValue?: { amount: number; currency: string; asOf: string; stale: boolean } | null;
  updatedAt: string;
}

export interface HoldingsPage {
  items: Holding[];
  nextCursor: string | null;
}

export interface SpotQuote {
  metal: Metal;
  pricePerOzt: number;
  currency: string;
  asOf: string;
  changePercent24h?: number | null;
  /** Stale is never silent (architecture doc): served from last known good
   *  price while a provider outage resolves. */
  stale: boolean;
  provider?: string | null;
}

export interface PricesCurrent {
  quotes: SpotQuote[];
  asOf: string;
}

export interface IdentificationCandidate {
  id: string;
  series: string;
  metal: Metal;
  yearRange?: string | null;
  catalogNo?: string | null;
  confidence: number; // 0..1
}

export type IdentificationStatus = 'queued' | 'processing' | 'completed' | 'failed';

export interface IdentificationJob {
  id: string;
  status: IdentificationStatus;
  error?: string | null;
  candidates?: IdentificationCandidate[];
  confirmedCandidateId?: string | null;
}

/** A picked/captured photo ready for multipart upload. */
export interface ImagePart {
  uri: string;
  name: string;
  type: string;
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

function imagePartFormData(
  obverse: ImagePart,
  reverse: ImagePart,
): FormData {
  const formData = new FormData();
  // RN FormData accepts {uri, name, type} parts at runtime; the DOM lib
  // signature only knows Blob, hence the cast.
  formData.append('obverse', obverse as unknown as Blob);
  formData.append('reverse', reverse as unknown as Blob);
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
    list(cursor?: string | null): Promise<HoldingsPage> {
      return request('GET', '/api/v1/holdings', { query: { cursor } });
    },
  },

  prices: {
    current(): Promise<PricesCurrent> {
      return request('GET', '/api/v1/prices/current');
    },
  },

  identification: {
    submit(images: {
      obverse: ImagePart;
      reverse: ImagePart;
    }): Promise<{ id: string }> {
      return request('POST', '/api/v1/identification', {
        formData: imagePartFormData(images.obverse, images.reverse),
        idempotencyKey: newIdempotencyKey('identification'),
      });
    },
    get(id: string): Promise<IdentificationJob> {
      return request('GET', `/api/v1/identification/${id}`);
    },
    confirm(
      id: string,
      candidateId: string,
      idempotencyKey = newIdempotencyKey('confirm'),
    ): Promise<IdentificationJob> {
      return request('POST', `/api/v1/identification/${id}/confirm`, {
        body: { candidateId },
        idempotencyKey,
      });
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

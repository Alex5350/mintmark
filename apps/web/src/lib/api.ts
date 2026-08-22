/**
 * Thin typed fetch client for the Mintmark API.
 *
 * TEMPORARY: the generated `@mintmark/api-client` replaces this module in a
 * later phase. Keep the exported function signatures stable (same names,
 * same args, same return DTOs) so the swap is mechanical.
 *
 * - Base URL from NEXT_PUBLIC_API_BASE_URL (default http://localhost:5100).
 * - JSON is camelCase; bodies are plain objects, FormData passes through.
 * - 401 → ONE refresh in flight (concurrent 401s queue on the same promise),
 *   then a single retry of the original request. Refresh failure logs out and
 *   redirects to /login.
 */
import { useAuthStore } from "@/lib/auth-store";
import type {
  ChartRange,
  ChartSeries,
  CoinTypeRef,
  Holding,
  HoldingCreateInput,
  HoldingUpdateInput,
  IdentificationJob,
  Metal,
  PortfolioRollup,
  SpotQuote,
  TokenPair,
  User,
} from "@/lib/api-types";

const BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5100";

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly path: string,
    readonly body?: string,
  ) {
    super(`API error ${status} on ${path}`);
    this.name = "ApiError";
  }
}

export interface AuthResult {
  user: User;
  tokens: TokenPair;
}

// ---------------------------------------------------------------------------
// Core request pipeline
// ---------------------------------------------------------------------------

/** One refresh promise shared by every concurrent 401 (single-flight). */
let refreshInFlight: Promise<TokenPair> | null = null;

async function doRefresh(): Promise<TokenPair> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) throw new ApiError(401, "/auth/refresh");
  const res = await fetch(`${BASE_URL}/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) throw new ApiError(res.status, "/auth/refresh");
  const tokens = (await res.json()) as TokenPair;
  useAuthStore.getState().setTokens(tokens);
  return tokens;
}

function forceLogout(): void {
  useAuthStore.getState().clear();
  // Full-page redirect on purpose: the fetch layer lives outside React, so a
  // router hook is unavailable here and the whole app must re-render signed out.
  if (typeof window !== "undefined" && window.location.pathname !== "/login") {
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.assign("/login");
  }
}

interface RequestOptions {
  method?: string;
  /** JSON body (omit for FormData). */
  body?: unknown;
  /** Attach Authorization header (default true). */
  auth?: boolean;
  /** Idempotency-Key header (holdings create — retried submits never duplicate). */
  idempotencyKey?: string;
  /** Set when the original request is the post-refresh retry. */
  isRetry?: boolean;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = "GET", body, auth = true, idempotencyKey, isRetry = false } = options;

  const headers: Record<string, string> = {};
  if (auth) {
    const { accessToken } = useAuthStore.getState();
    if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  }
  if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;
  const isFormData = body instanceof FormData;
  if (body !== undefined && !isFormData) headers["Content-Type"] = "application/json";

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : isFormData ? body : JSON.stringify(body),
  });

  if (res.status === 401 && auth && !isRetry) {
    try {
      refreshInFlight ??= doRefresh().finally(() => {
        refreshInFlight = null;
      });
      await refreshInFlight;
    } catch {
      forceLogout();
      throw new ApiError(401, path);
    }
    return request<T>(path, { ...options, isRetry: true });
  }

  if (!res.ok) {
    throw new ApiError(res.status, path, await res.text().catch(() => undefined));
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

function query(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== "");
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([k, v]) => [k, v as string])).toString()}`;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export const api = {
  auth: {
    register(input: { email: string; password: string; displayName?: string }): Promise<AuthResult> {
      return request<AuthResult>("/auth/register", { method: "POST", body: input, auth: false });
    },
    login(email: string, password: string): Promise<AuthResult> {
      return request<AuthResult>("/auth/login", {
        method: "POST",
        body: { email, password },
        auth: false,
      });
    },
    /** Manual refresh (the request pipeline also refreshes transparently). */
    refresh(): Promise<TokenPair> {
      return doRefresh();
    },
    async logout(): Promise<void> {
      useAuthStore.getState().clear();
      // Best-effort server-side revocation; local state is cleared regardless.
      await request<void>("/auth/logout", { method: "POST", isRetry: true }).catch(() => undefined);
    },
  },

  holdings: {
    list(): Promise<Holding[]> {
      return request<Holding[]>("/holdings");
    },
    detail(holdingId: string): Promise<Holding> {
      return request<Holding>(`/holdings/${holdingId}`);
    },
    /** Create carries an Idempotency-Key so retried submits never duplicate. */
    create(input: HoldingCreateInput): Promise<Holding> {
      return request<Holding>("/holdings", {
        method: "POST",
        body: input,
        idempotencyKey: crypto.randomUUID(),
      });
    },
    update(holdingId: string, input: HoldingUpdateInput): Promise<Holding> {
      return request<Holding>(`/holdings/${holdingId}`, { method: "PUT", body: input });
    },
    remove(holdingId: string): Promise<void> {
      return request<void>(`/holdings/${holdingId}`, { method: "DELETE" });
    },
  },

  catalog: {
    search(q: string): Promise<CoinTypeRef[]> {
      return request<CoinTypeRef[]>(`/catalog/search${query({ q })}`);
    },
  },

  prices: {
    current(): Promise<SpotQuote[]> {
      return request<SpotQuote[]>("/prices/current");
    },
    chart(metal: Metal, range: ChartRange): Promise<ChartSeries> {
      return request<ChartSeries>(`/prices/chart${query({ metal, range })}`);
    },
    /** First-class derived series: gold spot ÷ silver spot. */
    ratio(range: ChartRange): Promise<ChartSeries> {
      return request<ChartSeries>(`/prices/chart/ratio${query({ range })}`);
    },
  },

  portfolio: {
    rollup(): Promise<PortfolioRollup> {
      return request<PortfolioRollup>("/portfolio/rollup");
    },
  },

  identification: {
    submit(input: { obverse: File; reverse: File; provider?: string }): Promise<{ jobId: string }> {
      const form = new FormData();
      form.append("obverse", input.obverse);
      form.append("reverse", input.reverse);
      if (input.provider) form.append("provider", input.provider);
      return request<{ jobId: string }>("/identification/jobs", { method: "POST", body: form });
    },
    status(jobId: string): Promise<IdentificationJob> {
      return request<IdentificationJob>(`/identification/jobs/${jobId}`);
    },
    confirm(jobId: string, coinTypeId: string): Promise<IdentificationJob> {
      return request<IdentificationJob>(`/identification/jobs/${jobId}/confirm`, {
        method: "POST",
        body: { coinTypeId },
      });
    },
  },
} as const;

/**
 * Mintmark API access. The generated `@mintmark/api-client` (openapi-fetch
 * over the committed OpenAPI doc) owns the transport — paths, parameters, and
 * request bodies are typed from the schema; no hand-written fetch URLs here
 * (ADR 0008). This module adds what the schema cannot express:
 *
 * - Bearer Authorization headers from the in-memory auth store (not cookies).
 * - 401 → ONE single-flight POST /api/v1/auth/refresh rotation (concurrent
 *   401s share the promise), then a single retry of the original request.
 *   Refresh failure clears the session and redirects to /login.
 * - Cursor pagination for the holdings list.
 * - Response typing: the OpenAPI doc types request bodies but not response
 *   payloads, so `data` is cast to the DTO mirrors in lib/api-types.ts,
 *   which were verified field-for-field against the live API.
 */
import { createMintmarkClient } from "@mintmark/api-client";
import { useAuthStore } from "@/lib/auth-store";
import type { ChartRange } from "@/lib/api-types";
import { chartMetalParam, type Metal } from "@/lib/enums";
import type {
  AuthTokens,
  ChartSeries,
  CoinTypeDetail,
  HoldingDetail,
  HoldingListItem,
  HoldingListResponse,
  HoldingValuation,
  IdentificationStatusResponse,
  IdentificationSubmitResult,
  PortfolioRollup,
  RatioPoint,
  SpotQuote,
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

const client = createMintmarkClient(BASE_URL);

// ---------------------------------------------------------------------------
// Request pipeline (auth header + single-flight refresh rotation)
// ---------------------------------------------------------------------------

/** Structural slice of openapi-fetch's result — payloads are schema-untyped. */
interface OpenApiResult {
  data?: unknown;
  error?: unknown;
  response: Response;
}

type Fetcher = (headers: Record<string, string>) => Promise<OpenApiResult>;

/** One refresh promise shared by every concurrent 401 (single-flight). */
let refreshInFlight: Promise<void> | null = null;

async function rotateTokens(): Promise<void> {
  const { refreshToken } = useAuthStore.getState();
  if (!refreshToken) throw new ApiError(401, "/api/v1/auth/refresh");
  const { data, error, response } = await client.POST("/api/v1/auth/refresh", {
    body: { refreshToken },
  });
  if (error !== undefined || !response.ok) {
    throw new ApiError(response.status, "/api/v1/auth/refresh");
  }
  useAuthStore.getState().setTokens(data as unknown as AuthTokens);
}

function forceLogout(): void {
  useAuthStore.getState().clear();
  // Full-page redirect on purpose: the client lives outside React, so a
  // router hook is unavailable here and the whole app must re-render signed out.
  if (typeof window !== "undefined" && window.location.pathname !== "/login") {
    // eslint-disable-next-line @next/next/no-location-assign-relative-destination
    window.location.assign("/login");
  }
}

interface RequestOptions {
  /** Attach the Authorization header (default true). */
  auth?: boolean;
  /** Set when the original request is the post-refresh retry. */
  isRetry?: boolean;
}

async function request<T>(fetcher: Fetcher, options: RequestOptions = {}): Promise<T> {
  const { auth = true, isRetry = false } = options;

  const headers: Record<string, string> = {};
  if (auth) {
    const { accessToken } = useAuthStore.getState();
    if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  }

  const { data, error, response } = await fetcher(headers);

  if (response.status === 401 && auth && !isRetry) {
    try {
      refreshInFlight ??= rotateTokens().finally(() => {
        refreshInFlight = null;
      });
      await refreshInFlight;
    } catch {
      forceLogout();
      throw new ApiError(401, response.url);
    }
    return request<T>(fetcher, { ...options, isRetry: true });
  }

  if (error !== undefined || !response.ok) {
    throw new ApiError(
      response.status,
      response.url,
      error === undefined ? undefined : JSON.stringify(error),
    );
  }
  if (response.status === 204) return undefined as T;
  return data as T;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export const api = {
  auth: {
    register(input: { email: string; password: string; displayName?: string }): Promise<AuthTokens> {
      return request<AuthTokens>(
        (headers) =>
          client.POST("/api/v1/auth/register", {
            body: { email: input.email, password: input.password, displayName: input.displayName ?? null },
            headers,
          }),
        { auth: false },
      );
    },
    login(email: string, password: string): Promise<AuthTokens> {
      return request<AuthTokens>(
        (headers) => client.POST("/api/v1/auth/login", { body: { email, password }, headers }),
        { auth: false },
      );
    },
    async logout(): Promise<void> {
      const { refreshToken } = useAuthStore.getState();
      useAuthStore.getState().clear();
      if (!refreshToken) return;
      // Best-effort server-side revocation; local state is cleared regardless.
      await request<void>((headers) =>
        client.POST("/api/v1/auth/logout", { body: { refreshToken }, headers }),
      ).catch(() => undefined);
    },
  },

  holdings: {
    /**
     * Cursor-paginated upstream — follows every page so callers see one
     * list. `take` stops early for widgets that need only the newest rows
     * (the dashboard's five recent cards should not pay for the whole
     * collection).
     */
    async list(take?: number): Promise<HoldingListItem[]> {
      const items: HoldingListItem[] = [];
      let cursor: string | undefined;
      do {
        const limit = take !== undefined ? Math.min(take - items.length, 100) : 100;
        if (limit <= 0) break;
        const page = await request<HoldingListResponse>((headers) =>
          client.GET("/api/v1/holdings", { params: { query: { limit, cursor } }, headers }),
        );
        items.push(...page.items);
        cursor = page.nextCursor ?? undefined;
      } while (cursor);
      return items;
    },
    detail(holdingId: string | number): Promise<HoldingDetail> {
      return request<HoldingDetail>((headers) =>
        client.GET("/api/v1/holdings/{id}", {
          params: { path: { id: Number(holdingId) } },
          headers,
        }),
      );
    },
    /** Explainable valuation. 422s for generic holdings (no cataloged coin type). */
    valuation(holdingId: string | number): Promise<HoldingValuation> {
      return request<HoldingValuation>((headers) =>
        client.GET("/api/v1/holdings/{id}/valuation", {
          params: { path: { id: Number(holdingId) } },
          headers,
        }),
      );
    },
  },

  catalog: {
    /** Catalog row (with presigned reference images) — names for identification candidates. */
    coinType(coinTypeId: number): Promise<CoinTypeDetail> {
      return request<CoinTypeDetail>((headers) =>
        client.GET("/api/v1/catalog/coin-types/{id}", {
          params: { path: { id: coinTypeId } },
          headers,
        }),
      );
    },
  },

  prices: {
    current(): Promise<SpotQuote[]> {
      return request<SpotQuote[]>((headers) => client.GET("/api/v1/prices/current", { headers }));
    },
    chart(metal: Metal, range: ChartRange): Promise<ChartSeries> {
      return request<ChartSeries>((headers) =>
        client.GET("/api/v1/prices/chart", {
          params: { query: { metal: chartMetalParam(metal), range } },
          headers,
        }),
      );
    },
    /** First-class derived series: gold spot ÷ silver spot. */
    ratio(range: ChartRange): Promise<RatioPoint[]> {
      return request<RatioPoint[]>((headers) =>
        client.GET("/api/v1/prices/ratio", { params: { query: { range } }, headers }),
      );
    },
  },

  portfolio: {
    rollup(): Promise<PortfolioRollup> {
      return request<PortfolioRollup>((headers) =>
        client.GET("/api/v1/portfolio/rollup", { headers }),
      );
    },
  },

  identification: {
    /** Multipart obverse + reverse; the API binds by part name. 202 → { jobId, deduplicated }. */
    submit(input: { obverse: File; reverse: File }): Promise<IdentificationSubmitResult> {
      const form = new FormData();
      form.append("obverse", input.obverse);
      form.append("reverse", input.reverse);
      return request<IdentificationSubmitResult>((headers) =>
        client.POST("/api/v1/identification/submit", {
          body: form as unknown as { files: string[] },
          headers,
        }),
      );
    },
    status(jobId: number): Promise<IdentificationStatusResponse> {
      return request<IdentificationStatusResponse>((headers) =>
        client.GET("/api/v1/identification/{jobId}/status", {
          params: { path: { jobId } },
          headers,
        }),
      );
    },
    /** Records the decision; 204 No Content — refetch status to see it applied. */
    confirm(jobId: number, coinTypeId: number): Promise<void> {
      return request<void>((headers) =>
        client.POST("/api/v1/identification/{jobId}/confirm", {
          params: { path: { jobId } },
          body: { coinTypeId },
          headers,
        }),
      );
    },
  },
} as const;

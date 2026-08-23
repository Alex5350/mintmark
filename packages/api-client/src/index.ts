import createClient from "openapi-fetch";
import type { paths } from "./schema";

/**
 * The generated Mintmark API client. Both frontends consume this — no
 * hand-written fetch calls anywhere (ADR 0008). Regenerate with
 * `pnpm --filter @mintmark/api-client generate` after API changes; the
 * committed openapi.json diff makes drift reviewable.
 */
export function createMintmarkClient(baseUrl: string) {
  return createClient<paths>({
    baseUrl,
    // Bearer tokens travel in the Authorization header (set per-request by the
    // app's auth layer) — no cookie credentials, so the browser never needs
    // Access-Control-Allow-Credentials from the API's CORS policy.
    headers: { accept: "application/json" },
  });
}

export type Schema = paths;

// NOTE: the committed OpenAPI doc types request bodies but NOT response
// payloads (200 responses carry `content?: never`), so response types cannot
// be derived from the schema yet. Until the doc gains response schemas, these
// aliases stay `unknown` — apps carry response DTO mirrors verified against
// the live API (web: apps/web/src/lib/api-types.ts).
export type Holding = unknown;
export type SpotQuote = unknown;
export type PortfolioRollup = unknown;
export type ChartSeries = unknown;
export type IdentificationStatus = unknown;

# ADR 0004: Spot price providers: metals.dev primary, gold-api.com fallback

Status: Accepted (research verified 2026-08-27/28 from official domains,
live endpoint probes included)

## Context

The app needs XAU + XAG (USD base) spot prices and daily-close history at a
hobbyist budget (100-300 requests/month). Six candidate providers were
evaluated on quota, update frequency, historical access, licensing, and
practical shape.

## Decision

- **Primary: metals.dev** (free: 100 req/month, 60-second updates even on
  free, CORS, `/usage` quota endpoint). Decisive factor: one `/v1/latest`
  call returns gold, silver, platinum, and palladium together: the
  XAU+XAG requirement costs one request per refresh, not two. 30-day-window
  `/v1/timeseries` covers recent daily closes for backfill.
- **Fallback: gold-api.com** (keyless, unlimited XAU/XAG spot, ~140 ms
  measured, CORS, provider-side failover of its own; history endpoints at
  10 req/hour with a free key; ample for once-a-day close pulls).

## Rejected, with evidence

- **goldprice.dev**: silver (XAG) is plan-gated to $30/mo Pro; verified by
  a live `403 plan_gated` response on the free key. Cannot serve the
  requirement.
- **MetalpriceAPI**: free tier is daily-update only, **non-commercial
  only, requires visible attribution** (their FAQ). A collector's tool
  embedding mandatory third-party branding is the wrong trade.
- **Metals-API**: no verifiable free tier on the current pricing page
  (cheapest listed $19.99/mo); SSL restricted to paid plans per docs.
- **GoldAPI.io**: pricing/FAQ pages are JS-rendered; free quota could not
  be verified from official sources. Unverifiable ≠ free.

## Consequences

Composite provider order is `metalsdev → goldapicom` (configurable); each
stored `SpotPrice` row records which provider actually served it. metals.dev
ToS ties commercial *publishing* of rates to an active subscription: this
project is non-commercial/portfolio use; if that changes, the $1.79/mo
Copper plan (2,000 req) is the documented compliance path. Historical
depth beyond ~30 days depends on gold-api.com's `/history` (aggregated
avg/day); earliest-date limits are provider-side and surfaced in
`docs/open-questions.md` rather than assumed.

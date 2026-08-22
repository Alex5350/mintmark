# ADR 0008: REST + OpenAPI; GraphQL only if earned

Status: Accepted

## Context
The brief mandates REST-first. Two clients (web, mobile) consume the API.

## Decision
Versioned REST (`/api/v1`), OpenAPI document generated from code, committed
to the repo, and diffed in CI; the TypeScript client in `packages/api-client`
is generated from that document; no hand-written fetch calls anywhere.
GraphQL (HotChocolate) is only proposed with measurements if mobile
over-fetching materializes after Phase 5.

## Consequences
One contract, two clients, zero drift by construction. Breaking changes
surface as OpenAPI diffs in review.

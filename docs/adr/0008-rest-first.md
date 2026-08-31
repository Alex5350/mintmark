# ADR 0008: REST + OpenAPI; GraphQL only if earned

Status: Accepted

## Context
The brief mandates REST-first. Two clients (web, mobile) consume the API.

## Decision
Versioned REST (`/api/v1`). The OpenAPI document is generated from code,
committed to the repo, and regenerated in CI with a fail-on-drift diff; the
TypeScript client in `packages/api-client` is generated from that document
and diffed the same way. The web app consumes the generated client for its
transport. The mobile app ships a hand-written fetch client
(`apps/mobile/lib/api.ts`): Expo keeps the dependency tree lean and the app
has few flows, so it carries its own wire types, compile-checked by the
strict typecheck that CI runs on every push. The committed document types
requests only; response payloads have no schemas yet, so response shapes are
hand-mirrored in the clients (`apps/web/src/lib/api-types.ts` carries the
web mirrors; the mobile client declares its own). GraphQL (HotChocolate) is
only proposed with measurements if mobile over-fetching materializes after
Phase 5.

## Consequences
The request side of the contract is gated by construction: a breaking
request change fails CI as an OpenAPI or generated-client diff. Response
shapes rely on hand-mirrored types until response schemas land (tracked in
docs/open-questions.md); the one real drift incident to date was exactly a
response-shape drift in the mobile client, caught in the iOS simulator run
rather than by the gate, which is the honest limit of a request-side
contract.

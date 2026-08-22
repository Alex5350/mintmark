# Runbook

## Deploy

1. Provision: PostgreSQL 18 (pgvector + pg_trgm extensions), an
   S3-compatible bucket (private), a secrets store.
2. Set the environment (see `.env.example`): database, JWT signing key
   (≥48 random bytes), storage credentials, provider keys, OTLP endpoint.
3. `MINTMARK_ENVIRONMENT=Production`: enables Secure cookies, disables
   Scalar/docs UI and auto-migration.
4. **Migrations are deliberate**: `dotnet ef database update` (or the
   bundle) as a deploy step; never auto-applied in production.

## Migrations & rollback

- Migrations are sequential and checked in. Apply on deploy; the API fails
  fast on model/migration drift in Development.
- Rollback: EF migration down-scripts are generated per release; the spot
  tick table is the only high-volume store; rollups keep daily closes
  (retention policy in architecture.md), so rolling back jobs never loses
  chart history.

## When the price provider goes down

- The composite provider fails over metals.dev → gold-api.com
  automatically (ADR 0004).
- On total provider failure the last known good price is served **flagged
  stale** (API field, web badge, mobile badge). This is by design; check
  `/api/v1/prices/current` `stale` + `staleFor`.
- The poller derives its interval from `MINTMARK_PRICE_MONTHLY_BUDGET` and
  backs off with jitter; a circuit breaker stops hammering a failing
  provider. To force recovery: nothing to do; the breaker half-opens
  automatically. To switch providers: change `MINTMARK_PRICE_PRIMARY`.

## Key rotation

- JWT signing key: set the new value + redeploy (short access-token life
  bounds the overlap; refresh tokens are opaque and unaffected).
- Provider keys: replace the env value; the composite adapter reads config
  per poll.

## Investigating a bad identification

1. Find the run: `identification_runs` by id; the row is append-only and
   carries input image keys, model + prompt-template version, raw
   structured response, per-field confidences, candidates, and the user's
   confirmation.
2. Was `provider = 'offline'`? The deterministic evaluator labeled itself;
   expectations differ (see docs/ai-pipeline.md).
3. Prompt changes: bump the version in `prompts/` + `PromptCatalog`; the
   eval harness runs the ground-truth set so a regression fails CI before
   it ships.

## Storage

- Images live only in object storage (never Postgres). Presigned GET URLs
  expire in 15 minutes. Bucket must stay private: presign, never public.

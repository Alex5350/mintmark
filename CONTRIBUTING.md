# Contributing to Mintmark

Read [docs/architecture.md](docs/architecture.md) and the ADRs first: the
dependency rules are enforced by an architecture test that fails the build.

## Workflow

- Branch from `main`, conventional commits (`feat:`, `fix:`, `docs:`,
  `chore:`, `test:`), one logical change per commit, every commit green.
- PR checklist: tests + docs updated · OpenAPI diff reviewed (breaking
  changes are deliberate) · no new warnings · secrets nowhere · specs in
  the catalog carry source URLs or nulls.

## Code style

`dotnet format` enforced (warnings are errors; only the SixLabors license
notice is tolerated). File-scoped namespaces. Money is `decimal` only
(`Money` value object). One weight-conversion site. Prompts are versioned
files in `prompts/`, never inline strings.

## Common tasks

- **Add a migration:** `dotnet ef migrations add <Name> --project src/Mintmark.Infrastructure --startup-project src/Mintmark.Api` (from `backend/`)
- **Add a price provider:** implement `IMetalPriceProvider` in
  Infrastructure, add recorded-fixture contract tests, register in the
  composite order, document licensing in ADR 0004.
- **Add coin types:** edit `backend/seed/catalog.json`: every figure
  needs a `sourceUrl` or stays null; the seeder validator rejects
  unsourced specs. Re-run `just seed`.
- **Regenerate the client:** `just openapi` regenerates
  `docs/openapi.json` and the TypeScript client both frontends consume.

## Testing expectations

Domain/Application: xUnit + golden valuations (any number change is
deliberate). API: WebApplicationFactory + Testcontainers Postgres; never
the in-memory provider. Providers: recorded fixtures, never live calls.
UI: the generated API client only; no hand-written fetch calls.

# Mintmark developer tasks. `just` runs these; see https://github.com/casey/just
# Every command works from a clean clone after `docker compose up -d`.

default: list

# Boot infrastructure (Postgres 18 + pgvector, MinIO)
infra:
    docker compose up -d db storage createbuckets

# Apply EF migrations (local convenience; production applies them deliberately — see docs/runbook.md)
migrate:
    cd backend/src/Mintmark.Api && dotnet ef database update --project Mintmark.Infrastructure --startup-project Mintmark.Api

# Seed reference data + starter catalog (idempotent)
seed:
    cd backend/src/Mintmark.Api && dotnet run --project Mintmark.Api -- --seed

# Run the API on :5100
api:
    cd backend/src/Mintmark.Api && dotnet run --project Mintmark.Api

# Run the web app on :3100
web:
    cd apps/web && pnpm dev

# Full local stack after infra
dev: infra migrate seed
    @echo "infra up. run `just api` and `just web` in separate terminals, or `just up`"

up: infra migrate seed
    @cd backend/src/Mintmark.Api && dotnet run --project Mintmark.Api &
    @cd apps/web && pnpm dev &
    @wait

# Tests
test-backend:
    cd backend && dotnet test

test-web:
    cd apps/web && pnpm test

test-all: test-backend test-web

lint:
    cd backend && dotnet format --verify-no-changes
    cd apps/web && pnpm lint

# Regenerate the OpenAPI document + TS client (also enforced in CI)
openapi:
    cd backend/src/Mintmark.Api && dotnet run --project Mintmark.Api -- --export-openapi

# list available recipes
list:
    @just --list

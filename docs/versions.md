# Verified versions

Every row checked against the cited source on **2026-08-27/28**. The
master brief forbids pinning from memory; this file is the record. Bumping
anything here requires updating `backend/Directory.Packages.props` in the
same change.

## Backend (.NET)

| Package | Version | Note |
|---|---|---|
| .NET SDK / runtime | 10.0.400 / 10.0.11 | LTS; Aug 2026 servicing (dotnet.microsoft.com/download/dotnet/10.0) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | nuget.org (2026-07-10) |
| Microsoft.EntityFrameworkCore | 10.0.11 | aligned Aug servicing |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.11 | |
| Microsoft.AspNetCore.OpenApi | 10.0.11 | Swashbuckle deliberately absent (ADR 0002) |
| Scalar.AspNetCore | 2.17.1 | API reference UI at `/docs` |
| FluentValidation | 12.1.1 | **manual validation only**: the AspNetCore auto-validation package is officially deprecated; validators are invoked in endpoints |
| Quartz.Extensions.DependencyInjection / Serialization.Json | **3.19.1** | 3.20.0 released 2026-08-27 with no soak; deliberately one step back |
| TngTech.ArchUnitNET | 0.13.4 | architecture tests; NetArchTest.Rules unmaintained since 2021 |
| SixLabors.ImageSharp | 4.1.1 | **Split License**: free below $1M annual gross revenue; see open-questions #3 |
| NetDevPack.Security.PasswordHasher.Argon2 | 7.1.2 | Argon2id drop-in `IPasswordHasher`; .NET 10 Identity ships PBKDF2 only |
| Testcontainers.PostgreSql | 4.14.0 | real Postgres in tests; in-memory provider is banned |
| AWSSDK.S3 | 4.0.102.4 | S3 client for MinIO/prod object storage |
| Microsoft.NET.Test.Sdk / FluentAssertions / xunit.runner | 18.9.0 / 8.10.0 / 4.0.0 | verified via NuGet search API |
| OpenTelemetry (.Hosting/.OTLP/.AspNetCore) | 1.18.0 | 2026-08-21 stable train |

## Platform images

| Image | Tag | Note |
|---|---|---|
| pgvector/pgvector | pg18 (0.8.6) | Postgres 18 + pgvector; verified on Docker Hub |
| minio/minio | pinned by digest in docker-compose.yml (RELEASE.2025-09-07) | **repo archived**: no updates expected; verified caveat |

## Frontend (npm)

| Package | Version |
|---|---|
| next | 16.3.3 |
| @tanstack/react-query | 5.102.8 |
| recharts | 3.10.1 |
| zustand | 5.0.15 |
| pnpm | 11.24.0 |

## Npgsql EF 10 notes applied in this codebase

- `array.Contains(x)` now translates to `= ANY(...)`; GIN indexes modeled
  explicitly where arrays are filtered.
- JSON columns use EF 10 complex types (legacy owned-entity JSON mapping is
  out).

## Mobile (verified at scaffold time, 2026-08-28)

| Package | Version |
|---|---|
| expo (SDK 57) | 57.0.17 |
| react-native | 0.86.3 |
| react | 19.2.3 |
| expo-router / secure-store / sqlite / local-authentication / image-picker | 57.0.17 / 57.0.2 / 57.0.2 / 57.0.2 / 57.0.14 |

Runtime verification on physical devices/simulators is not possible in this
environment; the scaffold is typecheck- and expo-doctor-clean (21/21);
device testing is listed in docs/open-questions.md.

# ADR 0002: ASP.NET Core on .NET 10 for the backend

Status: Accepted (mandated by the product brief; verified, not relitigated)

## Context
The brief fixes the stack: minimal APIs on .NET 10 LTS, EF Core 10 +
Npgsql, PostgreSQL 18. Alternatives (Go/Fiber, Rust/Axum, Python/FastAPI)
were considered and rejected in the brief itself.

## Decision
Adopted as specified. Minimal APIs grouped into `IEndpointModule`
registrations; no MVC controllers; no Swashbuckle (removed from .NET
templates); `Microsoft.AspNetCore.OpenApi` + Scalar UI instead.

## Consequences
One `Program.cs` that only composes. Endpoint modules are discoverable by
convention. Exact resolved package versions live in
`backend/Directory.Packages.props` and `docs/versions.md`.

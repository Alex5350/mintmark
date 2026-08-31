# Mintmark

**A serious collector's tracker for gold and silver: catalog bullion, coins and bars;
photograph a coin for grounded catalog identification with candidates and confidence;
watch both melt and collectible value against live, source-attributed spot prices.**

[![CI](https://github.com/Alex5350/mintmark/actions/workflows/ci.yml/badge.svg)](https://github.com/Alex5350/mintmark/actions/workflows/ci.yml)

> **Two ways to read this page.** Not an engineer? Everything below the pictures stays in
> plain language: the problem, the pictures, and what the product delivers; jargon links to
> the [glossary](docs/GLOSSARY.md). Engineer? The deep dive lives in
> [TECHNICAL.md](TECHNICAL.md): architecture, flows, and every major decision mapped back
> to the business problem it solves.

## The problem

A serious collector tracks gold and silver across scattered sources: dealer sites for
prices, a spreadsheet for the collection, mint and catalog pages for specifications, and
memory for what a coin is worth. Three failures follow. A price is pasted into the
spreadsheet and quietly ages until the record is wrong. A valuation is guessed from a
number someone remembers, so an insurance record or a sale decision rests on nothing
checkable. And a photographed coin gets identified by eyeballing search results, with no
record of why the match was made or how confident it was.

Mintmark gives that collector one place where every figure says where it came from, every
identification is grounded in catalog evidence or says so, and stale prices are flagged
end to end, never silent.

## The product in screenshots

| Land in the dark-first app | See the whole collection valued live |
|:---:|:---:|
| ![Landing](docs/assets/07-landing-dark.png) | ![Dashboard: spot ticker, portfolio rollup, collection](docs/assets/dashboard.png) |

| Browse the collection as a gallery or a dense table | Open a holding: provenance and real coin photography |
|:---:|:---:|
| ![Collection](docs/assets/mintmark-web-collection.png) | ![Holding detail](docs/assets/mintmark-web-holding-detail.png) |

| Watch spot prices and the gold-to-silver ratio over time | Photograph a coin, review candidates, confirm the match |
|:---:|:---:|
| ![Prices](docs/assets/mintmark-web-prices.png) | ![Identify](docs/assets/mintmark-web-identify.png) |

The mobile client is the camera-first companion: guided two-shot capture, live portfolio
rollup and per-holding valuation, biometric lock and a durable offline queue. All six
screenshots below come from a real iOS simulator run against the live API.

| Check the rollup and live values from anywhere | See the premium factors behind each holding |
|:---:|:---:|
| ![Mobile collection](docs/assets/mintmark-mobile-collection.png) | ![Mobile holding detail](docs/assets/mintmark-mobile-holding-detail.png) |

| Spot and the Au:Ag ratio on the phone | Guided two-shot capture, with glare and focus checks |
|:---:|:---:|
| ![Mobile prices](docs/assets/mintmark-mobile-prices.png) | ![Mobile identify](docs/assets/mintmark-mobile-identify.png) |

| Sign in; tokens live in the device secure store | Control the biometric lock and offline queue |
|:---:|:---:|
| ![Mobile login](docs/assets/mintmark-mobile-login.png) | ![Mobile settings](docs/assets/mintmark-mobile-settings.png) |

Coin imagery in the app is **real photography, freely licensed**: sourced
from Wikimedia Commons and public-domain US Mint renders, every image's
license verified (PD/CC0/CC-BY/CC-BY-SA; no NC/ND) with per-file
attribution in
[backend/seed/images/CREDITS.md](backend/seed/images/CREDITS.md). Rows
without a freely-licensed photograph fall back to original rendered
bullion art (metallic sheen, reeded edges, generic legends; no protected
mint designs), so the retrieval pipeline always has imagery.

## What it delivers

- **Every number carries its provenance.** The dashboard's +67.7% is computed server-side
  against the same live spot price the ticker shows; the holding detail lists the exact
  premium factors behind the collectible estimate, and each spot price records the
  provider and timestamp that served it.
- **Identification is grounded, never auto-accepted.** Photographing a coin returns the
  top five catalog candidates with scores; the collector confirms the match, and the
  confirmation is written to an append-only audit trail. An identification is either
  grounded in catalog evidence or says so.
- **Unpublished catalog specs stay null rather than invented.** Every specification row
  carries a source URL; disputed or unavailable figures remain null, enforced by a seed
  validator that rejects unsourced specs.
- **Valuations carry confidence bands and method versions.** Estimates are shown with an
  honest uncertainty band, the plain-math [valuation method](docs/valuation.md) behind
  them, and a version stamp, so history stays explainable after factor tables or spot
  sources change.
- **The app works offline and flags stale data instead of silently showing old prices.**
  Mobile changes queue durably and sync when signal returns; when providers are down, the
  last known good price is served flagged stale in the API, on the web, and on the phone.

## What's implemented (honestly)

| Feature | Status |
|---|---|
| Catalog: 14 mints, 10 series, 12 sourced coin types | ✅ every figure source-attributed; unpublished specs stay null |
| Holdings CRUD with revision history + idempotent creates | ✅ |
| Auth: Argon2id + JWT + rotating single-use refresh tokens | ✅ family revocation on token reuse |
| Spot prices: metals.dev primary, gold-api.com failover | ✅ stale prices flagged end-to-end, never silent |
| Historical charts with server-side downsampling + Au:Ag ratio | ✅ LTTB for long ranges, bucketed averages for short |
| Melt + rules-based collectible valuation with provenance | ✅ itemized premium factors, confidence bands, method versioning |
| AI identification: capture → vision contract → hybrid retrieval → confirm | ✅ hosted adapters (OpenAI/Gemini) + labeled deterministic offline evaluator; append-only audit runs |
| Web client: dashboard, gallery + table collection, coin flip, identify | ✅ dark-first, WCAG-checked, tabular numerals |
| Mobile: guided capture, offline queue, biometric gate | ✅ run end-to-end on an iOS simulator; camera capture and biometric unlock **not device-tested** (see [open questions](docs/open-questions.md)) |
| Comparables-based valuation (Phase 2), learned model (Phase 3) | ❌ deliberately not built (ADR 0007) |

## How the engineering solves it

Plain-terms bridge; each item links to the full story in [TECHNICAL.md](TECHNICAL.md).

- **Identifying a coin from a photo could easily become confident guessing.** Guided
  two-shot capture (both sides of the coin, with glare and focus gates) feeds a retrieval
  pipeline over the sourced catalog; the collector confirms one of the top five
  candidates, so nothing is ever auto-accepted.
  ([the identification flow](TECHNICAL.md#request-and-data-flow))
- **A number without a source is useless for insurance or sale decisions.** The data model
  enforces provenance end to end: catalog specs carry a source URL or stay null, prices
  record their provider, valuations record their method version and the spot they derived
  from.
  ([provenance-enforced data model](TECHNICAL.md#how-the-tech-solves-the-business-problem))
- **A machine-learned valuation with no training data would look precise and be wrong.**
  Valuation runs as honest rules: plain multiplicative factors (mintage, finish, grade,
  demand, age) with confidence bands, whose [plain math](docs/valuation.md) is frozen by
  golden tests so any change is deliberate.
  ([valuation rules first](TECHNICAL.md#how-the-tech-solves-the-business-problem))
- **Collectors check values in basement safe rooms with no signal.** The mobile client is
  offline-first: failed changes land in a durable queue and sync later, and stale prices
  are flagged in every client rather than silently shown.
  ([offline-first engineering](TECHNICAL.md#how-the-tech-solves-the-business-problem))

<details>
<summary><b>For developers: quickstart</b></summary>

Prerequisites:

- Docker (any runtime: Docker Desktop, colima, OrbStack); `docker ps` works
- .NET SDK 10.0.400+: `brew install dotnet-sdk` / [windows/linux downloads](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node 22 LTS + pnpm 11: `brew install node && corepack enable`
- (mobile only) Expo CLI + Xcode/Android toolchains: [Expo docs](https://docs.expo.dev/)

Quickstart (clean machine, ~10 minutes):

```bash
git clone https://github.com/Alex5350/mintmark && cd mintmark
cp .env.example .env            # fill JWT key + provider keys (or run offline)
docker compose up -d            # postgres 18 + pgvector, minio, bucket init
just migrate                    # EF migrations
just seed                       # sourced catalog + demo user + demo holdings
just api                        # API on :5100 (docs at /docs)
# second terminal:
just web                        # web on :3100
```

Sign in as `demo@mintmark.local` / `mintmark-demo-2026`. Spot prices need a
metals.dev key (free, 100 req/mo); **without keys everything else works**:
identification runs the labeled offline evaluator and prices seed from
fixture history, flagged stale.

Every environment variable is documented in [.env.example](.env.example):
name, default, and how to obtain each key. Nothing real is committed.

</details>

## Documentation

| Document | What it covers | Audience |
|---|---|---|
| [TECHNICAL.md](TECHNICAL.md) | Architecture, request flow, decisions mapped to business problems, stack rationale, testing | Engineers |
| [docs/GLOSSARY.md](docs/GLOSSARY.md) | Collector and engineering terms, in plain English and precisely | Everyone |
| [docs/architecture.md](docs/architecture.md) | The phase 0 architecture: layers, pipelines, observability | Engineers |
| [docs/adr/](docs/adr/) | Nine architecture decision records | Engineers |
| [docs/valuation.md](docs/valuation.md) | Melt and collectible valuation, the plain math | Everyone |
| [docs/ai-pipeline.md](docs/ai-pipeline.md) | The identification pipeline, stage by stage | Engineers |
| [docs/security.md](docs/security.md) | OWASP ASVS L2 checklist, done and deferred | Engineers |
| [docs/runbook.md](docs/runbook.md) | Deploy, provider outages, key rotation | Operators |
| [docs/open-questions.md](docs/open-questions.md) | Honest inventory of gaps and deferred decisions | Everyone |
| [docs/versions.md](docs/versions.md) | Verified package versions | Engineers |
| [apps/mobile/README.md](apps/mobile/README.md) | The mobile client: setup, usage, offline behavior | Engineers |

Contributing: [CONTRIBUTING.md](CONTRIBUTING.md) · License: MIT
([LICENSE](LICENSE)).

**Data attribution:** spot prices by [metals.dev](https://metals.dev.com)
(primary) and [gold-api.com](https://gold-api.com) (fallback), per the
licensing constraints in [ADR 0004](docs/adr/0004-price-providers.md).
Catalog specifications are cited per-row in
[backend/seed/catalog.json](backend/seed/catalog.json).

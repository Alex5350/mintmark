# Open questions and known gaps

Honest inventory of what is not done, not verified, or decided-but-deferred.
Each item says what shipped instead.

## Verification gaps

1. **Mobile has run on the iOS simulator but not on real hardware.** The
   Expo app is typecheck-clean and expo-doctor 21/21, and an
   iPhone 17 Pro simulator run (Expo Go, live API) verified sign-in,
   collection + rollup, per-holding valuation detail, prices, settings,
   and produced the README's mobile screenshots. That run surfaced and
   fixed two real defects: the simulator sandbox cannot open IPv6
   loopback (so the app now defaults to `127.0.0.1`), and the mobile
   client's provisional wire types had drifted from the committed
   OpenAPI shapes (prices crashed; holdings/identification paths were
   wrong) - both realigned. Still compile-verified only: camera capture,
   biometric unlock, SecureStore on hardware, and the SQLite queue; no
   EAS build has been cut.
2. **Lighthouse CI budget enforcement is not wired.** The design targets
   LCP < 2.0s / Lighthouse 95+ (brief §10) but no CI job enforces it yet.
   Manual checks pass locally; enforcement is a CI addition.
3. **The AI evaluation harness has no real-photograph ground truth yet.**
   The offline evaluator's synthetic reference set exercises the pipeline;
   per-field accuracy on real coin photos requires a hosted vision key and
   a photographed ground-truth set - both future work. The accuracy bar in
   CI is therefore not yet meaningful for the hosted adapters.
4. **Load/performance budgets (p95 targets) are untested** - no k6/NBombe r
   run against a 10,000-holding dataset yet.

## Deferred decisions

5. **Quartz runs on the RAM job store, not the Postgres job store.** The
   brief demanded durable jobs surviving restarts; the ADO.NET job store
   requires the Quartz PostgreSQL DDL (nine tables) as a migration step we
   did not take in v1. Consequence: a restart loses in-flight poll
   scheduling (the poller re-registers on boot; spot history in Postgres is
   unaffected). Upgrading is a config + one script.
6. **Storage-location column encryption at rest** (security.md) - needs a
   key-management decision; the field is currently unindexed, unexported by
   default, and unlogged, but not encrypted.
7. **Breached-password (HIBP k-anonymity) check** - offline denylist ships;
   the networked check needs a privacy decision about outbound calls at
   registration.
8. **Comparables-based valuation (Phase 2)** - deliberately not started;
   requires a per-source ToS review before any ingestion code (ADR 0007).

## Data provenance notes (from catalog research)

9. **Serrated edge mapped to Reeded** (Canadian Maple Leaf) - the domain
   EdgeType set lacks Serrated; closest mapping applied in the seeder.
10. **Morgan/Peace gross gram weights are derived** (AMW ÷ fineness ≈
    26.73 g) - the US Mint publishes troy-oz silver weight, not grams; the
    derivation matches the published figure but is not mint-stated.
11. **Libertad mintages come from Numista** (community catalog) - Casa de
    Moneda / Banxico do not publish per-coin mintages; the 2 oz reverse
    proof 1,500 figure is the divergence test's real-world anchor.
12. **Historical depth beyond ~30 days depends on gold-api.com `/history`**
    - metals.dev free timeseries windows at 30 days; earliest-date limits
    are provider-side and unverified beyond the probe.

## Licensing

13. **SixLabors ImageSharp Split License** - free below $1M annual gross
    revenue (this project qualifies); the build emits a license notice
    warning, tolerated deliberately. Commercial use above the threshold
    requires a license key via `SixLaborsLicenseKey`.

## Environment caveats

14. **MinIO's Docker repository is archived** (pinned release, no expected
    updates) - swap endpoint to S3/Azure Blob for production (ADR 0006
    makes this a config change).
15. **The .NET 10 antiforgery trap** - form-binding endpoints carry
    implicit antiforgery metadata; the identification submit disables it
    per-endpoint (Bearer-token API, not cookie forms). Any future
    multipart endpoint must do the same.

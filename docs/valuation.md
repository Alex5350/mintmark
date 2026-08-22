# Valuation methodology

Both estimates are always shown together, always with inputs visible. These
are estimates for personal tracking: **not appraisals, not offers, not
investment advice.**

## Melt value

```
meltValue = actualMetalWeightTroyOz × quantity × spotPricePerTroyOz
```

Rules enforced in `Mintmark.Domain.Services.MeltValuation` and tested as
goldens:

- **Actual metal weight, never gross weight.** A 90% silver quarter
  (0.18084 ozt ASW) and a 1 oz Eagle both have a melt value; only one
  contains a troy ounce of silver. The catalog stores both figures
  separately and melt reads exactly one of them.
- Multi-metal items: per-metal contributions computed and summed for the
  precious portion only.
- All arithmetic in `decimal` via the `Money` value object; cross-currency
  math throws. Display rounding happens at the edge only.
- Provenance travels with the number: the spot price, its provider, and its
  source timestamp render inline. A figure without provenance is not
  usable information.

## Collectible value (Phase 1: rules)

```
collectibleEstimate = meltValue × Π(factors)      premium = collectible − melt
```

Every factor is itemized in the response and UI ("how was this calculated")
and every weight lives in the **`PremiumFactorTable`: data, not code**
(ADR 0007), tunable without a deployment.

| Factor | Tiers (defaults) | Rationale |
|---|---|---|
| Mintage | null→1.00 neutral · ≤5k→3.00 · ≤25k→2.00 · ≤100k→1.50 · ≤1M→1.15 · >1M→1.00 | scarcity relative to demand base |
| Finish primary | Proof 1.60 · ReverseProof 1.80 · Burnished 1.30 · MatteProof 1.50 · BU/Business 1.00 | production cost + collector preference |
| Finish flags | HighRelief ×1.15 · Antiqued ×1.10 · Colorized ×1.05 | stacking modifiers (ADR 0003) |
| Grade | raw 1.00 · 69 1.30 · 70 1.60 (+Ultra/Deep Cameo ×1.10) | condition premium |
| Series demand tier | High 1.40 · Medium 1.15 · Low 1.00 | reference data per series |
| Age | pre-1936 ×1.25 | numismatic historic premium |

**Confidence band:** ±(0.15 + 0.05 per applied factor beyond three), as
fractions of the estimate; a point estimate implies precision we don't
have.

### The canonical divergence test (frozen as a golden)

At silver $28.50/ozt:

| Fixture | Melt | Multiplier | Collectible |
|---|---|---|---|
| Common-date BU Eagle-style, mintage 14M, raw | $28.50 | **1.0000×** | **$28.50** |
| 2 oz reverse-proof high-relief Libertad-style, mintage 1,800, MS70, high demand | $57.00 | **13.9104×** | **$792.8928** |

Near-identical melt economics; 27.8× collectible gap. The divergence falls
out of the factor product (3.0 × 1.8 × 1.15 × 1.6 × 1.4) with **zero
special cases in code**; the golden test fails if anyone hardcodes it.

### What this model is not

- **Not comparables.** No realized-sale data feeds it (Phase 2; proposed
  via ADR before any code, and only from sources whose terms permit it).
- **Not a learned model.** Phase 3 waits for confirmed identifications and
  comparables volume.
- Every valuation row persists its **method version** (`rules-v1`), the spot
  row it derived from, and the provider; history stays explainable after
  factor tables or spot sources change.

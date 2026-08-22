# ADR 0007: Rules-based collectible valuation first; comparables later

Status: Accepted

## Context
Collectible value without a comparables feed must still be honest. Machine
learning on zero training data produces confident garbage.

## Decision
Phase 1 ships a transparent premium model:
`collectibleEstimate = meltValue + numismaticPremium`, where the premium is
a product of inspectable factors (mintage rarity tier, finish primary +
flags, grade/designation, label pedigree, series demand tier, age band)
with **weights stored as configuration/reference data** (tunable without a
deployment). Every estimate carries a confidence band and method version.
Phase 2 (realized-sale comparables) is proposed via ADR before any code;
Phase 3 (learned model) waits for confirmed identifications + comparables
volume. The canonical divergence test: a low-mintage 2 oz reverse-proof
Libertad vs a common-date Silver Eagle (near-identical melt, wildly
different collectible) must fall out of the factors alone, no special
cases.

## Consequences
Numbers are explainable line-by-line; tuning is data, not deploys. Golden
tests freeze outputs so any factor change is a deliberate, reviewed diff.

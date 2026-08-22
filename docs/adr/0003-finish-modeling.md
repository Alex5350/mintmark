# ADR 0003: Finish as primary value plus attribute flags

Status: Accepted

## Context
Finish names look like one enum but are not mutually exclusive: a coin can
be high-relief AND reverse proof; "first strike" is a stacking modifier on
any strike; "enhanced", "antiqued", "colorized" decorate several primaries.
A flat enum forces either an explosion of combined values
(`HighReliefReverseProof`) or lossy single-choice storage.

## Decision
`CoinType.Finish` = primary finish (`BusinessStrike`, `BullionUncirculated`,
`Proof`, `ReverseProof`, `Burnished`, `MatteProof`, `Unknown`) plus an
independent flag set (`HighRelief`, `Enhanced`, `Colorized`, `Antiqued`,
`FirstStrike`). Premium factors key on the primary; flags contribute
additive adjustments.

## Consequences
Combination coins are representable without enum explosion. The vision
contract reports the primary and any detected flags separately, which also
matches how the model perceives them (fields vs. devices).

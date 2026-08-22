# ADR 0009: Deterministic offline evaluator behind the vision port

Status: Accepted

## Context
The identification pipeline depends on a hosted vision model, but the
system must run and test without provider keys (CI, offline dev, the
fifteen-minute quickstart). The brief bans silent stubs: faking model
output and presenting it as real would be dishonest.

## Decision
`IVisionIdentifier` has two real implementations: a hosted adapter (OpenAI
or Gemini vision, per configuration) and an **offline evaluator** that
produces clearly-labeled deterministic output (perceptual-hash-matched
against seeded reference images, confidence reflecting hash distance;
mechanically honest, never presented as model inference). Responses carry
`provider: "offline"` and the UI labels them. The prompt contract
(Section 6.3 of the brief) is identical for both; the audit record
(`IdentificationRun`) marks which served.

## Consequences
The full pipeline (capture, preprocess, retrieval, confirmation, audit)
is exercisable end-to-end without keys or spend. Swapping in a real key
changes one configuration value. CI never depends on a third-party model.

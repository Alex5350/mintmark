# AI identification pipeline

The feature that makes Mintmark interesting: built as explicit stages,
each independently testable, with an append-only audit trail.

```
capture (guided two-shot)      preprocess              vision contract
─────────────────────────►     crop/deskew/normalize   ─────────────────►
gates: both sides, glare,      512px canonical PNG     strict JSON: value +
focus, resolution; EXIF        pHash                   confidence + evidence
stripped client AND server                             (null beats guessing)
                                                                    │
     user confirms ◄──── top-5 candidates ◄──── hybrid retrieval ◄────┘
     (audit written back)      with scores       pHash hamming ∥ pgvector
                                                ∥ trigram legends ∥ filters
```

## Stage details

**Capture** requires obverse + reverse (edge optional but diagnostic;
reeded vs lettered vs plain distinguishes series). Gates reject blurry/
glared/low-res images at capture time so a doomed model call is never
paid. EXIF (including GPS) is stripped client-side and again by the
server-side re-encode.

**The vision contract** (`prompts/identify-v1.md`, versioned as a file):
every field is `{value, confidence, evidence}`; a field without visual
evidence returns **null, never a guess**; finish definitions (proof vs
reverse proof vs burnished vs BU) are spelled out as visual cues in the
prompt itself; size estimates are hard-capped at low confidence. The model
proposes; the catalog and the user dispose.

**Hybrid retrieval** refuses to trust free-text as an answer: the vision
output becomes *queries*: perceptual-hash distance against reference
images, pgvector cosine when embeddings exist, pg_trgm on legends the
model read, and structured filters (metal, year ±2, AMW band). The user
sees the **top five candidates with scores** and confirms; the
confirmation is written back to `IdentificationRun` as training signal.

**Authenticity signals are advisory, always.** Observed cues are surfaced
(irregular reeding, wrong luster pattern) with the explicit framing that
this is not authentication and cannot substitute for physical testing
(specific gravity, measurement, conductivity, professional grading). The
system never outputs a genuine/fake verdict; an overconfident claim is
this product's largest liability.

## The offline evaluator (ADR 0009)

With no provider key, identification is served by a **deterministic
offline evaluator**: pHash-matched against seeded reference images,
per-field confidence derived from hash distance, every response labeled
`provider: "offline"` and rendered as such in both clients. It is never
presented as model inference, but it exercises the entire pipeline
(capture → preprocess → retrieval → confirm → audit) with zero keys and
zero spend, which is exactly what CI and the 15-minute quickstart need.

## Cost & abuse control

Per-user daily identification limit (25 default), perceptual-hash dedupe
(re-uploading the same photo returns the cached run), spend logged per
user/day with a configurable cap. Identification is asynchronous: submit
returns a job id immediately; no request blocks on a model call.

## Evaluation methodology

A ground-truth set of coin photographs with known catalog entries feeds an
eval harness measuring per-field accuracy (series, year, metal, finish).
The harness runs in CI with a minimum-accuracy bar, so a prompt change
that degrades identification fails the build: the only honest way to
iterate on prompts. **Current baseline:** the harness ships with the
offline evaluator's synthetic reference set (placeholder ground truth);
real-photograph accuracy numbers land when a hosted vision key is
configured and the eval set is populated; tracked in open-questions.

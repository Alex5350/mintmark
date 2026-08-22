# Security: OWASP ASVS Level 2 checklist

Target: **OWASP ASVS Level 2**. Each item: ✅ done, ➖ not applicable (with
reason), ⏸ deferred (with reason + tracking in open-questions).

## Authentication (ASVS V2)

- ✅ Argon2id password hashing (NetDevPack, drop-in `IPasswordHasher`)
- ✅ Password policy: 12+ chars, breached/common denylist check
- ✅ Access tokens 15 min; JWT issuer/audience pinned
- ✅ Refresh tokens: 256-bit random, hashed at rest, single-use, rotating;
  reuse revokes the token family (theft signal; ADR 0005)
- ⏸ breached-password HIBP k-anonymity API check (network dependency;
  local denylist ships now; tracked in open-questions)

## Session management (V3)

- ✅ Web holds tokens in memory only; mobile in `expo-secure-store`
  (`WHEN_UNLOCKED_THIS_DEVICE_ONLY`); never localStorage/AsyncStorage
- ✅ Logout revokes the refresh family server-side

## Access control (V4)

- ✅ Row-level: EF global query filter scopes every `Holding` query by user
- ✅ Integration test proves the filter holds even with endpoint
  authorization removed (defense in depth)
- ✅ Authorization checked at endpoints AND data layer

## Input handling / output encoding (V5)

- ✅ FluentValidation on every mutating endpoint; errors as RFC 9457
  Problem Details
- ✅ Uploads: content-type sniffing (magic bytes, not declared type), size
  caps, server-side image re-encode (strips EXIF/GPS + embedded payloads),
  presigned *expiring* URLs only; bucket is private (ADR 0006)
- ✅ Clients also strip EXIF before upload (belt and suspenders)

## Cryptography (V6)

- ✅ TLS terminated by the platform; HTTPS enforced in production config
- ✅ No custom crypto; SHA-256 for refresh-token hashing at rest

## Error handling / logging (V7/V8)

- ✅ Problem Details for every error; no stack traces to clients
- ✅ Structured logging never carries tokens, passwords, image bytes, or
  storage-location strings

## Data protection (V8): the collection-privacy rule

**This application knows what valuable metal a person owns and where they
keep it.** Consequences, by design:

- `storage location` is excluded from search indexing, excluded from
  default exports, and never logged
- ⏸ column-level encryption at rest for storage location (deferred: needs
  key-management decision; tracked in open-questions)
- EXIF/GPS stripped from every uploaded photograph, server-side

## Communications (V9)

- ✅ Strict CORS (web origin + expo scheme only); security headers
  middleware; HTTPS enforced outside Development

## Files and resources (V12)

- ✅ Per-user rate limits (register/login 10/min per IP; identification
  25/day per user); idempotency keys on mutations

## Not applicable

- V10 malware scoping (no user-supplied executables; image re-encode
  neutralizes payloads)
- V14 config hardening beyond defaults is deployment-specific; runbook
  covers production flags

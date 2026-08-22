# ADR 0005: JWT access tokens + rotating single-use refresh tokens

Status: Accepted

## Context
Web and mobile clients, no server-side sessions desired, offline-tolerant
mobile usage. Refresh tokens must be revocable and must not become
long-lived skeleton keys when leaked.

## Decision
- Access tokens: JWT, 15 minutes (configurable), issuer/audience pinned,
  signed with a symmetric key from configuration (rotate by config change).
- Refresh tokens: opaque 256-bit random values, **hashed at rest**, single
  use, rotating on every refresh. Reuse of a consumed token revokes the
  entire family (theft signal), per RFC 6819 refresh-token-rotation
  guidance.
- Passwords: Argon2id (see docs/security.md; ASP.NET Core Identity's
  default PBKDF2 is replaced: Identity's IPasswordHasher is implemented
  with Isopoh Argon2id).

## Consequences
Short access-token life bounds exposure; rotation + family revocation bounds
refresh theft. Mobile keeps tokens in `expo-secure-store` (never
AsyncStorage), web in memory only; no localStorage tokens.

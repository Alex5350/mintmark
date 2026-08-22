# ADR 0006: S3-compatible object storage for images

Status: Accepted

## Context
Coin photographs are the heaviest data in the system; the brief prohibits
storing image bytes in Postgres and mandates S3-compatible storage with
MinIO locally.

## Decision
AWS SDK for .NET (S3 client) against any S3-compatible endpoint; local
MinIO via compose; bucket private. Reads go through **presigned, expiring
URLs** minted by the API. Uploads use presigned PUT after server-side
validation of the declared content. Every stored image is re-encoded
server-side (ImageSharp), which strips EXIF/GPS as a side effect; clients
additionally strip before upload; belt and suspenders.

## Consequences
No image bytes in Postgres; no public bucket; leaked URLs expire. Storage
is swappable (MinIO → S3 → Azure Blob via endpoint config).

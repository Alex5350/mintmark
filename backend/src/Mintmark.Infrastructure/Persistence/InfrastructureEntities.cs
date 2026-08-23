using Microsoft.AspNetCore.Identity;
using Pgvector;
using Mintmark.Domain;
using Mintmark.Domain.Entities;

namespace Mintmark.Infrastructure.Persistence;

/// <summary>
/// The application user. Deliberately thin: ASP.NET Core Identity's EF store
/// provides the credential machinery; profile data lives elsewhere. Long keys
/// so user ids share the typed-id (<see cref="UserId"/>) backing type.
/// </summary>
public sealed class MintmarkUser : IdentityUser<long>
{
    /// <summary>Gets or sets the optional display name.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// An opaque refresh token, stored hashed (SHA-256) per ADR 0005. Tokens are
/// single-use and rotate on every refresh; reuse of a consumed token revokes
/// the whole family (theft signal).
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Gets or sets the persistence-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owning user id.</summary>
    public long UserId { get; set; }

    /// <summary>Gets or sets the rotation family id (issued together at login; revoked together on theft).</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Gets or sets the hex SHA-256 hash of the opaque token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets when the token expires (UTC).</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Gets or sets when the token was consumed by a refresh (UTC), if it was.</summary>
    public DateTimeOffset? ConsumedAtUtc { get; set; }

    /// <summary>Gets or sets when the token was revoked (UTC), if it was.</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Gets or sets the optional client device label.</summary>
    public string? DeviceLabel { get; set; }

    /// <summary>Gets or sets when the token was issued (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Gets a value indicating whether the token can no longer be exchanged, for any reason.</summary>
    public bool IsDead => ConsumedAtUtc is not null || RevokedAtUtc is not null || ExpiresAtUtc <= DateTimeOffset.UtcNow;
}

/// <summary>
/// Stored idempotency replay: the idempotency key, the response body and its
/// status code, so a retried create returns the original response verbatim.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Gets or sets the persistence-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owning user id (keys are scoped per user).</summary>
    public long UserId { get; set; }

    /// <summary>Gets or sets the caller-supplied idempotency key.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint the key was first used on.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized response body served on first use.</summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>Gets or sets the status code served on first use.</summary>
    public int StatusCode { get; set; }

    /// <summary>Gets or sets when the record was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; }
}

/// <summary>
/// A seeded reference image for a catalog row (obverse/reverse). Reference
/// images belong to the catalog, not to any user holding, so they cannot be
/// <see cref="CoinImage"/> rows (whose invariant binds them to a holding);
/// this Infrastructure-owned table is the retrieval corpus for perceptual
/// matching (ADR 0009) — see docs/open-questions.md.
/// </summary>
public sealed class ReferenceImage
{
    /// <summary>Gets or sets the persistence-assigned identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the cataloged coin type the image references.</summary>
    public CoinTypeId CoinTypeId { get; set; }

    /// <summary>Gets or sets which side of the coin the image shows.</summary>
    public CoinSide Side { get; set; }

    /// <summary>Gets or sets the object-storage key of the image bytes.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the 64-bit perceptual hash of the image.</summary>
    public ulong PerceptualHash { get; set; }

    /// <summary>
    /// Gets or sets the 768-dim embedding vector (pgvector). Nullable: the
    /// offline provider derives deterministic non-semantic vectors; real
    /// model embeddings land here later.
    /// </summary>
    public Vector? Embedding { get; set; }
}

/// <summary>
/// Series demand tier (ADR 0007 premium input). The bound Domain Series has
/// no tier property (it is reference data, not series identity), so it lives
/// in this Infrastructure-owned table keyed by series id.
/// </summary>
public sealed class SeriesDemandTierRow
{
    /// <summary>Gets or sets the series id (primary key).</summary>
    public SeriesId SeriesId { get; set; }

    /// <summary>Gets or sets the demand tier.</summary>
    public SeriesDemandTier Tier { get; set; }
}

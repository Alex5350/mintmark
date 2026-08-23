using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mintmark.Application.Dtos;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Infrastructure.Identity;

/// <summary>
/// Rotating single-use refresh tokens (ADR 0005). Opaque 256-bit random
/// values hashed at rest (SHA-256); every refresh consumes the presented
/// token and issues a successor in the same family. Presenting an
/// already-consumed token is a theft signal and revokes the entire family —
/// per RFC 6819 refresh-token-rotation guidance.
/// </summary>
public sealed class RefreshTokenService(
    MintmarkDbContext dbContext,
    AccessTokenIssuer accessTokenIssuer,
    JwtOptions jwtOptions,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Issues a brand-new token family for a user (login/register).</summary>
    public async Task<TokenResponse> IssueAsync(
        MintmarkUser user,
        string? deviceLabel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _clock.GetUtcNow();
        var (raw, hash) = Generate();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = hash,
            ExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays),
            DeviceLabel = deviceLabel,
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return BuildResponse(user, raw);
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair, consuming the old token and
    /// resolving the owning account from the token record itself. Reuse of a
    /// consumed token revokes the whole family (theft signal).
    /// </summary>
    /// <exception cref="InvalidRefreshTokenException">
    /// Thrown when the token is unknown, expired, revoked, or was already
    /// consumed — in the last case the entire family is revoked first.
    /// </exception>
    public async Task<TokenResponse> RotateAsync(
        UserManager<MintmarkUser> userManager,
        string refreshToken,
        string? deviceLabel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var now = _clock.GetUtcNow();
        var record = await FindByRawTokenAsync(refreshToken, cancellationToken)
            ?? throw new InvalidRefreshTokenException("Unknown refresh token.");

        if (record.RevokedAtUtc is not null)
        {
            throw new InvalidRefreshTokenException("Refresh token has been revoked.");
        }

        if (record.ExpiresAtUtc <= now)
        {
            throw new InvalidRefreshTokenException("Refresh token has expired.");
        }

        if (record.ConsumedAtUtc is not null)
        {
            // Reuse of a rotated token: revoke the whole family immediately.
            await RevokeFamilyAsync(record.FamilyId, now, cancellationToken);
            throw new InvalidRefreshTokenException(
                "Refresh token was already used; possible theft detected — the token family has been revoked.");
        }

        var user = await userManager.FindByIdAsync(record.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new InvalidRefreshTokenException("Refresh token belongs to a deleted account.");

        record.ConsumedAtUtc = now;
        var (raw, hash) = Generate();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = record.UserId,
            FamilyId = record.FamilyId,
            TokenHash = hash,
            ExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays),
            DeviceLabel = deviceLabel ?? record.DeviceLabel,
            CreatedAtUtc = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return BuildResponse(user, raw);
    }

    /// <summary>Revokes every live token in the family of the presented token (logout).</summary>
    public async Task RevokeFamilyAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var record = await FindByRawTokenAsync(refreshToken, cancellationToken);
        if (record is null)
        {
            return;
        }

        await RevokeFamilyAsync(record.FamilyId, _clock.GetUtcNow(), cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await dbContext.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in family)
        {
            token.RevokedAtUtc = now;
        }

        if (family.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<RefreshToken?> FindByRawTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.FromResult<RefreshToken?>(null);
        }

        var hash = Sha256Hex(rawToken);
        return dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private TokenResponse BuildResponse(MintmarkUser user, string rawRefreshToken)
    {
        var (accessToken, expiresAt) = accessTokenIssuer.Issue(user);
        return new TokenResponse(accessToken, rawRefreshToken, expiresAt);
    }

    private static (string Raw, string Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256-bit opaque token
        var raw = ToBase64Url(bytes);
        return (raw, Sha256Hex(raw));
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes).TrimEnd('=');
        return base64.Replace('+', '-').Replace('/', '_');
    }

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }
}

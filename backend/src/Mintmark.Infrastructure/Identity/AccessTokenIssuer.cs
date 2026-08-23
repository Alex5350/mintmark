
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mintmark.Application.Dtos;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Infrastructure.Identity;

/// <summary>Thrown when a refresh token cannot be exchanged (unknown, expired, revoked, or reused).</summary>
public sealed class InvalidRefreshTokenException(string message) : Exception(message);

/// <summary>
/// Issues short-lived JWT access tokens (ADR 0005): symmetric key, pinned
/// issuer/audience, <c>sub</c> = user id, 15 minutes by configuration.
/// </summary>
public sealed class AccessTokenIssuer(JwtOptions options)
{
    /// <summary>Gets the issuer claim value.</summary>
    public string Issuer => options.Issuer;

    /// <summary>Gets the audience claim value.</summary>
    public string Audience => options.Audience;

    /// <summary>Gets the access-token lifetime.</summary>
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(options.AccessTokenMinutes);

    /// <summary>Gets the validation key (shared with the API's JwtBearer setup).</summary>
    public SymmetricSecurityKey SigningKey => new(System.Text.Encoding.UTF8.GetBytes(options.SigningKey));

    /// <summary>Issues an access token for a user.</summary>
    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(MintmarkUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            NotBefore = now.UtcDateTime.AddSeconds(-5),
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["email"] = user.Email ?? string.Empty,
            },
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expires);
    }
}

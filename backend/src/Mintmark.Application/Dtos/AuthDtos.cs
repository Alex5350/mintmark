namespace Mintmark.Application.Dtos;

/// <summary>Registration request.</summary>
/// <param name="Email">Email address (unique).</param>
/// <param name="Password">Password; min 12 chars, screened against a common-password denylist.</param>
/// <param name="DisplayName">Optional display name.</param>
public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);

/// <summary>Login request.</summary>
/// <param name="Email">Email address.</param>
/// <param name="Password">Password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Refresh-token exchange request.</summary>
/// <param name="RefreshToken">The previously issued refresh token.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Issued token pair.</summary>
/// <param name="AccessToken">The bearer access token.</param>
/// <param name="RefreshToken">The refresh token.</param>
/// <param name="ExpiresAtUtc">When the access token expires.</param>
/// <param name="TokenType">Token type, always <c>Bearer</c>.</param>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string TokenType = "Bearer");

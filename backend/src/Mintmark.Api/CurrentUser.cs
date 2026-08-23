using System.Security.Claims;
using Mintmark.Domain;

namespace Mintmark.Api;

/// <summary>Current-user helpers over the JWT claims.</summary>
public static class CurrentUser
{
    /// <summary>Extracts the user id from the <c>sub</c> claim, or null when anonymous.</summary>
    public static UserId? TryGetUserId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? new UserId(id) : null;
    }

    /// <summary>Extracts the user id or throws (for endpoints that require authorization).</summary>
    public static UserId RequireUserId(this HttpContext context) =>
        context.TryGetUserId() ?? throw new InvalidOperationException("No authenticated user on an authorized endpoint.");
}

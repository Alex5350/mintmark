using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Api;

/// <summary>
/// Propagates the authenticated user into the DbContext's global query
/// filter (row-level authorization on Holding): after this middleware, any
/// query through the request's scope sees only that user's holdings.
/// </summary>
public sealed class UserContextMiddleware(RequestDelegate next)
{
    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, MintmarkDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetUserId() is { } userId)
        {
            dbContext.CurrentUserId = userId;
        }

        await next(context);
    }
}

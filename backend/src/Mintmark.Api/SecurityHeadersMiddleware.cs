namespace Mintmark.Api;

/// <summary>
/// Baseline response hardening (docs/security.md, ASVS V9): the API serves
/// data, not documents, so every response carries the mime-sniffing, framing,
/// and referrer closures, and a content security policy that allows nothing.
/// The Scalar reference UI at <c>/docs</c> is the one HTML surface and is
/// exempt from the CSP; it only exists outside production.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string DocsPath = "/docs";

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (!context.Request.Path.StartsWithSegments(DocsPath, StringComparison.OrdinalIgnoreCase))
        {
            headers["Content-Security-Policy"] = "default-src 'none'";
        }

        await next(context);
    }
}

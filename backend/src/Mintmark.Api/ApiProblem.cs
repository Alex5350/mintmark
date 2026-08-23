using FluentValidation.Results;

namespace Mintmark.Api;

/// <summary>
/// RFC 9457 problem responses. Validation failures are 422 with an
/// <c>errors</c> dictionary (field → messages); every error the API returns
/// is a problem document, never a bare string.
/// </summary>
public static class ApiProblem
{
    /// <summary>Builds the 422 validation problem from a FluentValidation result.</summary>
    public static IResult Validation(ValidationResult validation) =>
        Validation(validation.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    /// <summary>Builds the 422 validation problem from an explicit errors dictionary.</summary>
    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors) =>
        Results.Problem(
            title: "Validation failed",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: "https://mintmark.local/problems/validation",
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = errors,
            });

    /// <summary>Builds a 404 problem.</summary>
    public static IResult NotFound(string detail) =>
        Results.Problem(title: "Not found", statusCode: StatusCodes.Status404NotFound, detail: detail);

    /// <summary>Builds a 401 problem.</summary>
    public static IResult Unauthorized(string detail) =>
        Results.Problem(title: "Unauthorized", statusCode: StatusCodes.Status401Unauthorized, detail: detail);

    /// <summary>Builds a 409 problem.</summary>
    public static IResult Conflict(string detail) =>
        Results.Problem(title: "Conflict", statusCode: StatusCodes.Status409Conflict, detail: detail);

    /// <summary>Builds a 422 problem for domain-rule violations.</summary>
    public static IResult Unprocessable(string detail) =>
        Results.Problem(
            title: "Unprocessable entity",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            detail: detail);

    /// <summary>Builds a 503 problem (e.g. no price data yet).</summary>
    public static IResult ServiceUnavailable(string detail) =>
        Results.Problem(title: "Service unavailable", statusCode: StatusCodes.Status503ServiceUnavailable, detail: detail);
}

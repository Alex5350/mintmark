using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Mintmark.Api;
using Mintmark.Application.Dtos;
using Mintmark.Application.Validators;
using Mintmark.Infrastructure.Identity;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Api.EndpointModules;

/// <summary>Auth endpoints: register / login / refresh (rotating) / logout (family revocation).</summary>
public sealed class AuthModule : IEndpointModule
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IValidator<RegisterRequest> validator,
            UserManager<MintmarkUser> userManager,
            RefreshTokenService refreshTokens,
            HttpContext http) =>
        {
            var validation = await validator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                return ApiProblem.Validation(validation);
            }

            var existing = await userManager.FindByEmailAsync(request.Email);
            if (existing is not null)
            {
                return ApiProblem.Conflict("An account with this email already exists.");
            }

            var user = new MintmarkUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            };

            var created = await userManager.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                return ApiProblem.Unprocessable(string.Join(" ", created.Errors.Select(e => e.Description)));
            }

            var tokens = await refreshTokens.IssueAsync(user, DeviceLabel(http));
            return Results.Created($"/api/v1/users/{user.Id}", tokens);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<MintmarkUser> userManager,
            RefreshTokenService refreshTokens,
            HttpContext http) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                // Uniform failure: no account-enumeration signal.
                return ApiProblem.Unauthorized("Invalid email or password.");
            }

            var tokens = await refreshTokens.IssueAsync(user, DeviceLabel(http));
            return Results.Ok(tokens);
        });

        group.MapPost("/refresh", async (
            RefreshRequest request,
            UserManager<MintmarkUser> userManager,
            RefreshTokenService refreshTokens,
            HttpContext http) =>
        {
            try
            {
                var tokens = await refreshTokens.RotateAsync(userManager, request.RefreshToken, DeviceLabel(http));
                return Results.Ok(tokens);
            }
            catch (InvalidRefreshTokenException ex)
            {
                return ApiProblem.Unauthorized(ex.Message);
            }
        });

        group.MapPost("/logout", async (
            RefreshRequest request,
            RefreshTokenService refreshTokens) =>
        {
            await refreshTokens.RevokeFamilyAsync(request.RefreshToken);
            return Results.NoContent();
        });
    }

    private static string? DeviceLabel(HttpContext http) =>
        // Cap at the column maximum: some in-app browsers and privacy
        // extensions send User-Agents well past 200 chars, which would fail
        // the insert and turn every login attempt into a 500.
        http.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent
            ? agent[..Math.Min(agent.Length, 200)]
            : null;
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Mintmark.Api.IntegrationTests;

/// <summary>
/// One factory + one Testcontainers Postgres for the whole class — Quartz's
/// static log provider captures the first factory's logger, so per-test
/// factories would dispose it under later tests.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    // Parameterless builder + WithImage stays the practical 4.x surface for
    // non-default images; deprecation pragma carries a justification.
    private readonly Testcontainers.PostgreSql.PostgreSqlContainer _db =
#pragma warning disable CS0618 // parameterless PostgreSqlBuilder deprecated in 4.14
        new Testcontainers.PostgreSql.PostgreSqlBuilder()
#pragma warning restore CS0618
            .WithImage("pgvector/pgvector:pg18")
            .Build();

    static ApiFixture()
    {
        Quartz.Logging.LogProvider.IsDisabled = true;
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:ConnectionString", _db.GetConnectionString());
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting(
                "Jwt:SigningKey", "integration-test-signing-key-with-enough-length-0123456789");
        });
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
        await _db.DisposeAsync();
    }
}

/// <summary>
/// Full-stack smoke against real PostgreSQL via Testcontainers — never the
/// in-memory provider. Real Postgres via Docker (colima locally).
/// </summary>
public sealed class ApiSmokeTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client => fixture.Factory.CreateClient();

    [Fact]
    public async Task Health_Endpoint_Reports_Database_Up()
    {
        var response = await Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_Then_Bad_Login_Is_Clean_Unauthorized_Problem_Details()
    {
        var register = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"smoke-{Guid.NewGuid():N}@test.local",
            password = "collector-pass-2026",
            displayName = "Smoke Tester",
        });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "unknown-user@test.local",
            password = "wrong-password-123",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validation_Errors_Are_Problem_Details()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "not-an-email",
            password = "short",
            displayName = "",
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("title", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthenticated_Holdings_Is_Rejected()
    {
        var response = await Client.GetAsync("/api/v1/holdings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string? _sharedEmail;
    private static string? _sharedAccessToken;

    /// <summary>
    /// Registers (once) and signs in a user shared by the endpoint tests —
    /// the auth rate limiter allows 10 attempts/IP/minute, so one fresh
    /// user per test would starve the suite. Tests that consume refresh
    /// tokens must not use the shared identity.
    /// </summary>
    private async Task<HttpClient> SharedSignedInClientAsync()
    {
        if (_sharedAccessToken is null)
        {
            _sharedEmail = $"smoke-shared-{Guid.NewGuid():N}@test.local";
            var register = await Client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = _sharedEmail,
                password = "collector-pass-2026",
                displayName = "Smoke Tester",
            });
            Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());
            var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = _sharedEmail,
                password = "collector-pass-2026",
            });
            Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            _sharedAccessToken = auth!.AccessToken;
        }

        var authorized = fixture.Factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sharedAccessToken);
        return authorized;
    }

    /// <summary>A dedicated identity for the refresh-rotation test (it consumes tokens).</summary>
    private async Task<string> RegisterFreshRefreshTokenAsync()
    {
        var email = $"smoke-{Guid.NewGuid():N}@test.local";
        var register = await Client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "collector-pass-2026",
            displayName = "Smoke Tester",
        });
        Assert.True(register.IsSuccessStatusCode, await register.Content.ReadAsStringAsync());
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "collector-pass-2026",
        });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.RefreshToken;
    }

    private sealed record AuthResponse(string AccessToken, string RefreshToken);

    private sealed record CreateHoldingResponse(long Id, bool Created);

    [Fact]
    public async Task Malformed_Cursor_Is_Rejected_Not_Internal_Error()
    {
        var authorized = await SharedSignedInClientAsync();

        // "-1:1" base64-encoded: a valid integer, but not a valid instant.
        var response = await authorized.GetAsync("/api/v1/holdings?limit=5&cursor=LTE6MQ%3D%3D");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Reused_Refresh_Token_Is_Rejected_And_Revokes_Family()
    {
        var refreshToken = await RegisterFreshRefreshTokenAsync();

        var first = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());

        // Presenting the already-consumed token again is the theft signal:
        // 401, and the successor issued above is revoked with the family.
        var second = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        var rotated = await first.Content.ReadFromJsonAsync<AuthResponse>();
        var third = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = rotated!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, third.StatusCode);
    }

    [Fact]
    public async Task Holdings_Reject_Currency_Other_Than_Base()
    {
        var authorized = await SharedSignedInClientAsync();

        var response = await authorized.PostAsJsonAsync("/api/v1/holdings", new
        {
            itemForm = 2,
            quantity = 1,
            purchaseDate = "2026-01-15",
            purchasePricePerUnit = new { amount = 33.10m, currency = "EUR" },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("base currency", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Image_Register_Rejects_Upload_Key_From_Another_Account()
    {
        var authorized = await SharedSignedInClientAsync();

        // Create a holding to register against; the foreign upload key must
        // be refused before any object-store access.
        var create = await authorized.PostAsJsonAsync("/api/v1/holdings", new
        {
            itemForm = 2,
            quantity = 1,
            purchaseDate = "2026-01-15",
            purchasePricePerUnit = new { amount = 33.10m, currency = "USD" },
        });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        var holding = await create.Content.ReadFromJsonAsync<CreateHoldingResponse>();
        Assert.NotNull(holding);

        var response = await authorized.PostAsJsonAsync("/api/v1/images", new
        {
            holdingId = holding!.Id,
            side = "obverse",
            uploadKey = "uploads/999/foreign-key",
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UploadKey", body, StringComparison.OrdinalIgnoreCase);
    }
}

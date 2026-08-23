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
}

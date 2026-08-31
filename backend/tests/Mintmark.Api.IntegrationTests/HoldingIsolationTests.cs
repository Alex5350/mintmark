using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure.Identity;
using Mintmark.Infrastructure.Persistence;
using Xunit;

namespace Mintmark.Api.IntegrationTests;

/// <summary>
/// Both integration test classes share the ApiFixture (one host, one
/// Postgres container): a second factory would trip the Quartz static-log-
/// provider hazard the fixture documents, and a second host would also mean
/// a second rate-limiter window, which these tests deliberately stay clear
/// of anyway.
/// </summary>
[CollectionDefinition("api")]
public sealed class ApiIntegrationSuite : ICollectionFixture<ApiFixture>;

/// <summary>
/// Row-level holding isolation, the test the security docs cite. Two views:
/// the endpoint view (a signed-in user lists only their own holdings, cursor
/// pagination included) and the data-layer view (the EF global query filter
/// holds with no endpoint and no authorization middleware in the picture at
/// all, only the DbContext user context).
/// </summary>
[Collection("api")]
public sealed class HoldingIsolationTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.Factory.CreateClient();

    /// <summary>
    /// Provisions a user and an access token straight through UserManager and
    /// AccessTokenIssuer. The token is byte-for-byte what login returns, so
    /// requests below run the real bearer, user-context, and endpoint
    /// authorization pipeline; going direct keeps these tests from spending
    /// the auth endpoints' shared 10-per-minute rate budget, which the smoke
    /// tests already consume.
    /// </summary>
    private async Task<HttpClient> SignInNewUserAsync(string label)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MintmarkUser>>();
        var issuer = scope.ServiceProvider.GetRequiredService<AccessTokenIssuer>();

        var email = $"iso-{label}-{Guid.NewGuid():N}@test.local";
        var user = new MintmarkUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Isolation Tester",
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);

        var (token, _) = issuer.Issue(user);
        var authorized = fixture.Factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return authorized;
    }

    private static async Task CreateHoldingAsync(HttpClient client, string purchaseDate)
    {
        var response = await client.PostAsJsonAsync("/api/v1/holdings", new
        {
            itemForm = 2,
            quantity = 1,
            purchaseDate,
            purchasePricePerUnit = new { amount = 33.10m, currency = "USD" },
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<ListHoldingsResponse> ListHoldingsAsync(HttpClient client, string? query = null)
    {
        // limit is always sent: the endpoint binds it as a required
        // parameter, so a bare GET without it is a 500, not the default 50.
        var response = await client.GetAsync(query is null ? "/api/v1/holdings?limit=50" : $"/api/v1/holdings?{query}");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var page = await response.Content.ReadFromJsonAsync<ListHoldingsResponse>();
        Assert.NotNull(page);
        return page!;
    }

    [Fact]
    public async Task Holdings_List_Returns_Only_The_Calling_User_S_Holdings()
    {
        var alice = await SignInNewUserAsync("a");
        var bob = await SignInNewUserAsync("b");

        // A brand new user sees an empty page, not another account's rows.
        var empty = await ListHoldingsAsync(alice);
        Assert.Empty(empty.Items);
        Assert.Null(empty.NextCursor);

        await CreateHoldingAsync(alice, "2026-01-15");
        await CreateHoldingAsync(alice, "2026-02-15");
        await CreateHoldingAsync(bob, "2026-03-15");

        // Both users are brand new, so their full lists are exactly the rows
        // they just created; nobody else's rows can appear.
        var aliceIds = (await ListHoldingsAsync(alice)).Items.Select(h => h.Id).ToList();
        var bobIds = (await ListHoldingsAsync(bob)).Items.Select(h => h.Id).ToList();

        Assert.Equal(2, aliceIds.Count);
        Assert.Single(bobIds);
        Assert.Empty(aliceIds.Intersect(bobIds));
    }

    [Fact]
    public async Task Holdings_Pagination_Stays_Scoped_To_The_Calling_User()
    {
        var carol = await SignInNewUserAsync("c");
        var dave = await SignInNewUserAsync("d");

        await CreateHoldingAsync(carol, "2026-01-15");
        await CreateHoldingAsync(carol, "2026-02-15");
        await CreateHoldingAsync(carol, "2026-03-15");
        await CreateHoldingAsync(dave, "2026-04-15");
        await CreateHoldingAsync(dave, "2026-05-15");

        var carolIds = (await ListHoldingsAsync(carol)).Items.Select(h => h.Id).ToHashSet();
        var daveIds = (await ListHoldingsAsync(dave)).Items.Select(h => h.Id).ToHashSet();
        Assert.Equal(3, carolIds.Count);
        Assert.Equal(2, daveIds.Count);

        // Walk the cursor: every page, and the union of pages, stays inside
        // the caller's rows even though the foreign user's holdings sort
        // between them on the shared (purchased_at, id) key.
        var first = await ListHoldingsAsync(carol, "limit=2");
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.All(first.Items, h => Assert.Contains(h.Id, carolIds));

        var second = await ListHoldingsAsync(carol, $"limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
        Assert.All(second.Items, h => Assert.Contains(h.Id, carolIds));

        var pagedIds = first.Items.Concat(second.Items).Select(h => h.Id).ToHashSet();
        Assert.Equal(carolIds, pagedIds);
        Assert.Empty(pagedIds.Intersect(daveIds));
    }

    [Fact]
    public async Task Holding_Query_Filter_Holds_With_Endpoint_Authorization_Removed()
    {
        // No HTTP in this test: users and holdings are written straight
        // through the DbContext, so no endpoint, no RequireAuthorization,
        // and no authentication middleware can be what scopes the query.
        // Only the DbContext user context does, which is the defense the
        // docs claim: the filter survives even if an endpoint forgets to
        // authorize.
        using var scope = fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MintmarkUser>>();
        var database = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();

        var alice = new MintmarkUser
        {
            UserName = $"iso-db-a-{Guid.NewGuid():N}@test.local",
            Email = $"iso-db-a-{Guid.NewGuid():N}@test.local",
            EmailConfirmed = true,
        };
        var bob = new MintmarkUser
        {
            UserName = $"iso-db-b-{Guid.NewGuid():N}@test.local",
            Email = $"iso-db-b-{Guid.NewGuid():N}@test.local",
            EmailConfirmed = true,
        };
        Assert.True((await userManager.CreateAsync(alice)).Succeeded);
        Assert.True((await userManager.CreateAsync(bob)).Succeeded);

        var aliceHolding = Holding.Create(
            new UserId(alice.Id), ItemForm.Bar, 1,
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            new Money(33.10m, "USD"));
        var bobHolding = Holding.Create(
            new UserId(bob.Id), ItemForm.Bar, 1,
            new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero),
            new Money(34.20m, "USD"));
        database.Holdings.AddRange(aliceHolding, bobHolding);
        await database.SaveChangesAsync();

        database.CurrentUserId = new UserId(alice.Id);
        var aliceVisible = await database.Holdings.AsNoTracking().ToListAsync();
        Assert.Single(aliceVisible);
        Assert.All(aliceVisible, h => Assert.Equal(new UserId(alice.Id), h.UserId));

        // A direct lookup of the foreign row's id is invisible to alice.
        var foreignLookup = await database.Holdings.AsNoTracking()
            .Where(h => h.Id == bobHolding.Id)
            .ToListAsync();
        Assert.Empty(foreignLookup);

        database.CurrentUserId = new UserId(bob.Id);
        var bobVisible = await database.Holdings.AsNoTracking().ToListAsync();
        Assert.Single(bobVisible);
        Assert.All(bobVisible, h => Assert.Equal(new UserId(bob.Id), h.UserId));

        // No user context set: the safe default sees nothing at all.
        database.CurrentUserId = default;
        Assert.Empty(await database.Holdings.AsNoTracking().ToListAsync());
    }

    private sealed record HoldingRow(long Id);

    private sealed record ListHoldingsResponse(List<HoldingRow> Items, string? NextCursor);
}

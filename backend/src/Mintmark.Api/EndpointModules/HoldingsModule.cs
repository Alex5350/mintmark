using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mintmark.Api;
using Mintmark.Application.Dtos;
using Mintmark.Application.Validators;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Holdings CRUD. Every query is scoped by the DbContext's global
/// (user, not-deleted) filter, so another user's holding is indistinguishable
/// from a missing one (404). Creates honor Idempotency-Key: the key plus the
/// stored response replays verbatim on retry.
/// </summary>
public sealed class HoldingsModule : IEndpointModule
{
    private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/holdings")
            .WithTags("Holdings")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateHoldingRequest request,
            IValidator<CreateHoldingRequest> validator,
            MintmarkDbContext dbContext,
            HttpContext http) =>
        {
            var userId = http.RequireUserId();
            var validation = await validator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                return ApiProblem.Validation(validation);
            }

            var idempotencyKey = http.Request.Headers["Idempotency-Key"].ToString();
            var endpoint = "POST /api/v1/holdings";

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var replay = await dbContext.IdempotencyRecords
                    .Where(r => r.UserId == userId.Value && r.IdempotencyKey == idempotencyKey && r.Endpoint == endpoint)
                    .FirstOrDefaultAsync(http.RequestAborted);
                if (replay is not null)
                {
                    http.Response.Headers["Idempotent-Replay"] = "true";
                    return Results.Text(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
                }
            }

            var holding = Holding.Create(
                userId,
                request.ItemForm!.Value,
                request.Quantity,
                request.PurchaseDate!.Value,
                new Money(request.PurchasePricePerUnit!.Amount, request.PurchasePricePerUnit.Currency),
                request.CoinTypeId is { } coinTypeId ? new CoinTypeId(coinTypeId) : null,
                request.Dealer,
                request.StorageLocation,
                request.SerialNumber,
                notes: request.Notes);

            if (request.CoinTypeId is { } typeId
                && await dbContext.CoinTypes.AllAsync(c => c.Id != new CoinTypeId(typeId), http.RequestAborted))
            {
                return ApiProblem.Unprocessable($"CoinType {typeId} does not exist.");
            }

            dbContext.Holdings.Add(holding);

            // One transaction: holding + idempotency record commit together,
            // so a crash or concurrent duplicate cannot leave a holding that
            // the replay check will never see.
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    UserId = userId.Value,
                    IdempotencyKey = idempotencyKey,
                    Endpoint = endpoint,
                    ResponseBody = JsonSerializer.Serialize(new CreateHoldingResponse(holding.Id, Created: true), ResponseJson),
                    StatusCode = StatusCodes.Status201Created,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                });
            }

            try
            {
                await dbContext.SaveChangesAsync(http.RequestAborted);
            }
            catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                // A concurrent request with the same key won the unique
                // (user, key, endpoint) race — its holding stands; replay
                // the stored response verbatim.
                dbContext.ChangeTracker.Clear();
                var winner = await dbContext.IdempotencyRecords
                    .Where(r => r.UserId == userId.Value && r.IdempotencyKey == idempotencyKey && r.Endpoint == endpoint)
                    .FirstOrDefaultAsync(http.RequestAborted);
                if (winner is not null)
                {
                    http.Response.Headers["Idempotent-Replay"] = "true";
                    return Results.Text(winner.ResponseBody, "application/json", Encoding.UTF8, winner.StatusCode);
                }
                throw;
            }

            var body = JsonSerializer.Serialize(new CreateHoldingResponse(holding.Id, Created: true), ResponseJson);

            return Results.Text(body, "application/json", Encoding.UTF8, StatusCodes.Status201Created);
        });

        group.MapGet("/", async (
            string? cursor,
            int limit,
            MintmarkDbContext dbContext,
            HttpContext http) =>
        {
            var validation = ValidateList(cursor, limit);
            if (validation is not null)
            {
                return validation;
            }

            var pageSize = limit is < 1 or > 100 ? 50 : limit;

            var query = dbContext.Holdings
                .Include(h => h.Revisions)
                .AsNoTracking()
                .OrderByDescending(h => h.PurchasedAtUtc)
                .ThenByDescending(h => h.Id)
                .AsQueryable();

            if (cursor is not null && HoldingCursor.TryDecode(cursor, out var position))
            {
                // Keyset continuation on the (purchased_at, id) sort key.
                query = query.Where(h =>
                    h.PurchasedAtUtc < position.PurchasedAtUtc
                    || (h.PurchasedAtUtc == position.PurchasedAtUtc && h.Id.Value < position.Id));
            }

            var page = await query.Take(pageSize + 1).ToListAsync(http.RequestAborted);
            var coinTypeIds = page.Where(h => h.CoinTypeId is not null).Select(h => h.CoinTypeId!.Value).Distinct().ToList();
            var catalog = await dbContext.CoinTypes
                .Where(c => coinTypeIds.Contains(c.Id))
                .Join(dbContext.Series, c => c.SeriesId, s => s.Id, (c, s) => new { c.Id, c.Name, s.Metal })
                .ToDictionaryAsync(x => x.Id, http.RequestAborted);

            var items = page.Take(pageSize).Select(h =>
            {
                string? name = null;
                MetalKind? metal = null;
                if (h.CoinTypeId is { } typeId && catalog.TryGetValue(typeId, out var entry))
                {
                    name = entry.Name;
                    metal = entry.Metal;
                }

                return new HoldingSummary(
                    h.Id,
                    name ?? GenericLabel(h.Form),
                    metal,
                    h.Form,
                    h.EffectiveQuantity,
                    h.EffectivePurchasePricePerUnit,
                    currentValue: null);
            }).ToList();

            string? next = null;
            if (page.Count > pageSize)
            {
                var last = page[pageSize - 1];
                next = HoldingCursor.Encode(last.PurchasedAtUtc, last.Id.Value);
            }

            return Results.Ok(new ListHoldingsResponse(items, next));
        });

        group.MapGet("/{id:long}", async (long id, MintmarkDbContext dbContext, HttpContext http) =>
        {
            var holding = await LoadAsync(dbContext, id, http.RequestAborted);
            if (holding is null)
            {
                return ApiProblem.NotFound("Holding not found.");
            }

            var name = holding.CoinTypeId is { } typeId
                ? await dbContext.CoinTypes
                    .Where(c => c.Id == typeId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(http.RequestAborted)
                : null;

            return Results.Ok(new HoldingDetail(
                holding.Id,
                holding.CoinTypeId,
                name ?? GenericLabel(holding.Form),
                holding.Form,
                holding.Quantity,
                holding.EffectiveQuantity,
                holding.PurchasePricePerUnit,
                holding.EffectivePurchasePricePerUnit,
                holding.Revisions.Count,
                null,
                null,
                holding.PurchasedAtUtc,
                holding.IsDeleted));
        });

        group.MapPatch("/{id:long}", async (
            long id,
            UpdateHoldingRequest request,
            MintmarkDbContext dbContext,
            HttpContext http) =>
        {
            var holding = await LoadAsync(dbContext, id, http.RequestAborted);
            if (holding is null)
            {
                return ApiProblem.NotFound("Holding not found.");
            }

            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                errors[nameof(request.Reason)] = ["A revision reason is required."];
            }
            else if (request.Reason.Length > 500)
            {
                errors[nameof(request.Reason)] = ["A revision reason is at most 500 characters."];
            }

            var quantity = request.Quantity ?? holding.EffectiveQuantity;
            if (quantity < 1)
            {
                errors[nameof(request.Quantity)] = ["Quantity must be at least 1."];
            }

            var price = holding.EffectivePurchasePricePerUnit;
            if (request.PurchasePricePerUnit is { } input)
            {
                // Revisions revalue in the holding's original currency; the
                // domain forbids cross-currency revisions (and the rollup
                // cannot sum across units).
                if (!string.Equals(input.Currency, holding.EffectivePurchasePricePerUnit.Currency.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    errors[nameof(request.PurchasePricePerUnit)] =
                        ["Purchase currency cannot change across revisions; record a new holding instead."];
                }
                else
                {
                    price = new Money(input.Amount, input.Currency);
                }
            }
            if (request.PurchasePricePerUnit is { Amount: < 0m })
            {
                errors[nameof(request.PurchasePricePerUnit)] = ["Purchase price cannot be negative."];
            }

            if (errors.Count > 0)
            {
                return ApiProblem.Validation(errors);
            }

            try
            {
                _ = holding.AppendRevision(quantity, price, request.Reason);
            }
            catch (ArgumentException ex)
            {
                return ApiProblem.Unprocessable(ex.Message);
            }
            await dbContext.SaveChangesAsync(http.RequestAborted);
            return Results.NoContent();
        });

        group.MapDelete("/{id:long}", async (long id, MintmarkDbContext dbContext, HttpContext http) =>
        {
            var holding = await LoadAsync(dbContext, id, http.RequestAborted);
            if (holding is null)
            {
                return ApiProblem.NotFound("Holding not found.");
            }

            holding.SoftDelete();
            await dbContext.SaveChangesAsync(http.RequestAborted);
            return Results.NoContent();
        });
    }

    private static IResult? ValidateList(string? cursor, int limit) =>
        limit != 0 && limit is < 1 or > 100
            ? ApiProblem.Validation(new Dictionary<string, string[]> { [nameof(limit)] = ["Limit must be between 1 and 100."] })
            : null;

    private static Task<Holding?> LoadAsync(MintmarkDbContext dbContext, long id, CancellationToken cancellationToken) =>
        dbContext.Holdings
            .Include(h => h.Revisions)
            .FirstOrDefaultAsync(h => h.Id == new HoldingId(id), cancellationToken);

    private static string GenericLabel(ItemForm form) => form switch
    {
        ItemForm.Bar => "Generic bar",
        ItemForm.Round => "Generic round",
        ItemForm.JunkSilver => "Junk silver (generic)",
        ItemForm.Scrap => "Scrap metal",
        ItemForm.Jewelry => "Jewelry",
        _ => "Uncataloged item",
    };

    /// <summary>
    /// Opaque keyset cursor: base64 of "unixTicks:id" over the
    /// (purchased_at, id) sort key.
    /// </summary>
    public static class HoldingCursor
    {
        public readonly record struct Position(DateTimeOffset PurchasedAtUtc, long Id);

        public static string Encode(DateTimeOffset purchasedAtUtc, long id) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{purchasedAtUtc.UtcTicks}:{id}"));

        public static bool TryDecode(string cursor, out Position position)
        {
            position = default;
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                var separator = decoded.LastIndexOf(':');
                if (separator <= 0)
                {
                    return false;
                }

                if (!long.TryParse(decoded[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                    || !long.TryParse(decoded[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    return false;
                }

                position = new Position(new DateTimeOffset(ticks, TimeSpan.Zero), id);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ticks outside DateTimeOffset's range (a valid integer but
                // not a valid instant) must not 500 the list endpoint.
                return false;
            }
        }
    }
}

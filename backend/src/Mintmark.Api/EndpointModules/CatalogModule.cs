using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mintmark.Api;
using Mintmark.Application.Dtos;
using Mintmark.Application.Ports;
using Mintmark.Application.UseCases;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Public catalog browsing: hybrid search, coin-type detail (with presigned
/// reference image URLs), series and mint listings. Collection responses
/// carry an ETag derived from a catalog version stamp (counts + max ids) so
/// clients cache until the catalog changes.
/// </summary>
public sealed class CatalogModule : IEndpointModule
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("/api/v1/catalog").WithTags("Catalog");

        catalog.MapGet("/search", async (
            string? q,
            string? series,
            int? year,
            CatalogSearchService search,
            CancellationToken cancellationToken) =>
        {
            var candidates = await search.SearchAsync(
                new CoinSearchQuery(q, null, series, year, null, null, 5),
                cancellationToken);
            return Results.Ok(new CatalogSearchResponse([..
                candidates.Select(c => new CatalogCandidateResponse(c.CoinTypeId.Value, c.Score, c.DisplayName))]));
        });

        catalog.MapGet("/coin-types/{id:long}", async (
            long id,
            MintmarkDbContext dbContext,
            IImageStore imageStore,
            CancellationToken cancellationToken) =>
        {
            var row = await dbContext.CoinTypes
                .Where(c => c.Id == new CoinTypeId(id))
                .Join(dbContext.Series, c => c.SeriesId, s => s.Id, (c, s) => new { CoinType = c, Series = s })
                .Join(dbContext.Mints, x => x.CoinType.MintId, m => m.Id, (x, mint) => new { x.CoinType, x.Series, Mint = mint })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return ApiProblem.NotFound("CoinType not found.");
            }

            Uri? obverseUrl = null;
            Uri? reverseUrl = null;
            if (row.CoinType.ObverseImageKey is { } obverseKey)
            {
                obverseUrl = await imageStore.PresignGetAsync(obverseKey, TimeSpan.FromMinutes(15), cancellationToken);
            }

            if (row.CoinType.ReverseImageKey is { } reverseKey)
            {
                reverseUrl = await imageStore.PresignGetAsync(reverseKey, TimeSpan.FromMinutes(15), cancellationToken);
            }

            var detail = new CoinTypeDetail(
                row.CoinType.Id,
                row.CoinType.SeriesId,
                row.Series.Name,
                row.CoinType.MintId,
                row.Mint.Name,
                row.CoinType.Year,
                row.CoinType.Name,
                row.Series.Metal,
                row.CoinType.Fineness,
                row.CoinType.GrossWeightGrams,
                row.CoinType.ActualMetalWeightTroyOz,
                row.CoinType.DiameterMillimeters,
                row.CoinType.ThicknessMillimeters,
                row.CoinType.Edge,
                row.CoinType.Finish,
                row.CoinType.FinishAttributes,
                row.CoinType.Mintage,
                row.CoinType.SourceUrl,
                row.CoinType.KmNumber,
                row.CoinType.RedBookReference);

            return Results.Ok(new CoinTypeDetailResponse(detail, obverseUrl?.ToString(), reverseUrl?.ToString()));
        });

        catalog.MapGet("/series", async (MintmarkDbContext dbContext, HttpContext http, CancellationToken cancellationToken) =>
        {
            var stamp = await VersionStampAsync(dbContext, cancellationToken);
            var etag = $"\"{stamp}\"";
            if (http.Request.Headers.IfNoneMatch.ToString() == etag)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var rows = await dbContext.Series
                .Join(dbContext.Mints, s => s.MintId, m => m.Id, (s, m) => new { Series = s, Mint = m })
                .GroupJoin(dbContext.CoinTypes, sm => sm.Series.Id, c => c.SeriesId, (sm, types) => new { sm.Series, sm.Mint, Count = types.Count() })
                .OrderBy(x => x.Series.Name)
                .ToListAsync(cancellationToken);

            var summaries = rows.Select(x => new SeriesSummary(
                x.Series.Id, x.Series.Name, x.Mint.Name, x.Series.Metal, x.Series.StartYear, x.Series.EndYear, x.Count));

            http.Response.Headers.ETag = etag;
            return Results.Ok(summaries);
        });

        catalog.MapGet("/mints", async (MintmarkDbContext dbContext, HttpContext http, CancellationToken cancellationToken) =>
        {
            var stamp = await VersionStampAsync(dbContext, cancellationToken);
            var etag = $"\"{stamp}\"";
            if (http.Request.Headers.IfNoneMatch.ToString() == etag)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var mints = await dbContext.Mints
                .OrderBy(m => m.Name)
                .ToListAsync(cancellationToken);

            var summaries = mints.Select(m => new MintSummary(
                m.Id, m.Name, m.Country, m.CountryCode, m.MintMarks, m.FoundedYear, m.IsActive));

            http.Response.Headers.ETag = etag;
            return Results.Ok(summaries);
        });
    }

    /// <summary>
    /// Catalog version stamp: row counts plus the highest catalog id, hashed.
    /// Cheap (one scalar round-trip) and changes whenever the catalog does.
    /// </summary>
    private static async Task<string> VersionStampAsync(MintmarkDbContext dbContext, CancellationToken cancellationToken)
    {
        var stamp = await dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT (SELECT COUNT(*) FROM mints)
                    || '-' || (SELECT COUNT(*) FROM series)
                    || '-' || (SELECT COUNT(*) FROM coin_types)
                    || '-' || COALESCE((SELECT MAX(id) FROM coin_types), 0)
                    || '-' || COALESCE((SELECT MAX(id) FROM reference_images), 0)
                    || '-' || COALESCE((SELECT MAX(id) FROM mints), 0)
                    || '-' || COALESCE((SELECT MAX(id) FROM series), 0) AS "Value"
                """)
            .FirstAsync(cancellationToken);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stamp));
        return Convert.ToHexStringLower(hash)[..16];
    }
}

/// <summary>Search result row.</summary>
/// <param name="CoinTypeId">Matched catalog row.</param>
/// <param name="Score">Blended hybrid-search score.</param>
/// <param name="DisplayName">Catalog display name.</param>
public sealed record CatalogCandidateResponse(long CoinTypeId, decimal Score, string DisplayName);

/// <summary>Search result envelope.</summary>
/// <param name="Candidates">Top matches, best first.</param>
public sealed record CatalogSearchResponse(IReadOnlyList<CatalogCandidateResponse> Candidates);

/// <summary>Coin-type detail plus presigned reference image URLs (15-minute TTLs).</summary>
/// <param name="Detail">The catalog detail.</param>
/// <param name="ObverseImageUrl">Presigned obverse reference URL, when seeded.</param>
/// <param name="ReverseImageUrl">Presigned reverse reference URL, when seeded.</param>
public sealed record CoinTypeDetailResponse(CoinTypeDetail Detail, string? ObverseImageUrl, string? ReverseImageUrl);
using Microsoft.EntityFrameworkCore;
using Mintmark.Api;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Identification;
using Mintmark.Infrastructure.Storage;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Image upload flow (ADR 0006): a presigned PUT after server-side content
/// validation, then a register call that pulls the uploaded bytes back,
/// re-encodes them (strip/resize/JPEG), computes the perceptual hash and
/// creates the CoinImage row.
/// </summary>
public sealed class ImagesModule : IEndpointModule
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg", "image/png", "image/webp",
    ];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/images")
            .WithTags("Images")
            .RequireAuthorization();

        group.MapPost("/presign", async (PresignUploadRequest request, IUploadUrlFactory uploads, HttpContext http) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                errors[nameof(request.ContentType)] = [$"ContentType must be one of: {string.Join(", ", AllowedContentTypes)}."];
            }

            if (request.ContentLengthBytes is < 10 * 1024 or > 15 * 1024 * 1024)
            {
                errors[nameof(request.ContentLengthBytes)] = ["ContentLengthBytes must be between 10 KB and 15 MB."];
            }

            if (errors.Count > 0)
            {
                return ApiProblem.Validation(errors);
            }

            var key = $"uploads/{http.RequireUserId().Value}/{Guid.NewGuid():N}";
            var url = await uploads.PresignPutAsync(key, request.ContentType, TimeSpan.FromMinutes(15));
            return Results.Ok(new PresignUploadResponse(key, url.ToString(), DateTimeOffset.UtcNow.AddMinutes(15)));
        });

        group.MapPost("/", async (
            RegisterImageRequest request,
            MintmarkDbContext dbContext,
            MinioImageStore store,
            HttpContext http) =>
        {
            var userId = http.RequireUserId();

            // The object store is one shared bucket: the upload key must be
            // one this user's presign call minted (uploads/{userId}/…), or a
            // hostile client could read, re-encode, or delete any other
            // object — including other users' photos or the catalog corpus.
            var expectedPrefix = $"uploads/{userId.Value}/";
            if (!request.UploadKey.StartsWith(expectedPrefix, StringComparison.Ordinal)
                || request.UploadKey.Contains("..", StringComparison.Ordinal))
            {
                return ApiProblem.Unprocessable("UploadKey does not belong to this account.");
            }

            // Scoped by the global filter: another user's holding 404s.
            var holdingExists = await dbContext.Holdings
                .AnyAsync(h => h.Id == new HoldingId(request.HoldingId), http.RequestAborted);
            if (!holdingExists)
            {
                return ApiProblem.NotFound("Holding not found.");
            }

            if (!Enum.TryParse<CoinSide>(request.Side, ignoreCase: true, out var side))
            {
                return ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.Side)] = [$"Side must be one of: {string.Join(", ", Enum.GetNames<CoinSide>())}."],
                });
            }

            byte[] bytes;
            try
            {
                bytes = await store.GetObjectAsync(request.UploadKey, http.RequestAborted);
            }
            catch (Amazon.S3.AmazonS3Exception)
            {
                return ApiProblem.NotFound("Uploaded object not found; complete the presigned PUT first.");
            }

            if (bytes.Length is < 1 or > 15 * 1024 * 1024)
            {
                return ApiProblem.Unprocessable("Uploaded bytes are empty or exceed 15 MB.");
            }

            // Server-side re-encode + perceptual hash, then store under the
            // final holding-scoped key. Non-image bytes fail decoding here —
            // the presigned PUT cannot validate content, so this is the gate.
            byte[] canonical;
            try
            {
                canonical = ImagePreprocessor.Preprocess(bytes);
            }
            catch (SixLabors.ImageSharp.UnknownImageFormatException)
            {
                return ApiProblem.Unprocessable("Uploaded bytes are not a decodable image (JPEG, PNG, or WebP).");
            }

            var finalKey = $"holdings/{request.HoldingId}/{side.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}.jpg";
            await store.SaveAsync(finalKey, bytes, "image/jpeg", http.RequestAborted);
            await store.DeleteObjectAsync(request.UploadKey, http.RequestAborted);

            var hash = await new PerceptualHasher().HashAsync(canonical, http.RequestAborted);

            var image = CoinImage.Create(
                new HoldingId(request.HoldingId),
                side,
                finalKey,
                hash,
                capturedAtUtc: null,
                contentType: "image/jpeg",
                notes: request.Notes);

            dbContext.CoinImages.Add(image);
            await dbContext.SaveChangesAsync(http.RequestAborted);
            return Results.Created($"/api/v1/images/{image.Id.Value}", new RegisterImageResponse(image.Id.Value, finalKey));
        });
    }
}

/// <summary>Presigned-upload request.</summary>
/// <param name="ContentType">Declared content type (image/jpeg, image/png, image/webp).</param>
/// <param name="ContentLengthBytes">Declared byte length.</param>
public sealed record PresignUploadRequest(string ContentType, long ContentLengthBytes);

/// <summary>Presigned-upload response.</summary>
/// <param name="Key">Storage key to PUT to.</param>
/// <param name="UploadUrl">The presigned PUT URL (15 minutes).</param>
/// <param name="ExpiresAtUtc">When the URL expires.</param>
public sealed record PresignUploadResponse(string Key, string UploadUrl, DateTimeOffset ExpiresAtUtc);

/// <summary>Image registration request after a completed upload.</summary>
/// <param name="HoldingId">The owning holding.</param>
/// <param name="Side">Which side the photo shows.</param>
/// <param name="UploadKey">The key returned by the presign call.</param>
/// <param name="Notes">Optional notes.</param>
public sealed record RegisterImageRequest(long HoldingId, string Side, string UploadKey, string? Notes = null);

/// <summary>Image registration response.</summary>
/// <param name="ImageId">The created CoinImage id.</param>
/// <param name="StorageKey">Final (re-encoded) storage key.</param>
public sealed record RegisterImageResponse(long ImageId, string StorageKey);

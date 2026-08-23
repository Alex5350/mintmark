using FluentValidation;
using Mintmark.Api;
using Mintmark.Application.Dtos;
using Mintmark.Application.UseCases;
using Mintmark.Application.Validators;
using Mintmark.Domain;
using Mintmark.Application.Ports;

namespace Mintmark.Api.EndpointModules;

/// <summary>
/// Identification pipeline: multipart submit (obverse + reverse required,
/// optional edge) → validate → store → dedupe/identify via the
/// IdentificationService → job id; status polling; confirm.
/// </summary>
public sealed class IdentificationModule : IEndpointModule
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identification")
            .WithTags("Identification")
            .RequireAuthorization()
            .RequireRateLimiting("identification");

        group.MapPost("/submit", async (
            IFormFileCollection files,
            IValidator<SubmitIdentificationRequest> validator,
            IdentificationService identification,
            IImageStore imageStore,
            HttpContext http) =>
        {
            IFormFile? Pick(string name) => files.FirstOrDefault(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            var obverse = Pick("obverse");
            var reverse = Pick("reverse");
            var edge = Pick("edge");

            if (obverse is null || reverse is null)
            {
                return ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["obverseImage"] = obverse is null ? ["An obverse (front) photo is required."] : [],
                    ["reverseImage"] = reverse is null ? ["A reverse (back) photo is required."] : [],
                });
            }

            byte[] Read(IFormFile file)
            {
                using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }

            var request = new SubmitIdentificationRequest(
                Read(obverse),
                Read(reverse),
                edge is null ? null : Read(edge));

            var validation = await validator.ValidateAsync(request);
            if (!validation.IsValid)
            {
                return ApiProblem.Validation(validation);
            }

            var userId = http.RequireUserId();

            // Persist the (re-encoded) inputs under the run's namespace
            // before the pipeline consumes the bytes.
            var bundle = Guid.NewGuid().ToString("N");
            await imageStore.SaveAsync($"identifications/{userId.Value}/{bundle}/obverse.jpg", request.ObverseImage!, "image/jpeg", http.RequestAborted);
            await imageStore.SaveAsync($"identifications/{userId.Value}/{bundle}/reverse.jpg", request.ReverseImage!, "image/jpeg", http.RequestAborted);
            if (request.EdgeImage is not null)
            {
                await imageStore.SaveAsync($"identifications/{userId.Value}/{bundle}/edge.jpg", request.EdgeImage, "image/jpeg", http.RequestAborted);
            }

            var result = await identification.SubmitAsync(userId, request, http.RequestAborted);
            return Results.Accepted($"/api/v1/identification/{result.JobId.Value}/status", result);
        }).DisableAntiforgery();

        group.MapGet("/{jobId:long}/status", async (
            long jobId,
            IdentificationService identification,
            IIdentificationRunStore store,
            HttpContext http) =>
        {
            var runId = new IdentificationRunId(jobId);
            var run = await store.FindAsync(runId, http.RequestAborted);
            if (run is null || run.UserId != http.RequireUserId())
            {
                return ApiProblem.NotFound("Identification run not found.");
            }

            var status = await identification.GetStatusAsync(runId, http.RequestAborted);
            return Results.Ok(status);
        });

        group.MapPost("/{jobId:long}/confirm", async (
            long jobId,
            ConfirmIdentificationRequest request,
            IdentificationService identification,
            IIdentificationRunStore store,
            HttpContext http) =>
        {
            var runId = new IdentificationRunId(jobId);
            var run = await store.FindAsync(runId, http.RequestAborted);
            if (run is null || run.UserId != http.RequireUserId())
            {
                return ApiProblem.NotFound("Identification run not found.");
            }

            try
            {
                var userId = http.RequireUserId();
                await identification.ConfirmAsync(
                    runId,
                    request with { CorrectedBy = request.CorrectedBy ?? userId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    http.RequestAborted);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblem.Conflict(ex.Message);
            }
        });
    }
}

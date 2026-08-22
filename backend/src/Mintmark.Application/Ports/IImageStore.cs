namespace Mintmark.Application.Ports;

/// <summary>
/// Port to object storage for coin images. Implemented by Infrastructure
/// (S3-compatible). Keys are opaque; TTL presigned GETs serve photos to clients.
/// </summary>
public interface IImageStore
{
    /// <summary>Saves image bytes under a storage key.</summary>
    Task SaveAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Creates a time-limited presigned GET URI for a stored key.</summary>
    Task<Uri> PresignGetAsync(string key, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}

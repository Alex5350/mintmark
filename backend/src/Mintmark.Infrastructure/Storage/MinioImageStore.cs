using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Mintmark.Application.Ports;

namespace Mintmark.Infrastructure.Storage;

/// <summary>Presigned PUT URL minting (upload flow of ADR 0006).</summary>
public interface IUploadUrlFactory
{
    /// <summary>Creates a time-limited presigned PUT URI for a storage key.</summary>
    Task<Uri> PresignPutAsync(string key, string contentType, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}

/// <summary>Raw object access used by the upload/register pipeline.</summary>
public interface IObjectStorage
{
    /// <summary>Fetches stored object bytes.</summary>
    Task<byte[]> GetObjectAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored object.</summary>
    Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// S3-compatible image store (AWSSDK.S3 v4, path-style) against MinIO locally
/// and any S3-compatible endpoint in production (ADR 0006). Uploads are
/// server-side re-encoded (see <see cref="ImageReencoder"/>); reads go
/// through short-lived presigned GETs — the bucket is never public.
/// </summary>
public sealed class MinioImageStore : IImageStore, IUploadUrlFactory, IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    /// <summary>Initializes the store from storage options.</summary>
    public MinioImageStore(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _bucket = options.Bucket;
        var config = new AmazonS3Config
        {
            ServiceURL = $"http{(options.UseSsl ? "s" : string.Empty)}://{options.Endpoint}",
            ForcePathStyle = true,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(bytes);

        // Server-side re-encode: auto-orient, strip metadata (EXIF/GPS dies
        // in the decode/encode round-trip), cap 1600px, JPEG q82.
        byte[] encoded;
        using (var input = new MemoryStream(bytes))
        {
            encoded = ImageReencoder.Reencode(input);
        }

        using var upload = new MemoryStream(encoded);
        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = upload,
                ContentType = "image/jpeg",
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Uri> PresignGetAsync(string key, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return PresignAsync(key, HttpVerb.GET, contentType: null, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Uri> PresignPutAsync(string key, string contentType, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return PresignAsync(key, HttpVerb.PUT, contentType, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> GetObjectAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(
            new GetObjectRequest { BucketName = _bucket, Key = key },
            cancellationToken);

        using var stream = response.ResponseStream;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <inheritdoc />
    public Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = _bucket, Key = key },
            cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private async Task<Uri> PresignAsync(string key, HttpVerb verb, string? contentType, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        var url = await _client.GetPreSignedURLAsync(
            new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = key,
                Verb = verb,
                ContentType = contentType,
                Expires = DateTime.UtcNow.Add(timeToLive),
            });

        return new Uri(url);
    }
}

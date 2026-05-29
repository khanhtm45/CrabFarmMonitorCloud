using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace CrabFarmMonitor.Cloud.Services;

/// <summary>S3-compatible storage (MinIO, AWS S3, DigitalOcean Spaces).</summary>
public sealed class ObjectStorageService : IDisposable
{
    private readonly IAmazonS3? _client;
    private readonly string? _bucket;
    private readonly bool _enabled;

    public bool Enabled => _enabled;

    public ObjectStorageService(IConfiguration config)
    {
        var endpoint = config["S3_ENDPOINT"]?.Trim();
        _bucket = config["S3_BUCKET"]?.Trim();
        var accessKey = config["S3_ACCESS_KEY"]?.Trim();
        var secretKey = config["S3_SECRET_KEY"]?.Trim();

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(_bucket)
            || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            _enabled = false;
            return;
        }

        var useSsl = config.GetValue("S3_USE_SSL", false);
        var cfg = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            UseHttp = !useSsl,
            AuthenticationRegion = config["S3_REGION"] ?? "us-east-1"
        };
        _client = new AmazonS3Client(accessKey, secretKey, cfg);
        _enabled = true;
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        if (!_enabled || _client == null || _bucket == null) return;
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct);
            Console.WriteLine($"S3: created bucket {_bucket}");
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
            // bucket exists
        }
    }

    public async Task<string> UploadAsync(
        string objectKey,
        byte[] data,
        string contentType = "application/x-hdf5",
        CancellationToken ct = default)
    {
        if (!_enabled || _client == null || _bucket == null)
            throw new InvalidOperationException("S3 not configured");

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = new MemoryStream(data),
            ContentType = contentType
        }, ct);

        return $"s3://{_bucket}/{objectKey}";
    }

    public async Task<byte[]?> DownloadByStoragePathAsync(string storagePath, CancellationToken ct = default)
    {
        if (!_enabled || _client == null || _bucket == null)
            return null;
        if (!storagePath.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            return null;

        var rest = storagePath["s3://".Length..];
        var slash = rest.IndexOf('/');
        if (slash < 0) return null;

        var bucket = rest[..slash];
        var key = rest[(slash + 1)..];
        if (!string.Equals(bucket, _bucket, StringComparison.Ordinal))
            return null;

        using var resp = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key
        }, ct);
        using var ms = new MemoryStream();
        await resp.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public object GetStatus() => new
    {
        enabled = _enabled,
        bucket = _bucket,
        endpoint = _enabled ? "configured" : null
    };

    public void Dispose() => _client?.Dispose();
}

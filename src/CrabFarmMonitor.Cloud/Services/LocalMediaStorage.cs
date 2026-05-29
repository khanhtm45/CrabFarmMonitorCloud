namespace CrabFarmMonitor.Cloud.Services;

/// <summary>Lưu ảnh local khi S3 chưa cấu hình (dev / edge upload).</summary>
public sealed class LocalMediaStorage
{
    private readonly string _root;

    public LocalMediaStorage(IConfiguration config)
    {
        _root = config["UPLOAD_DIR"]?.Trim()
            ?? Path.Combine(config["DATA_DIR"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data"), "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string relativeKey, Stream content, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_root, relativeKey.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, ct);
        return $"local://{relativeKey.Replace('\\', '/')}";
    }

    public string? ResolvePhysicalPath(string storageUrl)
    {
        if (!storageUrl.StartsWith("local://", StringComparison.OrdinalIgnoreCase))
            return null;
        var rel = storageUrl["local://".Length..];
        return Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}

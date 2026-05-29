using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Shared;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class Hdf5CloudBrowseService
{
    private readonly RasCloudDbContext _db;
    private readonly ObjectStorageService _s3;
    private readonly CloudPythonScriptRunner _python;
    private readonly string _hdf5Root;

    public Hdf5CloudBrowseService(
        RasCloudDbContext db,
        ObjectStorageService s3,
        CloudPythonScriptRunner python,
        IConfiguration config)
    {
        _db = db;
        _s3 = s3;
        _python = python;
        _hdf5Root = config["DATA_DIR"] ?? "/data";
    }

    public async Task<object> ListUploadsAsync(IReadOnlyList<Guid> scopeFarmIds, Guid? farmId, int limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit, 1, 200);
        var q = _db.Hdf5Uploads.AsNoTracking().Where(u => scopeFarmIds.Contains(u.FarmId));
        if (farmId.HasValue)
            q = q.Where(u => u.FarmId == farmId.Value);

        var rows = await q
            .OrderByDescending(u => u.ReceivedAt)
            .Take(take)
            .Select(u => new
            {
                u.Id,
                u.FarmId,
                u.DeviceCode,
                fileName = FileNameFromStoragePath(u.StoragePath),
                u.StoragePath,
                u.SizeBytes,
                u.ChecksumSha256,
                u.ChunkStartMs,
                u.ChunkEndMs,
                u.Status,
                u.ReceivedAt
            })
            .ToListAsync(ct);

        return new { ok = true, count = rows.Count, data = rows };
    }

    public async Task<object?> ReadRowsAsync(
        Guid uploadId,
        IReadOnlyList<Guid> scopeFarmIds,
        Hdf5ReadQuery query,
        CancellationToken ct)
    {
        if (!_python.Available)
            return new { ok = false, error = "HDF5 reader not available (install python3-h5py in cloud-api)" };

        var upload = await _db.Hdf5Uploads.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == uploadId && scopeFarmIds.Contains(u.FarmId), ct);
        if (upload == null)
            return null;

        var bytes = await LoadFileBytesAsync(upload.StoragePath, ct);
        if (bytes == null || bytes.Length == 0)
            return new { ok = false, error = "file not found in storage" };

        var fileName = FileNameFromStoragePath(upload.StoragePath);
        var workDir = Path.Combine(Path.GetTempPath(), "ras-hdf5", uploadId.ToString("N"));
        Directory.CreateDirectory(workDir);
        var localPath = Path.Combine(workDir, fileName);

        try
        {
            await File.WriteAllBytesAsync(localPath, bytes, ct);
            var env = new Dictionary<string, string> { ["HDF5_DIR"] = workDir };
            using var doc = await _python.RunJsonScriptAsync(
                "hdf5_read.py",
                query.ToScriptPayload(fileName),
                env,
                ct);

            if (doc == null)
                return new { ok = false, error = "hdf5_read failed" };

            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okProp) && !okProp.GetBoolean())
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "read error";
                return new { ok = false, error = err };
            }

            return JsonSerializer.Deserialize<object>(root.GetRawText());
        }
        finally
        {
            try
            {
                if (File.Exists(localPath)) File.Delete(localPath);
                if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true);
            }
            catch { /* best effort */ }
        }
    }

    private async Task<byte[]?> LoadFileBytesAsync(string storagePath, CancellationToken ct)
    {
        if (storagePath.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            return await _s3.DownloadByStoragePathAsync(storagePath, ct);

        if (File.Exists(storagePath))
            return await File.ReadAllBytesAsync(storagePath, ct);

        var rel = storagePath.Replace('\\', '/');
        if (rel.StartsWith("/data/", StringComparison.Ordinal))
            return File.Exists(rel) ? await File.ReadAllBytesAsync(rel, ct) : null;

        var underRoot = Path.Combine(_hdf5Root, rel.TrimStart('/'));
        return File.Exists(underRoot) ? await File.ReadAllBytesAsync(underRoot, ct) : null;
    }

    private static string FileNameFromStoragePath(string storagePath)
    {
        var path = storagePath;
        if (path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var idx = path.LastIndexOf('/');
            return idx >= 0 ? path[(idx + 1)..] : path;
        }
        return Path.GetFileName(path);
    }
}

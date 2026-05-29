using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class Hdf5SyncService
{
    private readonly RasCloudDbContext _db;
    private readonly ObjectStorageService _s3;
    private readonly string _hdf5Root;
    private readonly IConfiguration _config;

    public Hdf5SyncService(RasCloudDbContext db, ObjectStorageService s3, IConfiguration config)
    {
        _db = db;
        _s3 = s3;
        _config = config;
        _hdf5Root = config["DATA_DIR"] ?? "/data";
        Directory.CreateDirectory(Path.Combine(_hdf5Root, "hdf5"));
    }

    public async Task<object> SaveUploadAsync(
        IFormFile file,
        Guid farmId,
        string? mac,
        string? deviceCode,
        string? idempotencyKey,
        long? chunkStartMs,
        long? chunkEndMs,
        CancellationToken ct)
    {
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrEmpty(key))
            key = $"upload-{Guid.NewGuid():N}";

        var existing = await _db.SyncJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.IdempotencyKey == key, ct);
        if (existing != null)
            return new { ok = true, duplicate = true, idempotencyKey = key };

        var originalName = Path.GetFileName(file.FileName);
        var normMac = MacNormalizer.Normalize(mac);
        var periodKey = Hdf5ArchiveKey.PeriodKeyFromFileName(originalName);

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var safeMac = normMac?.Replace(":", "") ?? "unknown";

        Device? device = null;
        if (!string.IsNullOrEmpty(normMac))
            device = await _db.Devices.FirstOrDefaultAsync(d => d.MacAddress == normMac, ct);

        if (device != null && chunkStartMs.HasValue)
        {
            var samePeriod = await _db.Hdf5Uploads.AsNoTracking()
                .Where(u => u.FarmId == farmId && u.DeviceId == device.Id && u.ChunkStartMs == chunkStartMs)
                .OrderByDescending(u => u.ReceivedAt)
                .FirstOrDefaultAsync(ct);
            if (samePeriod != null)
                return new { ok = true, duplicate = true, uploadId = samePeriod.Id, idempotencyKey = key, reason = "same_archive_period" };
        }

        string storagePath;
        string? s3Uri = null;

        if (_s3.Enabled)
        {
            var objectKey = !string.IsNullOrEmpty(periodKey)
                ? $"{farmId:N}/archive/{periodKey}.h5"
                : $"{farmId:N}/{safeMac}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{originalName}";
            s3Uri = await _s3.UploadAsync(objectKey, bytes, "application/x-hdf5", ct);
            storagePath = s3Uri;
        }
        else
        {
            var dir = Path.Combine(_hdf5Root, "hdf5", safeMac);
            Directory.CreateDirectory(dir);
            var fileName = !string.IsNullOrEmpty(periodKey)
                ? $"{periodKey.Replace('/', '_')}.h5"
                : $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{originalName}";
            var dest = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(dest, bytes, ct);
            storagePath = dest.Replace('\\', '/');
        }

        var upload = new Hdf5Upload
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            DeviceId = device?.Id,
            DeviceCode = deviceCode ?? device?.DeviceCode,
            StoragePath = storagePath,
            SizeBytes = bytes.Length,
            ChecksumSha256 = hash,
            ChunkStartMs = chunkStartMs,
            ChunkEndMs = chunkEndMs,
            Status = _s3.Enabled ? "indexed" : "uploaded",
            ReceivedAt = DateTime.UtcNow
        };
        _db.Hdf5Uploads.Add(upload);

        _db.SyncJobs.Add(new SyncJob
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Hdf5UploadId = upload.Id,
            IdempotencyKey = key,
            Status = "acked"
        });

        await _db.SaveChangesAsync(ct);

        return new
        {
            ok = true,
            uploadId = upload.Id,
            idempotencyKey = key,
            path = storagePath,
            s3 = s3Uri,
            size = upload.SizeBytes,
            checksum = hash,
            chunkStartMs,
            chunkEndMs
        };
    }
}

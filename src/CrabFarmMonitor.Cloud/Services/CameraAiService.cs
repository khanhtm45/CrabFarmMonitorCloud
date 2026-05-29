using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class CameraAiService
{
    private readonly RasCloudDbContext _db;
    private readonly ObjectStorageService _s3;
    private readonly LocalMediaStorage _local;

    public CameraAiService(RasCloudDbContext db, ObjectStorageService s3, LocalMediaStorage local)
    {
        _db = db;
        _s3 = s3;
        _local = local;
    }

    public async Task<List<CameraDevice>> ListCamerasAsync(Guid farmId, CancellationToken ct) =>
        await _db.CameraDevices.AsNoTracking()
            .Where(c => _db.Gateways.Any(g => g.Id == c.GatewayId && g.FarmId == farmId))
            .OrderBy(c => c.CameraCode)
            .ToListAsync(ct);

    public async Task<CameraDevice?> RegisterCameraAsync(
        Guid farmId,
        string gatewayCode,
        UpsertCameraRequest req,
        CancellationToken ct)
    {
        var gateway = await EnsureGatewayAsync(farmId, gatewayCode.Trim(), ct);

        var cam = await _db.CameraDevices
            .FirstOrDefaultAsync(c => c.GatewayId == gateway.Id && c.CameraCode == req.CameraCode.Trim(), ct);

        if (cam == null)
        {
            cam = new CameraDevice
            {
                Id = Guid.NewGuid(),
                GatewayId = gateway.Id,
                CameraCode = req.CameraCode.Trim(),
                Name = req.Name.Trim(),
                BoxId = req.BoxId,
                StreamUrl = req.StreamUrl,
                IpAddress = req.IpAddress,
                Status = "online",
                LastSeenAt = DateTime.UtcNow
            };
            _db.CameraDevices.Add(cam);
        }
        else
        {
            cam.Name = req.Name.Trim();
            cam.BoxId = req.BoxId ?? cam.BoxId;
            cam.StreamUrl = req.StreamUrl ?? cam.StreamUrl;
            cam.IpAddress = req.IpAddress ?? cam.IpAddress;
            cam.Status = "online";
            cam.LastSeenAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return cam;
    }

    public async Task<CameraSnapshot?> UploadSnapshotAsync(
        Guid farmId,
        string gatewayCode,
        string cameraCode,
        IFormFile file,
        CancellationToken ct)
    {
        var cam = await ResolveCameraAsync(farmId, gatewayCode, cameraCode, ct)
            ?? await RegisterCameraAsync(farmId, gatewayCode,
                new UpsertCameraRequest(cameraCode, cameraCode), ct);
        if (cam == null) return null;

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var key = $"camera/{farmId:N}/{cam.Id:N}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";

        string imageUrl;
        await using var stream = file.OpenReadStream();
        if (_s3.Enabled)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            imageUrl = await _s3.UploadAsync(key, ms.ToArray(), file.ContentType ?? "image/jpeg", ct);
        }
        else
        {
            imageUrl = await _local.SaveAsync(key, stream, ct);
        }

        var snap = new CameraSnapshot
        {
            Id = Guid.NewGuid(),
            CameraId = cam.Id,
            BoxId = cam.BoxId,
            ImageUrl = imageUrl,
            CapturedAt = DateTime.UtcNow,
            SyncStatus = "synced"
        };
        _db.CameraSnapshots.Add(snap);
        cam.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return snap;
    }

    public async Task<object?> SubmitAnalysisAsync(Guid farmId, SubmitAiAnalysisRequest req, CancellationToken ct)
    {
        var cam = await ResolveCameraAsync(farmId, req.GatewayId, req.CameraCode, ct)
            ?? await RegisterCameraAsync(farmId, req.GatewayId,
                new UpsertCameraRequest(req.CameraCode, req.CameraCode), ct);
        if (cam == null) return null;

        var model = await _db.AiModels
            .FirstOrDefaultAsync(m => m.ModelType == req.ModelType && m.Status == "active", ct);

        if (model == null)
        {
            model = new AiModel
            {
                Id = Guid.NewGuid(),
                ModelName = req.ModelType,
                ModelType = req.ModelType,
                Version = "1.0",
                Status = "active"
            };
            _db.AiModels.Add(model);
        }

        var result = new AiAnalysisResult
        {
            Id = Guid.NewGuid(),
            CameraId = cam.Id,
            BoxId = cam.BoxId,
            ModelId = model.Id,
            ResultType = req.ResultType,
            Confidence = req.Confidence,
            ResultDataJson = req.ResultData == null ? null : JsonSerializer.Serialize(req.ResultData),
            ImageUrl = req.ImageUrl,
            AnalyzedAt = req.AnalyzedAt?.ToUniversalTime() ?? DateTime.UtcNow
        };
        _db.AiAnalysisResults.Add(result);

        AiAlert? alert = null;
        if (req.Alert != null)
        {
            alert = new AiAlert
            {
                Id = Guid.NewGuid(),
                ResultId = result.Id,
                CameraId = cam.Id,
                BoxId = cam.BoxId,
                AlertType = req.Alert.AlertType,
                Severity = req.Alert.Severity,
                Message = req.Alert.Message,
                Status = "new",
                CreatedAt = DateTime.UtcNow
            };
            _db.AiAlerts.Add(alert);
        }

        await _db.SaveChangesAsync(ct);
        return new { result, alert };
    }

    public async Task<List<CameraSnapshot>> ListSnapshotsAsync(Guid cameraId, int limit, CancellationToken ct) =>
        await _db.CameraSnapshots.AsNoTracking()
            .Where(s => s.CameraId == cameraId)
            .OrderByDescending(s => s.CapturedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

    public async Task<List<AiAnalysisResult>> ListAnalysisAsync(Guid cameraId, int limit, CancellationToken ct) =>
        await _db.AiAnalysisResults.AsNoTracking()
            .Where(r => r.CameraId == cameraId)
            .OrderByDescending(r => r.AnalyzedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

    public async Task<List<AiAlert>> ListAiAlertsAsync(Guid farmId, int limit, CancellationToken ct) =>
        await _db.AiAlerts.AsNoTracking()
            .Where(a => _db.CameraDevices.Any(c =>
                c.Id == a.CameraId &&
                _db.Gateways.Any(g => g.Id == c.GatewayId && g.FarmId == farmId)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

    private async Task<Gateway> EnsureGatewayAsync(Guid farmId, string gatewayCode, CancellationToken ct)
    {
        var gateway = await _db.Gateways
            .FirstOrDefaultAsync(g => g.FarmId == farmId && g.GatewayCode == gatewayCode, ct);
        if (gateway != null) return gateway;

        gateway = new Gateway
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            GatewayCode = gatewayCode,
            Name = gatewayCode,
            Status = "online",
            LastSeenAt = DateTime.UtcNow
        };
        _db.Gateways.Add(gateway);
        await _db.SaveChangesAsync(ct);
        return gateway;
    }

    public async Task<CameraDevice?> ResolveCameraAsync(
        Guid farmId,
        string gatewayCode,
        string cameraCode,
        CancellationToken ct)
    {
        return await (from c in _db.CameraDevices
                      join g in _db.Gateways on c.GatewayId equals g.Id
                      where g.FarmId == farmId
                            && g.GatewayCode == gatewayCode.Trim()
                            && c.CameraCode == cameraCode.Trim()
                      select c).FirstOrDefaultAsync(ct);
    }
}

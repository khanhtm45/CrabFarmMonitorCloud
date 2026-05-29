using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;
using CrabFarmMonitor.Shared;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class TelemetryIngestService
{
    private readonly RasCloudDbContext _db;
    private readonly IConfiguration _config;

    public TelemetryIngestService(RasCloudDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Device> ResolveDeviceAsync(TelemetryPayload payload, Guid farmId, CancellationToken ct)
    {
        var mac = MacNormalizer.Normalize(payload.Mac)
            ?? throw new InvalidOperationException("mac required");

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.MacAddress == mac, ct);

        if (device != null)
        {
            device.LastTelemetryAt = DateTime.UtcNow;
            return device;
        }

        var code = _config["DEFAULT_DEVICE_CODE_PREFIX"] + mac.Replace(":", "").ToLowerInvariant();
        device = new Device
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            DeviceCode = code.Length > 64 ? code[..64] : code,
            MacAddress = mac,
            LastTelemetryAt = DateTime.UtcNow
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        return device;
    }

    private bool SamplesEnabled() =>
        !string.Equals(_config["TELEMETRY_SAMPLES_ENABLED"], "false", StringComparison.OrdinalIgnoreCase);

    public async Task UpsertLatestAsync(Device device, TelemetryPayload payload, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (SamplesEnabled())
        {
            foreach (var r in payload.Readings)
            {
                _db.TelemetrySamples.Add(new TelemetrySample
                {
                    FarmId = device.FarmId,
                    DeviceId = device.Id,
                    Pin = (short)r.Pin,
                    Val = r.Val,
                    RecordedAt = now,
                    ReceivedAt = now
                });
            }
        }

        foreach (var r in payload.Readings)
        {
            var row = await _db.TelemetryLatest
                .FirstOrDefaultAsync(t => t.DeviceId == device.Id && t.Pin == (short)r.Pin, ct);

            if (row == null)
            {
                _db.TelemetryLatest.Add(new TelemetryLatest
                {
                    FarmId = device.FarmId,
                    DeviceId = device.Id,
                    Pin = (short)r.Pin,
                    Val = r.Val,
                    RecordedAt = now,
                    ReceivedAt = now
                });
            }
            else
            {
                row.Val = r.Val;
                row.RecordedAt = now;
                row.ReceivedAt = now;
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

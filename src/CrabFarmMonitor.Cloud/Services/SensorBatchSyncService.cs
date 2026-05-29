using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;
using CrabFarmMonitor.Shared;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class SensorBatchSyncService
{
    private static readonly (Func<SensorRealtimeDto, double?> Getter, string SensorType, string Unit)[] MetricMap =
    [
        (d => d.Temperature, "temp", "C"),
        (d => d.Ph, "ph", "pH"),
        (d => d.DissolvedOxygen, "do", "mg/L"),
        (d => d.Salinity, "salinity", "ppt"),
        (d => d.Orp, "orp", "mV"),
        (d => d.Nh3, "nh3", "mg/L"),
        (d => d.No2, "no2", "mg/L"),
    ];

    private readonly RasCloudDbContext _db;

    public SensorBatchSyncService(RasCloudDbContext db) => _db = db;

    public async Task<object> IngestAsync(SensorBatchSyncDto batch, Guid farmId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(batch.GatewayId))
            throw new ArgumentException("gatewayId required");
        if (batch.Items == null || batch.Items.Count == 0)
            throw new ArgumentException("items required");

        var now = DateTime.UtcNow;
        var gateway = await _db.Gateways
            .FirstOrDefaultAsync(g => g.FarmId == farmId && g.GatewayCode == batch.GatewayId.Trim(), ct);

        if (gateway == null)
        {
            gateway = new Gateway
            {
                Id = Guid.NewGuid(),
                FarmId = farmId,
                GatewayCode = batch.GatewayId.Trim(),
                Name = batch.GatewayId.Trim(),
                Status = "online",
                LastSeenAt = now
            };
            _db.Gateways.Add(gateway);
        }
        else
        {
            gateway.Status = "online";
            gateway.LastSeenAt = now;
        }

        var inserted = 0;
        var devicesTouched = 0;

        foreach (var item in batch.Items)
        {
            if (string.IsNullOrWhiteSpace(item.DeviceCode)) continue;

            var device = await ResolveDeviceAsync(farmId, item.DeviceCode.Trim(), ct);
            devicesTouched++;

            Guid? boxId = null;
            if (!string.IsNullOrWhiteSpace(item.BoxCode))
                boxId = await ResolveBoxIdAsync(farmId, item.BoxCode.Trim(), ct);

            if (boxId.HasValue && device.BoxId != boxId)
            {
                device.BoxId = boxId;
                device.LastSeenAt = now;
            }

            var recordedAt = item.RecordedAt == default ? now : item.RecordedAt.ToUniversalTime();

            foreach (var (getter, sensorType, unit) in MetricMap)
            {
                var val = getter(item);
                if (!val.HasValue) continue;

                var sensor = await EnsureSensorAsync(device.Id, sensorType, unit, ct);
                var reading = new SensorReading
                {
                    Id = Guid.NewGuid(),
                    SensorId = sensor.Id,
                    BoxId = boxId ?? device.BoxId,
                    Value = val.Value,
                    Unit = unit,
                    RecordedAt = recordedAt,
                    SyncStatus = "synced"
                };
                _db.SensorReadings.Add(reading);
                inserted++;
            }
        }

        var correlationId = Guid.NewGuid();
        _db.SyncLogs.Add(new SyncLog
        {
            Id = Guid.NewGuid(),
            GatewayId = gateway.Id,
            TableName = "sensor_readings",
            RecordId = correlationId,
            Action = "create",
            SyncStatus = "success",
            CreatedAt = now,
            SyncedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return new
        {
            gatewayId = gateway.Id,
            gatewayCode = gateway.GatewayCode,
            readingsInserted = inserted,
            devicesTouched,
            batchCorrelationId = correlationId
        };
    }

    private async Task<Device> ResolveDeviceAsync(Guid farmId, string deviceCode, CancellationToken ct)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.FarmId == farmId && d.DeviceCode == deviceCode, ct);

        if (device != null)
        {
            device.Status = "online";
            device.LastSeenAt = DateTime.UtcNow;
            return device;
        }

        device = new Device
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            DeviceCode = deviceCode,
            DeviceName = deviceCode,
            Status = "online",
            LastSeenAt = DateTime.UtcNow
        };
        _db.Devices.Add(device);
        return device;
    }

    private async Task<Guid?> ResolveBoxIdAsync(Guid farmId, string boxCode, CancellationToken ct) =>
        await (from b in _db.Boxes.AsNoTracking()
               join r in _db.Rows on b.RowId equals r.Id
               join a in _db.Areas on r.AreaId equals a.Id
               where b.BoxCode == boxCode && a.FarmId == farmId
               select (Guid?)b.Id).FirstOrDefaultAsync(ct);

    private async Task<Sensor> EnsureSensorAsync(Guid deviceId, string sensorType, string unit, CancellationToken ct)
    {
        var sensor = await _db.Sensors
            .FirstOrDefaultAsync(s => s.DeviceId == deviceId && s.SensorType == sensorType, ct);
        if (sensor != null) return sensor;

        sensor = new Sensor
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            SensorType = sensorType,
            Unit = unit,
            Status = true
        };
        _db.Sensors.Add(sensor);
        return sensor;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class DeviceShadowService
{
    private readonly RasCloudDbContext _db;

    public DeviceShadowService(RasCloudDbContext db) => _db = db;

    public async Task<object?> GetByMacAsync(string mac, IReadOnlyList<Guid> scopeFarmIds, CancellationToken ct)
    {
        var norm = MacNormalizer.Normalize(mac);
        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => scopeFarmIds.Contains(d.FarmId) && d.MacAddress == norm, ct);
        if (device == null) return null;

        var shadow = await _db.DeviceShadows.AsNoTracking()
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, ct);

        return new
        {
            deviceId = device.Id,
            mac = device.MacAddress,
            deviceCode = device.DeviceCode,
            desired = ParseJson(shadow?.DesiredJson),
            reported = ParseJson(shadow?.ReportedJson),
            updatedAt = shadow?.UpdatedAt
        };
    }

    public async Task<object?> PutDesiredByMacAsync(
        string mac,
        JsonElement desired,
        IReadOnlyList<Guid> scopeFarmIds,
        CancellationToken ct)
    {
        var norm = MacNormalizer.Normalize(mac);
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => scopeFarmIds.Contains(d.FarmId) && d.MacAddress == norm, ct);
        if (device == null) return null;

        var row = await _db.DeviceShadows.FirstOrDefaultAsync(s => s.DeviceId == device.Id, ct);
        var json = desired.GetRawText();
        if (row == null)
        {
            row = new Data.Entities.DeviceShadow
            {
                DeviceId = device.Id,
                DesiredJson = json,
                UpdatedAt = DateTime.UtcNow
            };
            _db.DeviceShadows.Add(row);
        }
        else
        {
            row.DesiredJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await GetByMacAsync(mac, scopeFarmIds, ct);
    }

    private static object? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return json; }
    }
}

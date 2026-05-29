using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class WaterAlertService
{
    private readonly RasCloudDbContext _db;

    public WaterAlertService(RasCloudDbContext db) => _db = db;

    public async Task<List<object>> GetAlertsAsync(Guid farmId, CancellationToken ct)
    {
        var alerts = new List<object>();
        var latest = await _db.TelemetryLatest.AsNoTracking()
            .Where(t => t.FarmId == farmId)
            .ToListAsync(ct);

        double? ph = latest.FirstOrDefault(t => t.Pin == 2)?.Val;
        double? water = latest.FirstOrDefault(t => t.Pin == 5)?.Val;
        double? temp = latest.FirstOrDefault(t => t.Pin == 1)?.Val;
        double? tds = latest.FirstOrDefault(t => t.Pin == 3)?.Val;

        if (ph.HasValue && (ph < 7.0 || ph > 9.0))
            alerts.Add(new { severity = "high", code = "ph_out_of_range", message = $"pH bất thường: {ph:F2}", value = ph });
        else if (ph.HasValue && (ph < 7.5 || ph > 8.5))
            alerts.Add(new { severity = "medium", code = "ph_watch", message = $"pH cần theo dõi: {ph:F2}", value = ph });

        if (water.HasValue && water < 1)
            alerts.Add(new { severity = "high", code = "water_low", message = "Mực nước / bể bù: CẠN", value = water });

        if (temp.HasValue && (temp < 24 || temp > 32))
            alerts.Add(new { severity = "medium", code = "temp_watch", message = $"Nhiệt nước: {temp:F1}°C", value = temp });

        if (tds.HasValue && tds > 800)
            alerts.Add(new { severity = "medium", code = "tds_high", message = $"TDS cao: {tds:F0} ppm", value = tds });

        var offlineDevices = await _db.Devices.AsNoTracking()
            .Where(d => d.FarmId == farmId && (d.LastTelemetryAt == null || d.LastTelemetryAt < DateTime.UtcNow.AddMinutes(-10)))
            .Select(d => new { d.DeviceCode, d.LastTelemetryAt })
            .ToListAsync(ct);

        foreach (var d in offlineDevices)
            alerts.Add(new
            {
                severity = "high",
                code = "device_offline",
                message = $"Thiết bị offline: {d.DeviceCode}",
                lastSeen = d.LastTelemetryAt
            });

        var sickCrabs = await _db.CrabIndividuals.AsNoTracking()
            .CountAsync(c => c.FarmId == farmId && (c.Status == "sick" || c.Status == "watch"), ct);
        if (sickCrabs > 0)
            alerts.Add(new { severity = "medium", code = "crab_health", message = $"{sickCrabs} con cua cần theo dõi/bệnh", count = sickCrabs });

        return alerts;
    }
}

using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Shared;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class TelemetryHistoryService
{
    private readonly RasCloudDbContext _db;

    public TelemetryHistoryService(RasCloudDbContext db) => _db = db;

    public async Task<object?> QueryAsync(
        string mac,
        int minutes,
        int? pin,
        IReadOnlyList<Guid> scopeFarmIds,
        CancellationToken ct)
    {
        var norm = MacNormalizer.Normalize(mac);
        if (norm == null) return null;

        var device = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.MacAddress == norm && scopeFarmIds.Contains(d.FarmId), ct);
        if (device == null) return null;

        minutes = Math.Clamp(minutes, 1, 60 * 24 * 14);
        var from = DateTime.UtcNow.AddMinutes(-minutes);

        var q = _db.TelemetrySamples.AsNoTracking()
            .Where(s => s.DeviceId == device.Id && s.RecordedAt >= from);
        if (pin.HasValue)
            q = q.Where(s => s.Pin == (short)pin.Value);

        // ESP ~1–2s × nhiều pin → >15k rows/30 phút; lấy **mới nhất** rồi sort tăng dần cho chart
        var rows = await q
            .OrderByDescending(s => s.RecordedAt)
            .Take(15000)
            .OrderBy(s => s.RecordedAt)
            .Select(s => new { s.Pin, s.Val, s.RecordedAt })
            .ToListAsync(ct);

        var data = rows.Select(r => new
        {
            time = r.RecordedAt,
            pin = (int)r.Pin,
            val = r.Val,
            label = PinLabels.Label(r.Pin)
        }).ToList();

        return new
        {
            ok = true,
            mac = norm,
            minutes,
            pin,
            count = data.Count,
            source = "cloud_pg",
            data
        };
    }
}

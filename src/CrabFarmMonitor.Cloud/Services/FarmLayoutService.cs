using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class FarmLayoutService
{
    private readonly RasCloudDbContext _db;

    public FarmLayoutService(RasCloudDbContext db) => _db = db;

    public async Task<List<Area>> ListAreasAsync(Guid farmId, CancellationToken ct) =>
        await _db.Areas.AsNoTracking()
            .Where(a => a.FarmId == farmId)
            .OrderBy(a => a.AreaCode)
            .ToListAsync(ct);

    public async Task<List<AreaListItem>> ListAreasWithStatsAsync(Guid farmId, CancellationToken ct)
    {
        var areas = await ListAreasAsync(farmId, ct);
        if (areas.Count == 0) return [];

        var areaIds = areas.Select(a => a.Id).ToList();

        var rowCounts = await _db.Rows.AsNoTracking()
            .Where(r => areaIds.Contains(r.AreaId))
            .GroupBy(r => r.AreaId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var boxCounts = await (
                from b in _db.Boxes.AsNoTracking()
                join r in _db.Rows on b.RowId equals r.Id
                where areaIds.Contains(r.AreaId)
                group b by r.AreaId
                into g
                select new { AreaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AreaId, x => x.Count, ct);

        return areas.Select(a => new AreaListItem(
            a.Id,
            a.FarmId,
            a.AreaCode,
            a.AreaName,
            a.Description,
            NormalizeAreaStatus(a.Status),
            a.CreatedAt,
            rowCounts.GetValueOrDefault(a.Id),
            boxCounts.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<AreaDetailStats?> GetAreaDetailStatsAsync(Guid areaId, CancellationToken ct)
    {
        var area = await GetAreaAsync(areaId, ct);
        if (area == null) return null;

        var rowCount = await _db.Rows.AsNoTracking().CountAsync(r => r.AreaId == areaId, ct);
        var boxIds = await (
            from b in _db.Boxes.AsNoTracking()
            join r in _db.Rows on b.RowId equals r.Id
            where r.AreaId == areaId
            select b.Id).ToListAsync(ct);

        var esp32Count = boxIds.Count == 0
            ? 0
            : await _db.Devices.AsNoTracking()
                .CountAsync(d => d.BoxId != null && boxIds.Contains(d.BoxId.Value), ct);

        var cameraCount = boxIds.Count == 0
            ? 0
            : await _db.CameraDevices.AsNoTracking()
                .CountAsync(c => c.BoxId != null && boxIds.Contains(c.BoxId.Value), ct);

        return new AreaDetailStats(
            area.Id,
            area.FarmId,
            area.AreaCode,
            area.AreaName,
            area.Description,
            NormalizeAreaStatus(area.Status),
            area.CreatedAt,
            rowCount,
            boxIds.Count,
            esp32Count,
            cameraCount);
    }

    public async Task<List<Box>> ListBoxesByAreaAsync(Guid areaId, CancellationToken ct) =>
        await (
            from b in _db.Boxes.AsNoTracking()
            join r in _db.Rows on b.RowId equals r.Id
            where r.AreaId == areaId
            orderby b.BoxCode
            select b).ToListAsync(ct);

    public async Task<Area?> GetAreaAsync(Guid areaId, CancellationToken ct) =>
        await _db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId, ct);

    static string NormalizeAreaStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "maintenance" => "maintenance",
        "disabled" or "inactive" => "disabled",
        _ => "active"
    };

    public async Task<string> GenerateNextAreaCodeAsync(Guid farmId, CancellationToken ct)
    {
        var codes = await _db.Areas.AsNoTracking()
            .Where(a => a.FarmId == farmId)
            .Select(a => a.AreaCode)
            .ToListAsync(ct);
        return ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.AreaPrefix);
    }

    public async Task<Area> CreateAreaAsync(Guid farmId, UpsertAreaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.AreaName))
            throw new ArgumentException("areaName bắt buộc");

        var code = string.IsNullOrWhiteSpace(req.AreaCode)
            ? await GenerateNextAreaCodeAsync(farmId, ct)
            : req.AreaCode.Trim();

        var area = new Area
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            AreaCode = code,
            AreaName = req.AreaName.Trim(),
            Description = req.Description?.Trim(),
            Status = NormalizeAreaStatus(req.Status),
            CreatedAt = DateTime.UtcNow
        };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(ct);
        return area;
    }

    public async Task<Area?> UpdateAreaAsync(Guid areaId, UpsertAreaRequest req, CancellationToken ct)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == areaId, ct);
        if (area == null) return null;
        if (!string.IsNullOrWhiteSpace(req.AreaCode))
            area.AreaCode = req.AreaCode.Trim();
        area.AreaName = req.AreaName.Trim();
        area.Description = req.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(req.Status))
            area.Status = NormalizeAreaStatus(req.Status);
        await _db.SaveChangesAsync(ct);
        return area;
    }

    public async Task<bool> DeleteAreaAsync(Guid areaId, CancellationToken ct)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == areaId, ct);
        if (area == null) return false;
        _db.Areas.Remove(area);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<Row>> ListRowsAsync(Guid areaId, CancellationToken ct) =>
        await _db.Rows.AsNoTracking()
            .Where(r => r.AreaId == areaId)
            .OrderBy(r => r.RowCode)
            .ToListAsync(ct);

    public async Task<Row?> GetRowAsync(Guid rowId, CancellationToken ct) =>
        await _db.Rows.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rowId, ct);

    public async Task<string> GenerateNextRowCodeAsync(Guid areaId, CancellationToken ct)
    {
        var codes = await _db.Rows.AsNoTracking()
            .Where(r => r.AreaId == areaId)
            .Select(r => r.RowCode)
            .ToListAsync(ct);
        return ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.RowPrefix);
    }

    public async Task<Row?> CreateRowAsync(Guid areaId, UpsertRowRequest req, CancellationToken ct)
    {
        if (!await _db.Areas.AnyAsync(a => a.Id == areaId, ct))
            return null;

        if (string.IsNullOrWhiteSpace(req.RowName))
            throw new ArgumentException("rowName bắt buộc");

        var code = string.IsNullOrWhiteSpace(req.RowCode)
            ? await GenerateNextRowCodeAsync(areaId, ct)
            : req.RowCode.Trim();

        var row = new Row
        {
            Id = Guid.NewGuid(),
            AreaId = areaId,
            RowCode = code,
            RowName = req.RowName.Trim()
        };
        _db.Rows.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<Row?> UpdateRowAsync(Guid rowId, UpsertRowRequest req, CancellationToken ct)
    {
        var row = await _db.Rows.FirstOrDefaultAsync(r => r.Id == rowId, ct);
        if (row == null) return null;
        if (!string.IsNullOrWhiteSpace(req.RowCode))
            row.RowCode = req.RowCode.Trim();
        row.RowName = req.RowName.Trim();
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteRowAsync(Guid rowId, CancellationToken ct)
    {
        var row = await _db.Rows.FirstOrDefaultAsync(r => r.Id == rowId, ct);
        if (row == null) return false;
        _db.Rows.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AreaBelongsToFarmAsync(Guid areaId, Guid farmId, CancellationToken ct) =>
        await _db.Areas.AnyAsync(a => a.Id == areaId && a.FarmId == farmId, ct);

    public async Task<Guid?> RowFarmIdAsync(Guid rowId, CancellationToken ct) =>
        await (from r in _db.Rows.AsNoTracking()
               join a in _db.Areas on r.AreaId equals a.Id
               where r.Id == rowId
               select (Guid?)a.FarmId).FirstOrDefaultAsync(ct);
}

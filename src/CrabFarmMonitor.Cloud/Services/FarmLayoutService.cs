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

    public async Task<Area?> GetAreaAsync(Guid areaId, CancellationToken ct) =>
        await _db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId, ct);

    public async Task<Area> CreateAreaAsync(Guid farmId, UpsertAreaRequest req, CancellationToken ct)
    {
        var area = new Area
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            AreaCode = req.AreaCode.Trim(),
            AreaName = req.AreaName.Trim(),
            Description = req.Description?.Trim()
        };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(ct);
        return area;
    }

    public async Task<Area?> UpdateAreaAsync(Guid areaId, UpsertAreaRequest req, CancellationToken ct)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == areaId, ct);
        if (area == null) return null;
        area.AreaCode = req.AreaCode.Trim();
        area.AreaName = req.AreaName.Trim();
        area.Description = req.Description?.Trim();
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

    public async Task<Row?> CreateRowAsync(Guid areaId, UpsertRowRequest req, CancellationToken ct)
    {
        if (!await _db.Areas.AnyAsync(a => a.Id == areaId, ct))
            return null;

        var row = new Row
        {
            Id = Guid.NewGuid(),
            AreaId = areaId,
            RowCode = req.RowCode.Trim(),
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

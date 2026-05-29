using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class CrabBoxService
{
    private readonly RasCloudDbContext _db;

    public CrabBoxService(RasCloudDbContext db) => _db = db;

    public async Task<List<object>> ListBoxesAsync(Guid farmId, CancellationToken ct)
    {
        var boxes = await _db.CrabBoxes.AsNoTracking()
            .Where(b => b.FarmId == farmId)
            .OrderBy(b => b.Code)
            .ToListAsync(ct);

        var stats = await _db.CrabIndividuals.AsNoTracking()
            .Where(c => c.FarmId == farmId)
            .GroupBy(c => c.BoxId)
            .Select(g => new
            {
                BoxId = g.Key,
                CrabCount = g.Count(),
                WatchCount = g.Count(c => c.Status == "watch" || c.Status == "sick")
            })
            .ToListAsync(ct);

        var statMap = stats.ToDictionary(s => s.BoxId);

        return boxes.Select(b =>
        {
            statMap.TryGetValue(b.Id, out var s);
            return (object)new
            {
                b.Id,
                b.Code,
                b.Label,
                b.RowLabel,
                b.Status,
                b.Capacity,
                crabCount = s?.CrabCount ?? 0,
                watchCount = s?.WatchCount ?? 0
            };
        }).ToList();
    }

    public async Task<CrabBox?> GetBoxAsync(Guid id, CancellationToken ct) =>
        await _db.CrabBoxes.AsNoTracking()
            .Include(b => b.Crabs.OrderBy(c => c.TagCode))
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<CrabBox> CreateBoxAsync(Guid farmId, CreateBoxRequest req, CancellationToken ct)
    {
        var box = new CrabBox
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Code = req.Code.Trim(),
            Label = req.Label?.Trim(),
            RowLabel = req.RowLabel?.Trim(),
            Status = req.Status ?? "active",
            Capacity = req.Capacity > 0 ? req.Capacity : 1,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.CrabBoxes.Add(box);
        await _db.SaveChangesAsync(ct);
        return box;
    }

    public async Task<CrabBox?> UpdateBoxAsync(Guid id, UpdateBoxRequest req, CancellationToken ct)
    {
        var box = await _db.CrabBoxes.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (box == null) return null;
        if (req.Label != null) box.Label = req.Label;
        if (req.RowLabel != null) box.RowLabel = req.RowLabel;
        if (req.Status != null) box.Status = req.Status;
        if (req.Capacity.HasValue) box.Capacity = req.Capacity.Value;
        if (req.Notes != null) box.Notes = req.Notes;
        box.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return box;
    }

    public async Task<CrabIndividual?> AddCrabAsync(Guid boxId, CreateCrabRequest req, CancellationToken ct)
    {
        var box = await _db.CrabBoxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId, ct);
        if (box == null) return null;
        var crab = new CrabIndividual
        {
            Id = Guid.NewGuid(),
            BoxId = boxId,
            FarmId = box.FarmId,
            TagCode = req.TagCode.Trim(),
            Status = req.Status ?? "healthy",
            WeightGrams = req.WeightGrams,
            MoltStage = req.MoltStage,
            HealthNote = req.HealthNote,
            LastWeighedAt = req.WeightGrams.HasValue ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.CrabIndividuals.Add(crab);
        await _db.SaveChangesAsync(ct);
        return crab;
    }

    public async Task<CrabIndividual?> UpdateCrabAsync(Guid crabId, UpdateCrabRequest req, CancellationToken ct)
    {
        var crab = await _db.CrabIndividuals.FirstOrDefaultAsync(c => c.Id == crabId, ct);
        if (crab == null) return null;
        if (req.Status != null) crab.Status = req.Status;
        if (req.WeightGrams.HasValue)
        {
            crab.WeightGrams = req.WeightGrams;
            crab.LastWeighedAt = DateTime.UtcNow;
        }
        if (req.MoltStage != null) crab.MoltStage = req.MoltStage;
        if (req.HealthNote != null) crab.HealthNote = req.HealthNote;
        crab.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return crab;
    }

    public async Task<bool> DeleteCrabAsync(Guid crabId, CancellationToken ct)
    {
        var crab = await _db.CrabIndividuals.FirstOrDefaultAsync(c => c.Id == crabId, ct);
        if (crab == null) return false;
        _db.CrabIndividuals.Remove(crab);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public record CreateBoxRequest(string Code, string? Label, string? RowLabel, string? Status, int Capacity, string? Notes);
public record UpdateBoxRequest(string? Label, string? RowLabel, string? Status, int? Capacity, string? Notes);
public record CreateCrabRequest(string TagCode, string? Status, int? WeightGrams, string? MoltStage, string? HealthNote);
public record UpdateCrabRequest(string? Status, int? WeightGrams, string? MoltStage, string? HealthNote);

using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class ProductionService
{
    private readonly RasCloudDbContext _db;

    public ProductionService(RasCloudDbContext db) => _db = db;

    public async Task<List<Box>> ListBoxesAsync(Guid rowId, CancellationToken ct) =>
        await _db.Boxes.AsNoTracking()
            .Where(b => b.RowId == rowId)
            .OrderBy(b => b.BoxCode)
            .ToListAsync(ct);

    public async Task<Box?> GetBoxAsync(Guid boxId, CancellationToken ct) =>
        await _db.Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId, ct);

    public async Task<Guid?> BoxFarmIdAsync(Guid boxId, CancellationToken ct) =>
        await (from b in _db.Boxes.AsNoTracking()
               join r in _db.Rows on b.RowId equals r.Id
               join a in _db.Areas on r.AreaId equals a.Id
               where b.Id == boxId
               select (Guid?)a.FarmId).FirstOrDefaultAsync(ct);

    public async Task<string> GenerateNextBoxCodeAsync(Guid rowId, CancellationToken ct)
    {
        var codes = await _db.Boxes.AsNoTracking()
            .Where(b => b.RowId == rowId)
            .Select(b => b.BoxCode)
            .ToListAsync(ct);
        return ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.BoxPrefix);
    }

    public async Task<Box?> CreateBoxAsync(Guid rowId, UpsertBoxRequest req, CancellationToken ct)
    {
        if (!await _db.Rows.AnyAsync(r => r.Id == rowId, ct))
            return null;

        var code = string.IsNullOrWhiteSpace(req.BoxCode)
            ? await GenerateNextBoxCodeAsync(rowId, ct)
            : req.BoxCode.Trim();

        var box = new Box
        {
            Id = Guid.NewGuid(),
            RowId = rowId,
            BoxCode = code,
            Position = req.Position?.Trim(),
            Volume = req.Volume,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "empty" : req.Status.Trim().ToLowerInvariant()
        };
        _db.Boxes.Add(box);
        await _db.SaveChangesAsync(ct);
        return box;
    }

    public async Task<List<Box>> CreateBoxesBulkAsync(
        Guid rowId,
        BulkCreateBoxesRequest req,
        CancellationToken ct)
    {
        if (!await _db.Rows.AnyAsync(r => r.Id == rowId, ct))
            return [];

        if (req.Count is < 1 or > 100)
            throw new ArgumentException("count phải từ 1 đến 100");

        var codes = await _db.Boxes.AsNoTracking()
            .Where(b => b.RowId == rowId)
            .Select(b => b.BoxCode)
            .ToListAsync(ct);

        var status = string.IsNullOrWhiteSpace(req.Status)
            ? "empty"
            : req.Status.Trim().ToLowerInvariant();
        var prefix = req.PositionPrefix?.Trim();
        var created = new List<Box>(req.Count);

        for (var i = 0; i < req.Count; i++)
        {
            var code = ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.BoxPrefix);
            codes.Add(code);

            string? position = null;
            if (!string.IsNullOrEmpty(prefix))
                position = req.Count == 1 ? prefix : $"{prefix}-{i + 1}";

            created.Add(new Box
            {
                Id = Guid.NewGuid(),
                RowId = rowId,
                BoxCode = code,
                Position = position,
                Volume = req.Volume,
                Status = status
            });
        }

        _db.Boxes.AddRange(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<Box?> UpdateBoxAsync(Guid boxId, UpsertBoxRequest req, CancellationToken ct)
    {
        var box = await _db.Boxes.FirstOrDefaultAsync(b => b.Id == boxId, ct);
        if (box == null) return null;
        if (!string.IsNullOrWhiteSpace(req.BoxCode))
            box.BoxCode = req.BoxCode.Trim();
        box.Position = req.Position?.Trim();
        box.Volume = req.Volume;
        if (!string.IsNullOrWhiteSpace(req.Status))
            box.Status = req.Status.Trim().ToLowerInvariant();
        await _db.SaveChangesAsync(ct);
        return box;
    }

    public async Task<bool> DeleteBoxAsync(Guid boxId, CancellationToken ct)
    {
        var box = await _db.Boxes.FirstOrDefaultAsync(b => b.Id == boxId, ct);
        if (box == null) return false;
        _db.Boxes.Remove(box);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<FarmingBatch>> ListBatchesAsync(Guid boxId, CancellationToken ct) =>
        await _db.FarmingBatches.AsNoTracking()
            .Where(b => b.BoxId == boxId)
            .OrderByDescending(b => b.StartDate)
            .ThenBy(b => b.BatchCode)
            .ToListAsync(ct);

    public async Task<FarmingBatch?> GetBatchAsync(Guid batchId, CancellationToken ct) =>
        await _db.FarmingBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public async Task<Guid?> BatchFarmIdAsync(Guid batchId, CancellationToken ct) =>
        await (from fb in _db.FarmingBatches.AsNoTracking()
               join bx in _db.Boxes on fb.BoxId equals bx.Id
               join r in _db.Rows on bx.RowId equals r.Id
               join a in _db.Areas on r.AreaId equals a.Id
               where fb.Id == batchId
               select (Guid?)a.FarmId).FirstOrDefaultAsync(ct);

    public async Task<string> GenerateNextBatchCodeAsync(Guid boxId, CancellationToken ct)
    {
        var codes = await _db.FarmingBatches.AsNoTracking()
            .Where(b => b.BoxId == boxId)
            .Select(b => b.BatchCode)
            .ToListAsync(ct);
        return ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.BatchPrefix);
    }

    public async Task<FarmingBatch?> CreateBatchAsync(Guid boxId, UpsertFarmingBatchRequest req, CancellationToken ct)
    {
        if (!await _db.Boxes.AnyAsync(b => b.Id == boxId, ct))
            return null;

        var code = string.IsNullOrWhiteSpace(req.BatchCode)
            ? await GenerateNextBatchCodeAsync(boxId, ct)
            : req.BatchCode.Trim();

        var batch = new FarmingBatch
        {
            Id = Guid.NewGuid(),
            BoxId = boxId,
            BatchCode = code,
            StartDate = req.StartDate,
            ExpectedHarvestDate = req.ExpectedHarvestDate,
            ActualHarvestDate = req.ActualHarvestDate,
            InitialQuantity = req.InitialQuantity,
            CurrentQuantity = req.CurrentQuantity > 0 ? req.CurrentQuantity : req.InitialQuantity,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "active" : req.Status.Trim().ToLowerInvariant()
        };
        _db.FarmingBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return batch;
    }

    public async Task<FarmingBatch?> UpdateBatchAsync(Guid batchId, UpsertFarmingBatchRequest req, CancellationToken ct)
    {
        var batch = await _db.FarmingBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return null;
        if (!string.IsNullOrWhiteSpace(req.BatchCode))
            batch.BatchCode = req.BatchCode.Trim();
        batch.StartDate = req.StartDate;
        batch.ExpectedHarvestDate = req.ExpectedHarvestDate;
        batch.ActualHarvestDate = req.ActualHarvestDate;
        batch.InitialQuantity = req.InitialQuantity;
        batch.CurrentQuantity = req.CurrentQuantity;
        if (!string.IsNullOrWhiteSpace(req.Status))
            batch.Status = req.Status.Trim().ToLowerInvariant();
        await _db.SaveChangesAsync(ct);
        return batch;
    }

    public async Task<bool> DeleteBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await _db.FarmingBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return false;
        _db.FarmingBatches.Remove(batch);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<Crab>> ListBatchCrabsAsync(Guid batchId, CancellationToken ct) =>
        await _db.Crabs.AsNoTracking()
            .Where(c => c.BatchId == batchId)
            .OrderBy(c => c.CrabCode)
            .ToListAsync(ct);

    public async Task<Crab?> GetBatchCrabAsync(Guid crabId, CancellationToken ct) =>
        await _db.Crabs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crabId, ct);

    public async Task<Guid?> BatchCrabFarmIdAsync(Guid crabId, CancellationToken ct) =>
        await (from c in _db.Crabs.AsNoTracking()
               join fb in _db.FarmingBatches on c.BatchId equals fb.Id
               join bx in _db.Boxes on fb.BoxId equals bx.Id
               join r in _db.Rows on bx.RowId equals r.Id
               join a in _db.Areas on r.AreaId equals a.Id
               where c.Id == crabId
               select (Guid?)a.FarmId).FirstOrDefaultAsync(ct);

    public async Task<string> GenerateNextCrabCodeAsync(Guid batchId, CancellationToken ct)
    {
        var codes = await _db.Crabs.AsNoTracking()
            .Where(c => c.BatchId == batchId)
            .Select(c => c.CrabCode)
            .ToListAsync(ct);
        return ProductionCodeGenerator.Next(codes, ProductionCodeGenerator.CrabPrefix);
    }

    public async Task<Crab?> CreateBatchCrabAsync(Guid batchId, UpsertBatchCrabRequest req, CancellationToken ct)
    {
        if (!await _db.FarmingBatches.AnyAsync(b => b.Id == batchId, ct))
            return null;

        var code = string.IsNullOrWhiteSpace(req.CrabCode)
            ? await GenerateNextCrabCodeAsync(batchId, ct)
            : req.CrabCode.Trim();

        var crab = new Crab
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            CrabCode = code,
            Gender = string.IsNullOrWhiteSpace(req.Gender) ? "unknown" : req.Gender.Trim().ToLowerInvariant(),
            Weight = req.Weight,
            ShellWidth = req.ShellWidth,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "alive" : req.Status.Trim().ToLowerInvariant()
        };
        _db.Crabs.Add(crab);
        await _db.SaveChangesAsync(ct);
        return crab;
    }

    public async Task<Crab?> UpdateBatchCrabAsync(Guid crabId, UpsertBatchCrabRequest req, CancellationToken ct)
    {
        var crab = await _db.Crabs.FirstOrDefaultAsync(c => c.Id == crabId, ct);
        if (crab == null) return null;
        if (!string.IsNullOrWhiteSpace(req.CrabCode))
            crab.CrabCode = req.CrabCode.Trim();
        crab.Gender = string.IsNullOrWhiteSpace(req.Gender) ? crab.Gender : req.Gender.Trim().ToLowerInvariant();
        crab.Weight = req.Weight;
        crab.ShellWidth = req.ShellWidth;
        if (!string.IsNullOrWhiteSpace(req.Status))
            crab.Status = req.Status.Trim().ToLowerInvariant();
        await _db.SaveChangesAsync(ct);
        return crab;
    }

    public async Task<bool> DeleteBatchCrabAsync(Guid crabId, CancellationToken ct)
    {
        var crab = await _db.Crabs.FirstOrDefaultAsync(c => c.Id == crabId, ct);
        if (crab == null) return false;
        _db.Crabs.Remove(crab);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

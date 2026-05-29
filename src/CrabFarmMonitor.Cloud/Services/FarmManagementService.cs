using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class FarmManagementService
{
    private readonly RasCloudDbContext _db;

    public FarmManagementService(RasCloudDbContext db) => _db = db;

    public async Task<Farm> CreateAsync(FarmAccess access, UpsertFarmRequest req, CancellationToken ct)
    {
        if (!access.IsOrgAdmin)
            throw new UnauthorizedAccessException("Chỉ admin/owner được tạo trại mới");

        var code = req.Code.Trim();
        if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(req.Name))
            throw new ArgumentException("code và name bắt buộc");

        if (await _db.Farms.AnyAsync(f => f.OrgId == access.OrgId && f.Code == code, ct))
            throw new ArgumentException($"Mã trại '{code}' đã tồn tại");

        var farm = new Farm
        {
            Id = Guid.NewGuid(),
            OrgId = access.OrgId,
            Code = code,
            Name = req.Name.Trim(),
            Address = req.Address?.Trim(),
            Description = req.Description?.Trim(),
            OwnerId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Farms.Add(farm);
        await _db.SaveChangesAsync(ct);
        return farm;
    }

    public async Task<Farm?> UpdateAsync(
        FarmAccess access,
        Guid farmId,
        UpsertFarmRequest req,
        CancellationToken ct)
    {
        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == farmId && f.OrgId == access.OrgId, ct);
        if (farm == null) return null;

        if (!access.IsOrgAdmin && farm.OwnerId != access.UserId)
            throw new UnauthorizedAccessException("Không có quyền sửa trại này");

        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var code = req.Code.Trim();
            if (await _db.Farms.AnyAsync(
                    f => f.OrgId == access.OrgId && f.Code == code && f.Id != farmId, ct))
                throw new ArgumentException($"Mã trại '{code}' đã tồn tại");
            farm.Code = code;
        }

        if (!string.IsNullOrWhiteSpace(req.Name))
            farm.Name = req.Name.Trim();

        farm.Address = req.Address?.Trim();
        farm.Description = req.Description?.Trim();
        await _db.SaveChangesAsync(ct);
        return farm;
    }

    public async Task<bool> DeleteAsync(FarmAccess access, Guid farmId, CancellationToken ct)
    {
        if (!access.IsOrgAdmin)
            throw new UnauthorizedAccessException("Chỉ admin/owner được xóa trại");

        var farm = await _db.Farms
            .FirstOrDefaultAsync(f => f.Id == farmId && f.OrgId == access.OrgId, ct);
        if (farm == null) return false;

        _db.Farms.Remove(farm);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

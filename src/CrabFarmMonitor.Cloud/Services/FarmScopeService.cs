using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class FarmAccess
{
    public Guid UserId { get; init; }
    public Guid OrgId { get; init; }
    public string Role { get; init; } = "staff";
    public IReadOnlyList<Guid> FarmIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Admin / owner — xem và chọn mọi farm trong tổ chức.</summary>
    public bool IsOrgAdmin => FarmRolePolicy.CanViewAllFarmsInOrg(Role);
}

public static class FarmRolePolicy
{
    public static bool CanViewAllFarmsInOrg(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        return role.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("owner", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FarmScopeService
{
    private readonly RasCloudDbContext _db;
    private readonly IConfiguration _config;

    public FarmScopeService(RasCloudDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<FarmAccess?> LoadAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var orgClaim = user.FindFirstValue("org_id");
        if (!Guid.TryParse(orgClaim, out var orgId)) return null;

        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId)) return null;

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "staff";
        var farmIds = await QueryAccessibleFarmIdsAsync(orgId, userId, role, ct);

        return new FarmAccess { UserId = userId, OrgId = orgId, Role = role, FarmIds = farmIds };
    }

    public async Task<List<Farm>> ListAccessibleFarmsAsync(FarmAccess access, CancellationToken ct) =>
        await AccessibleFarmsQuery(access.OrgId, access.UserId, access.Role)
            .OrderBy(f => f.Code)
            .ToListAsync(ct);

    private async Task<List<Guid>> QueryAccessibleFarmIdsAsync(
        Guid orgId,
        Guid userId,
        string role,
        CancellationToken ct) =>
        await AccessibleFarmsQuery(orgId, userId, role)
            .Select(f => f.Id)
            .ToListAsync(ct);

    private IQueryable<Farm> AccessibleFarmsQuery(Guid orgId, Guid userId, string role)
    {
        var q = _db.Farms.AsNoTracking().Where(f => f.OrgId == orgId);
        if (!FarmRolePolicy.CanViewAllFarmsInOrg(role))
            q = q.Where(f => f.OwnerId == userId);
        return q;
    }

    /// <summary>Resolved farm for query. null = all farms in org (admin only).</summary>
    public (Guid? FarmId, IReadOnlyList<Guid> ScopeFarmIds, string? Error) Resolve(
        FarmAccess access,
        Guid? requestedFarmId)
    {
        if (access.FarmIds.Count == 0)
            return (null, access.FarmIds, "no farms in organization");

        if (requestedFarmId.HasValue)
        {
            if (!access.FarmIds.Contains(requestedFarmId.Value))
                return (null, access.FarmIds, "farm not in your organization");
            return (requestedFarmId, new[] { requestedFarmId.Value }, null);
        }

        if (access.IsOrgAdmin)
            return (null, access.FarmIds, null);

        return (access.FarmIds[0], new[] { access.FarmIds[0] }, null);
    }

    public Guid DefaultFarmId(FarmAccess access) =>
        access.FarmIds.Count > 0
            ? access.FarmIds[0]
            : Guid.Parse(_config["DEFAULT_FARM_ID"] ?? "11111111-1111-1111-1111-111111111111");
}

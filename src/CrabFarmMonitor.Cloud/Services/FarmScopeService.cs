using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class FarmAccess
{
    public Guid OrgId { get; init; }
    public string Role { get; init; } = "operator";
    public IReadOnlyList<Guid> FarmIds { get; init; } = Array.Empty<Guid>();

    public bool IsOrgAdmin => Role is "owner" or "admin";
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

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "operator";
        var farmIds = await _db.Farms.AsNoTracking()
            .Where(f => f.OrgId == orgId)
            .OrderBy(f => f.Code)
            .Select(f => f.Id)
            .ToListAsync(ct);

        return new FarmAccess { OrgId = orgId, Role = role, FarmIds = farmIds };
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

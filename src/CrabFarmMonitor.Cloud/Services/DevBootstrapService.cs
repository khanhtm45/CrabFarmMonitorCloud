using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public static class DevBootstrapService
{
    public static async Task EnsureAsync(RasCloudDbContext db, IConfiguration config)
    {
        if (await db.Users.AnyAsync()) return;

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Slug == "iras-demo");
        if (org == null)
        {
            org = new Organization { Id = Guid.NewGuid(), Name = "IRAS Demo", Slug = "iras-demo" };
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
        }

        var adminEmail = config["DEV_ADMIN_EMAIL"] ?? "admin@iras.local";
        var adminPass = config["DEV_ADMIN_PASSWORD"] ?? "admin123";

        var adminId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = adminId,
            OrgId = org.Id,
            Username = "admin",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPass),
            FullName = "Administrator",
            DisplayName = "Admin",
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var farmId = Guid.Parse(config["DEFAULT_FARM_ID"] ?? "11111111-1111-1111-1111-111111111111");
        if (!await db.Farms.AnyAsync(f => f.Id == farmId))
        {
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OrgId = org.Id,
                Code = "farm-01",
                Name = "Local Dev Farm",
                OwnerId = adminId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"Bootstrap: admin user {adminEmail} / {adminPass}");
    }

    /// <summary>Thêm farm demo (farm-02, farm-03) cho admin test đổi trại — idempotent.</summary>
    public static async Task EnsureDemoFarmsAsync(RasCloudDbContext db, IConfiguration config)
    {
        if (!config.GetValue("DEV_SEED_EXTRA_FARMS", false)) return;

        var org = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Slug == "iras-demo");
        if (org == null) return;

        var admin = await db.Users.AsNoTracking()
            .Where(u => u.OrgId == org.Id
                && (u.Role.ToLower() == "admin" || u.Role.ToLower() == "owner"))
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync();
        if (admin == null) return;

        var extra = new[]
        {
            (Code: "farm-02", Name: "Trại Demo B"),
            (Code: "farm-03", Name: "Trại Demo C"),
        };
        var added = false;
        foreach (var (code, name) in extra)
        {
            if (await db.Farms.AnyAsync(f => f.OrgId == org.Id && f.Code == code)) continue;
            db.Farms.Add(new Farm
            {
                Id = Guid.NewGuid(),
                OrgId = org.Id,
                Code = code,
                Name = name,
                OwnerId = admin.Id,
                CreatedAt = DateTime.UtcNow
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync();
            Console.WriteLine("Bootstrap: seeded demo farms farm-02, farm-03");
        }
    }
}

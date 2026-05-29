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

        var farmId = Guid.Parse(config["DEFAULT_FARM_ID"] ?? "11111111-1111-1111-1111-111111111111");
        if (!await db.Farms.AnyAsync(f => f.Id == farmId))
        {
            db.Farms.Add(new Farm
            {
                Id = farmId,
                OrgId = org.Id,
                Code = "farm-01",
                Name = "Local Dev Farm",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var adminEmail = config["DEV_ADMIN_EMAIL"] ?? "admin@iras.local";
        var adminPass = config["DEV_ADMIN_PASSWORD"] ?? "admin123";

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
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

        await db.SaveChangesAsync();
        Console.WriteLine($"Bootstrap: admin user {adminEmail} / {adminPass}");
    }
}

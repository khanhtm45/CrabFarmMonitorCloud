using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

/// <summary>Áp dụng domain schema trên DB đã chạy (không reset volume Docker).</summary>
public static class DomainSchemaBootstrap
{
    public static async Task EnsureAsync(RasCloudDbContext db)
    {
        try
        {
            var applied = false;
            foreach (var candidate in ResolveSqlPaths())
            {
                if (!File.Exists(candidate)) continue;
                var sql = await File.ReadAllTextAsync(candidate);
                await db.Database.ExecuteSqlRawAsync(sql);
                Console.WriteLine($"Domain schema: applied from {candidate}");
                applied = true;
                break;
            }

            if (!applied)
            {
                Console.WriteLine(
                    "Domain schema: cloud_domain_schema.sql not found — run deploy/apply-managed-db-schema.ps1 or include database/ in publish.");
            }

            await db.Database.ExecuteSqlRawAsync(@"
ALTER TABLE farms ADD COLUMN IF NOT EXISTS address TEXT;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS owner_id UUID;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE farms ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE users ADD COLUMN IF NOT EXISTS username VARCHAR(50);
ALTER TABLE users ADD COLUMN IF NOT EXISTS full_name VARCHAR(100);
ALTER TABLE users ADD COLUMN IF NOT EXISTS phone VARCHAR(20);
ALTER TABLE users ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE devices ADD COLUMN IF NOT EXISTS box_id UUID;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS device_name TEXT;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS firmware_version VARCHAR(20);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS ip_lan VARCHAR(50);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS status TEXT NOT NULL DEFAULT 'offline';
ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ;");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Domain schema bootstrap: {ex.Message}");
        }
    }

    private static IEnumerable<string> ResolveSqlPaths()
    {
        const string fileName = "cloud_domain_schema.sql";

        yield return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "database", fileName));
        yield return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "docs", fileName));
        yield return Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "database", fileName));
        yield return Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "docs", fileName));

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            yield return Path.GetFullPath(Path.Combine(dir, "database", fileName));
            yield return Path.GetFullPath(Path.Combine(dir, "docs", fileName));
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
    }
}

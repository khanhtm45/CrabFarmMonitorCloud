using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public static class CrabSchemaBootstrap
{
    public static async Task EnsureAsync(RasCloudDbContext db, IConfiguration config)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS crab_boxes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    code TEXT NOT NULL,
    label TEXT,
    row_label TEXT,
    status TEXT NOT NULL DEFAULT 'active',
    capacity INT NOT NULL DEFAULT 1,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (farm_id, code)
);
CREATE TABLE IF NOT EXISTS crab_individuals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    box_id UUID NOT NULL REFERENCES crab_boxes(id) ON DELETE CASCADE,
    farm_id UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    tag_code TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'healthy',
    weight_grams INT,
    molt_stage TEXT,
    health_note TEXT,
    last_weighed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (box_id, tag_code)
);
CREATE TABLE IF NOT EXISTS device_shadows (
    device_id UUID PRIMARY KEY REFERENCES devices(id) ON DELETE CASCADE,
    desired JSONB,
    reported JSONB,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);");

            var farmId = Guid.Parse(config["DEFAULT_FARM_ID"] ?? "11111111-1111-1111-1111-111111111111");
            if (!await db.CrabBoxes.AnyAsync(b => b.FarmId == farmId))
            {
                var box1 = new Data.Entities.CrabBox
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222201"),
                    FarmId = farmId,
                    Code = "A-01",
                    Label = "Hộp A-01",
                    RowLabel = "Dãy A",
                    Status = "active",
                    Capacity = 4,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                var box2 = new Data.Entities.CrabBox
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222202"),
                    FarmId = farmId,
                    Code = "A-02",
                    Label = "Hộp A-02",
                    RowLabel = "Dãy A",
                    Status = "watch",
                    Capacity = 4,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.CrabBoxes.AddRange(box1, box2);
                db.CrabIndividuals.AddRange(
                    new Data.Entities.CrabIndividual
                    {
                        Id = Guid.NewGuid(), BoxId = box1.Id, FarmId = farmId, TagCode = "CUA-001",
                        Status = "healthy", WeightGrams = 320, MoltStage = "intermolt",
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    },
                    new Data.Entities.CrabIndividual
                    {
                        Id = Guid.NewGuid(), BoxId = box1.Id, FarmId = farmId, TagCode = "CUA-002",
                        Status = "watch", WeightGrams = 280, MoltStage = "premolt",
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    },
                    new Data.Entities.CrabIndividual
                    {
                        Id = Guid.NewGuid(), BoxId = box2.Id, FarmId = farmId, TagCode = "CUA-003",
                        Status = "healthy", WeightGrams = 350,
                        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    });
                await db.SaveChangesAsync();
                Console.WriteLine("Bootstrap: demo crab boxes A-01, A-02");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Crab schema bootstrap: {ex.Message}");
        }
    }
}

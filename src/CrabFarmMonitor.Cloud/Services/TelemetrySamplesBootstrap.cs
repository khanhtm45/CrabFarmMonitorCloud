using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

public static class TelemetrySamplesBootstrap
{
    public static async Task EnsureAsync(RasCloudDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS telemetry_samples (
    id          BIGSERIAL PRIMARY KEY,
    farm_id     UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    device_id   UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    pin         SMALLINT NOT NULL,
    val         DOUBLE PRECISION NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS idx_telemetry_samples_device_pin_time
    ON telemetry_samples (device_id, pin, recorded_at DESC);
CREATE INDEX IF NOT EXISTS idx_telemetry_samples_farm_time
    ON telemetry_samples (farm_id, recorded_at DESC);");
    }
}

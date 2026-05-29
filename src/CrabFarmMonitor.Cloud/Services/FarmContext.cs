namespace CrabFarmMonitor.Cloud.Services;

public static class FarmContext
{
    public static Guid ResolveFarmId(HttpRequest req, IConfiguration config)
    {
        var header = req.Headers["X-Farm-Id"].FirstOrDefault();
        if (Guid.TryParse(header, out var id)) return id;
        return Guid.Parse(config["DEFAULT_FARM_ID"]!);
    }
}

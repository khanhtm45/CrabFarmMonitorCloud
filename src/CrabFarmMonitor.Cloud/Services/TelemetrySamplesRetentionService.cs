using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;

namespace CrabFarmMonitor.Cloud.Services;

/// <summary>Xóa mẫu telemetry cũ hơn TELEMETRY_SAMPLES_RETENTION_DAYS (mặc định 7).</summary>
public sealed class TelemetrySamplesRetentionService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;
    private readonly ILogger<TelemetrySamplesRetentionService> _log;

    public TelemetrySamplesRetentionService(
        IServiceProvider sp,
        IConfiguration config,
        ILogger<TelemetrySamplesRetentionService> log)
    {
        _sp = sp;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "telemetry_samples retention failed");
            }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        if (!SamplesEnabled()) return;

        var days = int.TryParse(_config["TELEMETRY_SAMPLES_RETENTION_DAYS"], out var d) ? d : 7;
        days = Math.Clamp(days, 1, 90);
        var cutoff = DateTime.UtcNow.AddDays(-days);

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RasCloudDbContext>();
        var deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM telemetry_samples WHERE recorded_at < {0}", cutoff);
        if (deleted > 0)
            _log.LogInformation("telemetry_samples: deleted {Count} rows older than {Days}d", deleted, days);
    }

    private bool SamplesEnabled() =>
        !string.Equals(_config["TELEMETRY_SAMPLES_ENABLED"], "false", StringComparison.OrdinalIgnoreCase);
}

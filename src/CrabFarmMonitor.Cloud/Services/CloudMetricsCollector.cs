using System.Text;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class CloudMetricsCollector
{
    private long _telemetryIngest;
    private long _hdf5Sync;
    private readonly DateTime _started = DateTime.UtcNow;

    public void IncTelemetry() => Interlocked.Increment(ref _telemetryIngest);
    public void IncHdf5Sync() => Interlocked.Increment(ref _hdf5Sync);

    public string RenderPrometheus()
    {
        var uptime = (DateTime.UtcNow - _started).TotalSeconds;
        var sb = new StringBuilder();
        sb.AppendLine("ras_cloud_up 1");
        sb.AppendLine(FormattableString.Invariant($"ras_cloud_uptime_seconds {uptime:F0}"));
        sb.AppendLine(FormattableString.Invariant($"ras_cloud_telemetry_ingest_total {Interlocked.Read(ref _telemetryIngest)}"));
        sb.AppendLine(FormattableString.Invariant($"ras_cloud_hdf5_sync_total {Interlocked.Read(ref _hdf5Sync)}"));
        return sb.ToString();
    }
}

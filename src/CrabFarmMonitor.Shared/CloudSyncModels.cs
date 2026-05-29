using System.Text.Json.Serialization;

namespace CrabFarmMonitor.Shared;

/// <summary>ESP32 → Edge/Cloud realtime (≈ 5s). Khớp payload Flutter/Cloud.</summary>
public sealed class SensorRealtimeDto
{
    [JsonPropertyName("deviceCode")]
    public string DeviceCode { get; set; } = "";

    [JsonPropertyName("boxCode")]
    public string? BoxCode { get; set; }

    [JsonPropertyName("recordedAt")]
    public DateTime RecordedAt { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("ph")]
    public double? Ph { get; set; }

    [JsonPropertyName("do")]
    public double? DissolvedOxygen { get; set; }

    [JsonPropertyName("salinity")]
    public double? Salinity { get; set; }

    [JsonPropertyName("orp")]
    public double? Orp { get; set; }

    [JsonPropertyName("nh3")]
    public double? Nh3 { get; set; }

    [JsonPropertyName("no2")]
    public double? No2 { get; set; }
}

/// <summary>Edge → Cloud batch sync (≈ 30–60s).</summary>
public sealed class SensorBatchSyncDto
{
    [JsonPropertyName("gatewayId")]
    public string GatewayId { get; set; } = "";

    [JsonPropertyName("items")]
    public List<SensorRealtimeDto> Items { get; set; } = new();
}

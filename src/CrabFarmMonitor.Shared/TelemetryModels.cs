using System.Text.Json.Serialization;

namespace CrabFarmMonitor.Shared;

public sealed class TelemetryPayload
{
    [JsonPropertyName("mac")]
    public string? Mac { get; set; }

    [JsonPropertyName("readings")]
    public List<ReadingDto> Readings { get; set; } = new();
}

public sealed class ReadingDto
{
    [JsonPropertyName("pin")]
    public int Pin { get; set; }

    [JsonPropertyName("val")]
    public double Val { get; set; }
}

public sealed class ControlCommandDto
{
    [JsonPropertyName("pin")]
    public int Pin { get; set; }

    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "";
}

public static class PinLabels
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [1] = "temp", [2] = "pH", [3] = "tds", [4] = "flow", [5] = "water",
        [6] = "voltage", [7] = "current", [8] = "power",
        [9] = "relay1", [10] = "relay2", [11] = "relay3", [12] = "relay4",
        [13] = "relay5", [14] = "relay6",
    };

    public static string Label(int pin) => Map.TryGetValue(pin, out var n) ? n : $"pin{pin}";

    public static Dictionary<string, double> ToMap(TelemetryPayload p)
    {
        var d = new Dictionary<string, double>();
        foreach (var r in p.Readings)
            d[Label(r.Pin)] = r.Val;
        return d;
    }
}

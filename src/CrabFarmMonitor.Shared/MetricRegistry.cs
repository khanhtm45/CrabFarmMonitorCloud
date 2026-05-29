namespace CrabFarmMonitor.Shared;

public sealed class MetricDefinition
{
    public string Metric { get; init; } = "";
    public string Unit { get; init; } = "";
    public string Label { get; init; } = "";
}

/// <summary>Pin → metric metadata for AI / warehouse (backward compatible with pin+val).</summary>
public static class MetricRegistry
{
    private static readonly Dictionary<int, MetricDefinition> ByPin = new()
    {
        [1] = new() { Metric = "temp", Unit = "C", Label = "temp" },
        [2] = new() { Metric = "ph", Unit = "pH", Label = "pH" },
        [3] = new() { Metric = "tds", Unit = "ppm", Label = "tds" },
        [4] = new() { Metric = "flow", Unit = "L/min", Label = "flow" },
        [5] = new() { Metric = "water_level", Unit = "bool", Label = "water" },
        [6] = new() { Metric = "voltage", Unit = "V", Label = "voltage" },
        [7] = new() { Metric = "current", Unit = "A", Label = "current" },
        [8] = new() { Metric = "power", Unit = "W", Label = "power" },
        [9] = new() { Metric = "relay1", Unit = "bool", Label = "relay1" },
        [10] = new() { Metric = "relay2", Unit = "bool", Label = "relay2" },
        [11] = new() { Metric = "relay3", Unit = "bool", Label = "relay3" },
        [12] = new() { Metric = "relay4", Unit = "bool", Label = "relay4" },
        [13] = new() { Metric = "relay5", Unit = "bool", Label = "relay5" },
        [14] = new() { Metric = "relay6", Unit = "bool", Label = "relay6" },
    };

    public static MetricDefinition Get(int pin) =>
        ByPin.TryGetValue(pin, out var d)
            ? d
            : new MetricDefinition { Metric = $"pin{pin}", Unit = "", Label = PinLabels.Label(pin) };

    public static List<EnrichedReadingDto> Enrich(TelemetryPayload payload, string quality = "good")
    {
        var list = new List<EnrichedReadingDto>();
        foreach (var r in payload.Readings)
        {
            var def = Get(r.Pin);
            list.Add(new EnrichedReadingDto
            {
                Pin = r.Pin,
                Val = r.Val,
                Metric = def.Metric,
                Unit = def.Unit,
                Label = def.Label,
                Quality = quality
            });
        }
        return list;
    }
}

public sealed class EnrichedReadingDto
{
    public int Pin { get; set; }
    public double Val { get; set; }
    public string Metric { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Label { get; set; } = "";
    public string Quality { get; set; } = "good";
}

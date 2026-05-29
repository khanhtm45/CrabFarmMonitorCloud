namespace CrabFarmMonitor.Shared;

public sealed class Hdf5ReadQuery
{
    public int Limit { get; init; } = 5000;
    public int? Pin { get; init; }
    public long? FromMs { get; init; }
    public long? ToMs { get; init; }
    public string? Chunk { get; init; }
    public string? FarmId { get; init; }

    public object ToScriptPayload(string fileName) => new
    {
        file = fileName,
        limit = Math.Clamp(Limit, 1, 20000),
        pin = Pin,
        from_ms = FromMs,
        to_ms = ToMs,
        chunk = Chunk,
        farm_id = FarmId,
    };
}

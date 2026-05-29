namespace CrabFarmMonitor.Cloud.Services;

/// <summary>Chunk key cho MinIO: yyyyMMdd_HH (v3) hoặc MAC/yyyyMMdd/HH (legacy).</summary>
public static class Hdf5ArchiveKey
{
    public static string? PeriodKeyFromFileName(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (baseName.StartsWith("telemetry_", StringComparison.OrdinalIgnoreCase))
        {
            var chunkId = baseName["telemetry_".Length..];
            if (chunkId.Length >= 9 && chunkId.Contains('_'))
                return $"chunks/{chunkId}";
        }

        var parts = baseName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Length >= 13 && long.TryParse(parts[0], out _))
            parts = parts.Skip(1).ToArray();

        if (parts.Length < 2 || parts[0].Length != 12 || parts[1].Length != 8)
            return null;

        var mac = parts[0].ToUpperInvariant();
        var day = parts[1];

        if (parts.Length >= 3 && parts[2].Length == 2 && int.TryParse(parts[2], out var hour))
            return $"{mac}/{day}/{hour:00}";

        return $"{mac}/{day}";
    }
}

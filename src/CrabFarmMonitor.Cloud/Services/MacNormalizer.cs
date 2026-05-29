namespace CrabFarmMonitor.Cloud.Services;

public static class MacNormalizer
{
    public static string? Normalize(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var s = mac.Trim().ToUpperInvariant().Replace('-', ':');
        return s;
    }
}

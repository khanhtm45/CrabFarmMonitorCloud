using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using CrabFarmMonitor.Cloud.Services;

namespace CrabFarmMonitor.Cloud.Data;

public sealed class MacAddressConverter : ValueConverter<string?, PhysicalAddress?>
{
    public MacAddressConverter()
        : base(
            v => ToPhysical(v),
            v => ToString(v))
    {
    }

    private static PhysicalAddress? ToPhysical(string? mac)
    {
        var norm = MacNormalizer.Normalize(mac);
        if (norm == null) return null;
        var parts = norm.Split(':');
        if (parts.Length != 6) return null;
        var bytes = parts.Select(p => Convert.ToByte(p, 16)).ToArray();
        return new PhysicalAddress(bytes);
    }

    private static string? ToString(PhysicalAddress? pa)
    {
        if (pa == null) return null;
        return string.Join(":", pa.GetAddressBytes().Select(b => b.ToString("X2")));
    }
}

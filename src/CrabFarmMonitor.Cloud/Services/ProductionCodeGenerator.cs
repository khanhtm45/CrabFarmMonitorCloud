using System.Text.RegularExpressions;

namespace CrabFarmMonitor.Cloud.Services;

/// <summary>Mã tự động theo cấp: K-1 (khu), D-1 (dãy), H-1 (hộp), BT-1 (đợt theo dãy), C-1 (cua theo đợt).</summary>
public static class ProductionCodeGenerator
{
    public const string AreaPrefix = "K";
    public const string RowPrefix = "D";
    public const string BoxPrefix = "H";
    public const string BatchPrefix = "BT";
    public const string CrabPrefix = "C";

    public static string Next(IReadOnlyList<string> existingCodes, string prefix)
    {
        var pattern = new Regex(
            $"^{Regex.Escape(prefix)}-(\\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var max = 0;
        foreach (var raw in existingCodes)
        {
            var m = pattern.Match(raw.Trim());
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > max)
                max = n;
        }

        var candidate = max + 1;
        while (existingCodes.Any(c =>
                   string.Equals(c.Trim(), $"{prefix}-{candidate}", StringComparison.OrdinalIgnoreCase)))
            candidate++;

        return $"{prefix}-{candidate}";
    }
}

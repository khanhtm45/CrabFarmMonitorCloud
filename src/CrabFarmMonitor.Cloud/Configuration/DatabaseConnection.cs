namespace CrabFarmMonitor.Cloud.Configuration;

/// <summary>
/// Resolves PostgreSQL connection string from App Platform / env (DATABASE_URL or ConnectionStrings__Default).
/// </summary>
public static class DatabaseConnection
{
    public static string Resolve(IConfiguration config)
    {
        var fromConfig = config.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        var databaseUrl = config["DATABASE_URL"]?.Trim();
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return Normalize(databaseUrl);

        return "Host=localhost;Port=5432;Database=ras_cloud;Username=ras;Password=ras_dev_password";
    }

    /// <summary>
    /// Supports Npgsql key=value (Host=…;Port=…) and postgres:// URIs from DigitalOcean.
    /// </summary>
    public static string Normalize(string value)
    {
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.Contains('=') && value.Contains(';', StringComparison.Ordinal))
        {
            if (!value.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("Ssl Mode", StringComparison.OrdinalIgnoreCase))
            {
                value += ";SSL Mode=Require;Trust Server Certificate=true";
            }
            return value;
        }

        return value;
    }
}

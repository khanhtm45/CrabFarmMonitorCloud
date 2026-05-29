using Npgsql;

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
            return Normalize(fromConfig.Trim());

        var databaseUrl = config["DATABASE_URL"]?.Trim();
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return Normalize(databaseUrl);

        return "Host=localhost;Port=5432;Database=ras_cloud;Username=ras;Password=ras_dev_password";
    }

    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["DATABASE_URL"])
        || !string.IsNullOrWhiteSpace(config.GetConnectionString("Default"));

    /// <summary>Host/database for /health — never includes password.</summary>
    public static object Describe(IConfiguration config)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(Resolve(config));
            return new { b.Host, b.Port, database = b.Database, username = b.Username, ssl = b.SslMode.ToString() };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>Supports Npgsql key=value, postgres:// URIs (DigitalOcean).</summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        try
        {
            var b = new NpgsqlConnectionStringBuilder(value);
            var managed = b.Host?.Contains("ondigitalocean.com", StringComparison.OrdinalIgnoreCase) == true
                || b.Host?.Contains("db.ondigitalocean.com", StringComparison.OrdinalIgnoreCase) == true;
            if (managed)
            {
                b.SslMode = SslMode.Require;
                b.TrustServerCertificate = true;
            }
            else if (b.SslMode == SslMode.Disable && value.Contains("SSL Mode=Require", StringComparison.OrdinalIgnoreCase))
            {
                b.SslMode = SslMode.Require;
                b.TrustServerCertificate = true;
            }
            return b.ConnectionString;
        }
        catch
        {
            if (value.Contains('=') && value.Contains(';', StringComparison.Ordinal)
                && !value.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase)
                && value.Contains("ondigitalocean", StringComparison.OrdinalIgnoreCase))
            {
                return value + ";SSL Mode=Require;Trust Server Certificate=true";
            }
            return value;
        }
    }
}

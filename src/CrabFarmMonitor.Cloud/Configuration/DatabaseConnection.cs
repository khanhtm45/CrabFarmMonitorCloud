using Npgsql;

namespace CrabFarmMonitor.Cloud.Configuration;

/// <summary>
/// Resolves PostgreSQL connection string from App Platform / env (DATABASE_URL or ConnectionStrings__Default).
/// </summary>
public static class DatabaseConnection
{
    public static string Resolve(IConfiguration config)
    {
        var fromParts = BuildFromDiscreteParts(config);
        if (!string.IsNullOrWhiteSpace(fromParts))
            return Normalize(fromParts);

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
        || !string.IsNullOrWhiteSpace(config.GetConnectionString("Default"))
        || !string.IsNullOrWhiteSpace(BuildFromDiscreteParts(config));

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
            if (managed || (b.SslMode == SslMode.Disable && value.Contains("SSL Mode=Require", StringComparison.OrdinalIgnoreCase)))
                b.SslMode = SslMode.Require;
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

    private static string? BuildFromDiscreteParts(IConfiguration config)
    {
        // Use DB_* / POSTGRES_* only — do NOT use bare "port" (conflicts with App Platform HTTP PORT).
        var host = First(config, "DB_HOST", "POSTGRES_HOST", "db_host");
        var port = First(config, "DB_PORT", "POSTGRES_PORT", "db_port");
        var database = First(config, "DB_NAME", "POSTGRES_DB", "db_name", "database");
        var username = First(config, "DB_USER", "POSTGRES_USER", "db_user", "username");
        var password = First(config, "DB_PASSWORD", "POSTGRES_PASSWORD", "db_password", "password");
        var sslmode = First(config, "DB_SSLMODE", "POSTGRES_SSLMODE", "db_sslmode", "sslmode");

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username))
            return null;

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.TryParse(port, out var p) ? p : 5432,
            Database = database,
            Username = username,
            Password = password ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(sslmode) && Enum.TryParse<SslMode>(sslmode, true, out var ssl))
            b.SslMode = ssl;
        return b.ConnectionString;
    }

    private static string? First(IConfiguration config, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }
}

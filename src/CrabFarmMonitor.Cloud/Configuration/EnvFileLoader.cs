namespace CrabFarmMonitor.Cloud.Configuration;

/// <summary>Đọc file .env ở thư mục gốc repo khi chạy local (dotnet run).</summary>
public static class EnvFileLoader
{
    public static void LoadFromRepoRoot()
    {
        var path = FindEnvFile();
        if (path is null)
        {
            Console.WriteLine("Env: no .env found (walk up from cwd); using environment variables only.");
            return;
        }

        ApplyFile(path);
        Console.WriteLine($"Env: loaded {path}");
    }

    static string? FindEnvFile()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, ".env");
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        return null;
    }

    static void ApplyFile(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (value.Length == 0) continue;

            key = MapLegacyKey(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>Alias cũ trong .env (host, port, …) → DB_*.</summary>
    static string MapLegacyKey(string key) => key.ToLowerInvariant() switch
    {
        "host" => "DB_HOST",
        "port" => "DB_PORT",
        "username" => "DB_USER",
        "password" => "DB_PASSWORD",
        "database" => "DB_NAME",
        "sslmode" => "DB_SSLMODE",
        _ => key
    };
}

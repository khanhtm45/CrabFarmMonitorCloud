using System.Diagnostics;
using System.Text.Json;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class CloudPythonScriptRunner
{
    private readonly string? _pythonExe;
    private readonly string _scriptsDir;

    public bool Available => _pythonExe != null;

    public CloudPythonScriptRunner(IConfiguration config)
    {
        _scriptsDir = config["SCRIPTS_DIR"]
            ?? Path.Combine(AppContext.BaseDirectory, "scripts");
        _pythonExe = ResolvePython(config["PYTHON_EXE"]);
    }

    private static string? ResolvePython(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        foreach (var name in new[] { "python3", "/usr/bin/python3", "python" })
        {
            try
            {
                var psi = new ProcessStartInfo(name, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(3000);
                    if (p.ExitCode == 0) return name;
                }
            }
            catch { /* next */ }
        }
        return null;
    }

    public async Task<JsonDocument?> RunJsonScriptAsync(
        string scriptName,
        object input,
        IDictionary<string, string>? env = null,
        CancellationToken ct = default)
    {
        if (_pythonExe == null) return null;

        var script = Path.Combine(_scriptsDir, scriptName);
        if (!File.Exists(script))
        {
            Console.WriteLine($"Script not found: {script}");
            return null;
        }

        var json = JsonSerializer.Serialize(input);
        var psi = new ProcessStartInfo(_pythonExe, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (env != null)
        {
            foreach (var kv in env)
                psi.Environment[kv.Key] = kv.Value;
        }

        using var proc = Process.Start(psi);
        if (proc == null) return null;

        await proc.StandardInput.WriteAsync(json.AsMemory(), ct);
        proc.StandardInput.Close();

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            Console.WriteLine($"[{scriptName}] exit {proc.ExitCode}: {stderr}");
            return null;
        }

        if (string.IsNullOrWhiteSpace(stdout)) return null;
        return JsonDocument.Parse(stdout);
    }
}

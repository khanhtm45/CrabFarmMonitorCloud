using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CrabFarmMonitor.Cloud.Configuration;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;
using CrabFarmMonitor.Cloud.Services;
using CrabFarmMonitor.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024 * 1024);

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "ras-dev-jwt-secret-change-in-production-32chars";

builder.Services.AddDbContext<RasCloudDbContext>(opt =>
{
    opt.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration));
});
builder.Services.AddSingleton<ObjectStorageService>();
builder.Services.AddSingleton<CloudPythonScriptRunner>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<TelemetryIngestService>();
builder.Services.AddScoped<TelemetryHistoryService>();
builder.Services.AddHostedService<TelemetrySamplesRetentionService>();
builder.Services.AddScoped<Hdf5SyncService>();
builder.Services.AddScoped<Hdf5CloudBrowseService>();
builder.Services.AddScoped<FarmScopeService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CrabBoxService>();
builder.Services.AddScoped<WaterAlertService>();
builder.Services.AddScoped<DeviceShadowService>();
builder.Services.AddSingleton<CloudMetricsCollector>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "ras-iot-cloud",
            ValidateAudience = true,
            ValidAudience = "ras-dashboard",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration["CORS_ORIGINS"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                if (corsOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                    return true;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return uri.Host is "localhost" or "127.0.0.1"
                    || uri.Host.EndsWith(".duckdns.org", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.StartsWith("192.168.", StringComparison.Ordinal)
                    || uri.Host.StartsWith("172.", StringComparison.Ordinal)
                    || uri.Host.StartsWith("10.", StringComparison.Ordinal);
            })
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RasCloudDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    try
    {
        if (await db.Database.CanConnectAsync())
        {
            Console.WriteLine("PostgreSQL: connected");
            await DevBootstrapService.EnsureAsync(db, config);
            await CrabSchemaBootstrap.EnsureAsync(db, config);
            await DomainSchemaBootstrap.EnsureAsync(db);
            await TelemetrySamplesBootstrap.EnsureAsync(db);
        }
        else
            Console.WriteLine("PostgreSQL: cannot connect");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"PostgreSQL: {ex.Message}");
    }

    var s3 = scope.ServiceProvider.GetRequiredService<ObjectStorageService>();
    if (s3.Enabled)
    {
        try
        {
            await s3.EnsureBucketAsync();
            Console.WriteLine("S3/MinIO: ready");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"S3/MinIO: {ex.Message}");
        }
    }
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var apiKey = builder.Configuration["TELEMETRY_API_KEY"]?.Trim();
bool CheckKey(HttpRequest req)
{
    if (string.IsNullOrEmpty(apiKey)) return true;
    var key = req.Headers["X-API-Key"].FirstOrDefault();
    return key == apiKey;
}

app.MapGet("/health", () => Results.Json(new { ok = true, service = "ras-iot-cloud" }));

app.MapGet("/metrics", (CloudMetricsCollector metrics) =>
    Results.Text(metrics.RenderPrometheus(), "text/plain; version=0.0.4"));

app.MapGet("/", () => Results.Json(new
{
    ok = true,
    service = "ras-iot-cloud",
    role = "BE trung tâm: Edge → telemetry + HDF5, dashboard JWT",
    endpoints = new[]
    {
        "POST /api/auth/login",
        "GET /api/auth/me (Bearer)",
        "GET /api/dashboard/summary (Bearer)",
        "GET /api/farms",
        "GET /api/devices",
        "POST /api/telemetry (Edge, X-API-Key)",
        "POST /api/sync/hdf5 (Edge)",
        "GET /api/hdf5/uploads (Bearer)",
        "GET /api/hdf5/rows?uploadId= (Bearer)",
        "GET /api/telemetry/realtime",
        "GET /api/telemetry/history?mac=&minutes=&pin= (Bearer)"
    },
    devLogin = new { email = "admin@iras.local", password = "123456" }
}));

app.MapPost("/api/auth/login", async (LoginRequest body, AuthService auth, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { ok = false, error = "email and password required" });
    var result = await auth.LoginAsync(body.Email, body.Password, ct);
    return result == null
        ? Results.Json(new { ok = false, error = "invalid credentials" }, statusCode: 401)
        : Results.Json(result);
});

app.MapGet("/api/auth/me", async (ClaimsPrincipal user, RasCloudDbContext db, FarmScopeService scope, CancellationToken ct) =>
{
    var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(id, out var uid)) return Results.Unauthorized();
    var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
    if (u == null) return Results.NotFound();

    var access = await scope.LoadAsync(user, ct);
    var farms = access == null
        ? []
        : await db.Farms.AsNoTracking()
            .Where(f => f.OrgId == access.OrgId)
            .OrderBy(f => f.Code)
            .Select(f => new { f.Id, f.Code, f.Name })
            .ToListAsync(ct);

    Guid? defaultFarmId = access == null ? null : scope.DefaultFarmId(access);

    return Results.Json(new
    {
        ok = true,
        user = new { u.Id, u.Email, u.DisplayName, u.Role, u.OrgId },
        farms,
        isOrgAdmin = access?.IsOrgAdmin ?? false,
        defaultFarmId
    });
}).RequireAuthorization();

var dashboard = app.MapGroup("/api").RequireAuthorization();

dashboard.MapGet("/dashboard/summary", async (
    Guid? farmId,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    RasCloudDbContext db,
    ObjectStorageService s3,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);

    var deviceQuery = db.Devices.AsNoTracking().Where(d => scopeIds.Contains(d.FarmId));
    if (fid.HasValue)
        deviceQuery = deviceQuery.Where(d => d.FarmId == fid.Value);

    var devices = await deviceQuery.ToListAsync(ct);
    var uploads = await db.Hdf5Uploads.AsNoTracking()
        .CountAsync(u => scopeIds.Contains(u.FarmId) && (!fid.HasValue || u.FarmId == fid.Value), ct);
    var latest = await db.TelemetryLatest.AsNoTracking()
        .Where(t => scopeIds.Contains(t.FarmId) && (!fid.HasValue || t.FarmId == fid.Value))
        .GroupBy(t => t.DeviceId)
        .Select(g => new { deviceId = g.Key, pins = g.Count(), updated = g.Max(x => x.ReceivedAt) })
        .ToListAsync(ct);

    object farmInfo;
    if (fid.HasValue)
    {
        var farm = await db.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid.Value, ct);
        if (farm == null) return Results.NotFound(new { ok = false, error = "farm not found" });
        farmInfo = new { farm.Id, farm.Code, farm.Name };
    }
    else
    {
        farmInfo = new { id = Guid.Empty, code = "ALL", name = "Tất cả trại" };
    }

    return Results.Json(new
    {
        ok = true,
        farm = farmInfo,
        scopeFarmCount = scopeIds.Count,
        deviceCount = devices.Count,
        hdf5Uploads = uploads,
        storage = s3.GetStatus(),
        devices = devices.Select(d => new
        {
            d.Id,
            d.DeviceCode,
            mac = d.MacAddress?.ToString(),
            d.LastTelemetryAt,
            d.FarmId,
            telemetryPins = latest.FirstOrDefault(x => x.deviceId == d.Id)?.pins ?? 0
        }),
        telemetryGroups = latest
    });
});

dashboard.MapGet("/farms", async (ClaimsPrincipal user, RasCloudDbContext db, FarmScopeService scope, CancellationToken ct) =>
{
    var access = await scope.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var farms = await db.Farms.AsNoTracking()
        .Where(f => f.OrgId == access.OrgId)
        .OrderBy(f => f.Code)
        .Select(f => new { f.Id, f.Code, f.Name, f.OrgId })
        .ToListAsync(ct);
    return Results.Json(new { ok = true, farms, isOrgAdmin = access.IsOrgAdmin });
});

dashboard.MapGet("/devices/{mac}/shadow", async (
    string mac,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    DeviceShadowService shadowSvc,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (_, scopeIds, err) = scopeSvc.Resolve(access, null);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    var shadow = await shadowSvc.GetByMacAsync(mac, scopeIds, ct);
    return shadow == null ? Results.NotFound(new { ok = false }) : Results.Json(new { ok = true, shadow });
});

dashboard.MapPut("/devices/{mac}/shadow/desired", async (
    string mac,
    JsonElement body,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    DeviceShadowService shadowSvc,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (_, scopeIds, err) = scopeSvc.Resolve(access, null);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    var shadow = await shadowSvc.PutDesiredByMacAsync(mac, body, scopeIds, ct);
    return shadow == null ? Results.NotFound(new { ok = false }) : Results.Json(new { ok = true, shadow });
});

dashboard.MapGet("/devices", async (
    Guid? farmId,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    RasCloudDbContext db,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);

    var q = db.Devices.AsNoTracking().Where(d => scopeIds.Contains(d.FarmId));
    if (fid.HasValue)
        q = q.Where(d => d.FarmId == fid.Value);

    var rows = await q
        .Select(d => new { d.Id, d.DeviceCode, d.MacAddress, d.LastTelemetryAt, d.FarmId })
        .ToListAsync(ct);
    return Results.Json(new { ok = true, farmId = fid, devices = rows });
});

dashboard.MapGet("/telemetry/history", async (
    string? mac,
    int? minutes,
    int? pin,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    TelemetryHistoryService hist,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(mac))
        return Results.BadRequest(new { ok = false, error = "mac required" });
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (_, scopeIds, err) = scopeSvc.Resolve(access, null);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);

    var result = await hist.QueryAsync(mac.Trim(), minutes ?? 60, pin, scopeIds, ct);
    return result == null
        ? Results.NotFound(new { ok = false, error = "device not found" })
        : Results.Json(result);
});

dashboard.MapGet("/water/alerts", async (
    Guid? farmId,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    WaterAlertService alerts,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);

    if (fid.HasValue)
    {
        var list = await alerts.GetAlertsAsync(fid.Value, ct);
        return Results.Json(new { ok = true, farmId = fid, alerts = list });
    }

    var all = new List<object>();
    foreach (var f in scopeIds)
        all.AddRange(await alerts.GetAlertsAsync(f, ct));
    return Results.Json(new { ok = true, farmId = (Guid?)null, alerts = all });
});

dashboard.MapGet("/crab-boxes", async (
    Guid? farmId,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    CrabBoxService svc,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);

    if (fid.HasValue)
    {
        var boxes = await svc.ListBoxesAsync(fid.Value, ct);
        return Results.Json(new { ok = true, farmId = fid, boxes });
    }

    var merged = new List<object>();
    foreach (var f in scopeIds)
        merged.AddRange(await svc.ListBoxesAsync(f, ct));
    return Results.Json(new { ok = true, farmId = (Guid?)null, boxes = merged });
});

dashboard.MapGet("/crab-boxes/{id:guid}", async (Guid id, CrabBoxService svc, CancellationToken ct) =>
{
    var box = await svc.GetBoxAsync(id, ct);
    return box == null ? Results.NotFound() : Results.Json(new { ok = true, box });
});

dashboard.MapPost("/crab-boxes", async (
    Guid? farmId,
    CreateBoxRequest body,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    CrabBoxService svc,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, _, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    if (!fid.HasValue)
        return Results.BadRequest(new { ok = false, error = "farmId required" });
    var box = await svc.CreateBoxAsync(fid.Value, body, ct);
    return Results.Json(new { ok = true, box }, statusCode: StatusCodes.Status201Created);
});

dashboard.MapPut("/crab-boxes/{id:guid}", async (Guid id, UpdateBoxRequest body, CrabBoxService svc, CancellationToken ct) =>
{
    var box = await svc.UpdateBoxAsync(id, body, ct);
    return box == null ? Results.NotFound() : Results.Json(new { ok = true, box });
});

dashboard.MapPost("/crab-boxes/{id:guid}/crabs", async (Guid id, CreateCrabRequest body, CrabBoxService svc, CancellationToken ct) =>
{
    var crab = await svc.AddCrabAsync(id, body, ct);
    return crab == null ? Results.NotFound() : Results.Json(new { ok = true, crab }, statusCode: StatusCodes.Status201Created);
});

dashboard.MapPut("/crabs/{id:guid}", async (Guid id, UpdateCrabRequest body, CrabBoxService svc, CancellationToken ct) =>
{
    var crab = await svc.UpdateCrabAsync(id, body, ct);
    return crab == null ? Results.NotFound() : Results.Json(new { ok = true, crab });
});

dashboard.MapDelete("/crabs/{id:guid}", async (Guid id, CrabBoxService svc, CancellationToken ct) =>
{
    var ok = await svc.DeleteCrabAsync(id, ct);
    return ok ? Results.NoContent() : Results.NotFound();
});

dashboard.MapGet("/hdf5/uploads", async (
    Guid? farmId,
    int? limit,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    Hdf5CloudBrowseService browse,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    return Results.Json(await browse.ListUploadsAsync(scopeIds, fid, limit ?? 50, ct));
});

dashboard.MapGet("/hdf5/rows", async (
    Guid uploadId,
    int? limit,
    int? pin,
    long? fromMs,
    long? toMs,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    Hdf5CloudBrowseService browse,
    CancellationToken ct) =>
{
    if (uploadId == Guid.Empty)
        return Results.BadRequest(new { ok = false, error = "uploadId required" });
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (_, scopeIds, err) = scopeSvc.Resolve(access, null);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    var query = new Hdf5ReadQuery
    {
        Limit = limit ?? 5000,
        Pin = pin,
        FromMs = fromMs,
        ToMs = toMs
    };
    var result = await browse.ReadRowsAsync(uploadId, scopeIds, query, ct);
    if (result == null)
        return Results.NotFound(new { ok = false, error = "upload not found" });
    return Results.Json(result);
});

app.MapPost("/api/telemetry", async (HttpRequest req, TelemetryIngestService svc, CloudMetricsCollector metrics, CancellationToken ct) =>
{
    if (!CheckKey(req)) return Results.Unauthorized();
    var payload = await req.ReadFromJsonAsync<TelemetryPayload>(ct);
    if (payload?.Readings == null || payload.Readings.Count == 0)
        return Results.BadRequest(new { ok = false, error = "readings required" });
    var farmId = FarmContext.ResolveFarmId(req, builder.Configuration);
    var device = await svc.ResolveDeviceAsync(payload, farmId, ct);
    await svc.UpsertLatestAsync(device, payload, ct);
    metrics.IncTelemetry();
    return Results.Json(new { ok = true, farmId, deviceId = device.Id, deviceCode = device.DeviceCode },
        statusCode: StatusCodes.Status201Created);
});

app.MapPost("/telemetry", async (HttpRequest req, TelemetryIngestService svc, CloudMetricsCollector metrics, CancellationToken ct) =>
{
    if (!CheckKey(req)) return Results.Unauthorized();
    var payload = await req.ReadFromJsonAsync<TelemetryPayload>(ct);
    if (payload?.Readings == null || payload.Readings.Count == 0)
        return Results.BadRequest(new { ok = false, error = "readings required" });
    var farmId = FarmContext.ResolveFarmId(req, builder.Configuration);
    var device = await svc.ResolveDeviceAsync(payload, farmId, ct);
    await svc.UpsertLatestAsync(device, payload, ct);
    metrics.IncTelemetry();
    return Results.Json(new { ok = true }, statusCode: StatusCodes.Status201Created);
});

app.MapPost("/api/sync/hdf5", async (HttpRequest req, Hdf5SyncService sync, CancellationToken ct) =>
{
    if (!CheckKey(req)) return Results.Unauthorized();
    if (!req.HasFormContentType)
        return Results.BadRequest(new { ok = false, error = "multipart required" });

    var form = await req.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { ok = false, error = "file required" });

    var mac = form["mac"].FirstOrDefault() ?? req.Headers["X-Device-Mac"].FirstOrDefault();
    var deviceCode = form["device_code"].FirstOrDefault();
    var idem = form["idempotency_key"].FirstOrDefault() ?? req.Headers["X-Idempotency-Key"].FirstOrDefault();
    long? chunkStart = long.TryParse(form["chunk_start_ms"].FirstOrDefault(), out var cs) ? cs : null;
    long? chunkEnd = long.TryParse(form["chunk_end_ms"].FirstOrDefault(), out var ce) ? ce : null;
    var farmId = FarmContext.ResolveFarmId(req, builder.Configuration);

    var result = await sync.SaveUploadAsync(file, farmId, mac, deviceCode, idem, chunkStart, chunkEnd, ct);
    return Results.Json(result, statusCode: StatusCodes.Status201Created);
});

app.MapGet("/api/storage/status", (ObjectStorageService s3) =>
    Results.Json(new { ok = true, storage = s3.GetStatus() }));

app.MapGet("/api/sync/uploads", async (
    Guid? farmId,
    int? limit,
    ClaimsPrincipal user,
    FarmScopeService scopeSvc,
    Hdf5CloudBrowseService browse,
    CancellationToken ct) =>
{
    var access = await scopeSvc.LoadAsync(user, ct);
    if (access == null) return Results.Unauthorized();
    var (fid, scopeIds, err) = scopeSvc.Resolve(access, farmId);
    if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
    return Results.Json(await browse.ListUploadsAsync(scopeIds, fid, limit ?? 50, ct));
}).RequireAuthorization();

app.MapGet("/api/telemetry/realtime", async (string? mac, RasCloudDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(mac))
        return Results.BadRequest(new { ok = false, error = "mac required" });
    var norm = MacNormalizer.Normalize(mac);
    var dev = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.MacAddress == norm, ct);
    if (dev == null) return Results.NotFound(new { ok = false, error = "device not found" });
    var rows = await db.TelemetryLatest.AsNoTracking()
        .Where(t => t.DeviceId == dev.Id)
        .ToListAsync(ct);
    var readings = rows.ToDictionary(
        r => PinLabels.Label(r.Pin),
        r => r.Val);
    return Results.Json(new
    {
        ok = true,
        mac = norm,
        deviceCode = dev.DeviceCode,
        readings,
        pins = rows.Select(r => new { r.Pin, label = PinLabels.Label(r.Pin), r.Val, r.RecordedAt })
    });
});

app.MapGet("/api/telemetry/latest", async (Guid? farmId, string? mac, RasCloudDbContext db, CancellationToken ct) =>
{
    IQueryable<TelemetryLatest> q = db.TelemetryLatest.AsNoTracking();
    if (farmId.HasValue)
        q = q.Where(t => t.FarmId == farmId.Value);
    if (!string.IsNullOrWhiteSpace(mac))
    {
        var norm = MacNormalizer.Normalize(mac);
        var dev = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.MacAddress == norm, ct);
        if (dev == null) return Results.NotFound();
        q = q.Where(t => t.DeviceId == dev.Id);
    }
    var rows = await q.ToListAsync(ct);
    return Results.Json(new { ok = true, data = rows });
});

var port = builder.Configuration["PORT"] ?? "8080";
Console.WriteLine($"RAS Cloud listening on 0.0.0.0:{port}");
app.Run($"http://0.0.0.0:{port}");

record LoginRequest(string Email, string Password);

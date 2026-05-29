using System.Security.Claims;
using CrabFarmMonitor.Cloud.Services;
using CrabFarmMonitor.Shared;

namespace CrabFarmMonitor.Cloud;

public static class DomainApiEndpoints
{
    public static void MapDomainApis(
        this WebApplication app,
        Func<HttpRequest, bool> checkApiKey,
        Func<HttpRequest, IConfiguration, Guid> resolveFarmId)
    {
        var config = app.Configuration;

        app.MapPost("/api/sync/sensor-batch", async (
            HttpRequest req,
            SensorBatchSyncDto body,
            SensorBatchSyncService sync,
            CancellationToken ct) =>
        {
            if (!checkApiKey(req)) return Results.Unauthorized();
            if (body.Items == null || body.Items.Count == 0)
                return Results.BadRequest(new { ok = false, error = "items required" });
            try
            {
                var farmId = resolveFarmId(req, config);
                var result = await sync.IngestAsync(body, farmId, ct);
                return Results.Json(new { ok = true, farmId, result }, statusCode: StatusCodes.Status201Created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, error = ex.Message });
            }
        });

        var dashboard = app.MapGroup("/api").RequireAuthorization();

        dashboard.MapGet("/areas", async (
            Guid? farmId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var (fid, _, err) = scopeSvc.Resolve(access, farmId);
            if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
            if (!fid.HasValue)
                return Results.BadRequest(new { ok = false, error = "farmId required" });
            var areas = await layout.ListAreasAsync(fid.Value, ct);
            return Results.Json(new { ok = true, farmId = fid, areas });
        });

        dashboard.MapPost("/areas", async (
            Guid? farmId,
            UpsertAreaRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var (fid, _, err) = scopeSvc.Resolve(access, farmId);
            if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
            if (!fid.HasValue)
                return Results.BadRequest(new { ok = false, error = "farmId required" });
            var area = await layout.CreateAreaAsync(fid.Value, body, ct);
            return Results.Json(new { ok = true, area }, statusCode: StatusCodes.Status201Created);
        });

        dashboard.MapPut("/areas/{id:guid}", async (
            Guid id,
            UpsertAreaRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var area = await layout.GetAreaAsync(id, ct);
            if (area == null) return Results.NotFound();
            if (!access.FarmIds.Contains(area.FarmId))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var updated = await layout.UpdateAreaAsync(id, body, ct);
            return Results.Json(new { ok = true, area = updated });
        });

        dashboard.MapDelete("/areas/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var area = await layout.GetAreaAsync(id, ct);
            if (area == null) return Results.NotFound();
            if (!access.FarmIds.Contains(area.FarmId))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var ok = await layout.DeleteAreaAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        dashboard.MapGet("/areas/{areaId:guid}/rows", async (
            Guid areaId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var area = await layout.GetAreaAsync(areaId, ct);
            if (area == null) return Results.NotFound();
            if (!access.FarmIds.Contains(area.FarmId))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var rows = await layout.ListRowsAsync(areaId, ct);
            return Results.Json(new { ok = true, areaId, rows });
        });

        dashboard.MapPost("/areas/{areaId:guid}/rows", async (
            Guid areaId,
            UpsertRowRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var area = await layout.GetAreaAsync(areaId, ct);
            if (area == null) return Results.NotFound();
            if (!access.FarmIds.Contains(area.FarmId))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var row = await layout.CreateRowAsync(areaId, body, ct);
            return row == null
                ? Results.NotFound()
                : Results.Json(new { ok = true, row }, statusCode: StatusCodes.Status201Created);
        });

        dashboard.MapPut("/rows/{id:guid}", async (
            Guid id,
            UpsertRowRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var row = await layout.UpdateRowAsync(id, body, ct);
            return row == null ? Results.NotFound() : Results.Json(new { ok = true, row });
        });

        dashboard.MapDelete("/rows/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var ok = await layout.DeleteRowAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        dashboard.MapGet("/devices/{deviceId:guid}/wifi", async (
            Guid deviceId,
            DeviceConfigService cfg,
            CancellationToken ct) =>
        {
            var wifi = await cfg.GetWifiAsync(deviceId, ct);
            return wifi == null ? Results.NotFound() : Results.Json(new { ok = true, wifi });
        });

        dashboard.MapPut("/devices/{deviceId:guid}/wifi", async (
            Guid deviceId,
            UpsertWifiRequest body,
            DeviceConfigService cfg,
            CancellationToken ct) =>
        {
            if (await cfg.GetDeviceAsync(deviceId, ct) == null) return Results.NotFound();
            var wifi = await cfg.UpsertWifiAsync(deviceId, body, ct);
            return Results.Json(new { ok = true, wifi });
        });

        dashboard.MapGet("/devices/{deviceId:guid}/mqtt", async (
            Guid deviceId,
            DeviceConfigService cfg,
            CancellationToken ct) =>
        {
            var mqtt = await cfg.GetMqttAsync(deviceId, ct);
            return mqtt == null ? Results.NotFound() : Results.Json(new { ok = true, mqtt });
        });

        dashboard.MapPut("/devices/{deviceId:guid}/mqtt", async (
            Guid deviceId,
            UpsertMqttRequest body,
            DeviceConfigService cfg,
            CancellationToken ct) =>
        {
            if (await cfg.GetDeviceAsync(deviceId, ct) == null) return Results.NotFound();
            var mqtt = await cfg.UpsertMqttAsync(deviceId, body, ct);
            return Results.Json(new { ok = true, mqtt });
        });

        dashboard.MapGet("/cameras", async (
            Guid? farmId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var (fid, _, err) = scopeSvc.Resolve(access, farmId);
            if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
            if (!fid.HasValue)
                return Results.BadRequest(new { ok = false, error = "farmId required" });
            var cameras = await cam.ListCamerasAsync(fid.Value, ct);
            return Results.Json(new { ok = true, farmId = fid, cameras });
        });

        dashboard.MapGet("/cameras/{cameraId:guid}/snapshots", async (
            Guid cameraId,
            int? limit,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            var snaps = await cam.ListSnapshotsAsync(cameraId, limit ?? 50, ct);
            return Results.Json(new { ok = true, cameraId, snapshots = snaps });
        });

        dashboard.MapGet("/cameras/{cameraId:guid}/analysis", async (
            Guid cameraId,
            int? limit,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            var rows = await cam.ListAnalysisAsync(cameraId, limit ?? 50, ct);
            return Results.Json(new { ok = true, cameraId, analysis = rows });
        });

        dashboard.MapGet("/ai/alerts", async (
            Guid? farmId,
            int? limit,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var (fid, _, err) = scopeSvc.Resolve(access, farmId);
            if (err != null) return Results.Json(new { ok = false, error = err }, statusCode: 403);
            if (!fid.HasValue)
                return Results.BadRequest(new { ok = false, error = "farmId required" });
            var alerts = await cam.ListAiAlertsAsync(fid.Value, limit ?? 100, ct);
            return Results.Json(new { ok = true, farmId = fid, alerts });
        });

        app.MapPost("/api/sync/camera/snapshot", async (
            HttpRequest req,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            if (!checkApiKey(req)) return Results.Unauthorized();
            if (!req.HasFormContentType)
                return Results.BadRequest(new { ok = false, error = "multipart required" });
            var form = await req.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var gatewayCode = form["gatewayId"].FirstOrDefault() ?? form["gateway_id"].FirstOrDefault();
            var cameraCode = form["cameraCode"].FirstOrDefault() ?? form["camera_code"].FirstOrDefault();
            if (file == null || string.IsNullOrWhiteSpace(gatewayCode) || string.IsNullOrWhiteSpace(cameraCode))
                return Results.BadRequest(new { ok = false, error = "file, gatewayId, cameraCode required" });
            var farmId = resolveFarmId(req, config);
            var snap = await cam.UploadSnapshotAsync(farmId, gatewayCode, cameraCode, file, ct);
            return snap == null
                ? Results.NotFound(new { ok = false, error = "camera not found — register camera first" })
                : Results.Json(new { ok = true, snapshot = snap }, statusCode: StatusCodes.Status201Created);
        });

        app.MapPost("/api/sync/camera/analysis", async (
            HttpRequest req,
            SubmitAiAnalysisRequest body,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            if (!checkApiKey(req)) return Results.Unauthorized();
            var farmId = resolveFarmId(req, config);
            var result = await cam.SubmitAnalysisAsync(farmId, body, ct);
            return result == null
                ? Results.NotFound(new { ok = false, error = "camera not found" })
                : Results.Json(new { ok = true, data = result }, statusCode: StatusCodes.Status201Created);
        });

        app.MapPost("/api/sync/camera/register", async (
            HttpRequest req,
            UpsertCameraRequest body,
            string gatewayId,
            CameraAiService cam,
            CancellationToken ct) =>
        {
            if (!checkApiKey(req)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(gatewayId))
                return Results.BadRequest(new { ok = false, error = "gatewayId query required" });
            var farmId = resolveFarmId(req, config);
            var camera = await cam.RegisterCameraAsync(farmId, gatewayId, body, ct);
            return camera == null
                ? Results.NotFound(new { ok = false, error = "gateway not found" })
                : Results.Json(new { ok = true, camera }, statusCode: StatusCodes.Status201Created);
        });

        app.MapGet("/api/media/{**path}", (string path, LocalMediaStorage media) =>
        {
            var physical = media.ResolvePhysicalPath("local://" + path);
            if (physical == null || !File.Exists(physical))
                return Results.NotFound();
            var ext = Path.GetExtension(physical).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            return Results.File(physical, contentType);
        });
    }
}

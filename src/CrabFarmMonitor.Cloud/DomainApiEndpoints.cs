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
            var areas = await layout.ListAreasWithStatsAsync(fid.Value, ct);
            var summary = new
            {
                total = areas.Count,
                active = areas.Count(a => a.Status == "active"),
                maintenance = areas.Count(a => a.Status == "maintenance"),
                disabled = areas.Count(a => a.Status == "disabled"),
                totalBoxes = areas.Sum(a => a.BoxCount)
            };
            return Results.Json(new { ok = true, farmId = fid, areas, summary });
        });

        dashboard.MapGet("/areas/{id:guid}/detail", async (
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
            var detail = await layout.GetAreaDetailStatsAsync(id, ct);
            var rows = await layout.ListRowsAsync(id, ct);
            var boxes = await layout.ListBoxesByAreaAsync(id, ct);
            return Results.Json(new { ok = true, detail, rows, boxes });
        });

        dashboard.MapGet("/areas/next-code", async (
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
            var code = await layout.GenerateNextAreaCodeAsync(fid.Value, ct);
            return Results.Json(new { ok = true, farmId = fid, code });
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

        dashboard.MapGet("/areas/{areaId:guid}/rows/next-code", async (
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
            var code = await layout.GenerateNextRowCodeAsync(areaId, ct);
            return Results.Json(new { ok = true, areaId, code });
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

        dashboard.MapGet("/rows/{rowId:guid}/boxes", async (
            Guid rowId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var boxes = await production.ListBoxesAsync(rowId, ct);
            return Results.Json(new { ok = true, rowId, boxes });
        });

        dashboard.MapGet("/rows/{rowId:guid}/boxes/next-code", async (
            Guid rowId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var code = await production.GenerateNextBoxCodeAsync(rowId, ct);
            return Results.Json(new { ok = true, rowId, code });
        });

        dashboard.MapPost("/rows/{rowId:guid}/boxes", async (
            Guid rowId,
            UpsertBoxRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var box = await production.CreateBoxAsync(rowId, body, ct);
            return box == null
                ? Results.NotFound()
                : Results.Json(new { ok = true, box }, statusCode: StatusCodes.Status201Created);
        });

        dashboard.MapPost("/rows/{rowId:guid}/boxes/bulk", async (
            Guid rowId,
            BulkCreateBoxesRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            try
            {
                var boxes = await production.CreateBoxesBulkAsync(rowId, body, ct);
                if (boxes.Count == 0)
                    return Results.NotFound();
                return Results.Json(
                    new { ok = true, count = boxes.Count, boxes },
                    statusCode: StatusCodes.Status201Created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, error = ex.Message });
            }
        });

        dashboard.MapPut("/boxes/{id:guid}", async (
            Guid id,
            UpsertBoxRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BoxFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var box = await production.UpdateBoxAsync(id, body, ct);
            return box == null ? Results.NotFound() : Results.Json(new { ok = true, box });
        });

        dashboard.MapDelete("/boxes/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BoxFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var ok = await production.DeleteBoxAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        dashboard.MapGet("/rows/{rowId:guid}/batches", async (
            Guid rowId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var batches = await production.ListBatchesByRowAsync(rowId, ct);
            return Results.Json(new { ok = true, rowId, batches });
        });

        dashboard.MapPost("/rows/{rowId:guid}/batches/bulk", async (
            Guid rowId,
            BulkCreateBatchesRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            FarmLayoutService layout,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await layout.RowFarmIdAsync(rowId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            if (body.BoxIds == null || body.BoxIds.Count == 0)
                return Results.BadRequest(new { ok = false, error = "boxIds required" });
            try
            {
                var batches = await production.CreateBatchesBulkAsync(body, ct);
                return Results.Json(
                    new { ok = true, count = batches.Count, batches },
                    statusCode: StatusCodes.Status201Created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { ok = false, error = ex.Message });
            }
        });

        dashboard.MapGet("/boxes/{boxId:guid}/batches", async (
            Guid boxId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BoxFarmIdAsync(boxId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var batches = await production.ListBatchesAsync(boxId, ct);
            return Results.Json(new { ok = true, boxId, batches });
        });

        dashboard.MapGet("/boxes/{boxId:guid}/batches/next-code", async (
            Guid boxId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BoxFarmIdAsync(boxId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var code = await production.GenerateNextBatchCodeAsync(boxId, ct);
            return Results.Json(new { ok = true, boxId, code });
        });

        dashboard.MapPost("/boxes/{boxId:guid}/batches", async (
            Guid boxId,
            UpsertFarmingBatchRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BoxFarmIdAsync(boxId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var batch = await production.CreateBatchAsync(boxId, body, ct);
            return batch == null
                ? Results.NotFound()
                : Results.Json(new { ok = true, batch }, statusCode: StatusCodes.Status201Created);
        });

        dashboard.MapPut("/farming-batches/{id:guid}", async (
            Guid id,
            UpsertFarmingBatchRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var batch = await production.UpdateBatchAsync(id, body, ct);
            return batch == null ? Results.NotFound() : Results.Json(new { ok = true, batch });
        });

        dashboard.MapDelete("/farming-batches/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var ok = await production.DeleteBatchAsync(id, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        dashboard.MapGet("/farming-batches/{batchId:guid}/crabs", async (
            Guid batchId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchFarmIdAsync(batchId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var crabs = await production.ListBatchCrabsAsync(batchId, ct);
            return Results.Json(new { ok = true, batchId, crabs });
        });

        dashboard.MapGet("/farming-batches/{batchId:guid}/crabs/next-code", async (
            Guid batchId,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchFarmIdAsync(batchId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var code = await production.GenerateNextCrabCodeAsync(batchId, ct);
            return Results.Json(new { ok = true, batchId, code });
        });

        dashboard.MapPost("/farming-batches/{batchId:guid}/crabs", async (
            Guid batchId,
            UpsertBatchCrabRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchFarmIdAsync(batchId, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var crab = await production.CreateBatchCrabAsync(batchId, body, ct);
            return crab == null
                ? Results.NotFound()
                : Results.Json(new { ok = true, crab }, statusCode: StatusCodes.Status201Created);
        });

        dashboard.MapPut("/batch-crabs/{id:guid}", async (
            Guid id,
            UpsertBatchCrabRequest body,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchCrabFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var crab = await production.UpdateBatchCrabAsync(id, body, ct);
            return crab == null ? Results.NotFound() : Results.Json(new { ok = true, crab });
        });

        dashboard.MapDelete("/batch-crabs/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            FarmScopeService scopeSvc,
            ProductionService production,
            CancellationToken ct) =>
        {
            var access = await scopeSvc.LoadAsync(user, ct);
            if (access == null) return Results.Unauthorized();
            var farmId = await production.BatchCrabFarmIdAsync(id, ct);
            if (!farmId.HasValue || !access.FarmIds.Contains(farmId.Value))
                return Results.Json(new { ok = false, error = "forbidden" }, statusCode: 403);
            var ok = await production.DeleteBatchCrabAsync(id, ct);
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

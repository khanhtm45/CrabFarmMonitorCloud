namespace CrabFarmMonitor.Cloud.Services;

public sealed record UpsertFarmRequest(
    string? Code,
    string Name,
    string? Address = null,
    string? Description = null);

public sealed record UpsertAreaRequest(
    string? AreaCode,
    string AreaName,
    string? Description,
    string Status = "active");

public sealed record AreaListItem(
    Guid Id,
    Guid FarmId,
    string AreaCode,
    string AreaName,
    string? Description,
    string Status,
    DateTime CreatedAt,
    int RowCount,
    int BoxCount);

public sealed record AreaDetailStats(
    Guid Id,
    Guid FarmId,
    string AreaCode,
    string AreaName,
    string? Description,
    string Status,
    DateTime CreatedAt,
    int RowCount,
    int BoxCount,
    int Esp32Count,
    int CameraCount);

public sealed record UpsertRowRequest(string? RowCode, string RowName);

public sealed record UpsertBoxRequest(
    string? BoxCode,
    string? Position = null,
    double? Volume = null,
    string Status = "empty");

public sealed record BulkCreateBoxesRequest(
    int Count,
    string? PositionPrefix = null,
    double? Volume = null,
    string Status = "empty");

public sealed record UpsertFarmingBatchRequest(
    string? BatchCode,
    DateOnly StartDate,
    DateOnly? ExpectedHarvestDate = null,
    DateOnly? ActualHarvestDate = null,
    int InitialQuantity = 0,
    int CurrentQuantity = 0,
    string Status = "active",
    bool StartNow = false);

public sealed record BulkCreateBatchesRequest(
    List<Guid> BoxIds,
    DateOnly StartDate,
    DateOnly? ExpectedHarvestDate = null,
    int InitialQuantity = 0,
    bool StartNow = true,
    string Status = "active");

public sealed record FarmingBatchListItem(
    Guid Id,
    Guid BoxId,
    string BoxCode,
    string BatchCode,
    DateOnly StartDate,
    DateOnly? ExpectedHarvestDate,
    DateOnly? ActualHarvestDate,
    int InitialQuantity,
    int CurrentQuantity,
    string Status);

public sealed record UpsertBatchCrabRequest(
    string? CrabCode,
    string Gender = "unknown",
    double? Weight = null,
    double? ShellWidth = null,
    string Status = "alive");

public sealed record UpsertWifiRequest(
    string Ssid,
    string? Password,
    string IpMode = "dhcp",
    string? LocalIp = null,
    string? SubnetMask = null,
    string? Gateway = null);

public sealed record UpsertMqttRequest(
    string BrokerUrl,
    int Port = 1883,
    string? Username = null,
    string? Password = null,
    string? PublishTopic = null,
    string? SubscribeTopic = null,
    int Qos = 0,
    bool SslEnable = false);

public sealed record UpsertCameraRequest(
    string CameraCode,
    string Name,
    Guid? BoxId = null,
    string? StreamUrl = null,
    string? IpAddress = null);

public sealed record SubmitAiAnalysisRequest(
    string CameraCode,
    string GatewayId,
    string ModelType,
    string ResultType,
    decimal? Confidence,
    object? ResultData,
    string? ImageUrl,
    DateTime? AnalyzedAt,
    AiAlertPayload? Alert);

public sealed record AiAlertPayload(
    string AlertType,
    string Severity,
    string Message);

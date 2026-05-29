namespace CrabFarmMonitor.Cloud.Services;

public sealed record UpsertFarmRequest(
    string? Code,
    string Name,
    string? Address = null,
    string? Description = null);

public sealed record UpsertAreaRequest(string AreaCode, string AreaName, string? Description);

public sealed record UpsertRowRequest(string RowCode, string RowName);

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

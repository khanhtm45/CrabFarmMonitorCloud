namespace CrabFarmMonitor.Cloud.Data.Entities;

public class Area
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public string AreaCode { get; set; } = "";
    public string AreaName { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Farm? Farm { get; set; }
    public List<Row> Rows { get; set; } = new();
}

public class Row
{
    public Guid Id { get; set; }
    public Guid AreaId { get; set; }
    public string RowCode { get; set; } = "";
    public string RowName { get; set; } = "";
    public Area? Area { get; set; }
    public List<Box> Boxes { get; set; } = new();
}

public class Box
{
    public Guid Id { get; set; }
    public Guid RowId { get; set; }
    public string BoxCode { get; set; } = "";
    public string? Position { get; set; }
    public double? Volume { get; set; }
    public string Status { get; set; } = "empty";
    public Row? Row { get; set; }
    public List<FarmingBatch> Batches { get; set; } = new();
}

public class FarmingBatch
{
    public Guid Id { get; set; }
    public Guid BoxId { get; set; }
    public string BatchCode { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? ExpectedHarvestDate { get; set; }
    public DateOnly? ActualHarvestDate { get; set; }
    public int InitialQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public string Status { get; set; } = "active";
    public Box? Box { get; set; }
    public List<Crab> Crabs { get; set; } = new();
}

public class Crab
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string CrabCode { get; set; } = "";
    public string Gender { get; set; } = "unknown";
    public double? Weight { get; set; }
    public double? ShellWidth { get; set; }
    public string Status { get; set; } = "alive";
    public FarmingBatch? Batch { get; set; }
    public CrabProfile? Profile { get; set; }
}

public class CrabProfile
{
    public Guid Id { get; set; }
    public Guid CrabId { get; set; }
    public int MoltCount { get; set; }
    public DateOnly? LastMoltDate { get; set; }
    public string? HealthStatus { get; set; }
    public string? GrowthStage { get; set; }
    public string? Note { get; set; }
    public Crab? Crab { get; set; }
}

public class CrabValue
{
    public Guid Id { get; set; }
    public Guid CrabId { get; set; }
    public decimal? MeatQuality { get; set; }
    public decimal? RoeQuality { get; set; }
    public decimal? EstimatedPrice { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

public class FeedingLog
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string FoodType { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public DateTime FedAt { get; set; }
    public string? Note { get; set; }
}

public class HealthRecord
{
    public Guid Id { get; set; }
    public Guid CrabId { get; set; }
    public double? Weight { get; set; }
    public string? ShellStatus { get; set; }
    public string? DiseaseStatus { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class Harvest
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public DateOnly HarvestDate { get; set; }
    public int Quantity { get; set; }
    public double? TotalWeight { get; set; }
    public decimal? PricePerKg { get; set; }
    public decimal? TotalRevenue { get; set; }
}

public class Gateway
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public string GatewayCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LocalIp { get; set; }
    public string? OsVersion { get; set; }
    public string Status { get; set; } = "offline";
    public DateTime? LastSeenAt { get; set; }
}

public class WifiConfig
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Ssid { get; set; } = "";
    public string? Password { get; set; }
    public string IpMode { get; set; } = "dhcp";
    public string? LocalIp { get; set; }
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
}

public class MqttConfig
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string BrokerUrl { get; set; } = "";
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PublishTopic { get; set; }
    public string? SubscribeTopic { get; set; }
    public int Qos { get; set; }
    public bool SslEnable { get; set; }
}

public class Sensor
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string SensorType { get; set; } = "";
    public string? Unit { get; set; }
    public double? MinThreshold { get; set; }
    public double? MaxThreshold { get; set; }
    public bool Status { get; set; } = true;
}

public class SensorReading
{
    public Guid Id { get; set; }
    public Guid SensorId { get; set; }
    public Guid? BoxId { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTime RecordedAt { get; set; }
    public string SyncStatus { get; set; } = "pending";
}

public class Alert
{
    public Guid Id { get; set; }
    public Guid? SensorId { get; set; }
    public Guid? BoxId { get; set; }
    public string AlertType { get; set; } = "";
    public string Severity { get; set; } = "medium";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "new";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class SyncLog
{
    public Guid Id { get; set; }
    public Guid GatewayId { get; set; }
    public string TableName { get; set; } = "";
    public Guid RecordId { get; set; }
    public string Action { get; set; } = "";
    public string SyncStatus { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SyncedAt { get; set; }
}

public class CameraDevice
{
    public Guid Id { get; set; }
    public Guid GatewayId { get; set; }
    public Guid? BoxId { get; set; }
    public string CameraCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? StreamUrl { get; set; }
    public string? IpAddress { get; set; }
    public string Status { get; set; } = "offline";
    public DateTime? LastSeenAt { get; set; }
}

public class CameraSnapshot
{
    public Guid Id { get; set; }
    public Guid CameraId { get; set; }
    public Guid? BoxId { get; set; }
    public string ImageUrl { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public string SyncStatus { get; set; } = "pending";
}

public class AiModel
{
    public Guid Id { get; set; }
    public string ModelName { get; set; } = "";
    public string ModelType { get; set; } = "";
    public string Version { get; set; } = "";
    public string? FilePath { get; set; }
    public string Status { get; set; } = "active";
}

public class AiAnalysisResult
{
    public Guid Id { get; set; }
    public Guid CameraId { get; set; }
    public Guid? BoxId { get; set; }
    public Guid ModelId { get; set; }
    public string ResultType { get; set; } = "";
    public decimal? Confidence { get; set; }
    public string? ResultDataJson { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

public class AiAlert
{
    public Guid Id { get; set; }
    public Guid? ResultId { get; set; }
    public Guid CameraId { get; set; }
    public Guid? BoxId { get; set; }
    public string AlertType { get; set; } = "";
    public string Severity { get; set; } = "medium";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "new";
    public DateTime CreatedAt { get; set; }
}

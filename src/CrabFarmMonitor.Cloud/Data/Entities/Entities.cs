namespace CrabFarmMonitor.Cloud.Data.Entities;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class Farm
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public Guid? OwnerId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Device
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public Guid? BoxId { get; set; }
    public string DeviceCode { get; set; } = "";
    public string? DeviceName { get; set; }
    public string? MacAddress { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? IpLan { get; set; }
    public string Status { get; set; } = "offline";
    public DateTime? LastTelemetryAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public Farm? Farm { get; set; }
}

public class TelemetryLatest
{
    public Guid FarmId { get; set; }
    public Guid DeviceId { get; set; }
    public short Pin { get; set; }
    public double Val { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class TelemetrySample
{
    public long Id { get; set; }
    public Guid FarmId { get; set; }
    public Guid DeviceId { get; set; }
    public short Pin { get; set; }
    public double Val { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class Hdf5Upload
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceCode { get; set; }
    public string StoragePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? ChecksumSha256 { get; set; }
    public long? ChunkStartMs { get; set; }
    public long? ChunkEndMs { get; set; }
    public string Status { get; set; } = "uploaded";
    public DateTime ReceivedAt { get; set; }
}

public class SyncJob
{
    public Guid Id { get; set; }
    public Guid FarmId { get; set; }
    public Guid? Hdf5UploadId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string Status { get; set; } = "acked";
}

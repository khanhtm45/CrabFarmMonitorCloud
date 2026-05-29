namespace CrabFarmMonitor.Cloud.Data.Entities;

public class DeviceShadow
{
    public Guid DeviceId { get; set; }
    public string? DesiredJson { get; set; }
    public string? ReportedJson { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Device? Device { get; set; }
}

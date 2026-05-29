namespace CrabFarmMonitor.Shared;

public enum UserRole { Admin, Manager, Staff }

public enum BoxStatus { Empty, Farming, Maintenance }

public enum FarmingBatchStatus { Active, Harvested, Failed }

public enum CrabGender { Male, Female, Unknown }

public enum CrabStatus { Alive, Dead, Molting, Harvested }

public enum DeviceStatus { Online, Offline, Error }

public enum GatewayStatus { Online, Offline }

public enum IpMode { Static, Dhcp }

public enum SensorType
{
    Ph, Temp, Do, Salinity, Orp, Nh3, No2, WaterLevel, Flow, Tds
}

public enum SyncStatus { Pending, Synced, Failed }

public enum AlertSeverity { Low, Medium, High, Critical }

public enum AlertStatus { New, Resolved }

public enum SyncLogAction { Create, Update, Delete }

public enum SyncLogStatus { Pending, Success, Failed }

public enum CameraStatus { Online, Offline, Error }

public enum AiModelType { CountCrab, DeadCrab, Molting, AbnormalBehavior }

public enum AiModelStatus { Active, Inactive }

public enum AiResultType { Count, DeadCrab, Molting, Abnormal, Movement }

public enum AiAlertType { DeadCrab, NoMovement, MoltingDetected, AbnormalBehavior }

public static class DomainEnumText
{
    public static string ToDb(UserRole v) => v switch
    {
        UserRole.Admin => "admin",
        UserRole.Manager => "manager",
        _ => "staff"
    };

    public static string ToDb(BoxStatus v) => v switch
    {
        BoxStatus.Farming => "farming",
        BoxStatus.Maintenance => "maintenance",
        _ => "empty"
    };

    public static string ToDb(SensorType v) => v switch
    {
        SensorType.Ph => "ph",
        SensorType.Temp => "temp",
        SensorType.Do => "do",
        SensorType.Salinity => "salinity",
        SensorType.Orp => "orp",
        SensorType.Nh3 => "nh3",
        SensorType.No2 => "no2",
        SensorType.WaterLevel => "water_level",
        SensorType.Flow => "flow",
        _ => "tds"
    };

    public static string ToDb(SyncStatus v) => v == SyncStatus.Synced ? "synced" : v == SyncStatus.Failed ? "failed" : "pending";

    public static string ToDb(AlertSeverity v) => v switch
    {
        AlertSeverity.Low => "low",
        AlertSeverity.High => "high",
        AlertSeverity.Critical => "critical",
        _ => "medium"
    };
}

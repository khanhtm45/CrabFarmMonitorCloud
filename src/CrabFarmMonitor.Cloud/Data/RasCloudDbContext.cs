using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using CrabFarmMonitor.Cloud.Data.Entities;
using CrabFarmMonitor.Cloud.Services;

namespace CrabFarmMonitor.Cloud.Data;

public sealed class RasCloudDbContext : DbContext
{
    public RasCloudDbContext(DbContextOptions<RasCloudDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<TelemetryLatest> TelemetryLatest => Set<TelemetryLatest>();
    public DbSet<TelemetrySample> TelemetrySamples => Set<TelemetrySample>();
    public DbSet<Hdf5Upload> Hdf5Uploads => Set<Hdf5Upload>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CrabBox> CrabBoxes => Set<CrabBox>();
    public DbSet<CrabIndividual> CrabIndividuals => Set<CrabIndividual>();
    public DbSet<DeviceShadow> DeviceShadows => Set<DeviceShadow>();

    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Row> Rows => Set<Row>();
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<FarmingBatch> FarmingBatches => Set<FarmingBatch>();
    public DbSet<Crab> Crabs => Set<Crab>();
    public DbSet<CrabProfile> CrabProfiles => Set<CrabProfile>();
    public DbSet<CrabValue> CrabValues => Set<CrabValue>();
    public DbSet<FeedingLog> FeedingLogs => Set<FeedingLog>();
    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<Gateway> Gateways => Set<Gateway>();
    public DbSet<WifiConfig> WifiConfigs => Set<WifiConfig>();
    public DbSet<MqttConfig> MqttConfigs => Set<MqttConfig>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<CameraDevice> CameraDevices => Set<CameraDevice>();
    public DbSet<CameraSnapshot> CameraSnapshots => Set<CameraSnapshot>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AiAnalysisResult> AiAnalysisResults => Set<AiAnalysisResult>();
    public DbSet<AiAlert> AiAlerts => Set<AiAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Slug).HasColumnName("slug");
        });

        modelBuilder.Entity<Farm>(e =>
        {
            e.ToTable("farms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrgId).HasColumnName("org_id");
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.OwnerId).HasColumnName("owner_id");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.DeviceCode).HasColumnName("device_code");
            e.Property(x => x.DeviceName).HasColumnName("device_name");
            e.Property(x => x.MacAddress)
                .HasColumnName("mac_address")
                .HasColumnType("macaddr")
                .HasConversion(new MacAddressConverter());
            e.Property(x => x.FirmwareVersion).HasColumnName("firmware_version");
            e.Property(x => x.IpLan).HasColumnName("ip_lan");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.LastTelemetryAt).HasColumnName("last_telemetry_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            e.HasIndex(x => new { x.FarmId, x.DeviceCode }).IsUnique();
        });

        modelBuilder.Entity<TelemetryLatest>(e =>
        {
            e.ToTable("telemetry_latest");
            e.HasKey(x => new { x.DeviceId, x.Pin });
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.Pin).HasColumnName("pin");
            e.Property(x => x.Val).HasColumnName("val");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
            e.HasIndex(x => x.FarmId);
        });

        modelBuilder.Entity<TelemetrySample>(e =>
        {
            e.ToTable("telemetry_samples");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.Pin).HasColumnName("pin");
            e.Property(x => x.Val).HasColumnName("val");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
            e.HasIndex(x => new { x.DeviceId, x.Pin, x.RecordedAt });
            e.HasIndex(x => new { x.FarmId, x.RecordedAt });
        });

        modelBuilder.Entity<Hdf5Upload>(e =>
        {
            e.ToTable("hdf5_uploads");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.DeviceCode).HasColumnName("device_code");
            e.Property(x => x.StoragePath).HasColumnName("storage_path");
            e.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            e.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256");
            e.Property(x => x.ChunkStartMs).HasColumnName("chunk_start_ms");
            e.Property(x => x.ChunkEndMs).HasColumnName("chunk_end_ms");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.ReceivedAt).HasColumnName("received_at");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrgId).HasColumnName("org_id");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.FullName).HasColumnName("full_name");
            e.Property(x => x.DisplayName).HasColumnName("display_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<CrabBox>(e =>
        {
            e.ToTable("crab_boxes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Label).HasColumnName("label");
            e.Property(x => x.RowLabel).HasColumnName("row_label");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Capacity).HasColumnName("capacity");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasMany(x => x.Crabs).WithOne(c => c.Box).HasForeignKey(c => c.BoxId);
        });

        modelBuilder.Entity<CrabIndividual>(e =>
        {
            e.ToTable("crab_individuals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.TagCode).HasColumnName("tag_code");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.WeightGrams).HasColumnName("weight_grams");
            e.Property(x => x.MoltStage).HasColumnName("molt_stage");
            e.Property(x => x.HealthNote).HasColumnName("health_note");
            e.Property(x => x.LastWeighedAt).HasColumnName("last_weighed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<DeviceShadow>(e =>
        {
            e.ToTable("device_shadows");
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.DesiredJson).HasColumnName("desired").HasColumnType("jsonb");
            e.Property(x => x.ReportedJson).HasColumnName("reported").HasColumnType("jsonb");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId);
        });

        modelBuilder.Entity<SyncJob>(e =>
        {
            e.ToTable("sync_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.Hdf5UploadId).HasColumnName("hdf5_upload_id");
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            e.Property(x => x.Status).HasColumnName("status");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        DomainModelBuilder.Apply(modelBuilder);
    }
}

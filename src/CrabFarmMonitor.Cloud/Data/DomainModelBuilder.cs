using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Data;

public static class DomainModelBuilder
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Area>(e =>
        {
            e.ToTable("areas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.AreaCode).HasColumnName("area_code");
            e.Property(x => x.AreaName).HasColumnName("area_name");
            e.Property(x => x.Description).HasColumnName("description");
            e.HasIndex(x => new { x.FarmId, x.AreaCode }).IsUnique();
        });

        modelBuilder.Entity<Row>(e =>
        {
            e.ToTable("rows");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AreaId).HasColumnName("area_id");
            e.Property(x => x.RowCode).HasColumnName("row_code");
            e.Property(x => x.RowName).HasColumnName("row_name");
            e.HasIndex(x => new { x.AreaId, x.RowCode }).IsUnique();
        });

        modelBuilder.Entity<Box>(e =>
        {
            e.ToTable("boxes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RowId).HasColumnName("row_id");
            e.Property(x => x.BoxCode).HasColumnName("box_code");
            e.Property(x => x.Position).HasColumnName("position");
            e.Property(x => x.Volume).HasColumnName("volume");
            e.Property(x => x.Status).HasColumnName("status");
            e.HasIndex(x => new { x.RowId, x.BoxCode }).IsUnique();
        });

        modelBuilder.Entity<FarmingBatch>(e =>
        {
            e.ToTable("farming_batches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.BatchCode).HasColumnName("batch_code");
            e.Property(x => x.StartDate).HasColumnName("start_date");
            e.Property(x => x.ExpectedHarvestDate).HasColumnName("expected_harvest_date");
            e.Property(x => x.ActualHarvestDate).HasColumnName("actual_harvest_date");
            e.Property(x => x.InitialQuantity).HasColumnName("initial_quantity");
            e.Property(x => x.CurrentQuantity).HasColumnName("current_quantity");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<Crab>(e =>
        {
            e.ToTable("crabs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.CrabCode).HasColumnName("crab_code");
            e.Property(x => x.Gender).HasColumnName("gender");
            e.Property(x => x.Weight).HasColumnName("weight");
            e.Property(x => x.ShellWidth).HasColumnName("shell_width");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<CrabProfile>(e =>
        {
            e.ToTable("crab_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CrabId).HasColumnName("crab_id");
            e.Property(x => x.MoltCount).HasColumnName("molt_count");
            e.Property(x => x.LastMoltDate).HasColumnName("last_molt_date");
            e.Property(x => x.HealthStatus).HasColumnName("health_status");
            e.Property(x => x.GrowthStage).HasColumnName("growth_stage");
            e.Property(x => x.Note).HasColumnName("note");
        });

        modelBuilder.Entity<CrabValue>(e =>
        {
            e.ToTable("crab_values");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CrabId).HasColumnName("crab_id");
            e.Property(x => x.MeatQuality).HasColumnName("meat_quality");
            e.Property(x => x.RoeQuality).HasColumnName("roe_quality");
            e.Property(x => x.EstimatedPrice).HasColumnName("estimated_price");
            e.Property(x => x.EvaluatedAt).HasColumnName("evaluated_at");
        });

        modelBuilder.Entity<FeedingLog>(e =>
        {
            e.ToTable("feeding_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.FoodType).HasColumnName("food_type");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.Unit).HasColumnName("unit");
            e.Property(x => x.FedAt).HasColumnName("fed_at");
            e.Property(x => x.Note).HasColumnName("note");
        });

        modelBuilder.Entity<HealthRecord>(e =>
        {
            e.ToTable("health_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CrabId).HasColumnName("crab_id");
            e.Property(x => x.Weight).HasColumnName("weight");
            e.Property(x => x.ShellStatus).HasColumnName("shell_status");
            e.Property(x => x.DiseaseStatus).HasColumnName("disease_status");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        });

        modelBuilder.Entity<Harvest>(e =>
        {
            e.ToTable("harvests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.HarvestDate).HasColumnName("harvest_date");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.TotalWeight).HasColumnName("total_weight");
            e.Property(x => x.PricePerKg).HasColumnName("price_per_kg");
            e.Property(x => x.TotalRevenue).HasColumnName("total_revenue");
        });

        modelBuilder.Entity<Gateway>(e =>
        {
            e.ToTable("gateways");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FarmId).HasColumnName("farm_id");
            e.Property(x => x.GatewayCode).HasColumnName("gateway_code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.LocalIp).HasColumnName("local_ip");
            e.Property(x => x.OsVersion).HasColumnName("os_version");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        });

        modelBuilder.Entity<WifiConfig>(e =>
        {
            e.ToTable("wifi_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.Ssid).HasColumnName("ssid");
            e.Property(x => x.Password).HasColumnName("password");
            e.Property(x => x.IpMode).HasColumnName("ip_mode");
            e.Property(x => x.LocalIp).HasColumnName("local_ip");
            e.Property(x => x.SubnetMask).HasColumnName("subnet_mask");
            e.Property(x => x.Gateway).HasColumnName("gateway");
        });

        modelBuilder.Entity<MqttConfig>(e =>
        {
            e.ToTable("mqtt_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.BrokerUrl).HasColumnName("broker_url");
            e.Property(x => x.Port).HasColumnName("port");
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.Password).HasColumnName("password");
            e.Property(x => x.PublishTopic).HasColumnName("publish_topic");
            e.Property(x => x.SubscribeTopic).HasColumnName("subscribe_topic");
            e.Property(x => x.Qos).HasColumnName("qos");
            e.Property(x => x.SslEnable).HasColumnName("ssl_enable");
        });

        modelBuilder.Entity<Sensor>(e =>
        {
            e.ToTable("sensors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.SensorType).HasColumnName("sensor_type");
            e.Property(x => x.Unit).HasColumnName("unit");
            e.Property(x => x.MinThreshold).HasColumnName("min_threshold");
            e.Property(x => x.MaxThreshold).HasColumnName("max_threshold");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<SensorReading>(e =>
        {
            e.ToTable("sensor_readings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SensorId).HasColumnName("sensor_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.Unit).HasColumnName("unit");
            e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            e.Property(x => x.SyncStatus).HasColumnName("sync_status");
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.ToTable("alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SensorId).HasColumnName("sensor_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.AlertType).HasColumnName("alert_type");
            e.Property(x => x.Severity).HasColumnName("severity");
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        });

        modelBuilder.Entity<SyncLog>(e =>
        {
            e.ToTable("sync_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.GatewayId).HasColumnName("gateway_id");
            e.Property(x => x.TableName).HasColumnName("table_name");
            e.Property(x => x.RecordId).HasColumnName("record_id");
            e.Property(x => x.Action).HasColumnName("action");
            e.Property(x => x.SyncStatus).HasColumnName("sync_status");
            e.Property(x => x.ErrorMessage).HasColumnName("error_message");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.SyncedAt).HasColumnName("synced_at");
        });

        modelBuilder.Entity<CameraDevice>(e =>
        {
            e.ToTable("camera_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.GatewayId).HasColumnName("gateway_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.CameraCode).HasColumnName("camera_code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.StreamUrl).HasColumnName("stream_url");
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        });

        modelBuilder.Entity<CameraSnapshot>(e =>
        {
            e.ToTable("camera_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CameraId).HasColumnName("camera_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.ImageUrl).HasColumnName("image_url");
            e.Property(x => x.CapturedAt).HasColumnName("captured_at");
            e.Property(x => x.SyncStatus).HasColumnName("sync_status");
        });

        modelBuilder.Entity<AiModel>(e =>
        {
            e.ToTable("ai_models");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ModelName).HasColumnName("model_name");
            e.Property(x => x.ModelType).HasColumnName("model_type");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.FilePath).HasColumnName("file_path");
            e.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<AiAnalysisResult>(e =>
        {
            e.ToTable("ai_analysis_results");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CameraId).HasColumnName("camera_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.ModelId).HasColumnName("model_id");
            e.Property(x => x.ResultType).HasColumnName("result_type");
            e.Property(x => x.Confidence).HasColumnName("confidence");
            e.Property(x => x.ResultDataJson).HasColumnName("result_data").HasColumnType("jsonb");
            e.Property(x => x.ImageUrl).HasColumnName("image_url");
            e.Property(x => x.AnalyzedAt).HasColumnName("analyzed_at");
        });

        modelBuilder.Entity<AiAlert>(e =>
        {
            e.ToTable("ai_alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ResultId).HasColumnName("result_id");
            e.Property(x => x.CameraId).HasColumnName("camera_id");
            e.Property(x => x.BoxId).HasColumnName("box_id");
            e.Property(x => x.AlertType).HasColumnName("alert_type");
            e.Property(x => x.Severity).HasColumnName("severity");
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}

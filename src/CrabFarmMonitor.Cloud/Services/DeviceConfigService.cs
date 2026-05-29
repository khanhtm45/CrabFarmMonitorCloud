using Microsoft.EntityFrameworkCore;
using CrabFarmMonitor.Cloud.Data;
using CrabFarmMonitor.Cloud.Data.Entities;

namespace CrabFarmMonitor.Cloud.Services;

public sealed class DeviceConfigService
{
    private readonly RasCloudDbContext _db;

    public DeviceConfigService(RasCloudDbContext db) => _db = db;

    public async Task<Device?> GetDeviceAsync(Guid deviceId, CancellationToken ct) =>
        await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, ct);

    public async Task<Device?> GetDeviceByCodeAsync(Guid farmId, string deviceCode, CancellationToken ct) =>
        await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.FarmId == farmId && d.DeviceCode == deviceCode.Trim(), ct);

    public async Task<WifiConfig?> GetWifiAsync(Guid deviceId, CancellationToken ct) =>
        await _db.WifiConfigs.AsNoTracking().FirstOrDefaultAsync(w => w.DeviceId == deviceId, ct);

    public async Task<WifiConfig> UpsertWifiAsync(Guid deviceId, UpsertWifiRequest req, CancellationToken ct)
    {
        var row = await _db.WifiConfigs.FirstOrDefaultAsync(w => w.DeviceId == deviceId, ct);
        if (row == null)
        {
            row = new WifiConfig { Id = Guid.NewGuid(), DeviceId = deviceId };
            _db.WifiConfigs.Add(row);
        }

        row.Ssid = req.Ssid.Trim();
        row.Password = req.Password;
        row.IpMode = req.IpMode is "static" ? "static" : "dhcp";
        row.LocalIp = req.LocalIp;
        row.SubnetMask = req.SubnetMask;
        row.Gateway = req.Gateway;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<MqttConfig?> GetMqttAsync(Guid deviceId, CancellationToken ct) =>
        await _db.MqttConfigs.AsNoTracking().FirstOrDefaultAsync(m => m.DeviceId == deviceId, ct);

    public async Task<MqttConfig> UpsertMqttAsync(Guid deviceId, UpsertMqttRequest req, CancellationToken ct)
    {
        var row = await _db.MqttConfigs.FirstOrDefaultAsync(m => m.DeviceId == deviceId, ct);
        if (row == null)
        {
            row = new MqttConfig { Id = Guid.NewGuid(), DeviceId = deviceId };
            _db.MqttConfigs.Add(row);
        }

        row.BrokerUrl = req.BrokerUrl.Trim();
        row.Port = req.Port;
        row.Username = req.Username;
        row.Password = req.Password;
        row.PublishTopic = req.PublishTopic;
        row.SubscribeTopic = req.SubscribeTopic;
        row.Qos = req.Qos;
        row.SslEnable = req.SslEnable;
        await _db.SaveChangesAsync(ct);
        return row;
    }
}

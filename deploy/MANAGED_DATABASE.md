# PostgreSQL Managed (DigitalOcean)

Production dùng cluster DO — **không** Postgres container.

## Trusted sources

Thêm IP outbound của **App Platform** (Settings → app → có thể xem trong build logs / networking) hoặc tạm cho phép mọi nguồn khi test.

## Schema

```powershell
.\deploy\apply-managed-db-schema.ps1 -Password "<doadmin-password>"
# hoặc
.\deploy\apply-managed-db-schema.ps1 -EnvFile "..\.env"
```

Database mới `ras_cloud` (tạo trong panel) an toàn hơn `defaultdb` nếu DB đã có bảng cũ:

```powershell
.\deploy\apply-managed-db-schema.ps1 -Password "<pass>" -Database ras_cloud
```

Reset `defaultdb` (xóa hết bảng public):

```powershell
.\deploy\apply-managed-db-schema.ps1 -Password "<pass>" -Database defaultdb -Fresh
```

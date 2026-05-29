# CrabFarmMonitor Cloud

API trung tâm (ASP.NET Core 8) — telemetry, HDF5 sync, auth, domain CRUD (trại/khu/dãy, WiFi/MQTT, camera AI). Deploy **DigitalOcean App Platform** từ GitHub (không Docker image).

Kiến trúc tổng thể: [ARCHITECTURE.md](../ARCHITECTURE.md). Edge gateway: [CrabFarmMonitorEdge](../CrabFarmMonitorEdge).

## Cấu trúc

| Thư mục | Mô tả |
|---------|--------|
| `src/CrabFarmMonitor.Cloud` | Web API |
| `src/CrabFarmMonitor.Shared` | DTO / metric registry (dùng chung với Edge) |
| `database/` | SQL schema cho Managed PostgreSQL |
| `.do/app.yaml` | App Platform spec |
| `deploy/` | Script áp schema lên DO DB |

## Database (DigitalOcean Managed PostgreSQL)

```
Host=ras-cloud-do-user-37760190-0.g.db.ondigitalocean.com
Port=25060
Database=defaultdb
Username=doadmin
SSL Mode=Require
```

1. **Databases** → cluster → **Trusted sources**: thêm outbound IP của App Platform (hoặc tạm “All” khi test).
2. Copy password `doadmin` — chỉ lưu trong App Platform **Secrets**, không commit Git.
3. Áp schema (một lần, cần Docker trên máy dev chỉ để chạy `psql`):

```powershell
cd d:\CN8\PRM392\CrabFarmMonitorCloud
copy .env.example .env
# Sửa Password trong DATABASE_URL
.\deploy\apply-managed-db-schema.ps1 -EnvFile .\.env
```

## Chạy local (không Docker)

```powershell
cd d:\CN8\PRM392\CrabFarmMonitorCloud
copy .env.example .env
# Điền DATABASE_URL + JWT_SECRET + ...
dotnet run --project src/CrabFarmMonitor.Cloud/CrabFarmMonitor.Cloud.csproj
```

Health: `http://localhost:8080/health`

## Deploy — DigitalOcean App Platform

1. Push repo lên GitHub (`CrabFarmMonitorCloud`).
2. Sửa `.do/app.yaml`: `github.repo` → `user/CrabFarmMonitorCloud`.
3. [cloud.digitalocean.com](https://cloud.digitalocean.com) → **Create App** → **GitHub** → chọn repo → branch `main`.
4. App Platform detect `.do/app.yaml` (hoặc chọn **Edit spec**).
5. **Resources** → **Add Database** → chọn cluster `ras-cloud-…` **hoặc** thêm biến `DATABASE_URL` (SECRET) dạng Npgsql:

   `Host=ras-cloud-do-user-37760190-0.g.db.ondigitalocean.com;Port=25060;Database=defaultdb;Username=doadmin;Password=<PASSWORD>;SSL Mode=Require;Trust Server Certificate=true`

6. Thêm secrets: `JWT_SECRET`, `TELEMETRY_API_KEY`, `DEV_ADMIN_PASSWORD`.
7. (Khuyến nghị) **Spaces** cho HDF5: `S3_ENDPOINT`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `S3_BUCKET`, `S3_USE_SSL=true`.
8. Deploy → **https://crabfarmmonitorcloud-uwkqk.ondigitalocean.app**

Cấu hình Edge/VPS trỏ sync:

```env
EDGE_REMOTE_SYNC_URL=https://crabfarmmonitorcloud-uwkqk.ondigitalocean.app/api/sync/hdf5
EDGE_REMOTE_SYNC_API_KEY=<same as TELEMETRY_API_KEY>
EDGE_CLOUD_FARM_ID=11111111-1111-1111-1111-111111111111
```

Desktop: `CLOUD_API_URL=https://crabfarmmonitorcloud-uwkqk.ondigitalocean.app`

## Biến môi trường

Xem [.env.example](.env.example). App đọc `DATABASE_URL` hoặc `ConnectionStrings__Default`.

## API domain (mới)

| Nhóm | Endpoint |
|------|----------|
| Sync (X-API-Key) | `POST /api/sync/sensor-batch`, `POST /api/sync/camera/snapshot`, `POST /api/sync/camera/analysis` |
| Layout (JWT) | `GET/POST /api/areas`, `GET/POST /api/areas/{id}/rows` |
| Thiết bị (JWT) | `GET/PUT /api/devices/{id}/wifi`, `…/mqtt` |
| Camera (JWT) | `GET /api/cameras`, `GET /api/cameras/{id}/snapshots`, `GET /api/ai/alerts` |

## Ghi chú

- Bootstrap schema chạy lúc startup (`DevBootstrapService`, `CrabSchemaBootstrap`, …) nếu kết nối DB OK; vẫn nên chạy `apply-managed-db-schema.ps1` lần đầu.
- HDF5 browse qua Python (`scripts/`) — App Platform mặc định không có Python; upload/sync HDF5 qua S3 vẫn hoạt động nếu cấu hình Spaces.
- Ảnh camera khi chưa có S3: lưu `UPLOAD_DIR`, phục vụ qua `GET /api/media/{path}`.

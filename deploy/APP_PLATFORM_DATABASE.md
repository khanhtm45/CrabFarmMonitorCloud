# Kết nối PostgreSQL trên App Platform

`/health` báo `database: false` → app **không** nối được Managed DB.

## Bước 1 — Trusted sources (bắt buộc)

1. [DigitalOcean](https://cloud.digitalocean.com) → **Databases** → cluster `ras-cloud-…`
2. **Settings** → **Trusted sources**
3. **Add trusted source** → chọn **App Platform** (app `crabfarmmonitorcloud`)  
   hoặc tạm **All IPv4** để test
4. Save

## Bước 2 — Gán `DATABASE_URL` cho app

### Cách A (khuyến nghị): Liên kết database

1. **Apps** → `crabfarmmonitorcloud-…` → **Settings** → **Resources**
2. **Add Resource** → **Database** → chọn cluster có sẵn
3. DO tự inject biến `DATABASE_URL` vào service `api`
4. **Redeploy**

### Cách B: Dán tay

1. **Databases** → cluster → **Connection details**
2. Copy **Connection string** (URI dạng `postgresql://doadmin:…@…:25060/defaultdb?sslmode=require`)
3. **Apps** → app → **Settings** → **Environment Variables**
4. Key: `DATABASE_URL` — Value: chuỗi vừa copy — **Encrypt** — Save
5. Redeploy

Hoặc dạng Npgsql:

```text
Host=ras-cloud-do-user-37760190-0.g.db.ondigitalocean.com;Port=25060;Database=defaultdb;Username=doadmin;Password=<PASSWORD>;SSL Mode=Require;Trust Server Certificate=true
```

## Bước 3 — Schema (máy dev)

```powershell
cd CrabFarmMonitorCloud
copy .env.example .env
# Dien password that vao DATABASE_URL
.\deploy\apply-managed-db-schema.ps1 -EnvFile .\.env
```

Trusted sources phải có **IP máy bạn** nếu chạy script từ PC.

## Bước 4 — Kiểm tra

```powershell
Invoke-RestMethod "https://crabfarmmonitorcloud-uwkqk.ondigitalocean.app/health"
```

Kỳ vọng:

```json
{
  "ok": true,
  "configPresent": true,
  "database": true,
  "users": 1
}
```

Sau đó login Desktop: `admin@iras.local` / `DEV_ADMIN_PASSWORD`.

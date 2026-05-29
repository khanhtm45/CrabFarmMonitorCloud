-- CrabFarmMonitor — domain đầy đủ (trại, cua, IoT, camera AI, đồng bộ)
-- Chạy sau cloud_postgresql_schema.sql. Giữ tương thích crab_boxes MVP (03-crab-boxes.sql).

-- ─── Phân cấp trại ───────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS areas (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id     UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    area_code   VARCHAR(20) NOT NULL,
    area_name   VARCHAR(100) NOT NULL,
    description TEXT,
    UNIQUE (farm_id, area_code)
);

CREATE TABLE IF NOT EXISTS rows (
    id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id  UUID NOT NULL REFERENCES areas(id) ON DELETE CASCADE,
    row_code VARCHAR(20) NOT NULL,
    row_name VARCHAR(100) NOT NULL,
    UNIQUE (area_id, row_code)
);

CREATE TABLE IF NOT EXISTS boxes (
    id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    row_id   UUID NOT NULL REFERENCES rows(id) ON DELETE CASCADE,
    box_code VARCHAR(20) NOT NULL,
    position VARCHAR(50),
    volume   DOUBLE PRECISION,
    status   TEXT NOT NULL DEFAULT 'empty'
        CHECK (status IN ('empty', 'farming', 'maintenance')),
    UNIQUE (row_id, box_code)
);

ALTER TABLE devices
    ADD COLUMN IF NOT EXISTS box_id UUID REFERENCES boxes(id) ON DELETE SET NULL;

-- ─── Nuôi cua ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS farming_batches (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    box_id                UUID NOT NULL REFERENCES boxes(id) ON DELETE CASCADE,
    batch_code            VARCHAR(50) NOT NULL,
    start_date            DATE NOT NULL,
    expected_harvest_date DATE,
    actual_harvest_date   DATE,
    initial_quantity      INT NOT NULL DEFAULT 0,
    current_quantity      INT NOT NULL DEFAULT 0,
    status                TEXT NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'harvested', 'failed')),
    UNIQUE (box_id, batch_code)
);

CREATE TABLE IF NOT EXISTS crabs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id    UUID NOT NULL REFERENCES farming_batches(id) ON DELETE CASCADE,
    crab_code   VARCHAR(50) NOT NULL,
    gender      TEXT NOT NULL DEFAULT 'unknown'
        CHECK (gender IN ('male', 'female', 'unknown')),
    weight      DOUBLE PRECISION,
    shell_width DOUBLE PRECISION,
    status      TEXT NOT NULL DEFAULT 'alive'
        CHECK (status IN ('alive', 'dead', 'molting', 'harvested')),
    UNIQUE (batch_id, crab_code)
);

CREATE TABLE IF NOT EXISTS crab_profiles (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    crab_id        UUID NOT NULL UNIQUE REFERENCES crabs(id) ON DELETE CASCADE,
    molt_count     INT NOT NULL DEFAULT 0,
    last_molt_date DATE,
    health_status  VARCHAR(50),
    growth_stage   VARCHAR(50),
    note           TEXT
);

CREATE TABLE IF NOT EXISTS crab_values (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    crab_id         UUID NOT NULL REFERENCES crabs(id) ON DELETE CASCADE,
    meat_quality    DECIMAL(5,2),
    roe_quality     DECIMAL(5,2),
    estimated_price DECIMAL(12,2),
    evaluated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS feeding_logs (
    id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id  UUID NOT NULL REFERENCES farming_batches(id) ON DELETE CASCADE,
    food_type VARCHAR(100) NOT NULL,
    quantity  DOUBLE PRECISION NOT NULL,
    unit      VARCHAR(20) NOT NULL,
    fed_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    note      TEXT
);

CREATE TABLE IF NOT EXISTS health_records (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    crab_id        UUID NOT NULL REFERENCES crabs(id) ON DELETE CASCADE,
    weight         DOUBLE PRECISION,
    shell_status   VARCHAR(50),
    disease_status VARCHAR(50),
    recorded_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS harvests (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id      UUID NOT NULL REFERENCES farming_batches(id) ON DELETE CASCADE,
    harvest_date  DATE NOT NULL,
    quantity      INT NOT NULL,
    total_weight  DOUBLE PRECISION,
    price_per_kg  DECIMAL(12,2),
    total_revenue DECIMAL(12,2)
);

-- ─── Gateway & cấu hình thiết bị ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS gateways (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id      UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    gateway_code VARCHAR(50) NOT NULL,
    name         VARCHAR(100) NOT NULL,
    local_ip     VARCHAR(50),
    os_version   VARCHAR(50),
    status       TEXT NOT NULL DEFAULT 'offline'
        CHECK (status IN ('online', 'offline')),
    last_seen_at TIMESTAMPTZ,
    UNIQUE (farm_id, gateway_code)
);

CREATE TABLE IF NOT EXISTS wifi_configs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id   UUID NOT NULL UNIQUE REFERENCES devices(id) ON DELETE CASCADE,
    ssid        VARCHAR(100) NOT NULL,
    password    VARCHAR(255),
    ip_mode     TEXT NOT NULL DEFAULT 'dhcp'
        CHECK (ip_mode IN ('static', 'dhcp')),
    local_ip    VARCHAR(50),
    subnet_mask VARCHAR(50),
    gateway     VARCHAR(50)
);

CREATE TABLE IF NOT EXISTS mqtt_configs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id       UUID NOT NULL UNIQUE REFERENCES devices(id) ON DELETE CASCADE,
    broker_url      VARCHAR(255) NOT NULL,
    port            INT NOT NULL DEFAULT 1883,
    username        VARCHAR(100),
    password        VARCHAR(255),
    publish_topic   VARCHAR(255),
    subscribe_topic VARCHAR(255),
    qos             INT NOT NULL DEFAULT 0,
    ssl_enable      BOOLEAN NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS sensors (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id     UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    sensor_type   TEXT NOT NULL
        CHECK (sensor_type IN (
            'ph', 'temp', 'do', 'salinity', 'orp', 'nh3', 'no2',
            'water_level', 'flow', 'tds'
        )),
    unit          VARCHAR(20),
    min_threshold DOUBLE PRECISION,
    max_threshold DOUBLE PRECISION,
    status        BOOLEAN NOT NULL DEFAULT true,
    UNIQUE (device_id, sensor_type)
);

CREATE TABLE IF NOT EXISTS sensor_readings (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sensor_id   UUID NOT NULL REFERENCES sensors(id) ON DELETE CASCADE,
    box_id      UUID REFERENCES boxes(id) ON DELETE SET NULL,
    value       DOUBLE PRECISION NOT NULL,
    unit        VARCHAR(20),
    recorded_at TIMESTAMPTZ NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending'
        CHECK (sync_status IN ('pending', 'synced', 'failed'))
);

CREATE INDEX IF NOT EXISTS idx_sensor_readings_sensor_time
    ON sensor_readings (sensor_id, recorded_at DESC);

CREATE INDEX IF NOT EXISTS idx_sensor_readings_sync
    ON sensor_readings (sync_status) WHERE sync_status = 'pending';

CREATE TABLE IF NOT EXISTS alerts (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sensor_id   UUID REFERENCES sensors(id) ON DELETE SET NULL,
    box_id      UUID REFERENCES boxes(id) ON DELETE SET NULL,
    alert_type  VARCHAR(100) NOT NULL,
    severity    TEXT NOT NULL DEFAULT 'medium'
        CHECK (severity IN ('low', 'medium', 'high', 'critical')),
    message     TEXT NOT NULL,
    status      TEXT NOT NULL DEFAULT 'new'
        CHECK (status IN ('new', 'resolved')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS sync_logs (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    gateway_id    UUID NOT NULL REFERENCES gateways(id) ON DELETE CASCADE,
    table_name    VARCHAR(50) NOT NULL,
    record_id     UUID NOT NULL,
    action        TEXT NOT NULL
        CHECK (action IN ('create', 'update', 'delete')),
    sync_status   TEXT NOT NULL DEFAULT 'pending'
        CHECK (sync_status IN ('pending', 'success', 'failed')),
    error_message TEXT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    synced_at     TIMESTAMPTZ
);

-- ─── Camera & AI ───────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS camera_devices (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    gateway_id   UUID NOT NULL REFERENCES gateways(id) ON DELETE CASCADE,
    box_id       UUID REFERENCES boxes(id) ON DELETE SET NULL,
    camera_code  VARCHAR(50) NOT NULL,
    name         VARCHAR(100) NOT NULL,
    stream_url   VARCHAR(255),
    ip_address   VARCHAR(50),
    status       TEXT NOT NULL DEFAULT 'offline'
        CHECK (status IN ('online', 'offline', 'error')),
    last_seen_at TIMESTAMPTZ,
    UNIQUE (gateway_id, camera_code)
);

CREATE TABLE IF NOT EXISTS camera_snapshots (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    camera_id   UUID NOT NULL REFERENCES camera_devices(id) ON DELETE CASCADE,
    box_id      UUID REFERENCES boxes(id) ON DELETE SET NULL,
    image_url   VARCHAR(255) NOT NULL,
    captured_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    sync_status TEXT NOT NULL DEFAULT 'pending'
        CHECK (sync_status IN ('pending', 'synced', 'failed'))
);

CREATE TABLE IF NOT EXISTS ai_models (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_name VARCHAR(100) NOT NULL,
    model_type TEXT NOT NULL
        CHECK (model_type IN ('count_crab', 'dead_crab', 'molting', 'abnormal_behavior')),
    version    VARCHAR(20) NOT NULL,
    file_path  VARCHAR(255),
    status     TEXT NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'inactive'))
);

CREATE TABLE IF NOT EXISTS ai_analysis_results (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    camera_id   UUID NOT NULL REFERENCES camera_devices(id) ON DELETE CASCADE,
    box_id      UUID REFERENCES boxes(id) ON DELETE SET NULL,
    model_id    UUID NOT NULL REFERENCES ai_models(id) ON DELETE RESTRICT,
    result_type TEXT NOT NULL
        CHECK (result_type IN ('count', 'dead_crab', 'molting', 'abnormal', 'movement')),
    confidence  DECIMAL(5,2),
    result_data JSONB,
    image_url   VARCHAR(255),
    analyzed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ai_alerts (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    result_id  UUID REFERENCES ai_analysis_results(id) ON DELETE SET NULL,
    camera_id  UUID NOT NULL REFERENCES camera_devices(id) ON DELETE CASCADE,
    box_id     UUID REFERENCES boxes(id) ON DELETE SET NULL,
    alert_type TEXT NOT NULL
        CHECK (alert_type IN ('dead_crab', 'no_movement', 'molting_detected', 'abnormal_behavior')),
    severity   TEXT NOT NULL DEFAULT 'medium'
        CHECK (severity IN ('low', 'medium', 'high', 'critical')),
    message    TEXT NOT NULL,
    status     TEXT NOT NULL DEFAULT 'new'
        CHECK (status IN ('new', 'resolved')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

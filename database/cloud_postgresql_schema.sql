-- CrabFarmMonitor Cloud — schema IoT lõi (telemetry, HDF5, đa tổ chức)
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TABLE IF NOT EXISTS organizations (
    id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS farms (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id      UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    code        TEXT NOT NULL,
    name        TEXT NOT NULL,
    address     TEXT,
    owner_id    UUID,
    description TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (org_id, code)
);

CREATE TABLE IF NOT EXISTS devices (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id           UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    box_id            UUID,
    device_code       TEXT NOT NULL,
    device_name       TEXT,
    mac_address       MACADDR,
    firmware_version  VARCHAR(20),
    ip_lan            VARCHAR(50),
    status            TEXT NOT NULL DEFAULT 'offline'
        CHECK (status IN ('online', 'offline', 'error')),
    last_telemetry_at TIMESTAMPTZ,
    last_seen_at      TIMESTAMPTZ,
    UNIQUE (farm_id, device_code)
);

CREATE TABLE IF NOT EXISTS telemetry_latest (
    farm_id     UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    device_id   UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    pin         SMALLINT NOT NULL,
    val         DOUBLE PRECISION NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (device_id, pin)
);

CREATE INDEX IF NOT EXISTS idx_telemetry_latest_farm ON telemetry_latest (farm_id);

CREATE TABLE IF NOT EXISTS hdf5_uploads (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id         UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    device_id       UUID REFERENCES devices(id) ON DELETE SET NULL,
    device_code     TEXT,
    storage_path    TEXT NOT NULL,
    size_bytes      BIGINT NOT NULL DEFAULT 0,
    checksum_sha256 TEXT,
    chunk_start_ms  BIGINT,
    chunk_end_ms    BIGINT,
    status          TEXT NOT NULL DEFAULT 'uploaded',
    received_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sync_jobs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id         UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    hdf5_upload_id  UUID REFERENCES hdf5_uploads(id) ON DELETE SET NULL,
    idempotency_key TEXT NOT NULL UNIQUE,
    status          TEXT NOT NULL DEFAULT 'acked'
);

CREATE TABLE IF NOT EXISTS users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id        UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    username      VARCHAR(50),
    email         TEXT NOT NULL,
    password_hash TEXT,
    full_name     VARCHAR(100),
    display_name  TEXT,
    phone         VARCHAR(20),
    role          TEXT NOT NULL DEFAULT 'staff'
        CHECK (role IN ('admin', 'manager', 'staff', 'owner', 'operator')),
    is_active     BOOLEAN NOT NULL DEFAULT true,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (org_id, email)
);

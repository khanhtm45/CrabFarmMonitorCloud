-- Hộp cua & từng con (RAS MVP)
CREATE TABLE IF NOT EXISTS crab_boxes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    farm_id         UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    code            TEXT NOT NULL,
    label           TEXT,
    row_label       TEXT,
    status          TEXT NOT NULL DEFAULT 'active'
                    CHECK (status IN ('active', 'watch', 'warning', 'disease', 'empty', 'harvested')),
    capacity        INT NOT NULL DEFAULT 1,
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (farm_id, code)
);

CREATE INDEX IF NOT EXISTS idx_crab_boxes_farm ON crab_boxes(farm_id);

CREATE TABLE IF NOT EXISTS crab_individuals (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    box_id          UUID NOT NULL REFERENCES crab_boxes(id) ON DELETE CASCADE,
    farm_id         UUID NOT NULL REFERENCES farms(id) ON DELETE CASCADE,
    tag_code        TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'healthy'
                    CHECK (status IN ('healthy', 'watch', 'sick', 'dead', 'harvested')),
    weight_grams    INT,
    molt_stage      TEXT,
    health_note     TEXT,
    last_weighed_at TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (box_id, tag_code)
);

CREATE INDEX IF NOT EXISTS idx_crab_individuals_box ON crab_individuals(box_id);
CREATE INDEX IF NOT EXISTS idx_crab_individuals_farm ON crab_individuals(farm_id);

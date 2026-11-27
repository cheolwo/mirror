PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS housing_region (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    parent_code TEXT REFERENCES housing_region(code),
    kind TEXT NOT NULL,
    as_of TEXT NOT NULL,
    registry_sha256 TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS housing_source (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    url TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS housing_snapshot (
    id INTEGER PRIMARY KEY,
    source_id TEXT NOT NULL REFERENCES housing_source(id),
    district_code TEXT NOT NULL REFERENCES housing_region(code),
    housing_type TEXT NOT NULL CHECK(housing_type IN ('apartment', 'villa')),
    contract_group TEXT NOT NULL CHECK(contract_group IN ('sale', 'rent')),
    contract_start TEXT NOT NULL,
    contract_end TEXT NOT NULL,
    retrieved_at TEXT NOT NULL,
    row_count INTEGER NOT NULL CHECK(row_count >= 0),
    raw_sha256 TEXT,
    UNIQUE(district_code, housing_type, contract_group, contract_start, contract_end, retrieved_at)
);

CREATE TABLE IF NOT EXISTS housing_transaction (
    snapshot_id INTEGER NOT NULL REFERENCES housing_snapshot(id),
    source_row INTEGER NOT NULL,
    district_code TEXT NOT NULL REFERENCES housing_region(code),
    legal_dong_code TEXT REFERENCES housing_region(code),
    legal_dong_name TEXT NOT NULL,
    housing_type TEXT NOT NULL CHECK(housing_type IN ('apartment', 'villa')),
    contract_group TEXT NOT NULL CHECK(contract_group IN ('sale', 'rent')),
    deal_kind TEXT NOT NULL CHECK(deal_kind IN ('sale', 'jeonse', 'monthly')),
    contract_date TEXT NOT NULL,
    building_name TEXT,
    area_m2 REAL CHECK(area_m2 > 0),
    floor INTEGER,
    build_year INTEGER,
    sale_krw INTEGER,
    deposit_krw INTEGER,
    monthly_krw INTEGER,
    canceled_on TEXT,
    contract_status TEXT,
    PRIMARY KEY (snapshot_id, source_row),
    CHECK(
        (deal_kind = 'sale' AND contract_group = 'sale' AND sale_krw > 0
            AND deposit_krw IS NULL AND monthly_krw IS NULL)
        OR (deal_kind = 'jeonse' AND contract_group = 'rent' AND sale_krw IS NULL
            AND deposit_krw > 0 AND monthly_krw = 0)
        OR (deal_kind = 'monthly' AND contract_group = 'rent' AND sale_krw IS NULL
            AND deposit_krw >= 0 AND monthly_krw > 0)
    )
);
CREATE INDEX IF NOT EXISTS housing_transaction_scope_idx
    ON housing_transaction(district_code, housing_type, contract_group, contract_date);
CREATE INDEX IF NOT EXISTS housing_transaction_lookup_idx
    ON housing_transaction(district_code, contract_date, housing_type, deal_kind, legal_dong_code, area_m2);

CREATE TABLE IF NOT EXISTS housing_unpriced_record (
    snapshot_id INTEGER NOT NULL REFERENCES housing_snapshot(id),
    source_row INTEGER NOT NULL,
    district_code TEXT NOT NULL REFERENCES housing_region(code),
    housing_type TEXT NOT NULL,
    contract_group TEXT NOT NULL,
    contract_date TEXT NOT NULL,
    deal_kind TEXT NOT NULL,
    deposit_krw INTEGER,
    monthly_krw INTEGER,
    exclusion_reason TEXT NOT NULL,
    PRIMARY KEY (snapshot_id, source_row)
);

CREATE TABLE IF NOT EXISTS housing_collection_attempt (
    id INTEGER PRIMARY KEY,
    district_code TEXT NOT NULL REFERENCES housing_region(code),
    housing_type TEXT NOT NULL,
    contract_group TEXT NOT NULL,
    contract_start TEXT NOT NULL,
    contract_end TEXT NOT NULL,
    finished_at TEXT NOT NULL,
    status TEXT NOT NULL CHECK(status IN ('stored', 'failed')),
    detail TEXT,
    snapshot_id INTEGER REFERENCES housing_snapshot(id)
);

CREATE TABLE IF NOT EXISTS district_income_reference (
    district_code TEXT NOT NULL REFERENCES housing_region(code),
    tax_year INTEGER NOT NULL,
    source_id TEXT NOT NULL REFERENCES housing_source(id),
    filer_count INTEGER NOT NULL CHECK(filer_count > 0),
    gross_wage_million_krw INTEGER NOT NULL CHECK(gross_wage_million_krw > 0),
    source_sheet TEXT NOT NULL,
    source_row INTEGER NOT NULL,
    PRIMARY KEY (district_code, tax_year)
);

CREATE VIEW IF NOT EXISTS v_housing_effective_transaction AS
    SELECT * FROM housing_transaction WHERE canceled_on IS NULL;
CREATE VIEW IF NOT EXISTS v_district_income_reference AS
    SELECT r.*, h.name AS district_name,
           CAST(ROUND(r.gross_wage_million_krw * 1000000.0 / r.filer_count) AS INTEGER) AS mean_gross_wage_krw
    FROM district_income_reference AS r JOIN housing_region AS h ON h.code = r.district_code;

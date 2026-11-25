PRAGMA application_id = 1213023553;

CREATE TABLE IF NOT EXISTS api_source (
    portal_id TEXT PRIMARY KEY,
    official_url TEXT NOT NULL
) STRICT;

CREATE TABLE IF NOT EXISTS api_operation (
    operation_id TEXT PRIMARY KEY,
    portal_id TEXT NOT NULL REFERENCES api_source,
    kind TEXT NOT NULL,
    endpoint TEXT NOT NULL,
    contract_sha256 TEXT NOT NULL
) STRICT;

CREATE TABLE IF NOT EXISTS collection_run (
    run_id INTEGER PRIMARY KEY,
    operation_id TEXT NOT NULL REFERENCES api_operation,
    request_json TEXT NOT NULL CHECK(json_valid(request_json)),
    started_at TEXT NOT NULL,
    finished_at TEXT,
    state TEXT NOT NULL CHECK(state IN ('running','complete','bounded','no_data','blocked','failed')),
    detail TEXT,
    row_count INTEGER NOT NULL DEFAULT 0,
    page_count INTEGER NOT NULL DEFAULT 0
) STRICT;

CREATE TABLE IF NOT EXISTS request_attempt (
    run_id INTEGER NOT NULL REFERENCES collection_run,
    page INTEGER NOT NULL,
    portal_id TEXT NOT NULL REFERENCES api_source,
    local_date TEXT NOT NULL,
    PRIMARY KEY(run_id, page)
) STRICT;
CREATE INDEX IF NOT EXISTS request_attempt_daily ON request_attempt(portal_id, local_date);

CREATE TABLE IF NOT EXISTS response_snapshot (
    snapshot_id INTEGER PRIMARY KEY,
    operation_id TEXT NOT NULL REFERENCES api_operation,
    contract_sha256 TEXT NOT NULL,
    request_json TEXT NOT NULL CHECK(json_valid(request_json)),
    content_sha256 TEXT NOT NULL CHECK(length(content_sha256) = 64),
    row_count INTEGER NOT NULL,
    UNIQUE(operation_id, contract_sha256, request_json, content_sha256)
) STRICT;

CREATE TABLE IF NOT EXISTS run_snapshot (
    run_id INTEGER NOT NULL,
    page INTEGER NOT NULL,
    snapshot_id INTEGER NOT NULL REFERENCES response_snapshot,
    response_sha256 TEXT NOT NULL CHECK(length(response_sha256) = 64),
    retrieved_at TEXT NOT NULL,
    PRIMARY KEY(run_id, page),
    FOREIGN KEY(run_id, page) REFERENCES request_attempt
) STRICT;

CREATE TABLE IF NOT EXISTS map_record (
    record_id INTEGER PRIMARY KEY,
    portal_id TEXT NOT NULL REFERENCES api_source,
    identity_scope TEXT NOT NULL,
    kind TEXT NOT NULL,
    source_key TEXT NOT NULL CHECK(json_valid(source_key)),
    UNIQUE(portal_id, identity_scope, source_key)
) STRICT;
CREATE INDEX IF NOT EXISTS map_record_kind ON map_record(kind);

CREATE TABLE IF NOT EXISTS record_revision (
    revision_id INTEGER PRIMARY KEY,
    record_id INTEGER NOT NULL REFERENCES map_record,
    content_sha256 TEXT NOT NULL CHECK(length(content_sha256) = 64),
    data_json TEXT NOT NULL CHECK(json_valid(data_json)),
    source_updated_at TEXT,
    UNIQUE(record_id, content_sha256),
    UNIQUE(record_id, revision_id)
) STRICT;

CREATE TABLE IF NOT EXISTS snapshot_record (
    snapshot_id INTEGER NOT NULL REFERENCES response_snapshot,
    revision_id INTEGER NOT NULL REFERENCES record_revision,
    PRIMARY KEY(snapshot_id, revision_id)
) STRICT;

CREATE TABLE IF NOT EXISTS record_current (
    record_id INTEGER PRIMARY KEY,
    revision_id INTEGER NOT NULL,
    run_id INTEGER NOT NULL REFERENCES collection_run,
    FOREIGN KEY(record_id, revision_id) REFERENCES record_revision(record_id, revision_id)
) STRICT;

CREATE TABLE IF NOT EXISTS region_release (
    release_id INTEGER PRIMARY KEY,
    filename TEXT NOT NULL,
    official_url TEXT NOT NULL,
    as_of TEXT NOT NULL,
    sha256 TEXT NOT NULL UNIQUE CHECK(length(sha256) = 64)
) STRICT;

CREATE TABLE IF NOT EXISTS region (
    release_id INTEGER NOT NULL REFERENCES region_release,
    kind TEXT NOT NULL CHECK(kind IN ('sido','sigungu','administrative','legal')),
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    parent_kind TEXT,
    parent_code TEXT,
    CHECK((parent_kind IS NULL) = (parent_code IS NULL)),
    PRIMARY KEY(release_id, kind, code),
    FOREIGN KEY(release_id, parent_kind, parent_code) REFERENCES region DEFERRABLE INITIALLY DEFERRED
) STRICT;

CREATE TABLE IF NOT EXISTS region_jurisdiction (
    release_id INTEGER NOT NULL,
    administrative_kind TEXT NOT NULL DEFAULT 'administrative' CHECK(administrative_kind = 'administrative'),
    administrative_code TEXT NOT NULL,
    legal_kind TEXT NOT NULL DEFAULT 'legal' CHECK(legal_kind = 'legal'),
    legal_code TEXT NOT NULL,
    PRIMARY KEY(release_id, administrative_code, legal_code),
    FOREIGN KEY(release_id, administrative_kind, administrative_code) REFERENCES region,
    FOREIGN KEY(release_id, legal_kind, legal_code) REFERENCES region
) STRICT;
CREATE INDEX IF NOT EXISTS region_jurisdiction_by_legal ON region_jurisdiction(release_id, legal_code);

CREATE TABLE IF NOT EXISTS record_region_hint (
    revision_id INTEGER NOT NULL REFERENCES record_revision,
    kind TEXT NOT NULL CHECK(kind IN ('sido','sigungu','administrative','legal')),
    code TEXT NOT NULL,
    evidence_field TEXT NOT NULL,
    PRIMARY KEY(revision_id, kind, code)
) STRICT;
CREATE INDEX IF NOT EXISTS record_region_by_code ON record_region_hint(kind, code);

CREATE TABLE IF NOT EXISTS map_subject (
    subject_id INTEGER PRIMARY KEY,
    scheme TEXT NOT NULL,
    source_identifier TEXT NOT NULL,
    UNIQUE(scheme, source_identifier)
) STRICT;

CREATE TABLE IF NOT EXISTS record_subject (
    revision_id INTEGER NOT NULL REFERENCES record_revision,
    subject_id INTEGER NOT NULL REFERENCES map_subject,
    role TEXT NOT NULL,
    PRIMARY KEY(revision_id, subject_id, role)
) STRICT;
CREATE INDEX IF NOT EXISTS record_subject_by_subject ON record_subject(subject_id);

CREATE TABLE IF NOT EXISTS spatial_feature (
    revision_id INTEGER PRIMARY KEY REFERENCES record_revision,
    source_crs TEXT NOT NULL,
    source_x REAL,
    source_y REAL,
    longitude REAL CHECK(longitude BETWEEN -180 AND 180),
    latitude REAL CHECK(latitude BETWEEN -90 AND 90),
    geometry_json TEXT CHECK(geometry_json IS NULL OR json_valid(geometry_json)),
    CHECK((longitude IS NULL) = (latitude IS NULL)),
    CHECK((source_x IS NULL) = (source_y IS NULL))
) STRICT;

CREATE TABLE IF NOT EXISTS map_observation (
    revision_id INTEGER NOT NULL REFERENCES record_revision,
    measure TEXT NOT NULL,
    value_decimal TEXT NOT NULL,
    unit TEXT NOT NULL,
    period_start TEXT,
    period_end TEXT,
    PRIMARY KEY(revision_id, measure)
) STRICT;

CREATE VIEW IF NOT EXISTS current_regions AS
SELECT r.*, p.as_of, p.sha256 AS registry_sha256
FROM region r JOIN region_release p USING(release_id)
WHERE p.release_id = (SELECT release_id FROM region_release ORDER BY as_of DESC, release_id DESC LIMIT 1);

CREATE VIEW IF NOT EXISTS current_map_records AS
SELECT m.*, v.revision_id, v.data_json, v.source_updated_at, c.run_id, q.finished_at AS collected_at,
       COALESCE(json_extract(v.data_json,'$.chgGb'),'')='D' AS is_deleted
FROM map_record m JOIN record_current c USING(record_id)
JOIN record_revision v ON v.revision_id=c.revision_id JOIN collection_run q USING(run_id);

CREATE VIEW IF NOT EXISTS current_map_regions AS
SELECT m.record_id, m.kind AS record_kind, h.kind AS region_kind, h.code AS region_code,
       r.name AS region_name, h.evidence_field, r.as_of AS registry_as_of
FROM current_map_records m JOIN record_region_hint h USING(revision_id)
LEFT JOIN current_regions r ON r.kind=h.kind AND r.code=h.code;

CREATE VIEW IF NOT EXISTS current_map_subjects AS
SELECT m.record_id, m.kind, s.scheme, s.source_identifier, b.role
FROM current_map_records m JOIN record_subject b USING(revision_id) JOIN map_subject s USING(subject_id);

CREATE VIEW IF NOT EXISTS current_map_features AS
SELECT m.record_id, m.kind, m.portal_id, f.source_crs, f.source_x, f.source_y,
       f.longitude, f.latitude, f.geometry_json
FROM current_map_records m JOIN spatial_feature f USING(revision_id)
WHERE m.is_deleted = 0;

CREATE VIEW IF NOT EXISTS current_map_observations AS
SELECT m.record_id, m.kind, m.portal_id, o.measure, o.value_decimal, o.unit, o.period_start, o.period_end
FROM current_map_records m JOIN map_observation o USING(revision_id);

PRAGMA user_version = 1;

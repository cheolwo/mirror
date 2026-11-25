PRAGMA application_id=1213023554;

CREATE TABLE source_file (
    role TEXT PRIMARY KEY,
    file_name TEXT NOT NULL,
    sha256 TEXT NOT NULL
) STRICT;

CREATE TABLE map_layer (
    layer_id TEXT PRIMARY KEY,
    label TEXT NOT NULL,
    map_scale TEXT NOT NULL,
    description TEXT NOT NULL
) STRICT;

CREATE TABLE region (
    kind TEXT NOT NULL,
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    parent_kind TEXT,
    parent_code TEXT,
    as_of TEXT NOT NULL,
    PRIMARY KEY(kind,code),
    FOREIGN KEY(parent_kind,parent_code) REFERENCES region DEFERRABLE INITIALLY DEFERRED
) STRICT;

CREATE TABLE region_boundary (
    region_code TEXT PRIMARY KEY,
    geometry_json TEXT NOT NULL CHECK(json_valid(geometry_json)),
    west REAL NOT NULL,
    east REAL NOT NULL,
    south REAL NOT NULL,
    north REAL NOT NULL,
    registry_match INTEGER NOT NULL CHECK(registry_match IN (0,1))
) STRICT;

CREATE TABLE map_entry (
    record_id INTEGER PRIMARY KEY,
    layer_id TEXT NOT NULL REFERENCES map_layer,
    revision_id INTEGER NOT NULL,
    operation_id TEXT NOT NULL,
    portal_id TEXT NOT NULL,
    run_id INTEGER NOT NULL,
    collected_at TEXT NOT NULL,
    source_updated_at TEXT,
    dimensions_json TEXT NOT NULL CHECK(json_valid(dimensions_json)),
    placement TEXT NOT NULL CHECK(placement IN
        ('api_point','api_area','projected_point','reference_point','region_only','provider_scope','unlocated'))
) STRICT;
CREATE INDEX map_entry_layer_placement ON map_entry(layer_id,placement);

CREATE TABLE entry_region (
    record_id INTEGER NOT NULL REFERENCES map_entry,
    region_kind TEXT NOT NULL,
    region_code TEXT NOT NULL,
    evidence TEXT NOT NULL,
    PRIMARY KEY(record_id,region_kind,region_code),
    FOREIGN KEY(region_kind,region_code) REFERENCES region
) STRICT;
CREATE INDEX entry_region_lookup ON entry_region(region_kind,region_code,record_id);

CREATE TABLE entry_subject (
    record_id INTEGER NOT NULL REFERENCES map_entry,
    scheme TEXT NOT NULL,
    identifier TEXT NOT NULL,
    role TEXT NOT NULL,
    PRIMARY KEY(record_id,scheme,identifier,role)
) STRICT;
CREATE INDEX entry_subject_lookup ON entry_subject(scheme,identifier,record_id);

CREATE TABLE entry_geometry (
    record_id INTEGER PRIMARY KEY REFERENCES map_entry,
    geometry_json TEXT NOT NULL CHECK(json_valid(geometry_json)),
    evidence TEXT NOT NULL,
    reference_id TEXT
) STRICT;
CREATE VIRTUAL TABLE entry_bounds USING rtree(record_id, west, east, south, north);

CREATE TABLE shop_building_candidate (
    record_id INTEGER PRIMARY KEY REFERENCES map_entry,
    administrative_code TEXT NOT NULL,
    building_id TEXT NOT NULL,
    evidence TEXT NOT NULL
) STRICT;
CREATE INDEX shop_building_region ON shop_building_candidate(administrative_code,building_id);

CREATE TABLE map_metric (
    record_id INTEGER NOT NULL REFERENCES map_entry,
    measure TEXT NOT NULL,
    value_decimal TEXT NOT NULL,
    unit TEXT NOT NULL,
    period_start TEXT,
    period_end TEXT,
    PRIMARY KEY(record_id,measure)
) STRICT;
CREATE INDEX map_metric_period ON map_metric(period_start,period_end,measure,unit);

CREATE TABLE collection_scope (
    run_id INTEGER PRIMARY KEY,
    layer_id TEXT NOT NULL REFERENCES map_layer,
    operation_id TEXT NOT NULL,
    state TEXT NOT NULL,
    detail TEXT,
    request_json TEXT NOT NULL CHECK(json_valid(request_json)),
    started_at TEXT NOT NULL,
    finished_at TEXT,
    rows_returned INTEGER NOT NULL
) STRICT;
CREATE INDEX collection_scope_layer ON collection_scope(layer_id,state);

CREATE VIEW regional_layer_inventory AS
SELECT x.region_kind,x.region_code,e.layer_id,e.placement,count(*) AS indexed_records
FROM entry_region x JOIN map_entry e USING(record_id)
GROUP BY x.region_kind,x.region_code,e.layer_id,e.placement;

CREATE VIEW layer_inventory AS
SELECT l.layer_id,l.label,l.map_scale,e.placement,count(e.record_id) AS indexed_records
FROM map_layer l LEFT JOIN map_entry e USING(layer_id)
GROUP BY l.layer_id,e.placement;

PRAGMA user_version=1;

CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE child_tile (
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6),
    slot_label TEXT NOT NULL,seed_x REAL NOT NULL,seed_y REAL NOT NULL,
    full_area_m2 REAL NOT NULL CHECK(full_area_m2>0),seoul_area_m2 REAL NOT NULL CHECK(seoul_area_m2>=0),
    vertices_json TEXT NOT NULL,
    PRIMARY KEY(q,r,slot), FOREIGN KEY(q,r) REFERENCES tile(q,r)
) WITHOUT ROWID;
CREATE TABLE child_admin (
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,admin_code TEXT NOT NULL REFERENCES admin_dong(code),
    overlap_m2 REAL NOT NULL CHECK(overlap_m2>0),
    PRIMARY KEY(q,r,slot,admin_code), FOREIGN KEY(q,r,slot) REFERENCES child_tile(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX child_admin_by_dong ON child_admin(admin_code,q,r,slot);
CREATE TABLE source_grid (
    grid_code TEXT PRIMARY KEY,xmin REAL NOT NULL,ymin REAL NOT NULL,xmax REAL NOT NULL,ymax REAL NOT NULL
) WITHOUT ROWID;
CREATE TABLE grid_child_weight (
    grid_code TEXT NOT NULL REFERENCES source_grid(grid_code),q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    overlap_m2 REAL NOT NULL CHECK(overlap_m2>=0),sample_points INTEGER NOT NULL CHECK(sample_points BETWEEN 0 AND 25),
    PRIMARY KEY(grid_code,q,r,slot), FOREIGN KEY(q,r,slot) REFERENCES child_tile(q,r,slot),
    CHECK(overlap_m2>0 OR sample_points>0)
) WITHOUT ROWID;
CREATE INDEX grid_child_weight_by_tile ON grid_child_weight(q,r,slot);
CREATE VIEW v_grid_child_weight AS
SELECT w.*,overlap_m2/((g.xmax-g.xmin)*(g.ymax-g.ymin)) AS geometry_fraction,
       sample_points/25.0 AS legacy_sample_fraction
FROM grid_child_weight w JOIN source_grid g USING(grid_code);

CREATE TABLE food_source (
    source_id TEXT PRIMARY KEY,dataset_id TEXT NOT NULL,period TEXT NOT NULL,url TEXT,sha256 TEXT NOT NULL
) WITHOUT ROWID;
CREATE TABLE food_category (category_id INTEGER PRIMARY KEY,name TEXT NOT NULL);
CREATE TABLE food_child_position (
    source_id TEXT NOT NULL REFERENCES food_source(source_id),source_row INTEGER NOT NULL,
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,category_id INTEGER NOT NULL REFERENCES food_category(category_id),
    PRIMARY KEY(source_id,source_row), FOREIGN KEY(q,r,slot) REFERENCES child_tile(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX food_child_position_by_tile ON food_child_position(q,r,slot,category_id);
CREATE VIEW v_food_child AS
SELECT q,r,slot,category_id,count(*) AS licenses FROM food_child_position GROUP BY 1,2,3,4;

CREATE TABLE transport_mode (mode TEXT PRIMARY KEY) WITHOUT ROWID;
CREATE TABLE transport_child_position (
    mode TEXT NOT NULL REFERENCES transport_mode(mode),place_id TEXT NOT NULL,name TEXT NOT NULL,line_or_ars TEXT,
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    PRIMARY KEY(mode,place_id), FOREIGN KEY(q,r,slot) REFERENCES child_tile(q,r,slot)
) WITHOUT ROWID;
CREATE INDEX transport_child_position_by_tile ON transport_child_position(q,r,slot,mode);

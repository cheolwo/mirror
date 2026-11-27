CREATE TABLE resident_month (
    admin_code TEXT NOT NULL REFERENCES admin_dong(code), period_end TEXT NOT NULL,
    people INTEGER NOT NULL, single_households INTEGER,
    people_source_id TEXT NOT NULL REFERENCES source_release(id),
    household_source_id TEXT REFERENCES source_release(id),
    PRIMARY KEY(admin_code,period_end)
);

CREATE TABLE transport_place (
    mode TEXT NOT NULL CHECK(mode IN ('rail','bus')), place_id TEXT NOT NULL,
    name TEXT NOT NULL, line_or_ars TEXT, q INTEGER,r INTEGER,
    admin_code TEXT REFERENCES admin_dong(code),
    tile_status TEXT NOT NULL, area_status TEXT NOT NULL, upstream_status TEXT NOT NULL,
    source_id TEXT REFERENCES source_release(id),
    PRIMARY KEY(mode,place_id), FOREIGN KEY(q,r) REFERENCES tile(q,r),
    CHECK((q IS NULL)=(r IS NULL))
);
CREATE INDEX transport_by_name ON transport_place(mode,name,line_or_ars);
CREATE INDEX transport_by_tile ON transport_place(q,r,mode,place_id);
CREATE INDEX transport_by_area ON transport_place(admin_code,mode,place_id);

CREATE TABLE transport_tag (
    mode TEXT NOT NULL, place_id TEXT NOT NULL,
    period_type TEXT NOT NULL CHECK(period_type IN ('month','day')), period TEXT NOT NULL,
    hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    boardings INTEGER, alightings INTEGER, scope_records INTEGER NOT NULL,
    source_id TEXT NOT NULL REFERENCES source_release(id),
    PRIMARY KEY(mode,place_id,period_type,period,hour),
    FOREIGN KEY(mode,place_id) REFERENCES transport_place(mode,place_id)
) WITHOUT ROWID;
CREATE VIEW v_transport_tile_hour AS
SELECT p.q,p.r,o.mode,o.period_type,o.period,o.hour,sum(o.boardings) AS boardings,
       sum(o.alightings) AS alightings,count(*) AS places,sum(o.scope_records) AS scope_records
FROM transport_tag o JOIN transport_place p USING(mode,place_id)
WHERE p.q IS NOT NULL GROUP BY p.q,p.r,o.mode,o.period_type,o.period,o.hour;

CREATE TABLE food_category (
    category_id INTEGER PRIMARY KEY, name TEXT NOT NULL
);
CREATE TABLE food_license (
    source_id TEXT NOT NULL REFERENCES source_release(id), source_row INTEGER NOT NULL,
    district_code TEXT NOT NULL REFERENCES district(code), legal_code TEXT REFERENCES legal_dong(code),
    admin_code TEXT REFERENCES admin_dong(code), admin_binding TEXT NOT NULL,
    category_id INTEGER NOT NULL REFERENCES food_category(category_id),
    q INTEGER,r INTEGER, tile_status TEXT NOT NULL,
    PRIMARY KEY(source_id,source_row), FOREIGN KEY(q,r) REFERENCES tile(q,r),
    FOREIGN KEY(admin_code,legal_code) REFERENCES admin_legal(admin_code,legal_code),
    CHECK((q IS NULL)=(r IS NULL))
) WITHOUT ROWID;
CREATE INDEX food_by_district ON food_license(district_code,category_id);
CREATE INDEX food_by_admin ON food_license(admin_code,category_id);
CREATE INDEX food_by_tile ON food_license(q,r,category_id);
CREATE VIEW v_food_tile AS
SELECT q,r,category_id,count(*) AS licenses FROM food_license
WHERE q IS NOT NULL GROUP BY q,r,category_id;
CREATE VIEW v_food_district AS
SELECT district_code,category_id,count(*) AS licenses,count(q) AS tile_located
FROM food_license GROUP BY district_code,category_id;

CREATE VIEW v_resident_district AS
SELECT d.district_code,r.period_end,count(*) AS admin_dongs,sum(r.people) AS people,
       sum(r.single_households) AS single_households
FROM resident_month r JOIN admin_dong d ON d.code=r.admin_code
GROUP BY d.district_code,r.period_end;

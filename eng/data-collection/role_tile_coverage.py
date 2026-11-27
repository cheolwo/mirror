"""Measure where the proposed role layers have evidence in the Seoul child grid."""

import argparse
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_bundle import LAYERS, database_path
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from role_tile_catalog import CATALOG_DB, OUTPUT, SOURCES
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import digest


CORE = TILE_OUTPUT / DBNAME
DBNAME_OUT = "role-tile-spatial-coverage.sqlite"
EVIDENCE = {
    "foodLicenses": None, "transitPoints": None, "crossings": None, "parksAndTrees": None,
    "presence": "allocated_visible_mean IS NOT NULL", "terrain": "elevation_count>0",
    "waterSurface": "water_union_inside_seoul_m2>0", "bike": None, "streetLighting": None,
    "seoulFacilities": None, "streetSurface": "sidewalk_source_rows>0 OR road_source_rows>0",
}
NEW_EVIDENCE = {
    "libraryHours": ("libraries", None), "riderRest": ("rider_support", None),
    "deliveryArea": ("delivery_area", "overlap_in_seoul_m2>0"),
    "commercialArea": ("commercial_geometry", None), "foodChurn": ("food_churn", None),
    "elevator": ("elevator_location", None), "motorcycleHotspot": ("motorcycle", None),
    "seoulFreightParking": ("seoul_freight_parking", None), "cropLand": ("farmmap", "cultivated_union_m2>0"),
    "pedestrianEntrance": ("pedestrian_entrance", None),
}
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE area (code TEXT PRIMARY KEY,name TEXT NOT NULL,district_code TEXT NOT NULL);
CREATE TABLE child (q INTEGER,r INTEGER,slot INTEGER,PRIMARY KEY(q,r,slot)) WITHOUT ROWID;
CREATE TABLE child_area (
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT REFERENCES area(code),
    PRIMARY KEY(q,r,slot,admin_code),FOREIGN KEY(q,r,slot) REFERENCES child
) WITHOUT ROWID;
CREATE INDEX children_by_area ON child_area(admin_code,q,r,slot);
CREATE TABLE selected_field (id TEXT PRIMARY KEY,rule TEXT NOT NULL);
CREATE TABLE tile_support (
    q INTEGER,r INTEGER,slot INTEGER,field_id TEXT REFERENCES selected_field(id),
    quality TEXT CHECK(quality IN('public_evidence','masked_only')),
    PRIMARY KEY (q,r,slot,field_id),FOREIGN KEY(q,r,slot) REFERENCES child
) WITHOUT ROWID;
CREATE INDEX support_by_field ON tile_support(field_id,q,r,slot);
CREATE TABLE first_field (
    role_id TEXT,field_id TEXT REFERENCES selected_field(id),
    PRIMARY KEY(role_id,field_id)
) WITHOUT ROWID;
CREATE TABLE role_dong_support (
    role_id TEXT,admin_code TEXT REFERENCES area(code),tiles_touching INTEGER NOT NULL,
    spatial_first_fields INTEGER NOT NULL,tiles_with_any_spatial_field INTEGER,
    tiles_with_all_spatial_fields INTEGER,
    PRIMARY KEY(role_id,admin_code)
) WITHOUT ROWID;
CREATE VIEW v_role_dong_spatial_coverage AS
SELECT s.*,a.name,a.district_code FROM role_dong_support s JOIN area a ON a.code=s.admin_code;
CREATE VIEW v_field_seoul_spatial_coverage AS
SELECT f.id,f.rule,COUNT(s.q) AS tiles_with_any_support,
       SUM(CASE WHEN s.quality='public_evidence' THEN 1 ELSE 0 END) AS tiles_with_values_or_geometry,
       SUM(CASE WHEN s.quality='masked_only' THEN 1 ELSE 0 END) AS tiles_with_masked_only,
       (SELECT COUNT(*) FROM child) AS all_seoul_children
FROM selected_field f LEFT JOIN tile_support s ON s.field_id=f.id GROUP BY f.id,f.rule;
"""


def relation_keys(file, relation, where):
    sql = f"SELECT DISTINCT q,r,slot FROM {relation} WHERE q IS NOT NULL AND slot BETWEEN 0 AND 6"
    if where:
        sql += " AND (" + where + ")"
    with closing(read_only(file)) as db:
        yield from db.execute(sql)


def road_speed_keys():
    with closing(read_only(database_path("roadSpeed"))) as db:
        db.execute("CREATE TEMP TABLE speed_sample_link(link_id TEXT PRIMARY KEY)")
        db.execute("""INSERT INTO speed_sample_link SELECT DISTINCT link_id FROM speed_day
            WHERE json_extract(by_hour_kmh,'$[12]') IS NOT NULL OR json_extract(by_hour_kmh,'$[18]') IS NOT NULL""")
        yield from db.execute("""SELECT DISTINCT q,r,slot FROM road_tile_link JOIN speed_sample_link USING(link_id)
                    WHERE slot BETWEEN 0 AND 6""")


def warehouse_keys():
    pyproj, _shapely = gis()
    convert = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    file, relation, _sid, _receipt = SOURCES["warehouses"]
    with closing(read_only(file)) as db:
        candidates = db.execute(f"""SELECT longitude,latitude FROM {relation}
            WHERE district_code LIKE '11%' AND geographic_crs IN('EPSG:4326','OGC:CRS84','CRS84')""")
        keys = {point_key(*convert(lon, lat)) for lon, lat in candidates}
    return sorted(keys)


def add_keys(db, field_id, rule, observed, valid, quality="public_evidence"):
    db.execute("INSERT OR IGNORE INTO selected_field VALUES (?,?)", (field_id, rule))
    db.executemany("INSERT INTO tile_support VALUES (?,?,?,?,?)",
                   ((*key, field_id, quality) for key in observed if key in valid))


def add_stay(db, tiles):
    file, view, _sid, _receipt = SOURCES["stay"]
    with closing(read_only(file)) as source:
        rows = source.execute(f"""SELECT q,r,slot,COUNT(mean_known_allocated_per_start_hour),
                    SUM(masked_or_partial_weighted_grid_hours) FROM {view} GROUP BY 1,2,3""").fetchall()
    rule = "공표 체류 성분이 하나 이상 배분된 칸과 수치 없이 가림만 확인된 칸을 분리"
    add_keys(db, "stay", rule, ((q,r,s) for q,r,s,n,_m in rows if n), tiles)
    add_keys(db, "stay", rule, ((q,r,s) for q,r,s,n,m in rows if not n and m), tiles, "masked_only")


def role_totals(db, catalog):
    keys = [row[0] for row in catalog.execute("SELECT id FROM role ORDER BY menu_order")]
    for role in keys:
        proposed = [row[0] for row in catalog.execute("""SELECT field_key FROM v_role_tile_inventory
            WHERE role_id=? AND phase='initial' AND evidence_class<>'area_context'""", (role,))]
        if not proposed:
            db.execute("""INSERT INTO role_dong_support SELECT ?,admin_code,COUNT(*),0,NULL,NULL
                          FROM child_area GROUP BY admin_code""", (role,))
            continue
        db.executemany("INSERT INTO first_field VALUES (?,?)", ((role, ref) for ref in proposed))
        db.execute("""WITH tally AS (
            SELECT s.q,s.r,s.slot,COUNT(*) AS n FROM tile_support s JOIN first_field f USING(field_id)
            WHERE f.role_id=? AND s.quality='public_evidence' GROUP BY 1,2,3
        ) INSERT INTO role_dong_support
        SELECT ?,a.admin_code,COUNT(*),?,SUM(CASE WHEN t.n>0 THEN 1 ELSE 0 END),
               SUM(CASE WHEN t.n=? THEN 1 ELSE 0 END)
        FROM child_area a LEFT JOIN tally t USING(q,r,slot) GROUP BY a.admin_code""",
                   (role, role, len(proposed), len(proposed)))


def build(output):
    catalog_file = output / CATALOG_DB
    with closing(read_only(CORE)) as core:
        tiles = set(core.execute("SELECT q,r,slot FROM child_tile WHERE seoul_area_m2>0"))
        areas = core.execute("SELECT code,name,district_code FROM admin_dong ORDER BY 1").fetchall()
        relation = core.execute("SELECT q,r,slot,admin_code FROM child_admin ORDER BY 1,2,3,4").fetchall()
    with TemporaryDirectory(dir=output) as tmp, closing(read_only(catalog_file)) as catalog:
        path = Path(tmp) / DBNAME_OUT
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO area VALUES (?,?,?)", areas)
                db.executemany("INSERT INTO child VALUES (?,?,?)", sorted(tiles))
                db.executemany("INSERT INTO child_area VALUES (?,?,?,?)", relation)
                for field, where in EVIDENCE.items():
                    relation_name = LAYERS[field][1]
                    add_keys(db, field, "공개 관측·배치 후보가 있는 칸", relation_keys(
                        database_path(field), relation_name, where), tiles)
                for field, (name, where) in NEW_EVIDENCE.items():
                    file, view, _sid, _receipt = SOURCES[name]
                    add_keys(db, field, "위치 행·공표 관측·겹침이 있는 칸", relation_keys(file, view, where), tiles)
                add_keys(db, "roadSpeed", "10월 13시·19시 종료 시각에 숫자가 하나 이상 있는 도로 방향과 겹침",
                         road_speed_keys(), tiles)
                add_keys(db, "warehouseLocations", "전국 물류 등록 자료의 서울 확인 대표점",
                         warehouse_keys(), tiles)
                add_stay(db, tiles)
                role_totals(db, catalog)
            check_database(db)
            db.row_factory = sqlite3.Row
            field_rows = [dict(row) for row in db.execute("SELECT * FROM v_field_seoul_spatial_coverage ORDER BY id")]
            role_rows = [dict(row) for row in db.execute("""SELECT role_id,COUNT(*) AS named_dongs,
                    MAX(spatial_first_fields) AS spatial_first_fields,
                    COUNT(CASE WHEN tiles_with_any_spatial_field>0 THEN 1 END) AS dongs_with_some,
                    COUNT(CASE WHEN tiles_with_all_spatial_fields>0 THEN 1 END) AS dongs_with_all
                FROM role_dong_support GROUP BY 1 ORDER BY 1""")]
        path.replace(output / DBNAME_OUT)
    result = {
        "geometryRevision": REVISION, "spatialExtent": "서울과 면적이 겹치는 세부칸",
        "roles": role_rows, "fields": field_rows, "sourceRoleCatalogSha256": digest(catalog_file),
        "tileCoreSha256": digest(CORE), "databaseSha256": digest(output / DBNAME_OUT),
        "integrityCheck": "ok", "foreignKeyViolations": 0,
    }
    atomic_json(output / "coverage-verification.json", result)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    print(json.dumps(build(parser.parse_args().output)["roles"], ensure_ascii=False))

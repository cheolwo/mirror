"""Index where each evidence layer can be displayed, independently of its value."""

from collections import defaultdict
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_bundle import LAYERS, database_path
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import ROOT, digest


SUPPORT_QUERIES = {
    "foodLicenses": "SELECT DISTINCT q,r,slot FROM v_food_child",
    "transitPoints": "SELECT DISTINCT q,r,slot FROM transport_child_position",
    "presence": "SELECT DISTINCT q,r,slot FROM child_presence_profile WHERE allocated_visible_mean IS NOT NULL",
    "buildings": "SELECT q,r,slot FROM v_building_child WHERE building_count>0",
    "terrain": "SELECT q,r,slot FROM v_terrain_child WHERE elevation_count>0",
    "spotElevation": "SELECT q,r,slot FROM v_spot_child WHERE spot_count>0",
    "landCover": """SELECT DISTINCT q,r,slot FROM v_tile_cover_reference
        WHERE slot>=0 AND pct_of_known_survey_source IS NOT NULL""",
    "shadeShelters": "SELECT DISTINCT q,r,slot FROM v_shade_tile_reference",
    "parksAndTrees": "SELECT DISTINCT q,r,slot FROM v_tile_reference_points",
    "roadCoverage": "SELECT q,r,slot FROM v_road_tile_coverage WHERE slot>=0 AND geometry_links>0",
    "roadSpeed": """SELECT DISTINCT q,r,slot FROM road_tile_link g WHERE slot>=0 AND EXISTS (
        SELECT 1 FROM speed_day s JOIN json_each(s.by_hour_kmh) h
        WHERE g.link_id=s.link_id AND h.value IS NOT NULL)""",
    "crossings": "SELECT DISTINCT q,r,slot FROM crossing_point",
    "walkingNetwork": "SELECT DISTINCT q,r,slot FROM walk_tile",
    "walkingSensors": "SELECT DISTINCT q,r,slot FROM v_sensor_tile_reference",
    "bike": "SELECT DISTINCT q,r,slot FROM v_bike_tile_month",
    "stationAccess": "SELECT DISTINCT q,r,slot FROM v_station_access_reference",
    "streetLighting": "SELECT DISTINCT q,r,slot FROM v_light_child",
    "seoulFacilities": "SELECT DISTINCT q,r,slot FROM v_tile_facility_count",
    "buildingFootprint": "SELECT q,r,slot FROM v_building_polygon_child WHERE footprint_union_m2>0",
    "streetSurface": "SELECT q,r,slot FROM tile_area WHERE slot>=0 AND (sidewalk_union_m2>0 OR road_union_m2>0)",
    "bus20261121": """SELECT DISTINCT q,r,slot FROM v_tile_bus_day_hour WHERE service_date='2026-11-21'
        AND (boardings IS NOT NULL OR alightings IS NOT NULL)""",
    "waterSurface": """SELECT q,r,slot FROM v_tile_water_reference WHERE slot>=0
        AND source_id='seoul-biotope-surface-water' AND water_union_inside_seoul_m2>0""",
    "evSupport": "SELECT q,r,slot FROM v_tile_ev_support WHERE source_sites>0",
    "landmarks": "SELECT DISTINCT q,r,slot FROM landmark_tile",
}
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE layer (
    id TEXT PRIMARY KEY, path TEXT NOT NULL, sha256 TEXT NOT NULL,
    has_support_sql TEXT NOT NULL, has_support_meaning TEXT NOT NULL
);
CREATE TABLE admin_dong (
    code TEXT PRIMARY KEY,name TEXT NOT NULL,district_code TEXT NOT NULL,
    tiles_touching INTEGER NOT NULL CHECK(tiles_touching>0),
    intersecting_area_m2 REAL NOT NULL CHECK(intersecting_area_m2>0)
);
CREATE TABLE dong_layer (
    admin_code TEXT REFERENCES admin_dong,layer_id TEXT REFERENCES layer,
    intersecting_tiles_with_support INTEGER NOT NULL CHECK(intersecting_tiles_with_support>=0),
    intersection_area_in_supported_tiles_m2 REAL NOT NULL CHECK(intersection_area_in_supported_tiles_m2>=0),
    PRIMARY KEY(admin_code,layer_id)
) WITHOUT ROWID;
CREATE INDEX coverage_by_layer ON dong_layer(layer_id,admin_code);
CREATE VIEW v_dong_layer_coverage AS
SELECT d.code,d.name,d.district_code,d.tiles_touching,l.layer_id,l.intersecting_tiles_with_support,
       d.intersecting_area_m2,l.intersection_area_in_supported_tiles_m2,
       l.intersection_area_in_supported_tiles_m2 / d.intersecting_area_m2 AS share_of_dong_in_tiles_with_support
FROM dong_layer l JOIN admin_dong d ON d.code=l.admin_code;
"""


def build():
    with closing(read_only(OUTPUT / DBNAME)) as core:
        overlaps = defaultdict(list)
        coverage = {}
        for q, r, slot, code, area in core.execute("SELECT q,r,slot,admin_code,overlap_m2 FROM child_admin"):
            overlaps[q,r,slot].append((code, area))
        admin = core.execute("""SELECT a.code,a.name,a.district_code,COUNT(*),SUM(m.overlap_m2)
            FROM admin_dong a JOIN child_admin m ON m.admin_code=a.code GROUP BY a.code ORDER BY a.code""").fetchall()
        all_keys = set(core.execute("SELECT q,r,slot FROM child_tile"))
    target = OUTPUT / "dong-layer-coverage.sqlite"
    with TemporaryDirectory(dir=OUTPUT) as temp:
        draft = Path(temp) / target.name
        with closing(sqlite3.connect(draft)) as out:
            out.executescript(SCHEMA)
            with out:
                out.executemany("INSERT INTO admin_dong VALUES (?,?,?,?,?)", admin)
                for name, sql in SUPPORT_QUERIES.items():
                    path = database_path(name)
                    with closing(read_only(path)) as db:
                        keys = set(db.execute(sql))
                    if keys - all_keys:
                        raise ValueError(f"layer_coverage_unrecognized_keys:{name}")
                    counts = defaultdict(int)
                    area_sum = defaultdict(float)
                    for key in sorted(keys):
                        for code, area in overlaps.get(key, ()):
                            counts[code] += 1
                            area_sum[code] += area
                    out.execute("INSERT INTO layer VALUES (?,?,?,?,?)",
                                (name,path.relative_to(ROOT).as_posix(),digest(path),sql,LAYERS[name][3]))
                    rows = [(code,name,counts[code],area_sum[code]) for code,_,_,n,a in admin]
                    if any(counts[code] > n or area_sum[code] > a+0.01 for code,_,_,n,a in admin):
                        raise ValueError(f"measured_tile_support_exceeds_dong:{name}")
                    out.executemany("INSERT INTO dong_layer VALUES (?,?,?,?)", rows)
                    coverage[name] = {"childTilesWithSupportInsideSeoul": len(keys & overlaps.keys()),
                                      "dongsTouchingSupportedTiles": sum(bool(row[2]) for row in rows)}
            check_database(out)
        draft.replace(target)
    result = {"geometryRevision": REVISION,"coverageMeaning": "presence_of_a_reference_in_touching_tile",
              "administrativeDongs": len(admin),"layerCoverageRows": len(admin)*len(SUPPORT_QUERIES),
              "databaseSha256": digest(target),"layers": coverage}
    atomic_json(OUTPUT / "coverage-summary.json", result)
    return result


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))

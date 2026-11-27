"""Link existing Seoul public facility records to the child grid with source provenance."""

from collections import Counter
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


INTAKE = PUBLIC / "diorama-public-layer-intake-20261123"
SEOUL = INTAKE / "facilities-same-pc.sqlite"
CIVIC = INTAKE / "r3-national-civic/national-civic.sqlite"
SCHOOL = INTAKE / "r47-school-zone-context/school-zone-context.sqlite"
BOUNDARY = PUBLIC / "national-admin-stat-map-20261119-r1/official-local/national/official-admin-statistics.sqlite"
SEOUL_LAYERS = ("heat_shade", "public_toilet", "drinking_water", "heat_shelter", "parcel_locker", "station_elevator")
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE input_database (input_id TEXT PRIMARY KEY,path TEXT NOT NULL,sha256 TEXT NOT NULL);
CREATE TABLE source (
    input_id TEXT NOT NULL REFERENCES input_database,id TEXT NOT NULL,url TEXT NOT NULL,
    PRIMARY KEY(input_id,id)
);
CREATE TABLE tile (q INTEGER,r INTEGER,slot INTEGER,PRIMARY KEY(q,r,slot)) WITHOUT ROWID;
CREATE TABLE admin_dong (code TEXT PRIMARY KEY,name TEXT NOT NULL,district_code TEXT NOT NULL);
CREATE TABLE tile_admin (
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT NOT NULL REFERENCES admin_dong,
    PRIMARY KEY(q,r,slot,admin_code),FOREIGN KEY(q,r,slot) REFERENCES tile
) WITHOUT ROWID;
CREATE TABLE facility(
    input_id TEXT NOT NULL,source_id TEXT NOT NULL,source_key TEXT NOT NULL,layer TEXT NOT NULL,
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT NOT NULL,period TEXT,period_means TEXT NOT NULL,
    subtype TEXT,access_type TEXT,operating_note TEXT,location_basis TEXT NOT NULL,
    PRIMARY KEY(input_id,source_id,source_key),FOREIGN KEY(input_id,source_id) REFERENCES source,
    FOREIGN KEY(q,r,slot,admin_code) REFERENCES tile_admin
) WITHOUT ROWID;
CREATE INDEX facilities_by_child ON facility(q,r,slot,layer);
CREATE INDEX facilities_by_admin ON facility(admin_code,layer,q,r,slot);
CREATE VIEW v_tile_facility_count AS
SELECT q,r,slot,layer,source_id,COUNT(*) AS referenced_rows,
       COUNT(period) AS known_periods,MIN(period) AS min_period,MAX(period) AS max_period,
       period_means
FROM facility GROUP BY q,r,slot,layer,source_id,period_means;
"""


def sources(db):
    db.executemany("INSERT INTO input_database VALUES (?,?,?)", (
        (key, path.relative_to(ROOT).as_posix(), digest(path))
        for key, path in (("seoul", SEOUL), ("civic", CIVIC), ("school", SCHOOL), ("boundary", BOUNDARY))
    ))


def official_seoul(shapely):
    with closing(read_only(BOUNDARY)) as src:
        rows = src.execute("""SELECT code,name,district_code,geometry_wkb FROM dong_map
            WHERE substr(district_code,1,2)='11' AND topology_valid=1 ORDER BY code""").fetchall()
    geometries, tags = [], []
    for code, name, district, wkb in rows:
        poly = shapely.from_wkb(wkb)
        if poly.is_valid and not poly.is_empty:
            geometries.append(poly)
            tags.append((code, name, district))
    return tags, shapely.STRtree(geometries)


def seoul_rows(stats):
    with closing(read_only(SEOUL)) as db:
        columns = """record_id,dataset_id,layer_id,lon,lat,dong_code,placement_state,
                     period_value,period_meaning,dimensions_json,source_url"""
        for record, source, layer, lon, lat, dong, state, period, meaning, raw, url in db.execute(
            f"SELECT {columns} FROM facility_for_diorama ORDER BY dataset_id,record_id"
        ):
            if layer not in SEOUL_LAYERS:
                continue
            fields = json.loads(raw)
            if state != "within_dong" or fields.get("repeatedProviderKey"):
                stats["seoul",layer,state if state != "within_dong" else "repeated_provider_key_review"] += 1
                continue
            subtype = fields.get("subtype") or fields.get("siteType")
            access = fields.get("accessType")
            schedule = fields.get("accessHours")
            if schedule is None and all(fields.get(k) for k in ("regularDays", "regularOpen", "regularClose")):
                schedule = " ".join([fields["regularDays"], fields["regularOpen"]+"–"+fields["regularClose"]])
            yield ("seoul",source,str(record),layer,lon,lat,dong,None,period,meaning,
                   subtype,access,schedule,url,"seoul_provider_point_and_official_dong")


def civic_rows(stats):
    with closing(read_only(CIVIC)) as db:
        for row in db.execute("""SELECT id,source_id,layer,source_lon,source_lat,district_name,
                observed_date,subtype,repeated_row,source_url,properties_json FROM facility_lookup
                WHERE province_code='11' AND placement_state='within_dong' ORDER BY source_id,id"""):
            record, source, layer, lon, lat, district, date, subtype, repeated, url, raw = row
            if repeated:
                stats["civic",layer,"repeated_raw_row"] += 1
                continue
            fields = json.loads(raw)
            access = fields.get("PRKPLCE_SE")
            rule = fields.get("PARKINGCHRGE_INFO")
            yield ("civic",source,str(record),layer,lon,lat,None,district,date,"개별 시설의 원본 보고 기준일",
                   subtype,access,rule,url,"public_point_with_historical_district_check")


def school_rows():
    with closing(read_only(SCHOOL)) as db:
        for number, gu, kind, date, lon, lat in db.execute("""SELECT workbook_row,gu,facility_kind,position_source_date,
                reviewed_longitude,reviewed_latitude FROM school_zone_reference_read
                WHERE identity_status='unique_same_name_and_road_address'
                AND dong_match_status='same_statistical_dong' AND reviewed_longitude IS NOT NULL ORDER BY workbook_row"""):
            yield ("school","OA-2799+15012891",str(number),"school_zone_reference",lon,lat,None,gu,date,
                   "전국 보호구역 위치 기록의 기준일",kind,None,None,
                   "https://data.seoul.go.kr/dataList/OA-2799/F/1/datasetView.do","registered_facility_reference_point")


def selected_points(db, shapely, official, transform, stats):
    tags, tree = official
    admin = dict(db.execute("SELECT code,district_name FROM upstream_admin_names"))
    geo = set(db.execute("SELECT q,r,slot,admin_code FROM tile_admin"))
    for read in (seoul_rows(stats), civic_rows(stats), school_rows()):
        for row in read:
            group, source, record, layer, lon, lat, dong, reported_district, period, period_means, subtype, access, note, url, basis = row
            candidates = tree.query(shapely.Point(lon, lat), predicate="within")
            if len(candidates) != 1:
                stats[group,layer,"no_unique_official_dong"] += 1
                continue
            code = tags[candidates[0]][0]
            if (dong and dong != code) or (reported_district and reported_district != admin.get(code)):
                stats[group,layer,"reporting_area_mismatch"] += 1
                continue
            key = point_key(*transform(lon, lat))
            if (*key, code) not in geo:
                stats[group,layer,"no_positive_overlap_for_dong_tile"] += 1
                continue
            db.execute("INSERT OR IGNORE INTO source VALUES (?,?,?)", (group,source,url))
            stats[group,layer,"linked"] += 1
            yield (group,source,record,layer,*key,code,period,period_means,subtype,access,note,basis)


def build():
    pyproj, shapely = gis()
    transform = pyproj.Transformer.from_crs(4326,5179,always_xy=True).transform
    output = OUTPUT / "facilities"
    output.mkdir(exist_ok=True)
    target = output / "tile-facilities.sqlite"
    stats = Counter()
    official = official_seoul(shapely)
    with TemporaryDirectory(dir=output) as temporary:
        draft = Path(temporary) / target.name
        with closing(sqlite3.connect(draft)) as db, closing(read_only(OUTPUT / DBNAME)) as geo:
            db.executescript(SCHEMA)
            with db:
                sources(db)
                db.executemany("INSERT INTO tile VALUES (?,?,?)", geo.execute("SELECT q,r,slot FROM child_tile"))
                db.executemany("INSERT INTO admin_dong VALUES (?,?,?)", geo.execute("SELECT code,name,district_code FROM admin_dong"))
                db.executemany("INSERT INTO tile_admin VALUES (?,?,?,?)",
                               geo.execute("SELECT q,r,slot,admin_code FROM child_admin"))
                db.execute("CREATE TEMP TABLE upstream_admin_names(code TEXT PRIMARY KEY,district_name TEXT NOT NULL)")
                db.executemany("INSERT INTO upstream_admin_names VALUES (?,?)",
                               geo.execute("SELECT a.code,d.name FROM admin_dong a JOIN district d ON d.code=a.district_code"))
                db.executemany("INSERT INTO facility VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                               selected_points(db, shapely, official, transform, stats))
            check_database(db)
            totals = dict(db.execute("SELECT layer,COUNT(*) FROM facility GROUP BY layer"))
            dongs = db.execute("SELECT COUNT(DISTINCT admin_code) FROM facility").fetchone()[0]
        draft.replace(target)
    summary = {"geometryRevision": REVISION, "distribution": "local-review", "officialValidDongs": len(official[0]),
               "districtDongsWithFacilities": dongs, "indexedRowsByLayer": totals,
               "sourceReview": [{"input": group, "layer": layer, "state": state, "rows": count}
                                for (group,layer,state),count in sorted(stats.items())],
               "observationKind": "facility_reference_points_as_reported",
               "prohibitsSummingOverlappingProviders": True, "databaseSha256": digest(target)}
    atomic_json(output / "facilities-summary.json", summary)
    return summary


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))

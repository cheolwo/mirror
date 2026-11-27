"""Index public S-DoT station readings and separately dated location candidates."""

from contextlib import closing
import csv
from datetime import datetime
import json
import math
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import ROOT, digest, local_path

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (sha256 TEXT PRIMARY KEY,path TEXT NOT NULL,url TEXT NOT NULL,kind TEXT NOT NULL,received_utc TEXT NOT NULL);
CREATE TABLE child (q INTEGER,r INTEGER,slot INTEGER CHECK(slot BETWEEN 0 AND 6),PRIMARY KEY(q,r,slot)) WITHOUT ROWID;
CREATE TABLE sensor_installation (
    sensor_code TEXT PRIMARY KEY, q INTEGER,r INTEGER,slot INTEGER,
    geo_state TEXT NOT NULL,position_reference_date TEXT NOT NULL,
    source_sha256 TEXT NOT NULL REFERENCES source(sha256),source_row INTEGER NOT NULL,
    FOREIGN KEY(q,r,slot) REFERENCES child,
    CHECK((q IS NULL)=(r IS NULL) AND (r IS NULL)=(slot IS NULL))
) WITHOUT ROWID;
CREATE TABLE sensor_reading (
    source_sha256 TEXT NOT NULL REFERENCES source(sha256),source_row INTEGER NOT NULL,
    sensor_code TEXT NOT NULL, matched_installation TEXT REFERENCES sensor_installation(sensor_code),
    measurement_local TEXT NOT NULL,registered_local TEXT NOT NULL,reported_visitor_count INTEGER NOT NULL CHECK(reported_visitor_count>=0),
    site_class TEXT,source_district_label TEXT,source_dong_label TEXT,
    PRIMARY KEY(source_sha256,source_row)
) WITHOUT ROWID;
CREATE INDEX readings_by_sensor ON sensor_reading(matched_installation,measurement_local);
CREATE VIEW v_sensor_tile_reference AS
SELECT s.q,s.r,s.slot,s.sensor_code,s.geo_state,s.position_reference_date,count(r.source_row) AS readings,
       min(r.measurement_local) AS first_measurement,max(r.measurement_local) AS last_measurement
FROM sensor_installation s JOIN sensor_reading r ON r.matched_installation=s.sensor_code
GROUP BY s.sensor_code;
"""


def numeric_code(value):
    raw = str(value).strip()
    if not raw.isascii() or not raw.isdecimal():
        raise ValueError("unexpected_sensor_code")
    return str(int(raw))


def build(output):
    from openpyxl import load_workbook

    receipts = json.loads((output / "source-receipts.json").read_text(encoding="utf-8"))["files"]
    paths = {}
    for item in receipts:
        path = local_path(ROOT / item["rawPath"])
        if path.stat().st_size != item["bytes"] or digest(path) != item["sha256"]:
            raise ValueError("sensor_raw_hash_mismatch")
        paths[item["sha256"]] = path
    (install,) = [item for item in receipts if item["kind"] == "installation"]
    with closing(read_only(OUTPUT / DBNAME)) as geo:
        keys = set(geo.execute("SELECT q,r,slot FROM child_tile"))
    pyproj, _ = gis()
    project = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    with TemporaryDirectory(dir=output) as tmp:
        path = Path(tmp) / "sensor-tile-reference.sqlite"
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO source VALUES (?,?,?,?,?)",
                               ((x["sha256"],x["rawPath"],x["sourceUrl"],x["kind"],x["retrievedAtUtc"]) for x in receipts))
                db.executemany("INSERT INTO child VALUES (?,?,?)", sorted(keys))
                matched = load_installation(db, install, paths[install["sha256"]], keys, project, load_workbook)
                for item in receipts:
                    if item["kind"] == "weekly":
                        load_readings(db, item, paths[item["sha256"]], matched)
            check_database(db)
            total, linked, sensors, first, last = db.execute("""SELECT count(*),count(matched_installation),
                count(DISTINCT sensor_code),min(measurement_local),max(measurement_local) FROM sensor_reading""").fetchone()
            tile_count = db.execute("""SELECT count(*) FROM (SELECT DISTINCT q,r,slot FROM v_sensor_tile_reference
                WHERE q IS NOT NULL)""").fetchone()[0]
            duplicates = db.execute("""SELECT count(*) FROM (
                SELECT sensor_code,measurement_local FROM sensor_reading GROUP BY 1,2 HAVING count(*)>1)""").fetchone()[0]
        path.replace(output / path.name)
    result = {"schema":"hongdal.dong-tile-sdot-reference.v1", "geometryRevision":REVISION,
              "positionReferenceDate":"2025-11-13", "installations":len(matched), "measurementRows":total,
              "rowsMatchedToDatedInstallation":linked, "sampleSensors":sensors,"candidateChildTiles":tile_count,
              "firstMeasurementKST":first, "lastMeasurementKST":last, "duplicatedDeviceTimestampKeys":duplicates,
              "unit":"공표 측정행별 방문자수, 기기별 측정방식 차이 포함",
              "binding":"CSV 시리얼의 숫자와 설치대장의 방문자 센서코드 일치 후보",
              "limitation":"설치일 이후 위치 유지 및 센서 방식 미확인. 생활인구 합산·보정에 사용하지 않음.",
              "distribution":"local-review", "databaseSha256":digest(output / "sensor-tile-reference.sqlite")}
    atomic_json(output/"sensor-summary.json", result)
    return result


def load_installation(db, item, path, keys, project, load_workbook):
    workbook = load_workbook(path, read_only=True, data_only=True)
    matches = set()
    try:
        rows = workbook.active.values
        if next(rows) != ("순번", "방문자 센서코드", "시리얼번호", "주소", "위도", "경도"):
            raise ValueError("sensor_installation_header_changed")
        for index, (_, code, _, _, lat, lon) in enumerate(rows, 2):
            if code is None:
                continue
            identity = numeric_code(code)
            latitude, longitude = float(lat), float(lon)
            if not (math.isfinite(latitude) and math.isfinite(longitude)):
                raise ValueError("sensor_coordinates_invalid")
            key = point_key(*project(longitude,latitude))
            state = "historical_position_candidate" if key in keys else "historical_position_outside_tiles"
            db.execute("INSERT INTO sensor_installation VALUES (?,?,?,?,?,?,?,?)", (
                identity, *(key if key in keys else (None,None,None)), state,"2025-11-13",item["sha256"],index
            ))
            matches.add(identity)
    finally:
        workbook.close()
    return matches


def load_readings(db, item, path, matched):
    with path.open(encoding="cp949", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != ["모델번호","시리얼","측정시간","지역","자치구","행정동","방문자수","등록일"]:
            raise ValueError("sensor_reading_header_changed")
        for index, row in enumerate(reader, 2):
            code = numeric_code(row["시리얼"])
            measured = datetime.fromisoformat(row["측정시간"].replace("_", "T"))
            registered = datetime.fromisoformat(row["등록일"])
            count = int(row["방문자수"])
            db.execute("INSERT INTO sensor_reading VALUES (?,?,?,?,?,?,?,?,?,?)", (
                item["sha256"],index,code,code if code in matched else None,measured.isoformat(),
                registered.isoformat(),count,row["지역"],row["자치구"],row["행정동"]
            ))


if __name__ == "__main__":
    print(json.dumps(build(OUTPUT/"sensors"), ensure_ascii=False, indent=2))

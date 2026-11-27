"""Build the Seoul-wide, station-line rail layer on the shared 300 m grid."""

import argparse
from collections import defaultdict
from contextlib import closing
import csv
from importlib.util import module_from_spec, spec_from_file_location
import json
import math
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from seoul_tile_transit import (
    MONTH, PUBLIC, RAIL, RAIL_DAILY_PAGE, RAIL_PAGE, RAIL_PATCH, RAIL_RAW, REFERENCE, ROOT,
    axial, digest, open_read, projection, rail_rows,
)


OUTPUT = PUBLIC / "seoul-area-evidence-20261125/rail"
DBNAME = "rail-area-index.sqlite"
KRIC = PUBLIC / "national-expansion-20261119-r1/transport/urban-rail-station-lines.csv"
KRIC_RECEIPT = KRIC.parent / "receipt.json"
KRIC_RAW = KRIC.parent / "raw/urban-rail-stations-kric.xlsx"
KRIC_PAGE = "https://www.data.go.kr/data/15013205/standard.do"
BOUNDARY_ZIP = PUBLIC / "national-expansion-20261119-r1/boundaries/raw/sgis-15129688-national-2025.zip"
BOUNDARY_HELPER = REFERENCE / "r3-national-parks/prepare.py"
BOUNDARY_PAGE = "https://www.data.go.kr/data/15129688/fileData.do"
LOCATION_BASE_PAGE = "https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=36"
LOCATION_PATCH_PAGE = "https://www.data.go.kr/data/15044230/fileData.do"
KORAIL_LINES = "https://info.korail.com/info/selectBbsNttView.do?bbsNo=199&key=911&nttNo=22491"
AREX_LINE = "https://www.airportrailroad.com/train/normal/introduce"
STATION_NAMES = "https://www.data.go.kr/data/15048028/fileData.do"

LINE_NAMES = {
    "경의선": ("경의중앙선", KORAIL_LINES),
    "중앙선": ("경의중앙선", KORAIL_LINES),
    "공항철도 1호선": ("인천국제공항선", AREX_LINE),
}

STATION_ALIASES = {
    ("4호선", "숙대입구(서울시교육청)"): ("숙대입구(갈월)", STATION_NAMES),
    ("5호선", "하남시청(덕풍?신장)"): ("하남시청(덕풍∙신장)", KRIC_PAGE),
    ("공항철도 1호선", "마곡나루(서울식물원)"): ("마곡나루", AREX_LINE),
    ("경의선", "서울역"): ("서울역(경의선)", KORAIL_LINES),
}

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE evidence_source (
    source_id TEXT PRIMARY KEY, source_url TEXT NOT NULL, local_input TEXT NOT NULL,
    sha256 TEXT NOT NULL
) WITHOUT ROWID;
CREATE TABLE rail_station (
    station_id TEXT PRIMARY KEY, name TEXT NOT NULL, line TEXT NOT NULL,
    longitude REAL, latitude REAL, epsg5179_x REAL, epsg5179_y REAL,
    sgis_district_code TEXT, sgis_dong_code TEXT, q INTEGER, r INTEGER,
    binding_state TEXT NOT NULL CHECK(binding_state IN
        ('bound','outside_seoul','identity_unresolved','position_not_verified')),
    identity_evidence TEXT NOT NULL, position_source_id TEXT REFERENCES evidence_source(source_id),
    registry_line TEXT, registry_name TEXT, registry_station_no TEXT,
    registry_source_row INTEGER, registry_reference_date TEXT, alias_evidence_url TEXT,
    CHECK((q IS NULL)=(r IS NULL)), CHECK((binding_state='bound')=(q IS NOT NULL))
) WITHOUT ROWID;
CREATE TABLE rail_hour (
    station_id TEXT NOT NULL REFERENCES rail_station(station_id),
    month TEXT NOT NULL, hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    boardings INTEGER CHECK(boardings>=0), alightings INTEGER CHECK(alightings>=0),
    source_row INTEGER NOT NULL, source_id TEXT NOT NULL REFERENCES evidence_source(source_id),
    PRIMARY KEY(station_id,month,hour), UNIQUE(source_row,month,hour)
) WITHOUT ROWID;
CREATE INDEX idx_rail_station_tile ON rail_station(q,r) WHERE binding_state='bound';
CREATE INDEX idx_rail_station_dong ON rail_station(sgis_dong_code) WHERE binding_state='bound';
CREATE VIEW v_rail_tile_hour AS
SELECT s.q,s.r,h.month,h.hour,SUM(h.boardings) AS boardings,SUM(h.alightings) AS alightings,
       COUNT(*) AS features,'monthly_card_tag_count' AS unit
FROM rail_station s JOIN rail_hour h USING(station_id)
WHERE s.binding_state='bound' GROUP BY s.q,s.r,h.month,h.hour;
CREATE VIEW v_rail_sgis_hour AS
SELECT s.sgis_district_code,s.sgis_dong_code,h.month,h.hour,SUM(h.boardings) AS boardings,
       SUM(h.alightings) AS alightings,COUNT(*) AS features,'monthly_card_tag_count' AS unit
FROM rail_station s JOIN rail_hour h USING(station_id)
WHERE s.binding_state='bound' GROUP BY s.sgis_district_code,s.sgis_dong_code,h.month,h.hour;
"""


def boundary_lookup():
    spec = spec_from_file_location("seoul_area_sgis_boundary", BOUNDARY_HELPER)
    module = module_from_spec(spec)
    spec.loader.exec_module(module)
    if digest(BOUNDARY_ZIP) != module.SGIS_SHA:
        raise ValueError("Boundary snapshot differs from the verified SGIS hash")
    return module.BoundaryLookup(BOUNDARY_ZIP)


def station_label(name):
    return "".join(name.split()).removesuffix("역")


def read_kric():
    release = json.loads(KRIC_RECEIPT.read_text(encoding="utf-8"))["urbanRail"]
    if digest(KRIC) != release["mapCsv"]["sha256"] or digest(KRIC_RAW) != release["raw"]["sha256"]:
        raise ValueError("KRIC station-line registry differs from its receipt")
    keys = defaultdict(list)
    with KRIC.open(encoding="utf-8-sig", newline="") as stream:
        records = list(csv.DictReader(stream))
    if len(records) != release["stationLineRows"]:
        raise ValueError("KRIC station-line registry has an unexpected row count")
    for row in records:
        keys[(row["line_name"].strip(), station_label(row["station_name"]))].append(row)
    return keys


def kric_match(line, name, keys, boundary):
    normalized_line, line_proof = LINE_NAMES.get(line, (line, None))
    normalized_name, name_proof = STATION_ALIASES.get((line, name), (name, None))
    matches = keys.get((normalized_line, station_label(normalized_name)), ())
    if len(matches) != 1:
        return None
    row = matches[0]
    lon_text, lat_text = row["longitude"], row["latitude"]
    if not lon_text or not lat_text:
        return None
    lon, lat = float(lon_text), float(lat_text)
    if not (math.isfinite(lon) and math.isfinite(lat)):
        return None
    _, dong, _ = boundary.locate(lon, lat)
    if dong is None:
        return None
    is_seoul = dong.startswith("11")
    address = row["source_address"].strip()
    if not address or is_seoul != address.startswith(("서울특별시", "서울시")):
        return None
    if is_seoul and address.split()[1] != boundary.counties[dong[:5]]:
        return None
    # KRIC numbers have a separate namespace from the metro codes in the prior joins.
    code = f'kric:{row["line_no"]}:{row["station_no"]}'
    return (code, lon, lat, dong[:5], dong, "candidate" if is_seoul else "outside_seoul", row,
            name_proof or line_proof)


def normalized_station(entry, keys, boundary, transform):
    station_id, name, line, lon, lat, district, dong, state, proof, source, row = entry
    official_line = official_name = official_id = registry_row = as_of = None
    alias = row["alias_evidence_url"]
    if state == "identity_unresolved":
        match = kric_match(line, name, keys, boundary)
        if match is not None:
            station_id, lon, lat, district, dong, state, registry, alias = match
            source, proof = "rail_identity_kric", "unique_line_and_station_label_with_sgis_address_check"
            official_line, official_name, official_id = (
                registry[key] for key in ("line_name", "station_name", "station_no")
            )
            registry_row, as_of = int(registry["source_row"]), registry["source_record_date"] or None
    if state == "identity_unresolved":
        source = None
    x, y = transform(lon, lat) if lon is not None and lat is not None else (None, None)
    if state == "candidate":
        if not dong or not dong.startswith("11") or x is None or not math.isfinite(x) or not math.isfinite(y):
            raise ValueError(f"Missing Seoul point or boundary proof for {station_id}")
        q, r = axial(x, y)
        state = "bound"
    else:
        q = r = None
    return (station_id, name, line, lon, lat, x, y, district, dong, q, r, state, proof, source,
            official_line, official_name, official_id, registry_row, as_of, alias)


def import_data(output_db, base, patch, boundary, transform):
    registry = read_kric()
    stations_by_source, monthly = {}, []
    for entry in rail_rows(base, patch):
        row = entry[-1]
        source_row = row["source_row"]
        station = stations_by_source.get(source_row)
        if station is None:
            station = normalized_station(entry, registry, boundary, transform)
            stations_by_source[source_row] = station
        monthly.append((station[0], MONTH, row["clock_hour"], row["boarding_tags"],
                        row["alighting_tags"], source_row, "rail_october"))
    output_db.executemany("INSERT INTO rail_station VALUES(" + ",".join(["?"] * 20) + ")",
                          stations_by_source.values())
    output_db.executemany("INSERT INTO rail_hour VALUES(?,?,?,?,?,?,?)", monthly)


def original_expected():
    receipts = json.loads((RAIL_RAW.parent.parent / "source-receipts.json").read_text(encoding="utf-8"))["months"]
    receipt = next(item for item in receipts if item["month"] == MONTH.replace("-", ""))
    if digest(RAIL_RAW) != receipt["sha256"] or RAIL_RAW.stat().st_size != receipt["sizeBytes"]:
        raise ValueError("Original Seoul rail observations differ from the source receipt")
    with RAIL_RAW.open(encoding=receipt["encoding"], newline="") as stream:
        official = list(csv.DictReader(stream))
    if len(official) != receipt["rows"]:
        raise ValueError("Original Seoul rail station-line count changed")
    return official


def records(db, query, variables=()):
    return [dict(row) for row in db.execute(query, variables)]


def verify(db, official):
    if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
        raise ValueError("Rail index integrity check failed")
    observed = db.execute("""
        SELECT s.name,s.line,h.source_row,h.hour,h.boardings,h.alightings
        FROM rail_hour h JOIN rail_station s USING(station_id)
    """).fetchall()
    if len(observed) != len(official) * 24:
        raise ValueError("Rail index lost station-hours")
    for name, line, index, hour, boarding, alighting in observed:
        raw = official[index - 1]
        field = f"{hour:02d}시-{hour + 1:02d}시 "
        raw_counts = tuple(int(raw[field + tag]) if raw[field + tag] else None for tag in ("승차인원", "하차인원"))
        if raw["지하철역"] != name or raw["호선명"] != line or raw_counts != (boarding, alighting):
            raise ValueError(f"Rail source content changed at {index} hour {hour}")
    tile_counts = db.execute("SELECT SUM(boardings),SUM(alightings) FROM v_rail_tile_hour").fetchone()
    bound_counts = db.execute("""
        SELECT SUM(h.boardings),SUM(h.alightings) FROM rail_hour h JOIN rail_station s USING(station_id)
        WHERE s.binding_state='bound'
    """).fetchone()
    if tuple(tile_counts) != tuple(bound_counts):
        raise ValueError("Rail tile aggregation changed linked tag totals")


def build(output=OUTPUT):
    output.mkdir(parents=True, exist_ok=True)
    sources = (("rail_october", RAIL_PAGE, RAIL_RAW), ("rail_identity_base", LOCATION_BASE_PAGE, RAIL),
               ("rail_identity_patch", LOCATION_PATCH_PAGE, RAIL_PATCH),
               ("rail_identity_kric", KRIC_PAGE, KRIC), ("rail_identity_kric_raw", KRIC_PAGE, KRIC_RAW),
               ("sgis_boundaries", BOUNDARY_PAGE, BOUNDARY_ZIP))
    boundary, transform = boundary_lookup(), projection()
    with TemporaryDirectory(dir=output) as temporary:
        db_path = Path(temporary) / DBNAME
        with closing(sqlite3.connect(db_path)) as db, closing(open_read(RAIL)) as base, closing(open_read(RAIL_PATCH)) as patch:
            db.row_factory = sqlite3.Row
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO evidence_source VALUES(?,?,?,?)", [
                    (source_id, url, path.relative_to(ROOT).as_posix(), digest(path))
                    for source_id, url, path in sources
                ])
                import_data(db, base, patch, boundary, transform)
            verify(db, original_expected())
            notes = {
                "schema": "hongdal.seoul-area-rail-index.v1", "period": MONTH, "unit": "monthly_card_tag_count",
                "crs": "EPSG:5179", "tileSpacingMeters": 300, "tileOrigin": [1000000, 2000000],
                "stationHours": db.execute("SELECT count(*) FROM rail_hour").fetchone()[0],
                "tiles": db.execute("SELECT count(*) FROM (SELECT DISTINCT q,r FROM v_rail_tile_hour)").fetchone()[0],
                "districts": db.execute("SELECT count(DISTINCT sgis_district_code) FROM v_rail_sgis_hour").fetchone()[0],
                "states": records(db, """SELECT binding_state,count(*) AS station_line_rows FROM rail_station
                                          GROUP BY binding_state ORDER BY binding_state"""),
                "registryJoins": records(db, """SELECT binding_state,count(*) AS station_line_rows FROM rail_station
                    WHERE position_source_id='rail_identity_kric' GROUP BY binding_state ORDER BY binding_state"""),
                "unknown": records(db, "SELECT station_id,name,line FROM rail_station WHERE binding_state='identity_unresolved'"),
                "examples": records(db, """SELECT s.name,s.line,s.q,s.r,s.sgis_district_code,h.boardings,h.alightings
                    FROM rail_hour h JOIN rail_station s USING(station_id) WHERE h.hour=12
                    AND s.binding_state='bound' AND s.name IN ('사가정','홍대입구','강남','잠실','서울역','공덕','상봉')
                    ORDER BY s.name,s.line"""),
                "sources": records(db, "SELECT * FROM evidence_source ORDER BY source_id"),
                "integrity": "ok", "periodBasis": "월 전체의 시간대별 승하차 태그 건수",
                "coverageScope": "서울 안 소속을 확인한 역·호선 대표점. 출입구 수·환승 동선·고유 이용자 수와 별개.",
                "dailyAlternative": {"sourceUrl": RAIL_DAILY_PAGE,
                                     "note": "2025년 서울교통공사 파일 자료. 이 월 누계와 별도 시간축."},
            }
        target = output / DBNAME
        db_path.replace(target)
        notes["databaseSha256"] = digest(target)
    atomic_json(output / "rail-area-summary.json", notes)
    return notes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    notes = build(args.output)
    print(json.dumps({key: notes[key] for key in ("period", "stationHours", "tiles", "districts",
                                                   "states", "registryJoins", "unknown", "databaseSha256")},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

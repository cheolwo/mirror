"""Index verified transit boarding and alighting counts against the Hongdal 300m grid."""

import argparse
import csv
from collections import Counter
from contextlib import closing
from datetime import datetime, timezone
from hashlib import file_digest, sha256
import io
import json
from math import sqrt
from pathlib import Path
import sqlite3
import sys
from tempfile import TemporaryDirectory
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


ROOT = Path(__file__).resolve().parents[2]
PUBLIC = ROOT / "artifacts/local/public-data"
PACK = PUBLIC / "floating-250m-20261125/raw/hongdal-tiles-gu.before.json"
OUTPUT = PUBLIC / "seoul-tile-evidence-20261125/transit"
REFERENCE = PUBLIC / "diorama-public-layer-intake-20261123"
RAIL = REFERENCE / "r60-seoul-rail-district-usage/seoul-rail-district-usage.sqlite"
RAIL_RAW = REFERENCE / "r42-station-ridership/raw/seoul-o12252-202610.csv"
RAIL_PATCH = PUBLIC / "hongdal-map-scale-index-20261123/district/station-coverage/rail-station-coverage.sqlite"
BUS = PUBLIC / "diorama-data-relations-20261123-r2/bus-stop-time-counts/bus-stop-time-counts.sqlite"
RAIL_PAGE = "https://data.seoul.go.kr/dataList/OA-12252/S/1/datasetView.do"
RAIL_DAILY_PAGE = "https://data.seoul.go.kr/dataList/OA-12921/S/1/datasetView.do"
BUS_PAGE = "https://data.seoul.go.kr/dataList/OA-12913/S/1/datasetView.do"
BUS_DAILY_PAGE = "https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=40"
BUS_DAILY_FILE = "https://t-data.seoul.go.kr/dataprovide/download.do?id=10233"
MONTH = "2026-10"
DBNAME = "transit-tile-index.sqlite"

SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE evidence_source (
    key TEXT PRIMARY KEY, source_url TEXT, local_input TEXT NOT NULL, sha256 TEXT NOT NULL
);
CREATE TABLE tile (
    q INTEGER NOT NULL, r INTEGER NOT NULL, PRIMARY KEY(q,r)
) WITHOUT ROWID;
CREATE TABLE transit_feature (
    mode TEXT NOT NULL CHECK(mode IN ('rail','bus')), feature_id TEXT NOT NULL,
    name TEXT NOT NULL, line TEXT, longitude REAL, latitude REAL,
    sgis_district_code TEXT, sgis_dong_code TEXT,
    q INTEGER, r INTEGER,
    binding_state TEXT NOT NULL CHECK(binding_state IN (
        'bound','outside_tile_pack','outside_seoul','identity_unresolved',
        'position_not_verified','no_eligible_observations')),
    identity_evidence TEXT NOT NULL,
    source_key TEXT NOT NULL REFERENCES evidence_source(key),
    PRIMARY KEY(mode,feature_id), FOREIGN KEY(q,r) REFERENCES tile(q,r),
    CHECK((q IS NULL)=(r IS NULL)),
    CHECK((binding_state='bound')=(q IS NOT NULL))
) WITHOUT ROWID;
CREATE TABLE monthly_hour (
    mode TEXT NOT NULL, feature_id TEXT NOT NULL, month TEXT NOT NULL,
    hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    boardings INTEGER CHECK(boardings >= 0), alightings INTEGER CHECK(alightings >= 0),
    eligible_entries INTEGER NOT NULL, source_entries INTEGER NOT NULL,
    boarding_entries INTEGER NOT NULL, alighting_entries INTEGER NOT NULL,
    source_row INTEGER, source_key TEXT NOT NULL REFERENCES evidence_source(key),
    PRIMARY KEY(mode,feature_id,month,hour),
    FOREIGN KEY(mode,feature_id) REFERENCES transit_feature(mode,feature_id),
    CHECK(eligible_entries BETWEEN 0 AND source_entries),
    CHECK(boarding_entries BETWEEN 0 AND eligible_entries),
    CHECK(alighting_entries BETWEEN 0 AND eligible_entries)
) WITHOUT ROWID;
CREATE VIEW v_tile_transit_hour AS
SELECT f.q, f.r, a.mode, a.month, a.hour, sum(a.boardings) AS boardings,
       sum(a.alightings) AS alightings, count(*) AS features,
       sum(a.eligible_entries) AS eligible_entries, sum(a.source_entries) AS source_entries,
       sum(a.boarding_entries) AS boarding_entries, sum(a.alighting_entries) AS alighting_entries,
       'monthly_card_tag_count' AS unit
FROM monthly_hour a JOIN transit_feature f USING(mode,feature_id)
WHERE f.binding_state='bound' GROUP BY f.q,f.r,a.mode,a.month,a.hour;
"""


def projection():
    try:
        from pyproj import Transformer
    except ModuleNotFoundError:
        sys.path.insert(0, str(PUBLIC / "gis-runtime-r1"))
        from pyproj import Transformer
    return Transformer.from_crs(4326, 5179, always_xy=True).transform


def axial(x, y):
    px, py = x - 1_000_000, y - 2_000_000
    radius = 300.0 / sqrt(3)
    qf, rf = (sqrt(3) / 3 * px - py / 3) / radius, (2 / 3 * py) / radius
    sf = -qf - rf
    q, r, s = round(qf), round(rf), round(sf)
    dq, dr, ds = abs(q - qf), abs(r - rf), abs(s - sf)
    if dq > dr and dq > ds:
        q = -r - s
    elif dr > ds:
        r = -q - s
    return q, r


def tile_binding(lon, lat, state, transform, tile_keys):
    if state != "candidate":
        return None, None, state
    key = axial(*transform(lon, lat))
    return (*key, "bound") if key in tile_keys else (None, None, "outside_tile_pack")


def open_read(path):
    db = sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)
    db.row_factory = sqlite3.Row
    return db


def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()


def rail_rows(db, patch_db):
    patch = {row["source_row"]: row for row in patch_db.execute(
        "SELECT * FROM v_rail_coverage_added_station WHERE use_month='202610'")}
    for row in db.execute("""SELECT * FROM station_hourly_read WHERE use_month='202610'
                            ORDER BY source_row,clock_hour"""):
        supplemented = patch.get(row["source_row"])
        if supplemented is not None:
            yield (supplemented["operator_station_id"], row["source_station_label"], row["source_line_label"],
                   supplemented["longitude"], supplemented["latitude"], supplemented["district_code"],
                   supplemented["dong_code"], "candidate", supplemented["equivalence_state"],
                   "rail_identity_patch", row)
        else:
            station_id = row["metro_station_code"]
            state = {"within_seoul_sgis_dong": "candidate",
                     "outside_seoul_within_other_sgis_dong": "outside_seoul"}.get(
                        row["placement_state"], "position_not_verified" if station_id else "identity_unresolved")
            yield (station_id or "unresolved-row-" + str(row["source_row"]), row["source_station_label"],
                   row["source_line_label"], row["longitude"], row["latitude"], row["sgis_district_code"],
                   row["sgis_dong_code"], state, row["identity_state"], "rail_identity_base", row)


def import_rail(out, rail, patch, transform, tile_keys):
    feature_rows, monthly_rows = {}, []
    for feature, name, line, lon, lat, district, dong, state, proof, source, row in rail_rows(rail, patch):
        if feature not in feature_rows:
            feature_rows[feature] = ("rail", feature, name, line, lon, lat, district, dong,
                                     *tile_binding(lon, lat, state, transform, tile_keys), proof, source)
        board, alight = row["boarding_tags"], row["alighting_tags"]
        monthly_rows.append(("rail", feature, MONTH, row["clock_hour"], board, alight,
                             1, 1, int(board is not None), int(alight is not None), row["source_row"], "rail_october"))
    out.executemany("INSERT INTO transit_feature VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)", feature_rows.values())
    out.executemany("INSERT INTO monthly_hour VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", monthly_rows)


def import_bus(out, bus, transform, tile_keys):
    location_rows = bus.execute("""SELECT s.*,d.district_code, count(a.original_data_row) AS source_entries,
       sum(CASE a.observation_location_status WHEN 'eligible_source_ids_name_position_agree' THEN 1 ELSE 0 END) AS eligible_entries
       FROM stop_reference_comparison s JOIN route_stop_month_observation a USING(standard_stop_id)
       LEFT JOIN focus_dong d ON s.after_dong_code=d.dong_code
       WHERE a.service_month=? GROUP BY s.standard_stop_id ORDER BY s.standard_stop_id""", (MONTH,))
    features, totals = [], {}
    for row in location_rows:
        key = row["standard_stop_id"]
        state = ("position_not_verified" if row["placement_status"] != "agrees_before_and_after"
                 else "candidate" if row["eligible_entries"] else "no_eligible_observations")
        lon, lat = row["after_lon"], row["after_lat"]
        features.append(("bus", key, row["after_name"] or row["before_name"] or key, None, lon, lat,
                         row["district_code"], row["after_dong_code"], *tile_binding(lon, lat, state, transform, tile_keys),
                         row["placement_status"], "bus_october"))
        totals[key] = row["source_entries"]
    out.executemany("INSERT INTO transit_feature VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)", features)
    counts = {(row["standard_stop_id"], row["clock_hour"]): row for row in bus.execute("""
        SELECT * FROM v_mapped_stop_hour_counts WHERE service_month=?""", (MONTH,))}
    for key, total in totals.items():
        values = []
        for hour in range(24):
            row = counts.get((key, hour))
            measures = (row["boarding_count_sum_of_eligible_rows"], row["alighting_count_sum_of_eligible_rows"],
                        row["eligible_route_stop_rows"], total,
                        row["observed_boarding_bins"], row["observed_alighting_bins"]) if row else (None, None, 0, total, 0, 0)
            values.append(("bus", key, MONTH, hour, *measures, None, "bus_october"))
        out.executemany("INSERT INTO monthly_hour VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", values)


def collect_daily_bus(output):
    """Download the dated public sample as-is, preserving the snapshot even after portal rotation."""
    request = Request(BUS_DAILY_FILE, headers={"User-Agent": "Hongdal public-data evidence"})
    with urlopen(request, timeout=35) as response:
        if (urlsplit(response.geturl()).hostname != "t-data.seoul.go.kr"
                or "attachment" not in response.headers.get("Content-Disposition", "").lower()):
            raise ValueError("Unexpected official bus response")
        data = response.read(45_000_001)
    if len(data) > 45_000_000:
        raise ValueError("Bus snapshot exceeded collection limit")
    rows = csv.DictReader(io.StringIO(data.decode("utf-8-sig")))
    need = {"기준일", "노선ID", "정류장ID", "정류장번호", "승차승객수량12H", "하차승객수량12H"}
    if not rows.fieldnames or not need.issubset(rows.fieldnames):
        raise ValueError("Official bus CSV does not match its documented grain")
    dates = Counter(row["기준일"] for row in rows)
    if not dates or any(len(day) != 8 or not day.isdigit() for day in dates):
        raise ValueError("Official bus CSV has invalid dates")
    stamp = sha256(data).hexdigest()
    name = f"seoul-t-data-40-{min(dates)}-{max(dates)}-{stamp[:12]}.csv"
    raw = output / "raw"
    raw.mkdir(exist_ok=True)
    (raw / name).write_bytes(data)
    receipt = {"page": BUS_DAILY_PAGE, "downloadUrl": BUS_DAILY_FILE, "path": f"raw/{name}",
               "retrievedAtUtc": datetime.now(timezone.utc).isoformat(), "bytes": len(data), "sha256": stamp,
               "rowsByDate": dict(sorted(dates.items())), "rows": sum(dates.values()),
               "grain": "service date × route × stop; 24 boarding and alighting hourly counts",
               "rights": "서울특별시 교통정책지원시스템(TAIMS), 저작자 표시(BY)",
               "usage": "원본 비교용. 표본 날짜만 포함하며 월 누계 및 생활인구와 합치지 않음."}
    (raw / "t-data-40-receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


def summary(query, db):
    result = db.execute(query)
    columns = [column[0] for column in result.description]
    return [dict(zip(columns, row)) for row in result]


def build(output, pack, download_daily_bus=False):
    output.mkdir(parents=True, exist_ok=True)
    pack_data = json.loads(pack.read_text(encoding="utf-8-sig"))
    if pack_data["schema"] != "hongdal-hex-tiles.v1" or pack_data["spacing"] != 300:
        raise ValueError("Unexpected hex grid revision or spacing")
    keys = {(t["q"], t["r"]) for t in pack_data["tiles"]}
    if len(keys) != len(pack_data["tiles"]):
        raise ValueError("Duplicate tile keys")
    transform = projection()
    sources = (("tile_pack", None, pack), ("rail_october", RAIL_PAGE, RAIL_RAW),
               ("rail_identity_base", RAIL_PAGE, RAIL), ("rail_identity_patch", "https://www.data.go.kr/data/15044230/fileData.do", RAIL_PATCH),
               ("bus_october", BUS_PAGE, BUS))
    with TemporaryDirectory(dir=output) as temp:
        path = Path(temp) / DBNAME
        with closing(sqlite3.connect(path)) as db, closing(open_read(RAIL)) as rail, closing(open_read(RAIL_PATCH)) as patch, closing(open_read(BUS)) as bus:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO evidence_source VALUES(?,?,?,?)",
                               [(key, url, path.resolve().relative_to(ROOT).as_posix() if path.resolve().is_relative_to(ROOT)
                                 else path.name, digest(path)) for key, url, path in sources])
                db.executemany("INSERT INTO tile VALUES(?,?)", sorted(keys))
                import_rail(db, rail, patch, transform, keys)
                import_bus(db, bus, transform, keys)
            integrity = db.execute("PRAGMA integrity_check").fetchone()[0]
            if integrity != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
                raise ValueError("The transit index is inconsistent")
            if summary("SELECT mode,month,hour,sum(boardings) b,sum(alightings) a FROM v_tile_transit_hour GROUP BY 1,2,3 ORDER BY 1,2,3", db) != summary(
                """SELECT m.mode,m.month,m.hour,sum(m.boardings) b,sum(m.alightings) a FROM monthly_hour m
                   JOIN transit_feature f USING(mode,feature_id) WHERE f.binding_state='bound'
                   GROUP BY 1,2,3 ORDER BY 1,2,3""", db):
                raise ValueError("Tile binding duplicated or lost transit counts")
            notes = {"schema": "hongdal-transit-tile-index.v1", "period": MONTH, "kind": "Observed locations plus projected tile join",
                     "tilePackRevision": pack_data["revision"], "tileCrs": "EPSG:5179", "tileCount": len(keys),
                     "sourceLineage": summary("SELECT * FROM evidence_source ORDER BY key", db),
                     "busUpstreamLineage": summary("SELECT * FROM source_release ORDER BY source_id", bus),
                     "bindingStateCounts": summary("SELECT mode,binding_state,count(*) count FROM transit_feature GROUP BY 1,2 ORDER BY 1,2", db),
                     "tileCoverage": summary("SELECT mode,count(*) tiles FROM (SELECT DISTINCT mode,q,r FROM v_tile_transit_hour) GROUP BY 1 ORDER BY 1", db),
                     "monthlyHourlyRows": summary("SELECT mode,count(*) as rows FROM monthly_hour GROUP BY 1 ORDER BY 1", db),
                     "sagajeongExamples": summary("""SELECT f.mode,f.feature_id,f.name,f.q,f.r,m.month,m.hour,m.boardings,m.alightings,
                        m.eligible_entries,m.source_entries FROM monthly_hour m JOIN transit_feature f USING(mode,feature_id)
                        WHERE f.feature_id IN ('2724','106000151') AND m.hour IN (8,12,18) ORDER BY f.mode,m.hour""", db),
                     "sagajeongTileByMode": summary("""SELECT * FROM v_tile_transit_hour WHERE q=-32 AND r=-179
                        AND hour IN (8,12,18) ORDER BY mode,hour""", db),
                     "limitations": ["월 누계이며 일별·평일 평균, 고유 인원, 동시 체류, 역 출입구별 수치가 아님.",
                                     "버스는 중랑·광진·동대문·노원구의 확인된 장소·관측 범위. 철도는 제공 기관·검증 대표점 및 타일 팩 범위에 한함.",
                                     "역 대표점 및 정류소 점을 한 타일에 귀속. 환승·재승차 중복 가능. 생활인구와 합산 금지."],
                     "nextDailySources": {"railThrough2025": RAIL_DAILY_PAGE, "busSample": BUS_DAILY_PAGE},
                     "integrity": integrity, "verifiedConservation": True}
        path.replace(output / DBNAME)
    daily = output / "raw/t-data-40-receipt.json"
    notes["dailyBusEvidence"] = (collect_daily_bus(output) if download_daily_bus else
                                 json.loads(daily.read_text(encoding="utf-8")) if daily.is_file() else None)
    (output / "transit-summary.json").write_text(json.dumps(notes, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return notes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path, default=PACK)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--download-daily-bus", action="store_true")
    args = parser.parse_args()
    result = build(args.output, args.pack, args.download_daily_bus)
    print(json.dumps({key: result[key] for key in ("bindingStateCounts", "tileCoverage", "monthlyHourlyRows",
                                                   "sagajeongExamples", "dailyBusEvidence")}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

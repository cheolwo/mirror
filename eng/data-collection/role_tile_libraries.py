"""Collect the published library register and index verified Seoul facility points."""

import argparse
from collections import Counter
from contextlib import closing
from datetime import date, datetime, timezone
import gzip
from hashlib import sha256
import json
import math
import re
import sqlite3
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from collect_public_snapshots import atomic_json
from dong_tile_facilities import official_seoul
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import PUBLIC, digest


PORTAL = "https://www.data.go.kr"
DATASET = "15013109"
SOURCE = f"{PORTAL}/data/{DATASET}/standard.do"
OUTPUT = PUBLIC / "role-tile-intake-20261126/libraries"
CORE = TILE_OUTPUT / DBNAME
FIELDS = (
    "LBRRY_NM", "CTPRVN_NM", "SIGNGU_NM", "LBRRY_SE", "CLOSE_DAY", "SEAT_CO",
    "WEEKDAY_OPER_OPEN_HHMM", "WEEKDAY_OPER_COLSE_HHMM", "SAT_OPER_OPER_OPEN_HHMM",
    "SAT_OPER_CLOSE_HHMM", "HOLIDAY_OPER_OPEN_HHMM", "HOLIDAY_CLOSE_OPEN_HHMM",
    "LATITUDE", "LONGITUDE", "REFERENCE_DATE",
)
SCHEDULE = tuple(zip(("weekday", "saturday", "holiday"), FIELDS[6:12:2], FIELDS[7:12:2]))
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE release (
    dataset_id TEXT PRIMARY KEY,url TEXT NOT NULL,retained_sha256 TEXT NOT NULL,
    native_grain TEXT NOT NULL,distribution TEXT NOT NULL
);
CREATE TABLE library_report (
    report_id TEXT PRIMARY KEY,dataset_id TEXT NOT NULL REFERENCES release(dataset_id),
    name TEXT NOT NULL,province TEXT,district TEXT,facility_type TEXT,reference_date TEXT,
    closure_text TEXT,seats INTEGER,identity_state TEXT NOT NULL,footing_state TEXT NOT NULL
);
CREATE TABLE library_hours (
    report_id TEXT REFERENCES library_report(report_id),day_group TEXT,
    published_open_text TEXT,published_close_text TEXT,
    opens_at TEXT,closes_at TEXT,schedule_state TEXT NOT NULL,
    PRIMARY KEY(report_id,day_group)
) WITHOUT ROWID;
CREATE TABLE child_key (
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT,geometry_revision TEXT NOT NULL,
    PRIMARY KEY(q,r,slot,admin_code)
) WITHOUT ROWID;
CREATE TABLE tile_library (
    report_id TEXT PRIMARY KEY REFERENCES library_report(report_id),
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,admin_code TEXT NOT NULL,
    FOREIGN KEY(q,r,slot,admin_code) REFERENCES child_key(q,r,slot,admin_code)
);
CREATE INDEX tile_library_by_point ON tile_library(q,r,slot);
CREATE VIEW v_tile_library AS
SELECT x.q,x.r,x.slot,x.admin_code,r.report_id,r.name,r.facility_type,r.reference_date,
       r.closure_text,r.seats,h.day_group,h.opens_at,h.closes_at,h.schedule_state
FROM tile_library x JOIN library_report r USING(report_id) JOIN library_hours h USING(report_id);
"""


def download(url):
    request = Request(url, headers={"User-Agent": "Mozilla/5.0", "Referer": SOURCE})
    with urlopen(request, timeout=40) as response:
        raw = response.read(12_000_001)
        if len(raw) > 12_000_000:
            raise ValueError("library_response_body_limit")
        return json.loads(raw), {"sha256": sha256(raw).hexdigest(), "bytes": len(raw),
                                 "httpStatus": response.status}


def collect(output):
    header, header_receipt = download(f"{PORTAL}/download/columList.json?pk={DATASET}&ext=CSV")
    total = int(header["totalCount"])
    table = header["tableVO"]
    if not 0 < total <= 10_000 or not set(FIELDS) <= set(table["colNmList"]):
        raise ValueError("library_catalog_changed")
    params = urlencode({"publicDataPk": DATASET, "colNmList": FIELDS, "totalCount": total,
                        "svcTableNm": table["svcTableNm"], "perPage": total, "page": 1}, doseq=True)
    payload, receipt = download(f"{PORTAL}/download/standard.json?{params}")
    if not isinstance(payload, list) or len(payload) != total or any(
        not {"LBRRY_NM", "CTPRVN_NM", "SIGNGU_NM", "REFERENCE_DATE"} <= row.keys() for row in payload
    ):
        raise ValueError("library_page_count_or_fields_mismatch")
    rows = [{key: row.get(key) for key in FIELDS} for row in payload]
    retained = output / "library-source-selected.jsonl.gz"
    content = "".join(json.dumps(row, ensure_ascii=False, sort_keys=True) + "\n" for row in rows)
    retained.write_bytes(gzip.compress(content.encode("utf-8"), mtime=0))
    atomic_json(output / "source-receipt.json", {
        "source": SOURCE, "metadata": f"{PORTAL}/catalog/{DATASET}/standard.json",
        "sourceId": DATASET, "retrievedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "provider": "지방자치단체·교육청", "updateFrequency": "연간, 기관별 기준일 상이",
        "table": table["svcTableNm"], "retainedFields": list(FIELDS), "rows": total,
        "nativeGrain": "library report", "distribution": "local-review", "header": header_receipt,
        "page": receipt, "retainedFile": retained.name, "retainedSha256": digest(retained),
    })


def clean(value):
    if value is None:
        return None
    return str(value).strip() or None


def point(row):
    try:
        x, y = float(row["LONGITUDE"]), float(row["LATITUDE"])
    except (TypeError, ValueError):
        return None
    if math.isfinite(x) and math.isfinite(y) and 124 <= x <= 132 and 32 <= y <= 39:
        return x, y
    return None


def identity_parts(row):
    return clean(row["CTPRVN_NM"]), clean(row["SIGNGU_NM"]), clean(row["LBRRY_NM"]), point(row)


def reported_date(value):
    try:
        return date.fromisoformat(value).isoformat()
    except (TypeError, ValueError):
        return None


def hhmm(value, closing):
    if value is None or not re.fullmatch(r"\d{1,2}:\d{2}", value):
        return None
    h, m = map(int, value.split(":"))
    return f"{h:02}:{m:02}" if (h < 24 and m < 60) or (closing and h == 24 and m == 0) else None


def hours(report_id, row):
    for group, opens, closes in SCHEDULE:
        raw_open, raw_close = clean(row[opens]), clean(row[closes])
        opens_at, closes_at = hhmm(raw_open, False), hhmm(raw_close, True)
        state = "published_hours" if opens_at and closes_at and opens_at < closes_at else "schedule_unresolved"
        if state == "schedule_unresolved":
            opens_at = closes_at = None
        yield report_id, group, raw_open, raw_close, opens_at, closes_at, state


def match_seoul(location, district, tree, labels, shapely, to_meters, valid_keys):
    if location is None:
        return None, "no_valid_provider_point"
    hits = tree.query(shapely.Point(*location), predicate="within")
    if len(hits) != 1:
        return None, "official_polygon_unresolved"
    admin, _name, district_code = labels[int(hits[0])]
    if district is None or district.split()[-1] != valid_keys[1].get(district_code):
        return None, "provider_district_conflict"
    key = (*point_key(*to_meters(*location)), admin)
    return (key, "official_dong_and_exact_child") if key in valid_keys[0] else (None, "child_key_unresolved")


def build(output):
    receipt = json.loads((output / "source-receipt.json").read_text(encoding="utf-8"))
    retained = output / receipt["retainedFile"]
    if digest(retained) != receipt["retainedSha256"]:
        raise ValueError("library_retained_fingerprint_mismatch")
    with gzip.open(retained, "rt", encoding="utf-8") as stream:
        rows = [json.loads(line) for line in stream]
    if len(rows) != receipt["rows"]:
        raise ValueError("library_retained_count_mismatch")
    distinct = {sha256(json.dumps(row, ensure_ascii=False, sort_keys=True).encode()).hexdigest(): row
                for row in rows}
    identities = Counter(map(identity_parts, distinct.values()))
    pyproj, shapely = gis()
    labels, tree = official_seoul(shapely)
    to_meters = pyproj.Transformer.from_crs(4326, 5179, always_xy=True).transform
    with closing(read_only(CORE)) as db:
        valid_keys = (set(db.execute("SELECT q,r,slot,admin_code FROM child_admin")),
                      dict(db.execute("SELECT code,name FROM district")))
    counts = Counter()
    with TemporaryDirectory(dir=output) as tmp:
        path = Path(tmp) / "library-tile.sqlite"
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.execute("INSERT INTO release VALUES (?,?,?,?,?)",
                           (DATASET, SOURCE, receipt["retainedSha256"], "facility report", "local-review"))
                for key, row in sorted(distinct.items()):
                    province, district, name, position = identity_parts(row)
                    identity = "unique_retain" if identities[(province, district, name, position)] == 1 else "report_conflict"
                    if not name:
                        counts["no_facility_name"] += 1
                        continue
                    mapped = None
                    state = "outside_seoul_grid" if province not in {"서울특별시", "서울"} else "report_conflict"
                    if state == "report_conflict" and identity == "unique_retain":
                        mapped, state = match_seoul(position, district, tree, labels, shapely, to_meters, valid_keys)
                    seats = clean(row["SEAT_CO"])
                    db.execute("INSERT INTO library_report VALUES (?,?,?,?,?,?,?,?,?,?,?)", (
                        key, DATASET, name, province, district, clean(row["LBRRY_SE"]),
                        reported_date(clean(row["REFERENCE_DATE"])), clean(row["CLOSE_DAY"]),
                        int(seats) if seats and seats.isdecimal() else None, identity, state,
                    ))
                    db.executemany("INSERT INTO library_hours VALUES (?,?,?,?,?,?,?)", hours(key, row))
                    counts[state] += 1
                    if mapped is not None:
                        db.execute("INSERT OR IGNORE INTO child_key VALUES (?,?,?,?,?)", (*mapped, REVISION))
                        db.execute("INSERT INTO tile_library VALUES (?,?,?,?,?)", (key, *mapped))
            check_database(db)
            stats = db.execute("SELECT COUNT(*),MIN(reference_date),MAX(reference_date) FROM library_report").fetchone()
            matched = db.execute("""SELECT COUNT(DISTINCT t.report_id),COUNT(DISTINCT t.admin_code),
                     COUNT(DISTINCT printf('%d/%d/%d',t.q,t.r,t.slot)) FROM tile_library t""").fetchone()
            schedule_counts = dict(db.execute("SELECT schedule_state,COUNT(*) FROM library_hours GROUP BY 1"))
        path.replace(output / path.name)
    atomic_json(output / "verification.json", {
        "sourceRows": len(rows), "exactDuplicatesCollapsed": len(rows) - len(distinct),
        "nationalLibraryReports": stats[0], "reportDateMin": stats[1], "reportDateMax": stats[2],
        "seoulPlacedReports": matched[0], "seoulDongsWithPlacedReports": matched[1],
        "seoulChildrenWithPlacedReports": matched[2], "placementStates": dict(counts),
        "schedules": schedule_counts, "integrityCheck": "ok", "foreignKeyViolations": 0,
        "coreSha256": digest(CORE), "geometryRevision": REVISION,
        "databaseSha256": digest(output / "library-tile.sqlite"),
    })


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--collect", action="store_true")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if args.collect:
        collect(args.output)
    build(args.output)


if __name__ == "__main__":
    main()

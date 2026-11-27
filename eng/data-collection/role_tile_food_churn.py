"""Index recorded Seoul food-license starts and reported closures from the retained official release."""

import argparse
from collections import Counter
from contextlib import closing
from datetime import date
import json
from math import isfinite
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_facilities import official_seoul
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_food import BASE, CATEGORY
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126/food-churn"
CORE = TILE_OUTPUT / DBNAME
OBSERVED_THROUGH = date(2026, 11, 18)
WINDOWS = {1: date(2025, 11, 19), 3: date(2023, 11, 19)}
DB = "food-churn.sqlite"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE release (
    dataset_id INTEGER PRIMARY KEY,label TEXT NOT NULL,official_url TEXT NOT NULL,
    source_sha256 TEXT NOT NULL,retained_gzip_sha256 TEXT NOT NULL,collected_utc TEXT NOT NULL
);
CREATE TABLE district (code TEXT PRIMARY KEY,name TEXT NOT NULL);
CREATE TABLE child_admin (
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT,district_code TEXT NOT NULL REFERENCES district(code),
    PRIMARY KEY(q,r,slot,admin_code,district_code)
) WITHOUT ROWID;
CREATE TABLE observation (
    dataset_id INTEGER REFERENCES release(dataset_id),source_record INTEGER,
    event TEXT CHECK(event IN ('license_start','reported_closed')),
    event_date TEXT NOT NULL,district_code TEXT NOT NULL REFERENCES district(code),category_id INTEGER NOT NULL,
    q INTEGER,r INTEGER,slot INTEGER,admin_code TEXT,placement_state TEXT NOT NULL,
    CHECK((q IS NULL AND r IS NULL AND slot IS NULL AND admin_code IS NULL) OR
          (q IS NOT NULL AND r IS NOT NULL AND slot IS NOT NULL AND admin_code IS NOT NULL)),
    PRIMARY KEY(dataset_id,source_record,event),
    FOREIGN KEY (q,r,slot,admin_code,district_code) REFERENCES child_admin
) WITHOUT ROWID;
CREATE INDEX churn_by_child ON observation(q,r,slot,event_date);
CREATE INDEX churn_by_district ON observation(district_code,event_date);
CREATE TABLE window (years INTEGER PRIMARY KEY,from_date TEXT NOT NULL,to_date TEXT NOT NULL);
CREATE VIEW v_food_churn_child AS
SELECT o.q,o.r,o.slot,o.district_code,o.category_id,o.event,w.years,w.from_date,w.to_date,
       COUNT(*) AS located_license_events
FROM observation o JOIN window w ON o.event_date BETWEEN w.from_date AND w.to_date
WHERE o.q IS NOT NULL GROUP BY 1,2,3,4,5,6,7,8,9;
CREATE VIEW v_food_churn_district AS
SELECT o.district_code,d.name,o.category_id,o.event,w.years,w.from_date,w.to_date,
       COUNT(*) AS reported_license_events,COUNT(o.q) AS located_license_events
FROM observation o JOIN district d ON o.district_code=d.code
JOIN window w ON o.event_date BETWEEN w.from_date AND w.to_date
GROUP BY 1,2,3,4,5,6,7;
"""


def dated(value, kind, counts):
    if not value:
        counts[f"{kind}_reported_date_missing"] += 1
        return None
    try:
        occurred = date.fromisoformat(value)
    except ValueError:
        counts[f"{kind}_reported_date_invalid"] += 1
        return None
    if occurred > OBSERVED_THROUGH:
        counts[f"{kind}_future_date_unresolved"] += 1
        return None
    return value if occurred >= WINDOWS[3] else None


def placement(x, y, district, tree, labels, shapely, to_geo, to_meters, valid_tiles):
    try:
        x, y = float(x), float(y)
    except (ValueError, TypeError):
        return (None,) * 4, "missing_or_invalid_source_xy"
    if not 100_000 < x < 400_000 or not 300_000 < y < 700_000:
        return (None,) * 4, "outside_original_crs_envelope"
    lon, lat = to_geo(x, y)
    if not isfinite(lon) or not isfinite(lat):
        return (None,) * 4, "invalid_transformation"
    hits = tree.query(shapely.Point(lon, lat), predicate="within")
    if len(hits) != 1:
        return (None,) * 4, "current_official_polygon_unresolved"
    code, _name, in_district = labels[int(hits[0])]
    if in_district != district:
        return (None,) * 4, "reported_district_disagrees"
    key = (*point_key(*to_meters(x, y)), code)
    if key not in valid_tiles:
        return (None,) * 4, "tile_admin_overlap_unresolved"
    return key, "validated_original_point"


def observations(source, geography, counts):
    items = source.execute("""SELECT dataset_id,source_record,district,reported_status,licensed_date,closed_date,
                    hygiene_type,business_type,source_x,source_y FROM license_observation
                    WHERE province='서울특별시' ORDER BY dataset_id,source_record""")
    districts, tree, labels, shapely, to_geo, to_meters, valid_tiles = geography
    for dataset, record, district_name, status, opened, closed, hygiene, business, x, y in items:
        counts["license_source_rows"] += 1
        closed_status = status == "폐업"
        counts["source_rows_with_closed_status"] += closed_status
        start = dated(opened, "license_start", counts)
        end = dated(closed, "reported_closed", counts) if closed_status else None
        if end is not None and opened and end < opened:
            counts["closed_before_license_invalid"] += 1
            end = None
        if start is None and end is None:
            continue
        district_code = districts.get(district_name)
        if district_code is None:
            counts["retained_event_district_unresolved"] += 1
            continue
        key, state = placement(x, y, district_code, tree, labels, shapely, to_geo, to_meters, valid_tiles)
        label = (hygiene or business or "").strip()
        category = 8 if dataset == 3 else CATEGORY.get(label, CATEGORY.get((business or "").strip(), 10))
        for kind, day in (("license_start", start), ("reported_closed", end)):
            if day is not None:
                counts[f"{kind}:{state}"] += 1
                yield dataset, record, kind, day, district_code, category, *key, state


def build(output):
    output.mkdir(parents=True, exist_ok=True)
    pyproj, shapely = gis()
    labels, tree = official_seoul(shapely)
    with closing(read_only(CORE)) as core:
        districts = core.execute("SELECT code,name FROM district ORDER BY 1").fetchall()
        keys = core.execute("""SELECT c.q,c.r,c.slot,c.admin_code,d.district_code
            FROM child_admin c JOIN admin_dong d ON c.admin_code=d.code ORDER BY 1,2,3,4""").fetchall()
    geography = ({name: code for code, name in districts}, tree, labels, shapely,
                 pyproj.Transformer.from_crs(5174, 4326, always_xy=True).transform,
                 pyproj.Transformer.from_crs(5174, 5179, always_xy=True).transform,
                 {key[:4] for key in keys})
    counts = Counter()
    with TemporaryDirectory(dir=output) as temporary, closing(read_only(BASE)) as source:
        path = Path(temporary) / DB
        if source.execute("""SELECT EXISTS(SELECT 1 FROM license_observation
              WHERE province='서울특별시' AND reported_status='폐업' AND closed_date<>'')""").fetchone()[0] != 1:
            raise ValueError("food_churn_requires_actual_closed_records")
        if source.execute("""SELECT dataset_id FROM license_observation WHERE province='서울특별시'
            GROUP BY dataset_id,authority_code,management_no HAVING COUNT(*)>1 LIMIT 1""").fetchone():
            raise ValueError("food_license_repeat_id_review_required")
        releases = source.execute("""SELECT id,label,official_catalog,original_sha256,gzip_sha256,acquired_at_utc
                    FROM dataset ORDER BY id""").fetchall()
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO release VALUES (?,?,?,?,?,?)", releases)
                db.executemany("INSERT INTO district VALUES (?,?)", districts)
                db.executemany("INSERT INTO child_admin VALUES (?,?,?,?,?)", keys)
                db.executemany("INSERT INTO window VALUES (?,?,?)",
                               ((years, str(start), str(OBSERVED_THROUGH)) for years, start in WINDOWS.items()))
                db.executemany("INSERT INTO observation VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                               observations(source, geography, counts))
            check_database(db)
            aggregate = db.execute("""SELECT event,years,SUM(reported_license_events),SUM(located_license_events)
                                  FROM v_food_churn_district GROUP BY event,years ORDER BY 1,2""").fetchall()
            child_totals = dict(((event, years), total) for event, years, total in db.execute("""SELECT event,years,
                SUM(located_license_events) FROM v_food_churn_child GROUP BY 1,2"""))
            if any(child_totals.get((event, years)) != located for event, years, _, located in aggregate):
                raise ValueError("food_churn_child_totals_do_not_reconcile")
            sample = db.execute("""SELECT category_id,event,years,located_license_events FROM v_food_churn_child
                WHERE q=-32 AND r=-179 AND slot=2 ORDER BY 1,2,3""").fetchall()
        path.replace(output / DB)
    proof = {"observedThrough": str(OBSERVED_THROUGH), "geometryRevision": REVISION,
             "sourceIndex": BASE.relative_to(ROOT).as_posix(), "sourceIndexSha256": digest(BASE),
             "sourceReleases": releases, "counts": dict(counts), "seoulWindowTotals": aggregate,
             "sagajeongSlot2": sample, "databaseSha256": digest(output / DB),
             "integrityCheck": "ok", "foreignKeyViolations": 0}
    atomic_json(output / "verification.json", proof)
    return proof


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    print(json.dumps(build(parser.parse_args().output)["seoulWindowTotals"], ensure_ascii=False))

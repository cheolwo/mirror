"""Keep a new official bus service day as an independently reproducible tile layer."""

from contextlib import closing
import csv
from hashlib import sha256
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_bus import DAILY_RECEIPT, DBNAME as AREA_BUS_DB, OUTPUT as AREA_BUS_DIR, scan_daily
from seoul_area_geography import check_database, read_only
from seoul_tile_population import ROOT, digest


SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source(id TEXT PRIMARY KEY,path TEXT NOT NULL,url TEXT NOT NULL,sha256 TEXT NOT NULL);
CREATE TABLE stop(standard_stop_id TEXT PRIMARY KEY);
CREATE TABLE stop_tile(
    standard_stop_id TEXT PRIMARY KEY REFERENCES stop,q INTEGER NOT NULL,r INTEGER NOT NULL,
    slot INTEGER NOT NULL CHECK(slot BETWEEN 0 AND 6)
) WITHOUT ROWID;
CREATE INDEX stop_tile_location ON stop_tile(q,r,slot);
CREATE TABLE daily_stop_hour(
    standard_stop_id TEXT NOT NULL REFERENCES stop,service_date TEXT NOT NULL,hour INTEGER NOT NULL,
    boardings INTEGER CHECK(boardings>=0),alightings INTEGER CHECK(alightings>=0),
    observed_route_stop_rows INTEGER NOT NULL,boarding_rows INTEGER NOT NULL,alighting_rows INTEGER NOT NULL,
    source_id TEXT NOT NULL REFERENCES source,PRIMARY KEY(standard_stop_id,service_date,hour)
) WITHOUT ROWID;
CREATE TABLE daily_row_status(
    service_date TEXT NOT NULL,review_state TEXT NOT NULL,source_rows INTEGER NOT NULL,
    source_id TEXT NOT NULL REFERENCES source,PRIMARY KEY(service_date,review_state)
) WITHOUT ROWID;
CREATE VIEW v_tile_bus_day_hour AS
SELECT t.q,t.r,t.slot,d.service_date,d.hour,SUM(d.boardings) AS boardings,SUM(d.alightings) AS alightings,
       COUNT(*) AS stops,SUM(d.observed_route_stop_rows) AS observed_route_stop_rows,
       SUM(d.boarding_rows) AS boarding_rows,SUM(d.alighting_rows) AS alighting_rows
FROM daily_stop_hour d JOIN stop_tile t USING(standard_stop_id)
GROUP BY t.q,t.r,t.slot,d.service_date,d.hour;
"""


def check_source_overlap(raw, prior, overlap):
    def records(path):
        with path.open(encoding="utf-8-sig",newline="") as stream:
            reader = csv.DictReader(stream)
            fingerprints = {}
            for row in reader:
                if row["기준일"] not in overlap:
                    continue
                key = tuple(row[field] for field in ("기준일","노선ID","정류장ID","정류장순서"))
                if key in fingerprints:
                    raise ValueError("duplicate_shared_source_row")
                normalized = json.dumps(row,ensure_ascii=False,sort_keys=True).encode("utf-8")
                fingerprints[key] = sha256(normalized).digest()
            return fingerprints
    before, after = records(prior), records(raw)
    if before != after:
        raise ValueError("overlapping_source_day_changed; store as a separate revision")
    return len(before)


def build():
    directory = OUTPUT / "transport/bus-latest"
    receipt = json.loads((directory/"source-receipt.json").read_text(encoding="utf-8"))
    raw = directory / receipt["file"]
    if digest(raw) != receipt["sha256"] or raw.stat().st_size != receipt["bytes"]:
        raise ValueError("new_bus_source_digest_mismatch")
    previous = json.loads(DAILY_RECEIPT.read_text(encoding="utf-8"))
    prior = DAILY_RECEIPT.parents[1] / previous["path"]
    if digest(prior) != previous["sha256"]:
        raise ValueError("previous_bus_source_digest_mismatch")
    shared_dates = set(previous["rowsByDate"]) & receipt["rowsByDate"].keys()
    shared_rows = check_source_overlap(raw, prior, shared_dates)
    new_days = sorted(set(receipt["rowsByDate"]) - shared_dates)
    if not new_days:
        raise ValueError("same_service_days_keep_prior_index")
    with closing(read_only(AREA_BUS_DIR/AREA_BUS_DB)) as current, closing(read_only(OUTPUT/DBNAME)) as core:
        stops = {row[0]:row for row in current.execute("SELECT * FROM stop ORDER BY standard_stop_id")}
        located = core.execute("SELECT place_id,q,r,slot FROM transport_child_position WHERE mode='bus'").fetchall()
    if any(stops[identifier][5:7] != (q,r) for identifier,q,r,_ in located):
        raise ValueError("bus_position_differs_between_verified_indices")
    target = directory/"bus-tile-daily.sqlite"
    with TemporaryDirectory(dir=directory) as work:
        db_path = Path(work)/target.name
        with closing(sqlite3.connect(db_path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.execute("INSERT INTO source VALUES (?,?,?,?)", (
                    "daily_tags",raw.relative_to(ROOT).as_posix(),receipt["url"],receipt["sha256"]))
                db.executemany("INSERT INTO stop VALUES(?)", ((identifier,) for identifier in stops))
                db.executemany("INSERT INTO stop_tile VALUES(?,?,?,?)", located)
                scan_daily(db, raw, stops, receipt["rowsByDate"])
            check_database(db)
            counts = [list(row) for row in db.execute("""SELECT service_date,COUNT(DISTINCT standard_stop_id),
                COUNT(*),SUM(boardings),SUM(alightings) FROM daily_stop_hour GROUP BY service_date""")]
            if counts != [list(row) for row in db.execute("""SELECT d.service_date,COUNT(DISTINCT d.standard_stop_id),
                    COUNT(*),SUM(d.boardings),SUM(d.alightings)
                    FROM daily_stop_hour d JOIN stop_tile t USING(standard_stop_id) GROUP BY d.service_date""")]:
                raise ValueError("valid_bus_daily_observations_missing_child_key")
            status = [list(row) for row in db.execute("SELECT service_date,review_state,source_rows FROM daily_row_status")]
        db_path.replace(target)
    result = {"geometryRevision":REVISION, "distribution":"local-review", "rawSharedDates":sorted(shared_dates),
              "verifiedIdenticalSharedSourceRows":shared_rows,"newServiceDays":new_days,
              "columnsInDayResults":["service_date","distinct_stops","stop_hours","card_boardings","card_alightings"],
              "dayResults":counts,"reviewStateRows":status,"databaseSha256":digest(target)}
    atomic_json(directory/"bus-daily-summary.json", result)
    return result


if __name__ == "__main__":
    print(json.dumps(build(),ensure_ascii=False,indent=2))

"""Bind all six published rental months to identically located official dock editions."""

from contextlib import closing
import csv
import json
from pathlib import Path
import re
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, point_key
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import check_database, gis, read_only
from seoul_tile_population import PUBLIC, ROOT, digest, local_path

STATIONS = PUBLIC / "diorama-data-relations-20261123-r2/public-bike-station-status-history/bike-station-history.sqlite"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source (id TEXT PRIMARY KEY,path TEXT NOT NULL,sha256 TEXT NOT NULL,url TEXT NOT NULL);
CREATE TABLE tile (q INTEGER,r INTEGER,slot INTEGER, PRIMARY KEY(q,r,slot)) WITHOUT ROWID;
CREATE TABLE station (
    station_number INTEGER PRIMARY KEY,name TEXT NOT NULL,district TEXT NOT NULL,
    q INTEGER NOT NULL,r INTEGER NOT NULL,slot INTEGER NOT NULL,
    position_first TEXT NOT NULL,position_last TEXT NOT NULL,
    FOREIGN KEY(q,r,slot) REFERENCES tile(q,r,slot)
);
CREATE TABLE monthly (
    source_row INTEGER PRIMARY KEY,source_id TEXT NOT NULL REFERENCES source,
    month TEXT NOT NULL,reported_district TEXT NOT NULL,station_number INTEGER,
    linked_station INTEGER REFERENCES station,rentals INTEGER NOT NULL CHECK(rentals>=0),
    returns INTEGER NOT NULL CHECK(returns>=0),binding_state TEXT NOT NULL
);
CREATE INDEX month_station ON monthly(month,linked_station);
CREATE VIEW v_bike_tile_month AS
SELECT s.q,s.r,s.slot,a.month,count(*) AS source_rows,count(DISTINCT s.station_number) AS stations,
       sum(a.rentals) AS rentals,sum(a.returns) AS returns
FROM monthly a JOIN station s ON s.station_number=a.linked_station
GROUP BY s.q,s.r,s.slot,a.month;
"""


def comparable_name(label):
    return "".join(label.split())


def stable_positions(src, keys, transform):
    dates = dict(src.execute("SELECT snapshot_month,release_id FROM source_release"))
    first, last = "2025-12", "2026-06"
    rows = src.execute("""SELECT a.station_number,b.station_name,b.district,b.latitude,b.longitude
        FROM pm_station_observation a JOIN pm_station_observation b USING(station_number)
        WHERE a.release_id=? AND b.release_id=? AND a.latitude=b.latitude AND a.longitude=b.longitude
        AND a.district=b.district""", (dates[first], dates[last]))
    for number, name, district, lat, lon in rows:
        key = point_key(*transform(lon, lat))
        if key in keys:
            yield number, name, district, *key, first, last


def rental_rows(path, positions):
    with path.open(encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        if reader.fieldnames != ["자치구","대여소명","기준년월","대여건수","반납건수"]:
            raise ValueError("unexpected_monthly_bike_source_fields")
        for source_row, row in enumerate(reader, 2):
            label = row["대여소명"].strip()
            match = re.fullmatch(r"([0-9]+)[.]\s*(.+)", label)
            number = int(match[1]) if match else None
            point = positions.get(number)
            verified = point and point[2] == row["자치구"] and comparable_name(point[1]) == comparable_name(match[2])
            state = ("exact_station_and_stable_position" if verified else
                     "label_or_district_mismatch" if point else "position_not_verified_in_both_editions")
            year_month = row["기준년월"]
            if not re.fullmatch(r"20260[1-6]", year_month):
                raise ValueError("unexpected_monthly_bike_period")
            yield (source_row, "rentals", year_month[:4]+"-"+year_month[4:], row["자치구"], number,
                   number if verified else None, int(row["대여건수"]), int(row["반납건수"]), state)


def build():
    output = OUTPUT / "bike"
    receipt = json.loads((output/"source-receipt.json").read_text(encoding="utf-8"))
    path = local_path(ROOT/receipt["rawPath"])
    if digest(path) != receipt["sha256"] or path.stat().st_size != receipt["bytes"]:
        raise ValueError("monthly_bike_source_hash_mismatch")
    pyproj, _ = gis()
    transform = pyproj.Transformer.from_crs(4326,5179,always_xy=True).transform
    with closing(read_only(OUTPUT/DBNAME)) as geo, closing(read_only(STATIONS)) as src:
        keys = set(geo.execute("SELECT q,r,slot FROM child_tile"))
        positions = {row[0]:row for row in stable_positions(src,keys,transform)}
    target = output / "bike-child-index.sqlite"
    with TemporaryDirectory(dir=output) as temp:
        draft = Path(temp) / target.name
        with closing(sqlite3.connect(draft)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO source VALUES (?,?,?,?)", [
                    ("rentals",receipt["rawPath"],receipt["sha256"],receipt["sourceUrl"]),
                    ("positions",STATIONS.relative_to(ROOT).as_posix(),digest(STATIONS),
                     "https://data.seoul.go.kr/dataList/OA-13252/F/1/datasetView.do")
                ])
                db.executemany("INSERT INTO tile VALUES (?,?,?)",sorted(keys))
                db.executemany("INSERT INTO station VALUES (?,?,?,?,?,?,?,?)",positions.values())
                db.executemany("INSERT INTO monthly VALUES (?,?,?,?,?,?,?,?,?)",rental_rows(path,positions))
            check_database(db)
            total = db.execute("SELECT count(*) FROM monthly").fetchone()[0]
            if total != receipt["officialRows"]:
                raise ValueError("monthly_bike_source_row_count_mismatch")
            state = dict(db.execute("SELECT binding_state,count(*) FROM monthly GROUP BY 1"))
            by_month = [list(row) for row in db.execute("""SELECT month,count(*),count(linked_station),
                            sum(CASE WHEN linked_station IS NOT NULL THEN rentals END),
                            sum(CASE WHEN linked_station IS NOT NULL THEN returns END)
                            FROM monthly GROUP BY month ORDER BY month""")]
            by_tile = [list(row) for row in db.execute("""SELECT month,sum(source_rows),sum(rentals),sum(returns)
                            FROM v_bike_tile_month GROUP BY month ORDER BY month""")]
            if [[x[0],*x[2:]] for x in by_month] != by_tile:
                raise ValueError("monthly_bike_counts_not_conserved")
        draft.replace(target)
    summary = {"schema":"hongdal.dong-tile-bike.v1","geometryRevision":REVISION,"sourceRows":total,
               "stablePositionKeys":len(positions),"stateCounts":state,"monthlySummary":by_month,
               "monthlyColumns":["month","sourceRows","locatedRows","locatedRentals","locatedReturns"],
               "limitation":"같은 제공기관 번호·이름·구 및 2025-12/2026-06 두 위치판의 일치 표본. 개시일·이전일·실시간 재고 미확인.",
               "databaseSha256":digest(target),"distribution":"local-review"}
    atomic_json(output/"bike-summary.json",summary)
    return summary


if __name__ == "__main__":
    print(json.dumps(build(),ensure_ascii=False,indent=2))

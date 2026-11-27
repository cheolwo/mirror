"""Index the bounded KMA snapshots collected during the diorama data review."""

import argparse
from collections import Counter
from contextlib import closing
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import ROOT, digest


DAY = "2026-11-26"
OFFICIAL = "https://www.weather.go.kr/w/weather/land/city-obs.do"
KST = timezone(timedelta(hours=9))
TIME_KEYS = tuple(f"{DAY}T{hour:02d}:00" for hour in range(4))
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_snapshot (
    as_of_local TEXT PRIMARY KEY,as_of_unix_seconds INTEGER NOT NULL,source_url TEXT NOT NULL,
    source_response_sha256 TEXT NOT NULL,ingested_db_path TEXT NOT NULL,ingested_db_sha256 TEXT NOT NULL,
    retrieved_utc TEXT NOT NULL
);
CREATE TABLE station_reference (
    station_number TEXT PRIMARY KEY,name TEXT NOT NULL,location_source_id TEXT,location_source_key TEXT,
    q INTEGER,r INTEGER,slot INTEGER CHECK(slot BETWEEN 0 AND 6),sgis_dong_code TEXT,
    reference_state TEXT NOT NULL
);
CREATE TABLE station_observation (
    station_number TEXT REFERENCES station_reference,as_of_local TEXT REFERENCES source_snapshot,
    temperature_c REAL,precipitation_today_mm REAL,precipitation_display TEXT,
    snow_cm REAL,snow_display TEXT,humidity_percent REAL,wind_direction_text TEXT,
    wind_m_s REAL,sea_level_pressure_hpa REAL,conditions_as_reported TEXT,
    PRIMARY KEY(as_of_local,station_number)
) WITHOUT ROWID;
CREATE INDEX observation_by_station_clock ON station_observation(station_number,as_of_local);
CREATE INDEX station_tile ON station_reference(q,r,slot);
CREATE VIEW v_weather_station_observation AS
SELECT s.station_number,s.name,s.q,s.r,s.slot,s.sgis_dong_code,s.reference_state,
       o.as_of_local,p.as_of_unix_seconds,o.temperature_c,o.precipitation_today_mm,o.precipitation_display,
       o.snow_cm,o.snow_display,o.humidity_percent,o.wind_direction_text,o.wind_m_s,
       o.sea_level_pressure_hpa,o.conditions_as_reported
FROM station_observation o JOIN station_reference s USING(station_number)
JOIN source_snapshot p USING(as_of_local);
"""


def frozen_inputs(directory):
    for clock in TIME_KEYS:
        name = "kma-observed-" + clock.replace("-","").replace(":","").lower()
        receipt_file = directory / (name + ".receipt.json")
        if not receipt_file.is_file():
            continue
        receipt = json.loads(receipt_file.read_text(encoding="utf-8"))
        path = directory / receipt["databaseName"]
        if (receipt["asOfLocal"],receipt["timezone"],receipt["sourceUrl"]) != (clock,"Asia/Seoul",OFFICIAL):
            raise ValueError("unexpected_weather_snapshot_scope")
        if digest(path) != receipt["databaseSha256"]:
            raise ValueError("weather_snapshot_digest_changed")
        if digest(directory / receipt["csvFilename"]) != receipt["csvSha256"]:
            raise ValueError("weather_covariate_source_csv_digest_changed")
        yield receipt, path


def write_window(target, inputs):
    known = {}
    results = Counter()
    reported_stations = {}
    with closing(sqlite3.connect(target)) as out:
        out.executescript(SCHEMA)
        with out:
            for receipt,path in inputs:
                clock = receipt["asOfLocal"]
                reported_stations[clock] = set()
                epoch = int(datetime.fromisoformat(clock).replace(tzinfo=KST).timestamp())
                out.execute("INSERT INTO source_snapshot VALUES (?,?,?,?,?,?,?)", (
                    clock,epoch,OFFICIAL,receipt["sourceResponseSha256"],path.relative_to(ROOT).as_posix(),
                    receipt["databaseSha256"],receipt["retrievedUtc"]))
                with closing(read_only(path)) as source:
                    rows = source.execute("""SELECT o.station_number,reported_name,source_location_id,
                        source_location_key,q,r,slot,sgis_dong_code,state,temperature_c,precipitation_today_mm,
                        precipitation_display,snow_cm,snow_display,humidity_percent,wind_direction_text,
                        wind_m_s,sea_level_pressure_hpa,conditions_text
                        FROM v_observed_station_tile o WHERE as_of_local=? ORDER BY CAST(station_number AS INT)""", (clock,))
                    for row in rows:
                        key = row[0]
                        reported_stations[clock].add(key)
                        identity = row[:9]
                        if key not in known:
                            out.execute("INSERT INTO station_reference VALUES (?,?,?,?,?,?,?,?,?)", identity)
                            known[key] = identity
                        elif known[key] != identity:
                            raise ValueError(f"within_window_station_geography_changed:{key}")
                        out.execute("INSERT INTO station_observation VALUES (?,?,?,?,?,?,?,?,?,?,?,?)", (key,clock,*row[9:]))
                        results[clock] += 1
                if results[clock] != receipt["sourceRows"]:
                    raise ValueError("weather_snapshot_count_mismatch")
        check_database(out)
        located, = out.execute("SELECT COUNT(*) FROM station_reference WHERE q IS NOT NULL").fetchone()
    first = inputs[0][0]["asOfLocal"]
    missing = {clock: sorted(reported_stations[first] - codes) for clock,codes in reported_stations.items()}
    return {"observationsByHour":dict(results),"distinctStationNumbers":len(known),"tileBoundStations":located,
            "sourceMissingStationsByHourComparedWithFirst":missing}


def build(require_all):
    directory = OUTPUT / "ecology/national"
    inputs = list(frozen_inputs(directory))
    if not inputs or require_all and len(inputs)!=len(TIME_KEYS):
        raise ValueError("required_observation_hours_not_all_verified")
    target = directory / "kma-observation-window.sqlite"
    with TemporaryDirectory(dir=directory) as temp:
        draft = Path(temp) / target.name
        counts = write_window(draft, inputs)
        draft.replace(target)
    result = {"geometryRevision":REVISION,"distribution":"local-review","timezone":"Asia/Seoul",
              "expectedHours":TIME_KEYS,"observedHours":[item[0]["asOfLocal"] for item in inputs],
              "distributionAtStation":"published_point_observation_no_area_interpolation",
              "precipitationUnit":"mm since date boundary as published","databaseSha256":digest(target),**counts}
    atomic_json(directory/"kma-observation-window-summary.json",result)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-all",action="store_true")
    print(json.dumps(build(parser.parse_args().require_all),ensure_ascii=False,indent=2))

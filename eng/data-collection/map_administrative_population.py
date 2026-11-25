"""Bind official resident counts to administrative codes and published map boundaries."""
from __future__ import annotations

import argparse
from contextlib import closing
from hashlib import file_digest
import json
from pathlib import Path
import sqlite3
from uuid import uuid4

from downloaded_map_data import json_text, population_rows


PRIORITY_DISTRICTS = ("11215", "11230", "11260", "11350")

SCHEMA = """
PRAGMA foreign_keys = ON;
CREATE TABLE provenance (
    kind TEXT PRIMARY KEY,
    details_json TEXT NOT NULL CHECK (json_valid(details_json))
) STRICT;
CREATE TABLE administrative_area (
    administrative_code TEXT PRIMARY KEY,
    administrative_name TEXT NOT NULL,
    district_code TEXT,
    registry_as_of TEXT NOT NULL
) STRICT;
CREATE INDEX administrative_area_district ON administrative_area(district_code);
CREATE TABLE resident_population (
    administrative_code TEXT NOT NULL REFERENCES administrative_area,
    observed_date TEXT NOT NULL,
    source_row INTEGER NOT NULL UNIQUE,
    source_sido TEXT NOT NULL,
    source_sigungu TEXT NOT NULL,
    source_administrative_name TEXT NOT NULL,
    total INTEGER NOT NULL CHECK (total >= 0),
    male INTEGER NOT NULL CHECK (male >= 0),
    female INTEGER NOT NULL CHECK (female >= 0),
    male_age_json TEXT NOT NULL CHECK (json_array_length(male_age_json) = 111),
    female_age_json TEXT NOT NULL CHECK (json_array_length(female_age_json) = 111),
    PRIMARY KEY(administrative_code, observed_date),
    CHECK(total = male + female)
) STRICT;
CREATE TABLE administrative_boundary (
    administrative_code TEXT PRIMARY KEY REFERENCES administrative_area,
    geometry_json TEXT NOT NULL CHECK (json_valid(geometry_json)),
    west REAL NOT NULL,
    east REAL NOT NULL,
    south REAL NOT NULL,
    north REAL NOT NULL
) STRICT;
CREATE VIEW administrative_population AS
SELECT a.administrative_code, a.administrative_name, a.district_code, a.registry_as_of,
       p.observed_date, p.total, p.male, p.female, p.source_row, p.source_sido,
       p.source_sigungu, p.source_administrative_name
FROM administrative_area a LEFT JOIN resident_population p USING(administrative_code);
CREATE VIEW administrative_population_map AS
SELECT p.*, b.geometry_json, b.west, b.east, b.south, b.north
FROM administrative_population p JOIN administrative_boundary b USING(administrative_code);
"""


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as file:
        return file_digest(file, "sha256").hexdigest()


def build(db, source, receipt, map_db, boundary_receipt):
    source_info = read_json(receipt)
    source_hash = digest(source)
    if source_hash != source_info["sha256"] or source.stat().st_size != source_info["bytes"]:
        raise ValueError("Population CSV differs from its receipt")

    boundary_info = read_json(boundary_receipt)
    db.executescript(SCHEMA)
    with closing(sqlite3.connect(map_db.resolve().as_uri() + "?mode=ro", uri=True)) as map_source:
        db.executemany(
            "INSERT INTO administrative_area VALUES (?, ?, ?, ?)",
            map_source.execute(
                """SELECT code, name, parent_code, as_of FROM region
                WHERE kind = 'administrative' ORDER BY code"""
            ),
        )
        boundary_count = db.executemany(
            "INSERT INTO administrative_boundary VALUES (?, ?, ?, ?, ?, ?)",
            map_source.execute(
                """SELECT region_code, geometry_json, west, east, south, north
                FROM region_boundary WHERE registry_match = 1 ORDER BY region_code"""
            ),
        ).rowcount
    if boundary_count == 0 or boundary_count != boundary_info["regionCount"]:
        raise ValueError("Verified boundary count differs from its receipt")

    rows = population_rows(source, administrative=True, encoding=source_info["encoding"])
    for source_row, code, _, observed, sido, sigungu, dong, _, total, male, female, male_ages, female_ages in rows:
        if observed != source_info["observedDate"]:
            raise ValueError(f"Population date differs from receipt in data row {source_row}")
        if min(total, male, female) < 0:
            raise ValueError(f"Negative population in data row {source_row}")
        db.execute(
            "INSERT INTO resident_population VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            (code, observed, source_row, sido, sigungu, dong, total, male, female, male_ages, female_ages),
        )
    if not db.execute("SELECT 1 FROM resident_population LIMIT 1").fetchone():
        raise ValueError("Empty population source")
    db.executemany(
        "INSERT INTO provenance VALUES (?, ?)",
        (
            ("population", json_text(source_info)),
            ("boundary", json_text({
                "release": boundary_info["release"], "crs": boundary_info["crs"],
                "restriction": boundary_info["restriction"], "boundaryReceiptSha256": digest(boundary_receipt),
                "inputMapDatabaseSha256": digest(map_db),
            })),
        ),
    )
    db.commit()
    if db.execute("PRAGMA integrity_check").fetchone() != ("ok",) or db.execute("PRAGMA foreign_key_check").fetchone():
        raise RuntimeError("SQLite validation failed")


def export_priority_map(db, output):
    rows = db.execute(
        """SELECT administrative_code, administrative_name, district_code, observed_date,
               total, male, female, geometry_json
        FROM administrative_population_map
        WHERE district_code IN (?, ?, ?, ?) ORDER BY administrative_code""",
        PRIORITY_DISTRICTS,
    )
    features = []
    for code, name, district, observed, total, male, female, geometry in rows:
        features.append({
            "type": "Feature", "id": code, "geometry": json.loads(geometry),
            "properties": {
                "administrativeCode": code, "name": name, "districtCode": district,
                "referenceDate": observed, "residentPopulation": total,
                "maleResidentPopulation": male, "femaleResidentPopulation": female,
            },
        })
    restriction = db.execute(
        "SELECT json_extract(details_json, '$.restriction') FROM provenance WHERE kind = 'boundary'"
    ).fetchone()[0]
    payload = {"type": "FeatureCollection", "restriction": restriction, "features": features}
    output.write_text(json_text(payload) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csv", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--map-db", type=Path, required=True)
    parser.add_argument("--boundary-receipt", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    output = args.output_dir.resolve()
    if output.exists():
        parser.error("Use a new output directory")
    output.parent.mkdir(parents=True, exist_ok=True)
    working = output.with_name(f".{output.name}.{uuid4().hex}.tmp")
    working.mkdir()
    target = working / "administrative-resident-population.sqlite"
    geojson = working / "priority-districts-resident-population.geojson"
    try:
        with closing(sqlite3.connect(target)) as db:
            build(db, args.csv, args.receipt, args.map_db, args.boundary_receipt)
            export_priority_map(db, geojson)
        working.rename(output)
    finally:
        if working.exists():
            target.unlink(missing_ok=True)
            geojson.unlink(missing_ok=True)
            working.rmdir()


if __name__ == "__main__":
    main()

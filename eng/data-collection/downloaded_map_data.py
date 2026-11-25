"""Index reviewed public files already present in Downloads for local map review."""
from __future__ import annotations

import argparse
from contextlib import closing
import csv
from datetime import date
from hashlib import file_digest
import json
from pathlib import Path
import sqlite3
from uuid import uuid4
from zipfile import BadZipFile, ZipFile

from downloaded_public_transport_rows import iter_transport_rows


MAP_DATASETS = {
    "data.go.kr/data/15067528": "bus_stop",
    "data.go.kr/data/15081953": "road_bridge",
    "data.go.kr/data/15081955": "road_tunnel",
    "data.go.kr/data/15099158": "legal_dong_population",
}
ELEVATION_ALIAS = {"한반도 (1).zip": "한반도.zip"}

SCHEMA = """
PRAGMA foreign_keys = ON;
CREATE TABLE source_asset (
    source_id TEXT PRIMARY KEY,
    receipt_filename TEXT NOT NULL,
    local_filename TEXT,
    provider_reference TEXT NOT NULL,
    license_note TEXT,
    downloaded_kst TEXT,
    reference_path TEXT,
    expected_sha256 TEXT,
    sha256 TEXT,
    bytes INTEGER,
    status TEXT NOT NULL CHECK (status IN (
        'imported', 'catalog_only', 'reuse_existing', 'missing', 'mismatch', 'invalid_archive'
    )),
    archive_members INTEGER,
    imported_rows INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE transport_record (
    record_id INTEGER PRIMARY KEY,
    source_id TEXT NOT NULL REFERENCES source_asset(source_id),
    source_row INTEGER NOT NULL,
    provider_id TEXT,
    name TEXT,
    area_hint TEXT,
    attributes_json TEXT NOT NULL,
    UNIQUE(source_id, source_row)
);
CREATE INDEX transport_area ON transport_record(source_id, area_hint);
CREATE INDEX transport_provider ON transport_record(source_id, provider_id);
CREATE TABLE transport_point (
    point_id INTEGER PRIMARY KEY,
    record_id INTEGER NOT NULL REFERENCES transport_record(record_id),
    endpoint INTEGER NOT NULL CHECK (endpoint IN (1, 2)),
    longitude REAL NOT NULL,
    latitude REAL NOT NULL,
    UNIQUE(record_id, endpoint)
);
CREATE VIRTUAL TABLE transport_point_rtree USING rtree(
    point_id, min_lon, max_lon, min_lat, max_lat
);
CREATE TABLE legal_dong_population (
    source_id TEXT NOT NULL REFERENCES source_asset(source_id),
    source_row INTEGER NOT NULL,
    legal_dong_code TEXT NOT NULL CHECK (length(legal_dong_code) = 10),
    sigungu_code TEXT NOT NULL CHECK (length(sigungu_code) = 5),
    observed_date TEXT NOT NULL,
    sido_name TEXT NOT NULL,
    sigungu_name TEXT NOT NULL,
    eup_myeon_dong_name TEXT NOT NULL,
    ri_name TEXT NOT NULL,
    total INTEGER NOT NULL,
    male INTEGER NOT NULL,
    female INTEGER NOT NULL,
    male_age_json TEXT NOT NULL,
    female_age_json TEXT NOT NULL,
    PRIMARY KEY(source_id, observed_date, legal_dong_code),
    UNIQUE(source_id, source_row)
) WITHOUT ROWID;
CREATE INDEX population_area ON legal_dong_population(sigungu_code, observed_date);
"""


def json_text(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def inspect_asset(downloads, entry):
    file_name = entry["file"]
    if Path(file_name).name != file_name:
        raise ValueError("Receipt must contain a filename within Downloads")
    path = downloads / file_name
    if not path.exists() and file_name in ELEVATION_ALIAS:
        path = downloads / ELEVATION_ALIAS[file_name]
    if not path.is_file() or path.resolve().parent != downloads:
        return path, None, None, "missing", None

    with path.open("rb") as file:
        sha256 = file_digest(file, "sha256").hexdigest()
    size = path.stat().st_size
    if sha256 != entry["sha256"] or size != entry["bytes"]:
        return path, sha256, size, "mismatch", None

    member_count = None
    if path.suffix.lower() == ".zip":
        try:
            with ZipFile(path) as archive:
                member_count = sum(not member.is_dir() for member in archive.infolist())
        except BadZipFile:
            return path, sha256, size, "invalid_archive", None
    status = "reuse_existing" if entry.get("reference_path") else "catalog_only"
    return path, sha256, size, status, member_count


def import_transport(db, source_id, path):
    count = 0
    for row in iter_transport_rows(path, source_id):
        record_id = db.execute(
            """INSERT INTO transport_record(
                source_id, source_row, provider_id, name, area_hint, attributes_json
            ) VALUES (?, ?, ?, ?, ?, ?)""",
            (source_id, row["source_row"], row["provider_id"], row["name"],
             row["area_hint"], json_text(row["attributes"])),
        ).lastrowid
        points = (
            (1, row["longitude"], row["latitude"]),
            (2, row["end_longitude"], row["end_latitude"]),
        )
        for endpoint, longitude, latitude in points:
            if longitude is None or latitude is None:
                continue
            point_id = db.execute(
                "INSERT INTO transport_point(record_id, endpoint, longitude, latitude) VALUES (?, ?, ?, ?)",
                (record_id, endpoint, longitude, latitude),
            ).lastrowid
            db.execute(
                "INSERT INTO transport_point_rtree VALUES (?, ?, ?, ?, ?)",
                (point_id, longitude, longitude, latitude, latitude),
            )
        count += 1
    return count


def population_rows(path, *, administrative=False, encoding="cp949"):
    code_field = "행정기관코드" if administrative else "법정동코드"
    ages = {
        sex: [f"{age}세{sex}" for age in range(110)] + [f"110세이상 {sex}"]
        for sex in ("남자", "여자")
    }
    with path.open(encoding=encoding, newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        expected = {
            code_field, "기준연월", "시도명", "시군구명", "읍면동명",
            "계", "남자", "여자", *ages["남자"], *ages["여자"],
        }
        if not administrative:
            expected.add("리명")
        if len(reader.fieldnames or []) != len(expected) or set(reader.fieldnames or []) != expected:
            raise ValueError("Unexpected population CSV header")
        for source_row, row in enumerate(reader, 1):
            if None in row or None in row.values():
                raise ValueError(f"Invalid population column count in data row {source_row}")
            code = row[code_field]
            if len(code) != 10 or not code.isascii() or not code.isdecimal():
                raise ValueError(f"Invalid {code_field} in data row {source_row}")
            observed = date.fromisoformat(row["기준연월"]).isoformat()
            total, male, female = (int(row[field]) for field in ("계", "남자", "여자"))
            male_ages, female_ages = (
                [int(row[field]) for field in ages[sex]] for sex in ("남자", "여자")
            )
            if total != male + female or male != sum(male_ages) or female != sum(female_ages):
                raise ValueError(f"Inconsistent population totals in data row {source_row}")
            yield (
                source_row, code, code[:5], observed, row["시도명"], row["시군구명"],
                row["읍면동명"], row.get("리명", ""), total, male, female,
                json_text(male_ages), json_text(female_ages),
            )


def build(db, downloads, receipt, extra_receipt):
    if not downloads.is_dir():
        raise ValueError("Downloads directory does not exist")
    db.executescript(SCHEMA)
    entries = [(MAP_DATASETS.get(entry["source"], entry["source"]), entry) for entry in receipt]
    entries.extend((entry["source_id"], entry) for entry in extra_receipt)
    for source_id, entry in entries:
        path, sha256, size, status, members = inspect_asset(downloads, entry)
        db.execute(
            """INSERT INTO source_asset(
                source_id, receipt_filename, local_filename, provider_reference, license_note,
                downloaded_kst, reference_path, expected_sha256, sha256, bytes, status, archive_members
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (source_id, entry["file"], path.name if sha256 else None, entry["source"],
             entry.get("license"), entry.get("downloadedKst"), entry.get("reference_path"),
             entry["sha256"], sha256, size, status, members),
        )
        if status != "catalog_only" or source_id not in MAP_DATASETS.values():
            continue
        if source_id == "legal_dong_population":
            count = db.executemany(
                """INSERT INTO legal_dong_population(
                    source_id, source_row, legal_dong_code, sigungu_code, observed_date,
                    sido_name, sigungu_name, eup_myeon_dong_name, ri_name, total, male,
                    female, male_age_json, female_age_json
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                ((source_id, *row) for row in population_rows(path)),
            ).rowcount
        else:
            count = import_transport(db, source_id, path)
        if not count:
            raise ValueError(f"No rows for {source_id}")
        db.execute(
            "UPDATE source_asset SET status = 'imported', imported_rows = ? WHERE source_id = ?",
            (count, source_id),
        )
    db.commit()
    if db.execute("PRAGMA integrity_check").fetchone() != ("ok",) or db.execute(
        "PRAGMA foreign_key_check"
    ).fetchone():
        raise RuntimeError("SQLite validation failed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--downloads", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--extra-receipt", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists():
        parser.error("Use a new output database path")
    receipt = json.loads(args.receipt.read_text(encoding="utf-8-sig"))["files"]
    extra = json.loads(args.extra_receipt.read_text(encoding="utf-8-sig"))["files"]
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.{uuid4().hex}.tmp")
    try:
        with closing(sqlite3.connect(temporary)) as db:
            build(db, args.downloads.resolve(), receipt, extra)
        output.hardlink_to(temporary)
    finally:
        temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    main()

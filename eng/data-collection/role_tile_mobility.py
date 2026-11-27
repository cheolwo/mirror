"""Summarize Seoul-KT mobility reports at the published administrative scale."""

import argparse
from collections import Counter
from contextlib import closing
import csv
from datetime import date
from io import TextIOWrapper
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
import zipfile

from collect_public_snapshots import atomic_json
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import PUBLIC, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126/mobility"
CORE = TILE_OUTPUT / DBNAME
SOURCE = "https://data.seoul.go.kr/dataList/OA-22657/F/1/datasetView.do"
ATTRIBUTION = "서울특별시·KT, 서울시 통신데이터"
DATASET = "OA-22657"
SAMPLES = {
    "2026-10-30": "DED71FFCD515392365F1E66542B2D6D68A87AE470FA13F04A1ABF2C098C93EB7",
    "2026-10-31": "B03E46BA5F6B8C545F118400F20166E5FDF1FD30BDB7624D7C41B0E5EB8E79C5",
}
HEADER = ["o_admdong_cd", "d_admdong_cd", "st_time_cd", "fns_time_cd", "in_forn_div_nm",
          "forn_citiz_nm", "move_trans", "move_dist", "move_time", "cnt", "etl_ymd"]
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_release (
    observed_date TEXT PRIMARY KEY,source_id TEXT NOT NULL,url TEXT NOT NULL,attribution TEXT NOT NULL,
    raw_filename TEXT NOT NULL,sha256 TEXT NOT NULL,day_kind TEXT NOT NULL
);
CREATE TABLE admin_dong (
    admin_code TEXT PRIMARY KEY,district_code TEXT NOT NULL,name TEXT NOT NULL,native_code TEXT NOT NULL UNIQUE
);
CREATE TABLE mobility_dong_hour (
    observed_date TEXT NOT NULL REFERENCES source_release(observed_date),
    admin_code TEXT NOT NULL REFERENCES admin_dong(admin_code),
    direction TEXT NOT NULL CHECK (direction IN ('depart','arrive')),
    hour INTEGER NOT NULL CHECK(hour BETWEEN 0 AND 23),
    published_source_rows INTEGER NOT NULL CHECK(published_source_rows>0),
    usable_rows INTEGER NOT NULL CHECK(usable_rows<=published_source_rows),
    published_moves_cent INTEGER CHECK(published_moves_cent>=0),
    CHECK((usable_rows=0)=(published_moves_cent IS NULL)),
    PRIMARY KEY(observed_date,admin_code,direction,hour)
) WITHOUT ROWID;
CREATE INDEX mobility_by_dong ON mobility_dong_hour(admin_code,observed_date,direction,hour);
CREATE VIEW v_mobility_dong_hour AS
SELECT o.admin_code,d.name,d.district_code,o.observed_date,s.day_kind,o.direction,o.hour,
       o.published_moves_cent/100.0 AS published_moves_estimate,
       o.published_source_rows,o.usable_rows,s.source_id,s.attribution
FROM mobility_dong_hour o JOIN admin_dong d USING(admin_code) JOIN source_release s USING(observed_date);
"""


def hour_lookup():
    result = {str(n): n for n in range(24)}
    result.update((f"{h:02d}", h) for h in range(24))
    result.update((value, h) for h in range(24) for m in range(0, 60, 20)
                  for value in (f"{h:02d}{m:02d}", str(h * 100 + m)))
    # The source also uses explicit 1–2-digit hours. They take priority over
    # any leading-zero-stripped 00:20/00:40 label which has the same digits.
    result.update((str(n), n) for n in range(24))
    result.pop("40", None)
    return result


def count_cents(value):
    whole, dot, fraction = value.partition(".")
    if not whole.isdecimal() or (dot and (not fraction.isdecimal() or len(fraction) > 2)):
        return None
    return 100 * int(whole) + int(fraction.ljust(2, "0"))


def source_rows(archive, sample_date):
    expected = f"seoul_trans_admdong3_final_{sample_date.replace('-', '')}.csv"
    with zipfile.ZipFile(archive) as zipped:
        if zipped.namelist() != [expected] or zipped.getinfo(expected).file_size > 800_000_000:
            raise ValueError("mobility_unexpected_archive_layout")
        with zipped.open(expected) as stream:
            with TextIOWrapper(stream, encoding="utf-8-sig", newline="") as csv_stream:
                rows = csv.reader(csv_stream)
                if next(rows) != HEADER:
                    raise ValueError("mobility_unexpected_header")
                yield from rows


def group_archive(archive, sample_date, native_codes):
    states = Counter()
    unknown_seoul_codes = Counter()
    unknown_time_codes = Counter()
    grouped = {}
    hours = hour_lookup()
    expected_day = sample_date.replace("-", "")
    for row in source_rows(archive, sample_date):
        states["source_rows"] += 1
        if len(row) != len(HEADER) or row[-1] != expected_day:
            raise ValueError("mobility_observation_date_or_width_mismatch")
        outbound, inbound, st, end = row[:4]
        count = count_cents(row[9])
        if count is None:
            states["unknown_or_masked_count_rows"] += 1
        for direction, source_code, time in (("depart", outbound, st), ("arrive", inbound, end)):
            code = native_codes.get(source_code)
            if code is None:
                if source_code.startswith("11"):
                    unknown_seoul_codes[source_code] += 1
                continue
            hour = hours.get(time)
            if hour is None:
                unknown_time_codes[time] += 1
                continue
            key = code, direction, hour
            counts = grouped.get(key)
            if counts is None:
                counts = grouped[key] = [0, 0, 0]
            counts[0] += 1
            if count is not None:
                counts[1] += 1
                counts[2] += count
    data = [(sample_date, *key, seen, known, total if known else None)
            for key, (seen, known, total) in sorted(grouped.items())]
    return data, {**states, "unmatchedSeoulNativeEndpoints": dict(sorted(unknown_seoul_codes.items())),
                  "unresolvedTimeEndpointRows": dict(sorted(unknown_time_codes.items())),
                  "matchedSeoulEndpointRows": sum(record[4] for record in data),
                  "seoulDongs": len({record[1] for record in data}), "publishedProfileRows": len(data)}


def build(output):
    output.mkdir(parents=True, exist_ok=True)
    with closing(read_only(CORE)) as db:
        areas = [(code, district, label, code[:-2]) for code, district, label in db.execute(
            "SELECT code,district_code,name FROM admin_dong ORDER BY code") if code.endswith("00")]
    native_codes = {native: code for code, _district, _label, native in areas}
    if len(native_codes) != len(areas) or not areas:
        raise ValueError("mobility_native_dong_crosswalk_not_unique")
    records = []
    with TemporaryDirectory(dir=output) as tmp:
        path = Path(tmp) / "seoul-mobility-dong.sqlite"
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO admin_dong VALUES (?,?,?,?)", areas)
                for sample_date, sha in sorted(SAMPLES.items()):
                    filename = f"seoul_trans_admdong3_final_{sample_date.replace('-', '')}.zip"
                    archive = output / filename
                    if digest(archive) != sha.lower():
                        raise ValueError(f"mobility_archive_fingerprint_mismatch:{sample_date}")
                    day_kind = "weekday" if date.fromisoformat(sample_date).weekday() < 5 else "weekend"
                    db.execute("INSERT INTO source_release VALUES (?,?,?,?,?,?,?)",
                               (sample_date, DATASET, SOURCE, ATTRIBUTION, filename, sha.lower(), day_kind))
                    rows, counts = group_archive(archive, sample_date, native_codes)
                    if not rows:
                        raise ValueError(f"mobility_no_matching_seoul_data:{sample_date}")
                    db.executemany("INSERT INTO mobility_dong_hour VALUES (?,?,?,?,?,?,?)", rows)
                    records.append({"date": sample_date, "file": filename, "sha256": sha.lower(),
                                    "bytes": archive.stat().st_size, "dayKind": day_kind, **counts})
            check_database(db)
        path.replace(output / path.name)
    result = {
        "source": SOURCE, "sourceId": DATASET, "attribution": ATTRIBUTION,
        "nativeGrain": "report day, source admin dong, published hour and direction",
        "metric": "known published estimated moves in the file, scaled from hundredths",
        "scope": "local-review; native dong context; no OD pairs, identity or tile allocation",
        "sourceTermsLocal": "official-source.html", "sourceTermsSha256": digest(output / "official-source.html"),
        "samples": records, "databaseSha256": digest(output / "seoul-mobility-dong.sqlite"),
        "adminCrosswalkCoreSha256": digest(CORE), "integrityCheck": "ok", "foreignKeyViolations": 0,
    }
    atomic_json(output / "verification.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()
    print(json.dumps(build(args.output), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

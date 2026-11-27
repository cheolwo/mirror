"""Build an internal multi-day Seoul living-population tile index from audited daily ZIP files."""

import argparse
from collections import Counter, defaultdict
from contextlib import closing
import csv
from datetime import date, timedelta
from hashlib import file_digest
import io
import json
import math
from pathlib import Path
import sqlite3
from tempfile import NamedTemporaryFile
from zipfile import ZipFile

from collect_public_snapshots import atomic_json

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / "artifacts/local"
PUBLIC = LOCAL / "public-data"
OUTPUT = PUBLIC / "seoul-tile-evidence-20261125/population"
TILE_PACK = PUBLIC / "floating-250m-20261125/raw/hongdal-tiles-gu.before.json"
GRID_DB = PUBLIC / "diorama-public-layer-intake-20261123/r80-grid-life-population/grid-life-local.sqlite"
BANDS = {"lunch": (11, 12, 13), "evening": (17, 18, 19, 20)}
HOURS = {hour for band in BANDS.values() for hour in band}
SAGAJEO = (-32, -179)
FRACTION = 25
EXPECTED_HEADER = ["일자", "시간", "행정동코드", "250M격자", "생활인구합계"]
TILE_SCHEMA = """
PRAGMA foreign_keys = ON;
CREATE TABLE tile (
    q INTEGER, r INTEGER, pack_index INTEGER UNIQUE, station_label TEXT,
    seoul_share_permille INTEGER NOT NULL, resident_estimate INTEGER,
    licensed_restaurants INTEGER, cafe_bakery_licenses INTEGER, PRIMARY KEY(q,r)
);
"""
OBSERVATION_SCHEMA = """
CREATE TABLE grid_tile_weight (
    grid_code TEXT, q INTEGER, r INTEGER, samples INTEGER NOT NULL CHECK(samples BETWEEN 1 AND 25),
    PRIMARY KEY(grid_code,q,r), FOREIGN KEY(q,r) REFERENCES tile(q,r)
);
CREATE TABLE source_day (
    day TEXT PRIMARY KEY, day_kind TEXT NOT NULL, sha256 TEXT NOT NULL, source_path TEXT NOT NULL,
    source_url TEXT NOT NULL, retrieved_at TEXT, acquisition TEXT NOT NULL,
    raw_rows INTEGER NOT NULL, selected_rows INTEGER NOT NULL,
    selected_masked_rows INTEGER NOT NULL, selected_blank_rows INTEGER NOT NULL
);
CREATE TABLE unmapped_source_grid (
    day TEXT REFERENCES source_day(day), grid_code TEXT, raw_rows INTEGER NOT NULL,
    PRIMARY KEY(day,grid_code)
);
CREATE TABLE tile_presence_day (
    day TEXT REFERENCES source_day(day), q INTEGER, r INTEGER, band TEXT,
    known_presence_average REAL, expected_hours INTEGER NOT NULL, observed_hours INTEGER NOT NULL,
    expected_grid_hour_samples INTEGER NOT NULL, numeric_grid_hour_samples INTEGER NOT NULL,
    masked_grid_hour_samples INTEGER NOT NULL, absent_grid_hour_samples INTEGER NOT NULL,
    blank_grid_hour_samples INTEGER NOT NULL,
    PRIMARY KEY(day,q,r,band), FOREIGN KEY(q,r) REFERENCES tile(q,r)
);
CREATE INDEX tile_presence_lookup ON tile_presence_day(q,r,band,day);
CREATE VIEW v_tile_presence_profile AS
SELECT p.q, p.r, d.day_kind, p.band, min(d.day) AS sample_from, max(d.day) AS sample_to,
       count(*) AS sample_days, count(p.known_presence_average) AS usable_days,
       avg(p.known_presence_average) AS known_presence_average,
       min(p.known_presence_average) AS known_daily_min, max(p.known_presence_average) AS known_daily_max,
       sum(p.numeric_grid_hour_samples) AS numeric_grid_hour_samples,
       sum(p.masked_grid_hour_samples) AS masked_grid_hour_samples,
       sum(p.absent_grid_hour_samples) AS absent_grid_hour_samples,
       sum(p.blank_grid_hour_samples) AS blank_grid_hour_samples,
       sum(p.expected_grid_hour_samples) AS expected_grid_hour_samples
FROM tile_presence_day AS p JOIN source_day AS d ON d.day=p.day
GROUP BY p.q,p.r,d.day_kind,p.band;
"""
SCHEMA = TILE_SCHEMA + OBSERVATION_SCHEMA


def local_path(path):
    resolved = path.resolve()
    if not resolved.is_relative_to(LOCAL.resolve()):
        raise ValueError("input_output_must_be_local_artifact")
    return resolved


def digest(path):
    with path.open("rb") as raw:
        return file_digest(raw, "sha256").hexdigest()


def axial(east, north, spacing):
    x, y = east - 1_000_000, north - 2_000_000
    radius = spacing / math.sqrt(3)
    q_frac = (math.sqrt(3) / 3 * x - y / 3) / radius
    r_frac = (2 / 3 * y) / radius
    s_frac = -q_frac - r_frac
    q, r, s = round(q_frac), round(r_frac), round(s_frac)
    dq, dr, ds = abs(q - q_frac), abs(r - r_frac), abs(s - s_frac)
    if dq > dr and dq > ds:
        q = -r - s
    elif dr > ds:
        r = -q - s
    return q, r


def geometry(db, pack_path, grid_path):
    pack = json.loads(pack_path.read_text(encoding="utf-8-sig"))
    spacing = pack["spacing"]
    if spacing != 300 or pack["foods"][8] != "카페·제과":
        raise ValueError("tile_geometry_or_food_categories_changed")
    tiles = {(row["q"], row["r"]): row for row in pack["tiles"]}
    if len(tiles) != len(pack["tiles"]):
        raise ValueError("duplicate_tile_coordinates")
    db.executemany("INSERT INTO tile VALUES (?,?,?,?,?,?,?,?)", (
        (row["q"], row["r"], index, row.get("s"), row["w"], row.get("p"), row.get("n"),
         row["m"][8] if len(row.get("m", ())) > 8 else None)
        for index, row in enumerate(pack["tiles"])
    ))
    mapping = {}
    expected = Counter()
    with closing(sqlite3.connect(grid_path.as_uri() + "?mode=ro", uri=True)) as grid_db:
        for code, xmin, ymin, area in grid_db.execute("SELECT cell_id,xmin,ymin,whole_cell_area_m2 FROM grid"):
            if area != 62_500:
                raise ValueError("unexpected_source_grid_area")
            portions = Counter(axial(xmin + x, ymin + y, spacing)
                               for x in range(25, 250, 50) for y in range(25, 250, 50))
            mapping[code] = {key: weight for key, weight in portions.items() if key in tiles}
            expected.update(mapping[code])
    db.executemany("INSERT INTO grid_tile_weight VALUES (?,?,?,?)", (
        (cell, *key, weight) for cell, weights in mapping.items() for key, weight in weights.items()
    ))
    return tiles, mapping, expected


def day_inputs(receipt):
    rows = receipt["days"]
    if not rows or receipt["sourceId"] != "OA-22784" or receipt.get("failures"):
        raise ValueError("population_source_receipt_incomplete")
    observed = [date.fromisoformat(row["date"]) for row in rows]
    first, last = date.fromisoformat(receipt["requestedStart"]), date.fromisoformat(receipt["requestedEnd"])
    expected = [first + timedelta(days=offset) for offset in range((last - first).days + 1)]
    if observed != expected:
        raise ValueError("missing_or_unordered_observation_dates")
    return zip(observed, rows, strict=True)


def read_day(day, source, mapping):
    path = local_path(ROOT / source["path"])
    day_key = day.strftime("%Y%m%d")
    if path.stat().st_size != source["bytes"] or digest(path) != source["sha256"]:
        raise ValueError("population_raw_hash_mismatch")
    counts = Counter()
    unmapped = Counter()
    observations = {hour: defaultdict(lambda: [0.0, 0, 0, 0]) for hour in HOURS}
    distinct = set()
    with ZipFile(path) as archive:
        if archive.namelist() != [f"250_LOCAL_RESD_{day_key}.csv"] or archive.infolist()[0].file_size > 90_000_000:
            raise ValueError("unexpected_population_archive")
        with archive.open(archive.infolist()[0]) as raw:
            reader = csv.reader(io.TextIOWrapper(raw, encoding=source["encoding"], newline=""))
            if next(reader)[:5] != EXPECTED_HEADER:
                raise ValueError("population_source_schema_changed")
            for row in reader:
                if len(row) < 5:
                    raise ValueError("population_source_row_short")
                day_text, hour_text, district, cell, value = (part.strip() for part in row[:5])
                hour = int(hour_text)
                if day_text != day_key or not 0 <= hour <= 23 or not cell:
                    raise ValueError("population_date_hour_or_grid_invalid")
                counts["all"] += 1
                if cell not in mapping:
                    unmapped[cell] += 1
                    continue
                if hour not in HOURS:
                    continue
                row_key = (hour, district, cell)
                if row_key in distinct:
                    raise ValueError("duplicate_population_grid_hour_district")
                distinct.add(row_key)
                counts["selected"] += 1
                bucket = observations[hour][cell]
                if value in {"*", "[*]"}:
                    bucket[2] += 1
                    counts["masked"] += 1
                elif not value:
                    bucket[3] += 1
                    counts["blank"] += 1
                else:
                    population = float(value)
                    if not math.isfinite(population) or population < 0:
                        raise ValueError("invalid_population_count")
                    bucket[0] += population
                    bucket[1] += 1
    if not counts["all"] or not all(observations.values()):
        raise ValueError("population_hour_data_missing")
    return observations, counts, unmapped


def aggregate_day(observations, mapping, expected, tiles):
    hourly = {}
    for hour, cells in observations.items():
        sums, numeric, masked, found, blank = defaultdict(float), Counter(), Counter(), Counter(), Counter()
        for cell, (part, observed, private, empty) in cells.items():
            for key, weight in mapping[cell].items():
                found[key] += weight
                if observed:
                    sums[key] += part * weight / FRACTION
                    numeric[key] += weight
                if private:
                    masked[key] += weight
                if empty:
                    blank[key] += weight
        hourly[hour] = (sums, numeric, masked, found, blank)
    for band, hours in BANDS.items():
        for key in tiles:
            visible = [hourly[hour][0][key] for hour in hours if key in hourly[hour][0]]
            usable = len(visible) == len(hours)
            expected_samples = expected[key] * len(hours)
            observed, hidden, present, blank = (
                sum(hourly[hour][column][key] for hour in hours) for column in range(1, 5)
            )
            yield (*key, band, round(sum(visible) / len(hours), 6) if usable else None, len(hours),
                   len(visible), expected_samples, observed, hidden, expected_samples - present, blank)


def build(output, receipt_path, pack_path, grid_path):
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with NamedTemporaryFile(dir=output, suffix=".sqlite", delete=False) as handle:
            temporary = Path(handle.name)
        with closing(sqlite3.connect(temporary)) as db:
            db.row_factory = sqlite3.Row
            db.executescript(SCHEMA)
            tiles, mapping, expected = geometry(db, pack_path, grid_path)
            for day, source in day_inputs(receipt):
                observations, counts, unmapped = read_day(day, source, mapping)
                with db:
                    db.execute("INSERT INTO source_day VALUES (?,?,?,?,?,?,?,?,?,?,?)", (
                        str(day), "weekday" if day.weekday() < 5 else "weekend", source["sha256"],
                        source["path"], source["sourceUrl"], source.get("retrievedAt"), source["acquisition"],
                        counts["all"], counts["selected"], counts["masked"], counts["blank"],
                    ))
                    db.executemany("INSERT INTO unmapped_source_grid VALUES (?,?,?)",
                                   ((str(day), cell, count) for cell, count in unmapped.items()))
                    db.executemany("INSERT INTO tile_presence_day VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                                   ((str(day), *row) for row in aggregate_day(observations, mapping, expected, tiles)))
            if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
                raise ValueError("population_sqlite_integrity_failed")
            profiles = [dict(row) for row in db.execute(
                "SELECT * FROM v_tile_presence_profile WHERE q=? AND r=? ORDER BY day_kind,band DESC", SAGAJEO)]
            reference = [dict(row) for row in db.execute("""SELECT day, band, known_presence_average
                FROM tile_presence_day WHERE q=? AND r=? AND day IN ('2026-11-19','2026-11-21')
                ORDER BY day,band DESC""", SAGAJEO)]
            summary = {
                "schema": "hongdal.tile-population-evidence.v1", "sourceUrl": receipt["sourceUrl"],
                "attribution": receipt["attribution"], "distribution": "local-review",
                "firstDay": receipt["requestedStart"], "lastDay": receipt["requestedEnd"],
                "daysByKind": dict(db.execute("SELECT day_kind,count(*) FROM source_day GROUP BY day_kind")),
                "tileCount": len(tiles), "sourceGridCount": len(mapping),
                "gridTileWeightCount": db.execute("SELECT count(*) FROM grid_tile_weight").fetchone()[0],
                "dailyTileBandRows": db.execute("SELECT count(*) FROM tile_presence_day").fetchone()[0],
                "rawRowCount": db.execute("SELECT sum(raw_rows) FROM source_day").fetchone()[0],
                "unmappedRawRows": db.execute("SELECT sum(raw_rows) FROM unmapped_source_grid").fetchone()[0] or 0,
                "unmappedGridCount": db.execute("SELECT count(DISTINCT grid_code) FROM unmapped_source_grid").fetchone()[0],
                "dailyBands": BANDS, "dataUnit": "공개값이 있는 분량의 시각별 내국인 생활인구 추정 평균(명)",
                "sagajeong": {"tile": [*SAGAJEO], "baselineDays": reference, "profiles": profiles},
                "tilePackSha256": digest(pack_path), "gridIndexSha256": digest(grid_path),
                "sourceReceiptSha256": digest(receipt_path),
            }
        temporary.replace(output / "population-tile-index.sqlite")
        atomic_json(output / "population-summary.json", summary)
        return summary
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--receipt", type=Path, default=OUTPUT / "population-source-receipt.json")
    parser.add_argument("--pack", type=Path, default=TILE_PACK)
    parser.add_argument("--grid", type=Path, default=GRID_DB)
    args = parser.parse_args()
    result = build(*(local_path(path) for path in (args.output, args.receipt, args.pack, args.grid)))
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

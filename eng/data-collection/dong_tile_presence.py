"""Allocate public Seoul population components to child slots without changing parent totals."""

import argparse
from contextlib import closing
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT, PARENT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import BANDS, HOURS, OUTPUT as SOURCE_DIR, day_inputs, digest, local_path, read_day

DB = "child-presence.sqlite"
SAMPLE_METHOD = "existing-50m-sample-points-per-parent"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_day (
    date TEXT PRIMARY KEY, day_kind TEXT NOT NULL, sha256 TEXT NOT NULL,
    path TEXT NOT NULL, raw_rows INTEGER NOT NULL, withheld_rows INTEGER NOT NULL,
    unmapped_rows INTEGER NOT NULL
) WITHOUT ROWID;
CREATE TABLE child (
    q INTEGER,r INTEGER,slot INTEGER CHECK(slot BETWEEN 0 AND 6),PRIMARY KEY(q,r,slot)
) WITHOUT ROWID;
CREATE TABLE child_presence_profile (
    q INTEGER,r INTEGER,slot INTEGER,day_kind TEXT,band TEXT,
    first_day TEXT NOT NULL,last_day TEXT NOT NULL,
    parent_usable_days INTEGER NOT NULL,child_with_values_days INTEGER NOT NULL,child_complete_hour_days INTEGER NOT NULL,
    allocated_visible_mean REAL,
    expected_sample_hours INTEGER NOT NULL,numeric_sample_hours INTEGER NOT NULL,
    suppressed_sample_hours INTEGER NOT NULL,no_record_sample_hours INTEGER NOT NULL,blank_sample_hours INTEGER NOT NULL,
    PRIMARY KEY(q,r,slot,day_kind,band), FOREIGN KEY(q,r,slot) REFERENCES child(q,r,slot)
) WITHOUT ROWID;
"""


def projected_hour(np, observations, cells, targets, rows, weights, child_count):
    raw = np.zeros((len(cells), 5), dtype=np.float64)
    for cell, (population, observed, hidden, blank) in observations.items():
        i = cells[cell]
        raw[i] = population, bool(observed), bool(hidden), 1, bool(blank)
    return np.stack([np.bincount(targets, weights=raw[rows, column] * weights, minlength=child_count)
                     for column in range(raw.shape[1])])


def calculate(output, input_path, sample_parents=()):
    import numpy as np

    receipt_path = SOURCE_DIR / "population-source-receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    with closing(read_only(input_path)) as base:
        tile_keys = base.execute("SELECT q,r,slot FROM child_tile ORDER BY 1,2,3").fetchall()
        indices = {key: index for index, key in enumerate(tile_keys)}
        cells = {code: index for index, (code,) in enumerate(
            base.execute("SELECT grid_code FROM source_grid ORDER BY grid_code"))}
        edges = [(cells[code], indices[q, r, slot], samples)
                 for code, q, r, slot, samples in base.execute("""SELECT grid_code,q,r,slot,sample_points
                         FROM grid_child_weight WHERE sample_points>0 ORDER BY grid_code,q,r,slot""")]
    sample_indices = {(q,r): [indices[q,r,slot] for slot in range(7)] for q,r in set(sample_parents)}
    daily_samples = []
    files = list(day_inputs(receipt))
    src_rows, target_rows, weights = (np.array(column, dtype=np.int64) for column in zip(*edges))
    child_count = len(tile_keys)
    expected = np.bincount(target_rows, weights=weights, minlength=child_count)
    if child_count % 7 or not edges or any(slot != index % 7 for index, (_, _, slot) in enumerate(tile_keys)):
        raise ValueError("invalid_child_tile_index")
    recorded = set()
    info = {}
    accum = {}
    day_groups = {}
    daily_shape = (8, child_count)
    for day, source in files:
        kind = "weekday" if day.weekday() < 5 else "weekend"
        day_groups.setdefault(kind, []).append(str(day))
        hours, source_count, unmapped = read_day(day, source, cells)
        info[day] = (str(day), kind, source["sha256"], source["path"], source_count["all"],
                     source_count["masked"], sum(unmapped.values()))
        for hour in HOURS:
            hours[hour] = projected_hour(np, hours[hour], cells, target_rows, src_rows, weights, child_count)
        for band, labels in BANDS.items():
            data = np.stack([hours[hour] for hour in labels])
            parent_visible = (data[:, 1].reshape(len(labels), -1, 7).sum(axis=2) > 0).all(axis=0)
            eligible = np.repeat(parent_visible, 7)
            child_visible = (data[:, 1] > 0)
            group = accum.setdefault((kind, band), np.zeros(daily_shape, dtype=np.float64))
            group += np.stack((
                data[:, 0].sum(axis=0) / (25 * len(labels)),
                eligible,
                child_visible.any(axis=0),
                child_visible.all(axis=0),
                data[:, 1].sum(axis=0),
                data[:, 2].sum(axis=0),
                expected * len(labels) - data[:, 3].sum(axis=0),
                data[:, 4].sum(axis=0),
            )) * eligible
            recorded.add((kind, band))
            for parent, positions in sorted(sample_indices.items()):
                day_is_usable = bool(eligible[positions[0]])
                for index in positions:
                    numeric_hours = int(child_visible[:, index].sum())
                    daily_samples.append({
                        "q": parent[0], "r": parent[1], "slot": tile_keys[index][2], "day": str(day),
                        "dayKind": kind, "band": band, "parentUsable": day_is_usable,
                        "childHoursWithNumericValue": numeric_hours,
                        "allocatedVisibleMean": float(data[:, 0, index].sum() / (25 * len(labels)))
                        if day_is_usable and numeric_hours else None,
                        "expectedSampleHours": int(expected[index] * len(labels)),
                        "numericSampleHours": int(data[:, 1, index].sum()),
                        "suppressedSampleHours": int(data[:, 2, index].sum()),
                        "missingSampleHours": int(expected[index] * len(labels) - data[:, 3, index].sum()),
                        "blankSampleHours": int(data[:, 4, index].sum()),
                    })

    with TemporaryDirectory(dir=output) as temporary:
        path = Path(temporary) / DB
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO child VALUES (?,?,?)", tile_keys)
                db.executemany("INSERT INTO source_day VALUES (?,?,?,?,?,?,?)", info.values())
                for kind, band in sorted(recorded):
                    group = accum[kind, band]
                    minimum, maximum = day_groups[kind][0], day_groups[kind][-1]
                    rows = []
                    for i, (q, r, slot) in enumerate(tile_keys):
                        total, days, with_values, with_hours, observed, masked, missing, blank = group[:, i]
                        estimate = total / days if days and with_values else None
                        rows.append((q, r, slot, kind, band, minimum, maximum, int(days), int(with_values),
                                     int(with_hours), estimate, int(expected[i] * len(BANDS[band]) * days),
                                     int(observed), int(masked), int(missing), int(blank)))
                    db.executemany("INSERT INTO child_presence_profile VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", rows)
            check_database(db)
            verification = verify(db)
        sequence = verify_day_samples(daily_samples) if daily_samples else None
        path.replace(output / DB)
    if sequence is not None:
        atomic_json(output / "presence-daily-examples.json", {
            "geometryRevision": REVISION, "method": SAMPLE_METHOD, "valueKind": "allocated_public_component",
            "referenceBands": BANDS, "distribution": "local-review", "verification": sequence,
            "rows": daily_samples,
        })
        verification["dailyExampleRows"] = len(daily_samples)
        verification.update(sequence)
    summary = {
        "schema": "hongdal.dong-tile-presence.v1", "geometryRevision": REVISION,
        "method": SAMPLE_METHOD, "distribution": "local-review", "valueKind": "allocated_public_component",
        "dayCount": len(files), "daysByKind": {key: len(value) for key, value in day_groups.items()},
        "profileRows": child_count * len(recorded), "databaseSha256": digest(output / DB),
        **verification,
    }
    atomic_json(output / "presence-summary.json", summary)
    return summary


def verify(db):
    with closing(read_only(PARENT)) as source:
        parents = source.execute("""SELECT q,r,day_kind,band,usable_days,known_presence_average
            FROM v_tile_presence_profile ORDER BY 1,2,3,4""")
        children = db.execute("""SELECT q,r,day_kind,band,min(parent_usable_days),sum(allocated_visible_mean),
            max(parent_usable_days) FROM child_presence_profile GROUP BY 1,2,3,4 ORDER BY 1,2,3,4""")
        max_diff = 0.0
        tested = 0
        for parent, child in zip(parents, children, strict=True):
            if parent[:5] != child[:5] or child[4] != child[6]:
                raise ValueError("parent_scope_or_day_denominator_mismatch")
            expected, actual = parent[5], child[5]
            if (expected is None) != (actual is None):
                raise ValueError("missing_population_was_not_preserved")
            if expected is not None:
                max_diff = max(max_diff, abs(expected - actual))
                tested += 1
        if max_diff > 0.000002:
            raise ValueError("child_population_does_not_conserve_parent")
    return {"comparedParentProfiles": tested, "maxParentDifferencePersons": max_diff}


def verify_day_samples(samples):
    from collections import defaultdict

    grouped = defaultdict(list)
    for row in samples:
        key = row["day"], row["q"], row["r"], row["band"]
        grouped[key].append(row["allocatedVisibleMean"])
    differences = []
    with closing(read_only(PARENT)) as source:
        for key, values in grouped.items():
            if len(values) != 7:
                raise ValueError("daily_sample_not_partitioned")
            expected, = source.execute("""SELECT known_presence_average FROM tile_presence_day
                WHERE day=? AND q=? AND r=? AND band=?""", key).fetchone()
            numeric = [value for value in values if value is not None]
            if (expected is None) != (not numeric):
                raise ValueError("daily_sample_scope_or_missingness_mismatch")
            if numeric:
                differences.append(abs(sum(numeric) - expected))
    max_diff = max(differences, default=0.0)
    if max_diff > 0.000002:
        raise ValueError("daily_sample_does_not_conserve_parent")
    return {"comparedDailyParentValues": len(differences), "maxDailyParentDifferencePersons": max_diff}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--sample-parent", type=int, action="append", nargs=2, metavar=("Q", "R"), default=[])
    args = parser.parse_args()
    output = local_path(args.output)
    print(json.dumps(calculate(output, output / DBNAME, map(tuple, args.sample_parent)), indent=2))


if __name__ == "__main__":
    main()

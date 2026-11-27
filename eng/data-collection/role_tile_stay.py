"""Collect Seoul stay-duration reports and join only published components to the tile grid."""

import argparse
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor
from contextlib import closing
import csv
from datetime import date, datetime, timedelta, timezone
from io import TextIOWrapper
import json
from pathlib import Path
import re
import shutil
import sqlite3
from tempfile import TemporaryDirectory
from urllib.parse import urlencode
from urllib.request import Request, urlopen
import zipfile

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT as TILE_OUTPUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import BANDS, PUBLIC, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126/stay"
CORE = TILE_OUTPUT / DBNAME
START = date(2026, 11, 1)
END = date(2026, 11, 21)
FILENAME = re.compile(r"downloadFile\('([^']+)'\);\">(SEOUL_STYTIME_[^<]+_([0-9]{8})\.zip)")
ENDPOINT = "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?&useCache=false"
SOURCE = {"OA-22892": "native", "OA-22893": "foreign_long", "OA-22894": "foreign_short"}
DURATIONS = ("000", "030", "060", "120", "180", "240")
ATTRIBUTION = "서울특별시·KT, 서울시 통신데이터"
DEFINITION = "https://data.seoul.go.kr/dataVisual/seoul/seoulStay.do"
DB = "stay-tile.sqlite"
SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE source_release (
    source_key TEXT, observed_date TEXT, published_file TEXT NOT NULL,relative_path TEXT NOT NULL,
    sha256 TEXT NOT NULL,raw_rows INTEGER NOT NULL,source_url TEXT NOT NULL,
    day_kind TEXT NOT NULL,PRIMARY KEY(source_key,observed_date)
) WITHOUT ROWID;
CREATE TABLE child (q INTEGER,r INTEGER,slot INTEGER,PRIMARY KEY(q,r,slot)) WITHOUT ROWID;
CREATE TABLE observed_sample (
    source_key TEXT,day_kind TEXT,first_day TEXT NOT NULL,last_day TEXT NOT NULL,days INTEGER NOT NULL,
    PRIMARY KEY(source_key,day_kind)
) WITHOUT ROWID;
CREATE TABLE child_stay_profile (
    q INTEGER,r INTEGER,slot INTEGER,source_key TEXT,day_kind TEXT,band TEXT,duration_code TEXT,
    mean_known_allocated_per_start_hour REAL,
    eligible_days INTEGER NOT NULL, numeric_days INTEGER NOT NULL,
    known_weighted_grid_hours INTEGER NOT NULL,masked_or_partial_weighted_grid_hours INTEGER NOT NULL,
    not_listed_weighted_grid_hours INTEGER NOT NULL,raw_sample_days INTEGER NOT NULL,
    PRIMARY KEY(q,r,slot,source_key,day_kind,band,duration_code),
    CHECK((numeric_days=0)=(mean_known_allocated_per_start_hour IS NULL)),
    FOREIGN KEY(q,r,slot) REFERENCES child,FOREIGN KEY(source_key,day_kind) REFERENCES observed_sample
) WITHOUT ROWID;
CREATE VIEW v_child_stay AS
SELECT p.*,s.first_day,s.last_day FROM child_stay_profile p JOIN observed_sample s USING(source_key,day_kind);
"""


def source_url(source):
    return f"https://data.seoul.go.kr/dataList/{source}/F/1/datasetView.do"


def files_in_window():
    for days in range((END - START).days + 1):
        yield START + timedelta(days=days)


def published_files(source, output):
    ref = source_url(source)
    request = Request(ref, headers={"User-Agent": "Mozilla/5.0"})
    with urlopen(request, timeout=30) as response:
        raw = response.read(6_000_000)
    catalog = output / f"{source}-official-source.html"
    catalog.write_bytes(raw)
    matches = {}
    for seq, name, observation in FILENAME.findall(raw.decode("utf-8")):
        sample = datetime.strptime(observation, "%Y%m%d").date()
        if START <= sample <= END:
            matches[sample] = (seq, name)
    if not matches:
        raise ValueError(f"no_official_stay_daily_attachments:{source}")
    return matches


def retrieve(output, source, day, attachment, previous):
    seq, name = attachment
    local = output / "raw" / f"{source}-{day}.zip"
    reused = local.is_file() and previous is not None and digest(local) == previous["sha256"]
    if not reused:
        ref = source_url(source)
        post = urlencode({"infId": source, "infSeq": "1", "seq": seq, "seqNo": ""}).encode()
        request = Request(ENDPOINT, data=post, headers={"User-Agent": "Mozilla/5.0", "Referer": ref})
        with TemporaryDirectory(dir=local.parent) as temporary:
            path = Path(temporary) / local.name
            with urlopen(request, timeout=90) as response:
                disposition = response.headers.get("Content-Disposition", "")
                if response.status != 200 or name not in disposition:
                    raise ValueError(f"stay_download_unexpected_attachment:{source}:{day}")
                with path.open("wb") as stream:
                    shutil.copyfileobj(response, stream)
            expected_member = name[:-4] + ".csv"
            with zipfile.ZipFile(path) as zipped:
                if zipped.namelist() != [expected_member] or zipped.getinfo(expected_member).file_size > 200_000_000:
                    raise ValueError(f"stay_archive_invalid:{source}:{day}")
            path.replace(local)
    return {"sourceId": source, "observedDate": str(day), "attachment": name,
            "sourceUrl": source_url(source), "relativePath": f"raw/{local.name}",
            "downloadedAtUtc": previous["downloadedAtUtc"] if reused else datetime.now(timezone.utc).isoformat(),
            "bytes": local.stat().st_size, "sha256": digest(local)}


def collect(output):
    (output / "raw").mkdir(parents=True, exist_ok=True)
    receipt = output / "source-receipt.json"
    previous = {}
    if receipt.is_file():
        body = json.loads(receipt.read_text(encoding="utf-8"))
        previous = {(item["sourceId"], item["observedDate"]): item for item in body["files"]}
    catalogs = {source: published_files(source, output) for source in SOURCE}
    tasks = [(source, day, catalogs[source][day]) for day in files_in_window()
             for source in SOURCE if day in catalogs[source]]
    files, failed = [], []
    with ThreadPoolExecutor(max_workers=2) as executor:
        futures = [(source, day, executor.submit(retrieve, output, source, day, attachment,
                    previous.get((source, str(day))))) for source, day, attachment in tasks]
        for source, day, future in futures:
            try:
                files.append(future.result())
            except (OSError, ValueError, zipfile.BadZipFile) as error:
                failed.append({"sourceId": source, "observedDate": str(day), "errorType": type(error).__name__})
    body = {
        "requestedDates": [str(START), str(END)], "retrievedUtc": datetime.now(timezone.utc).isoformat(),
        "attribution": ATTRIBUTION, "definitionUrl": DEFINITION, "distribution": "local-review",
        "sourceTerms": {source: {"localHtml": f"{source}-official-source.html",
                                 "sha256": digest(output / f"{source}-official-source.html")}
                        for source in SOURCE},
        "notPublished": [{"sourceId": source, "observedDate": str(day)} for source in SOURCE
                         for day in files_in_window() if day not in catalogs[source]],
        "failedDownloads": failed, "files": files,
    }
    atomic_json(receipt, body)
    return body


def source_duration_rows(path, entry, center_by_grid, observed_hours):
    """Use only the published total or published subgroup components. Keep mask counts separately."""
    counts = Counter()
    slots = defaultdict(lambda: [0.0, 0, 0])
    checked_coordinates = {}
    blocked = set()
    seen_native_keys = set()
    seen_foreign_keys = set()
    native = entry["sourceId"] == "OA-22892"
    position = 7 if native else 9
    expected_date = entry["observedDate"].replace("-", "")
    member = entry["attachment"][:-4] + ".csv"
    with zipfile.ZipFile(path) as zipped, zipped.open(member) as archive:
        first = archive.peek(3)[:3]
        if first == b"\xef\xbb\xbf":
            archive.read(3)
        elif first[:2] == b'?"':
            archive.read(1)
        with TextIOWrapper(archive, encoding="cp949", newline="") as stream:
            rows = csv.reader(stream)
            header = next(rows)
            if not all(piece in label for piece, label in zip(("기준", "행정동", "격자"), header[:3])) or (
                not "인구" in header[position] or len(header) != (24 if native else 10)
            ):
                raise ValueError(f"stay_unexpected_columns:{path.name}")
            for row in rows:
                counts["rows"] += 1
                if len(row) != len(header) or row[0] != expected_date:
                    raise ValueError(f"stay_date_or_row_shape:{path.name}")
                if row[5] not in observed_hours:
                    continue
                counts["target_hour_rows"] += 1
                if row[6] not in DURATIONS:
                    raise ValueError(f"stay_unexpected_duration_code:{path.name}")
                grid = row[2]
                if grid not in center_by_grid:
                    counts["target_unknown_grid_rows"] += 1
                    continue
                coordinate = row[3], row[4]
                if checked_coordinates.get(grid) != coordinate:
                    if (int(row[3]), int(row[4])) != center_by_grid[grid]:
                        blocked.add(grid)
                    checked_coordinates[grid] = coordinate
                key = row[5], row[6], grid
                values = slots[key]
                if native and (values[1] or values[2]):
                    counts["native_additional_admin_components"] += 1
                provider_key = (*key, row[1])
                if not native:
                    provider_key += (row[7], row[8])
                source_keys = seen_native_keys if native else seen_foreign_keys
                if provider_key in source_keys:
                    blocked.add(grid)
                    counts["duplicate_full_provider_keys"] += 1
                source_keys.add(provider_key)
                if row[position].strip() in {"*", ""}:
                    values[2] += 1
                    counts["hidden_or_blank_rows"] += 1
                else:
                    measured = float(row[position])
                    if not 0 <= measured < 1_000_000:
                        raise ValueError(f"stay_invalid_count:{path.name}")
                    values[0] += measured
                    values[1] += 1
                    counts["numeric_rows"] += 1
    counts["quarantined_grids"] = len(blocked)
    return ({key: value for key, value in slots.items() if key[2] not in blocked}, counts)


def grid_inputs(core, np):
    with closing(read_only(core)) as db:
        tiles = db.execute("SELECT q,r,slot FROM child_tile ORDER BY 1,2,3").fetchall()
        grids = dict((code, (int(x), int(y))) for code, x, y in db.execute(
            "SELECT grid_code,(xmin+xmax)/2,(ymin+ymax)/2 FROM source_grid ORDER BY grid_code"))
        tile_indices = {key: index for index, key in enumerate(tiles)}
        grid_indices = {key: index for index, key in enumerate(grids)}
        edges = [(grid_indices[grid], tile_indices[(q, r, slot)], samples)
                 for grid, q, r, slot, samples in db.execute("""SELECT grid_code,q,r,slot,sample_points
                     FROM grid_child_weight WHERE sample_points>0 ORDER BY 1,2,3,4""")]
    if len(tiles) % 7 or any(slot != i % 7 for i, (_, _, slot) in enumerate(tiles)):
        raise ValueError("stay_geometry_cannot_group_parent_slots")
    grid, tile, weights = (np.array(column, dtype=np.int64) for column in zip(*edges))
    if np.bincount(grid, weights=weights).max() > 25:
        raise ValueError("stay_source_mass_exceeds_50m_samples")
    return tiles, grids, grid_indices, grid, tile, weights


def allocate_day(np, raw, grid_indices, grid, tiles, weights, tile_count, expected, output):
    for band, times in BANDS.items():
        for duration in DURATIONS:
            day = np.zeros((5, tile_count), dtype=np.float64)
            parent_support = np.ones(tile_count // 7, dtype=np.bool_)
            for hour in times:
                components = np.zeros((len(grid_indices), 4), dtype=np.float64)
                hour_code = f"{hour:02d}"
                for code, index in grid_indices.items():
                    result = raw.get((hour_code, duration, code))
                    if result is not None:
                        value, known, hidden = result
                        components[index] = value, bool(known), bool(hidden), 1
                projected = np.stack([np.bincount(tiles, weights=components[grid, i] * weights,
                                                  minlength=tile_count)
                                      for i in range(4)])
                if abs(projected[0].sum() - (components[grid, 0] * weights).sum()) > 1e-5:
                    raise ValueError("stay_projection_did_not_conserve_sample_mass")
                parent_support &= (projected[3].reshape(-1, 7).sum(axis=1) > 0)
                day[:4] += projected
                day[4] += projected[1] > 0
            valid = np.repeat(parent_support, 7)
            have_value = (day[4] > 0) & valid
            ledger = output.setdefault((band, duration), np.zeros((6, tile_count), dtype=np.float64))
            ledger += np.stack((day[0] * have_value / (25 * len(times)), valid, have_value,
                                day[1], day[2], expected * len(times) - day[3]))


def profile_rows(tiles, source, kind, profiles, days):
    for (band, duration), values in sorted(profiles.items()):
        for index in range(len(tiles)):
            total, eligible, available, observed, masked, missing = values[:, index]
            if not (observed or masked):
                continue
            yield (*tiles[index], source, kind, band, duration,
                   float(total / available) if available else None, int(eligible), int(available),
                   int(observed), int(masked), int(missing), days)


def build(output):
    import numpy as np

    receipt = json.loads((output / "source-receipt.json").read_text(encoding="utf-8"))
    tiles, grids, indices, grid, target, weights = grid_inputs(CORE, np)
    expected = np.bincount(target, weights=weights, minlength=len(tiles))
    hours = {f"{hour:02d}" for times in BANDS.values() for hour in times}
    checks = []
    with TemporaryDirectory(dir=output) as tmp:
        path = Path(tmp) / DB
        with closing(sqlite3.connect(path)) as db:
            db.executescript(SCHEMA)
            with db:
                db.executemany("INSERT INTO child VALUES (?,?,?)", tiles)
                groups = defaultdict(list)
                for entry in receipt["files"]:
                    day = date.fromisoformat(entry["observedDate"])
                    kind = "weekday" if day.weekday() < 5 else "weekend"
                    groups[entry["sourceId"], kind].append(entry)
                for (source, kind), entries in sorted(groups.items()):
                    entries.sort(key=lambda e: e["observedDate"])
                    days = len(entries)
                    db.execute("INSERT INTO observed_sample VALUES (?,?,?,?,?)",
                               (source, kind, entries[0]["observedDate"], entries[-1]["observedDate"], days))
                    out = {}
                    for item in entries:
                        file = output / item["relativePath"]
                        if digest(file) != item["sha256"]:
                            raise ValueError(f"stay_source_fingerprint_mismatch:{file.name}")
                        raw, counts = source_duration_rows(file, item, grids, hours)
                        db.execute("INSERT INTO source_release VALUES (?,?,?,?,?,?,?,?)", (
                            source, item["observedDate"], item["attachment"], item["relativePath"],
                            item["sha256"], counts["rows"], item["sourceUrl"], kind,
                        ))
                        checks.append({"sourceId": source, "date": item["observedDate"], **counts})
                        allocate_day(np, raw, indices, grid, target, weights, len(tiles), expected, out)
                    db.executemany("INSERT INTO child_stay_profile VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                                   profile_rows(tiles, source, kind, out, days))
            check_database(db)
            sample = db.execute("""SELECT source_key,day_kind,band,duration_code,
                mean_known_allocated_per_start_hour,eligible_days,numeric_days,masked_or_partial_weighted_grid_hours
                FROM child_stay_profile WHERE q=-32 AND r=-179 AND slot=2
                ORDER BY 1,2,3,4""").fetchall()
            counts = db.execute("""SELECT COUNT(*),COUNT(mean_known_allocated_per_start_hour),
                    COUNT(DISTINCT printf('%d/%d/%d',q,r,slot)) FROM child_stay_profile""").fetchone()
        path.replace(output / DB)
    atomic_json(output / "verification.json", {
        "geometryRevision": REVISION, "definition": DEFINITION, "attribution": ATTRIBUTION,
        "datesRequested": receipt["requestedDates"], "notPublished": receipt["notPublished"],
        "archiveCount": len(checks), "files": checks, "profileRows": counts[0],
        "profileNumericRows": counts[1], "childTilesWithReportedComponent": counts[2],
        "sagajeongSlot2": sample, "databaseSha256": digest(output / DB),
        "sourceCoreSha256": digest(CORE), "integrityCheck": "ok", "foreignKeyViolations": 0,
    })


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--collect", action="store_true")
    parser.add_argument("--build", action="store_true")
    args = parser.parse_args()
    if args.collect:
        body = collect(args.output)
        print(json.dumps({key: len(body[key]) for key in ("files", "notPublished", "failedDownloads")}))
    if args.build:
        build(args.output)


if __name__ == "__main__":
    main()

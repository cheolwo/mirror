"""Compare repeatable boundary allocation assumptions on the official stay sample."""

import argparse
from collections import Counter, defaultdict
from contextlib import closing
import csv
from datetime import date
from io import TextIOWrapper
import json
from pathlib import Path
from statistics import mean
import zipfile

from collect_public_snapshots import atomic_json
from dong_tile_facilities import official_seoul
from dong_tile_geometry import REVISION, point_key
from role_tile_stay import ATTRIBUTION, CORE, OUTPUT
from seoul_area_geography import gis, read_only
from seoul_tile_population import BANDS, digest


PARENTS = {(-32, -179): "사가정역", (-75, -189): "홍대입구", (-68, -204): "여의도", (-28, -183): "용마산"}
TIME_TO_BAND = {f"{hour:02d}": band for band, hours in BANDS.items() for hour in hours}


def grid_sample_links():
    pyproj, shapely = gis()
    labels, tree = official_seoul(shapely)
    convert = pyproj.Transformer.from_crs(5179, 4326, always_xy=True).transform
    grids = {}
    with closing(read_only(CORE)) as db:
        for q, r in PARENTS:
            candidates = db.execute("""SELECT DISTINCT g.grid_code,g.xmin,g.ymin
                 FROM grid_child_weight w JOIN source_grid g USING(grid_code)
                 WHERE w.q=? AND w.r=? AND w.sample_points>0""", (q, r))
            grids.update((key, (x, y)) for key, x, y in candidates)
    links = {}
    for code, (west, south) in sorted(grids.items()):
        aggregate = Counter()
        by_admin = defaultdict(Counter)
        for x in range(25, 250, 50):
            for y in range(25, 250, 50):
                east, north = west + x, south + y
                key = point_key(east, north)
                aggregate[key] += 1
                candidates = tree.query(shapely.Point(*convert(east, north)), predicate="within")
                if len(candidates) == 1:
                    admin = labels[int(candidates[0])][0]
                    by_admin[admin][key] += 1
        if sum(aggregate.values()) != 25:
            raise ValueError("unexpected_number_of_source_grid_samples")
        links[code] = ((int(west + 125), int(south + 125)), aggregate, by_admin)
    return links


def allocate_archive(path, entry, links):
    if digest(path) != entry["sha256"]:
        raise ValueError("boundary_check_source_changed")
    date_expected = entry["observedDate"].replace("-", "")
    factors = {}
    audit = Counter()
    recorded = set()
    member = entry["attachment"][:-4] + ".csv"
    with zipfile.ZipFile(path) as archive, archive.open(member) as bytes_stream:
        first = bytes_stream.peek(3)[:3]
        if first == b"\xef\xbb\xbf":
            bytes_stream.read(3)
        elif first[:2] == b'?"':
            bytes_stream.read(1)
        with TextIOWrapper(bytes_stream, encoding="cp949", newline="") as stream:
            source = csv.reader(stream)
            next(source)
            for row in source:
                if row[2] not in links or row[5] not in TIME_TO_BAND or row[6] != "060":
                    continue
                if row[0] != date_expected or len(row[1]) != 8:
                    raise ValueError("boundary_check_source_grain_changed")
                grid, hour, admin = row[2], row[5], row[1] + "00"
                if (grid, hour, admin) in recorded:
                    raise ValueError("boundary_check_duplicate_full_source_id")
                recorded.add((grid, hour, admin))
                audit["selectedSourceRows"] += 1
                published = row[7]
                if published in {"*", ""}:
                    audit["maskedOrBlankSourceRows"] += 1
                    continue
                center, legacy, delimited = links[grid]
                if (int(row[3]), int(row[4])) != center:
                    raise ValueError("boundary_check_grid_center_mismatch")
                amount = float(published)
                admin_weights = delimited.get(admin)
                if not admin_weights:
                    audit["numericRowsWithoutInsideAdminSample"] += 1
                divisor = sum(admin_weights.values()) if admin_weights else None
                audit["numericSourceRows"] += 1
                for key, weight in legacy.items():
                    if key[:2] not in PARENTS:
                        continue
                    band = TIME_TO_BAND[hour]
                    record = factors.setdefault((*key, band), [0.0, 0.0, 0, 0])
                    record[0] += amount * weight / 25
                    record[2] += 1
                if admin_weights:
                    for key, weight in admin_weights.items():
                        if key[:2] not in PARENTS:
                            continue
                        band = TIME_TO_BAND[hour]
                        record = factors.setdefault((*key, band), [0.0, 0.0, 0, 0])
                        record[1] += amount * weight / divisor
                        record[3] += 1
    return factors, audit


def build(output):
    proof = json.loads((output / "source-receipt.json").read_text(encoding="utf-8"))
    archives = [row for row in proof["files"] if row["sourceId"] == "OA-22892"]
    report = defaultdict(list)
    totals = Counter()
    daily = []
    links = grid_sample_links()
    for row in archives:
        day = date.fromisoformat(row["observedDate"])
        weekday = "weekday" if day.weekday() < 5 else "weekend"
        values, audit = allocate_archive(output / row["relativePath"], row, links)
        totals.update(audit)
        for (q, r, slot, band), evidence in sorted(values.items()):
            common, constrained, common_n, constrained_n = evidence
            length = len(BANDS[band])
            pair = {
                "q": q, "r": r, "slot": slot, "band": band, "date": str(day), "kind": weekday,
                "existingGridSample": common / length if common_n else None,
                "testedAdminSample": constrained / length if constrained_n else None,
            }
            daily.append(pair)
            if common_n and constrained_n:
                report[q, r, slot, band, weekday].append((common / length, constrained / length))
    comparable = []
    for (q, r, slot, band, kind), rows in sorted(report.items()):
        before = mean(row[0] for row in rows)
        after = mean(row[1] for row in rows)
        comparable.append({"q": q, "r": r, "slot": slot, "band": band, "kind": kind,
                           "sameDaysWithBoth": len(rows), "existingGridSampleMean": before,
                           "testedAdminSampleMean": after, "differenceOnSample": after - before})
    result = {"case": "OA-22892 공개 내국인 60–119분 시작 성분과 공간 배분 지점 민감도",
              "geometryRevision": REVISION, "sourceAttribution": ATTRIBUTION,
              "sourceReceiptSha256": digest(output / "source-receipt.json"),
              "meanings": {"existingGridSample": "격자 25개 지점에 공개 수의 1/25을 배분",
                  "testedAdminSample": "같은 격자의 원 공표 행정동 안에 드는 지점에만 공개 수를 배분하는 검토법"},
              "status": "local_review_only_observation", "productionLayerChanged": False,
              "perSourceDay": [row["observedDate"] for row in archives], "gridCount": len(links),
              "counts": dict(totals), "dailyCases": daily, "matchedSampleMeans": comparable}
    atomic_json(output / "administrative-boundary-sensitivity.json", result)
    return len(daily), len(comparable), dict(totals)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    print(json.dumps(build(parser.parse_args().output), ensure_ascii=False))

"""Compare two ways of distributing the same observed parent total for a data review."""

from collections import defaultdict
from contextlib import closing
import json
from math import fsum
from statistics import median

from collect_public_snapshots import atomic_json
from dong_tile_bundle import CASES
from dong_tile_geometry import REVISION
from dong_tile_index import DBNAME, OUTPUT
from seoul_area_geography import read_only
from seoul_tile_population import BANDS, OUTPUT as SOURCE_DIR, day_inputs, read_day


def reweight(core):
    edges = defaultdict(list)
    grids = {r[0] for r in core.execute("SELECT grid_code FROM source_grid")}
    for grid, q, r, slot, area, points in core.execute("""SELECT grid_code,q,r,slot,overlap_m2,sample_points
            FROM grid_child_weight ORDER BY grid_code,q,r,slot"""):
        edges[grid,q,r].append((slot,area,points))
    counts = defaultdict(int)
    variations = []
    case_keys = {(q,r) for q,r,_ in CASES.values()}
    case_edges = defaultdict(list)
    for (grid,q,r), rows in sorted(edges.items()):
        geometric_support = fsum(row[1] for row in rows)
        sample_support = sum(row[2] for row in rows)
        if not sample_support or geometric_support <= 0:
            counts["geometry_without_sample" if not sample_support else "sample_without_geometry"] += 1
            continue
        counts["comparable_grid_parent"] += 1
        samples = {slot: points/25 for slot,_,points in rows}
        weights = {slot: (sample_support/25)*area/geometric_support for slot,area,_ in rows}
        if abs(fsum(samples.values()) - fsum(weights.values())) > 1e-12:
            raise ValueError("same_parent_weight_not_conserved")
        if (q,r) in case_keys:
            case_edges[q,r].append((grid,samples,weights))
        variations.append(0.5*fsum(abs(samples[s]-weights[s]) for s in samples)/(sample_support/25))
    if set(case_edges) != case_keys:
        raise ValueError("case_without_grid_parent_support")
    source_summary = {
        "gridParentCounts": dict(counts),
        "medianDifferentShareInSameParent": median(variations),
        "maxDifferentShareInSameParent": max(variations),
        "countOverQuarterShift": sum(value > 0.25 for value in variations),
    }
    return source_summary, case_edges, grids


def hour_pair(cells, edges):
    left, right = defaultdict(list), defaultdict(list)
    for grid, sample_weights, area_weights in edges:
        value, valid_rows, _, _ = cells.get(grid, (0,0,0,0))
        if valid_rows:
            for slot, weight in sample_weights.items():
                if weight > 0:
                    left[slot].append(value*weight)
            for slot, weight in area_weights.items():
                if weight > 0:
                    right[slot].append(value*weight)
    return left, right


def values(hours, edges):
    results = [hour_pair(hour, edges) for hour in hours]
    if not all(left for left, _ in results):
        return None, None
    averages = []
    for method in (0,1):
        components = [[] for _ in range(7)]
        for hour in results:
            for slot, group in hour[method].items():
                components[slot].extend(group)
        averages.append([fsum(slot)/len(hours) if slot else None for slot in components])
    return tuple(averages)


def build():
    with closing(read_only(OUTPUT / DBNAME)) as core:
        summary, edges, grids = reweight(core)
    if summary["gridParentCounts"].get("sample_without_geometry", 0):
        raise ValueError("sample_has_no_geometric_support_use_separate_review")
    receipt = json.loads((SOURCE_DIR/"population-source-receipt.json").read_text(encoding="utf-8"))
    observations_by_case = {name: [] for name in CASES}
    max_diff = 0.0
    for day, source in day_inputs(receipt):
        rows, _, _ = read_day(day, source, grids)
        for name,(q,r,slot) in CASES.items():
            for band, labels in BANDS.items():
                sample, area = values([rows[i] for i in labels], edges[q,r])
                if sample is not None:
                    s = fsum(value for value in sample if value is not None)
                    a = fsum(value for value in area if value is not None)
                    max_diff = max(max_diff, abs(s-a))
                observations_by_case[name].append({"day":str(day), "band":band,
                    "parentHasNumericSampleForAllHours": sample is not None,
                    "currentSampleValue": sample[slot] if sample is not None else None,
                    "areaSensitivityValue": area[slot] if area is not None else None})
    if max_diff > 1e-8:
        raise ValueError("same_parent_people_not_conserved")
    case_stats = {}
    for name, rows in observations_by_case.items():
        numeric = [abs(row["currentSampleValue"]-row["areaSensitivityValue"]) for row in rows
                   if row["currentSampleValue"] is not None and row["areaSensitivityValue"] is not None]
        case_stats[name] = {"tileKey": CASES[name], "daysAndBands": len(rows), "bothValues": len(numeric),
                            "medianAbsoluteChangePersons": median(numeric) if numeric else None,
                            "maxAbsoluteChangePersons": max(numeric, default=None)}
    result = {
        "geometryRevision": REVISION, "status": "review-only", "distribution": "local-review",
        "method": "same-parent-area-weighted-redistribution-of-same-known-grid-components",
        "original": "existing-50m-sample-points-per-parent",
        **summary, "caseStatistics": case_stats, "maxParentDifferencePersons": max_diff,
    }
    atomic_json(OUTPUT / "presence-weight-sensitivity.json", {**result, "caseRows": observations_by_case})
    return result


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))

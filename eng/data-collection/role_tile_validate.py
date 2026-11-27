"""Independently check the handoff's files, roles, date joins and missing-data examples."""

import argparse
from collections import defaultdict
from contextlib import closing
import json
from pathlib import Path
from statistics import mean

from collect_public_snapshots import atomic_json
from dong_tile_bundle import query
from dong_tile_geometry import REVISION
from dong_tile_index import OUTPUT as TILE_OUTPUT
from role_tile_catalog import CATALOG_DB, OUTPUT, SOURCES
from role_tile_coverage import DBNAME_OUT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import ROOT, digest


def matched_presence_and_stay(output):
    path = TILE_OUTPUT / "presence-daily-examples.json"
    daily = json.loads(path.read_text(encoding="utf-8"))
    if daily["geometryRevision"] != REVISION:
        raise ValueError("daily_presence_geometry_mismatch")
    file, table, _sid, _receipt = SOURCES["stay"]
    dates = {row["observed_date"] for row in query(file, "SELECT observed_date FROM source_release "
                                                      "WHERE source_key='OA-22892'")}
    indexed = defaultdict(list)
    for row in daily["rows"]:
        if row["day"] in dates:
            indexed[row["q"], row["r"], row["slot"], row["dayKind"], row["band"]].append(row)
    concordance = []
    for key, records in sorted(indexed.items()):
        stay = query(file, f"""SELECT mean_known_allocated_per_start_hour,numeric_days,
            masked_or_partial_weighted_grid_hours,raw_sample_days
            FROM {table} WHERE (q,r,slot,day_kind,band)=(?,?,?,?,?)
            AND source_key='OA-22892' AND duration_code='060'""", key)
        numeric = [row["allocatedVisibleMean"] for row in records if row["allocatedVisibleMean"] is not None]
        if stay and stay[0]["raw_sample_days"] != len(records):
            raise ValueError(f"stay_profile_wrong_sample_count:{key}")
        concordance.append({"q": key[0], "r": key[1], "slot": key[2], "dayKind": key[3], "band": key[4],
            "sharedSourceDates": sorted(row["day"] for row in records),
            "simultaneousPresence": {"publicValue": mean(numeric) if numeric else None,
                                     "numericDays": len(numeric), "sourceId": "OA-22784"},
            "startedStay60To119Minutes": stay[0] if stay else None})
    atomic_json(output / "presence-stay-matched-dates.json", {
        "geometryRevision": REVISION, "sampleParents": 4, "caseRows": len(concordance),
        "sharedPublishedDays": sorted(dates), "presenceDailyEvidenceSha256": digest(path),
        "measureRelationship": "same source dates and grid keys, different population measures",
        "rows": concordance,
    })
    return len(concordance)


def evidence_fingerprints(catalog):
    proof = []
    for relative, expected in catalog.execute("SELECT path,sha256 FROM artifact ORDER BY path"):
        path = ROOT / relative
        if digest(path) != expected:
            raise ValueError(f"role_input_moved_or_changed:{relative}")
        with closing(read_only(path)) as source:
            check_database(source)
        proof.append({"path": relative, "sha256": expected, "integrity": "ok", "foreignKeyViolations": 0})
    return proof


def check_cases(output, num_roles):
    example_dir = output / "role-cases"
    index = json.loads((example_dir / "index.json").read_text(encoding="utf-8"))
    artifact_hashes = []
    for reference in index:
        path = example_dir / reference["file"]
        case = json.loads(path.read_text(encoding="utf-8"))
        if case["tileKey"] != reference["key"] or case["geometryRevision"] != REVISION:
            raise ValueError(f"role_case_key_mismatch:{path.name}")
        for entry in case["roles"].values():
            if len(entry["layers"]) != len({field["id"] for field in entry["layers"]}):
                raise ValueError(f"duplicate_field_for_role:{path.name}")
            for field in entry["layers"]:
                if field["phase"] == "action" and field["rows"] is not None:
                    raise ValueError(f"work_authority_in_public_example:{path.name}")
                if case["geoState"] == "outside_seoul" and field["rows"] is not None:
                    raise ValueError(f"seoul_values_inserted_outside_seoul:{path.name}")
                if field.get("nativeScope") == "법정동":
                    keys = {row["referenceKey"] for row in field["rows"]}
                    if len(keys) != len(field["rows"]) or not keys.issuperset(
                        key for region in field["regionContext"] for key in region["referenceKeys"]
                    ):
                        raise ValueError(f"shared_legal_observation_not_unique:{path.name}")
        if path.name.startswith("sagajeong") and len(case["roles"]) != num_roles:
            raise ValueError("same_child_case_requires_each_runtime_stance")
        if path.name == "rider-overlapping-historical-hotspots.json":
            overlay = next(x for x in case["roles"]["rider"]["layers"] if x["id"] == "motorcycleHotspot")
            if len({a["source_fid"] for a in overlay["sourceAreas"]}) < 2:
                raise ValueError("official_overlapping_areas_proof_missing")
        if path.name == "seoul-shared-curb-parking.json":
            parking = next(x for x in case["roles"]["cargo"]["layers"] if x["id"] == "seoulFreightParking")
            if not parking["publishedSchedules"]:
                raise ValueError("parking_weekday_source_schedule_not_resolved")
        artifact_hashes.append({"file": reference["file"], "sha256": digest(path)})
    return artifact_hashes


def validate(output):
    with closing(read_only(output / CATALOG_DB)) as catalog, closing(read_only(output / DBNAME_OUT)) as coverage:
        check_database(catalog)
        check_database(coverage)
        sources = evidence_fingerprints(catalog)
        total_roles = catalog.execute("SELECT COUNT(*) FROM role").fetchone()[0]
        total_dongs = coverage.execute("SELECT COUNT(*) FROM area").fetchone()[0]
        actual_relations = coverage.execute("SELECT COUNT(*) FROM v_role_dong_spatial_coverage").fetchone()[0]
        if actual_relations != total_roles * total_dongs:
            raise ValueError("role_coverage_area_relation_incomplete")
    cases = check_cases(output, total_roles)
    matched = matched_presence_and_stay(output)
    result = {"geometryRevision": REVISION, "roles": total_roles, "seoulDongs": total_dongs,
              "coverageRelations": actual_relations, "matchedPresenceStayCaseRows": matched,
              "inputDatabases": sources, "caseFiles": cases,
              "roleCatalogSha256": digest(output / CATALOG_DB), "coverageSha256": digest(output / DBNAME_OUT),
              "evidence": "read-only local database and case replay; separate from game execution"}
    atomic_json(output / "independent-readback.json", result)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    result = validate(parser.parse_args().output)
    print(json.dumps({key: len(value) if isinstance(value, list) else value for key, value in result.items()},
                     ensure_ascii=False))

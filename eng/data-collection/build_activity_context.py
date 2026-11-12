"""Build offline legal-dong/road references to area population; never allocate it."""
from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path
import re
import zipfile


ROOT = Path(__file__).resolve().parents[2]
JURISDICTION = Path("Ssalddel/.local-private-storage/external-data/raw/mois-resident-registration-codes/korea-administrative-legal-jurisdictions/2026-08-12/4a10e6ee2ec94f4c93638b7c0bd8ebe4.zip")
WALK = Path("artifacts/local/public-data/admin-dong-walk-network-northeast-seoul-20260915-g3c-r1/raw/jungnang.csv")
WALK_RECEIPT = WALK.parent / "receipt.json"
SOURCES = ("seoul-trading-population", "seoul-administrative-population", "seoul-trading-areas", "seoul-administrative-areas")
EXPECTED_DATASETS = dict(zip(SOURCES, ("OA-15568", "OA-22178", "OA-15560", "OA-22160")))


class ContextError(ValueError):
    pass


def digest(body):
    return hashlib.sha256(body).hexdigest()


def checked_body(path, expected):
    body = path.read_bytes()
    if not isinstance(expected, str) or digest(body).lower() != expected.lower():
        raise ContextError("source_sha256_mismatch")
    return body


def read_snapshot(directory):
    pointer = json.loads((directory / "latest.json").read_text(encoding="utf-8-sig"))
    if directory.name not in EXPECTED_DATASETS:
        raise ContextError("snapshot_source_unknown")
    if (pointer.get("id") != directory.name or pointer.get("sourceId") != "seoul-open-data"
            or pointer.get("datasetId") != EXPECTED_DATASETS[directory.name]):
        raise ContextError("snapshot_source_identity_mismatch")
    relative = pointer.get("body")
    if not isinstance(relative, str) or Path(relative).is_absolute():
        raise ContextError("snapshot_body_path_invalid")
    path = (directory / relative).resolve()
    if not path.is_relative_to(directory.resolve()):
        raise ContextError("snapshot_body_path_invalid")
    body = checked_body(path, pointer.get("sha256"))
    encoding = pointer.get("encoding", "cp949")
    if encoding not in {"cp949", "utf-8", "utf-8-sig"}:
        raise ContextError("snapshot_encoding_invalid")
    rows = list(csv.DictReader(io.StringIO(body.decode(encoding), newline="")))
    if len(rows) != pointer.get("recordCount"):
        raise ContextError("snapshot_row_count_mismatch")
    ref = {k: pointer.get(k) for k in ("id", "sourceId", "datasetId", "sha256", "collectedAt", "evidenceAsOf", "unit", "spatialUnit", "recordCount")}
    ref["path"] = str(path)
    return rows, ref


def read_jurisdictions(path):
    raw = path.read_bytes()
    if len(raw) > 10 * 1024 * 1024:
        raise ContextError("jurisdiction_archive_too_large")
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        names = [n for n in archive.namelist() if re.fullmatch(r"(?:.*/)?KIKmix\.\d{8}", n)]
        if len(names) != 1 or archive.getinfo(names[0]).file_size > 20 * 1024 * 1024:
            raise ContextError("jurisdiction_entry_invalid")
        content = archive.read(names[0])
    lines = content.splitlines()
    if not lines or not lines[0].decode("cp949").startswith("행정동코드"):
        raise ContextError("jurisdiction_header_invalid")
    relations = []
    for line in lines[1:]:
        if not line.strip():
            continue
        field = lambda start, size: line[start:start + size].decode("cp949").strip()
        admin, legal = field(0, 10), field(104, 10)
        if not re.fullmatch(r"\d{10}", admin) or not re.fullmatch(r"\d{10}", legal):
            raise ContextError("jurisdiction_code_invalid")
        if field(155, 8) or not field(73, 30) or not legal.startswith("11"):
            continue
        if not admin.endswith("00") or not legal.endswith("00"):
            raise ContextError("jurisdiction_code_level_invalid")
        relations.append({"administrativeCode": admin[:8], "legalCode": legal,
                          "legalName": field(115, 30), "administrativeName": field(73, 30)})
    ref = {"sourceId": "mois-resident-registration-codes", "datasetId": "korea-administrative-legal-jurisdictions",
           "path": str(path), "sha256": digest(raw), "evidenceAsOf": names[0].split(".")[-1],
           "unit": "행정기관·법정동 관할관계", "spatialUnit": "administrative-to-legal-jurisdiction",
           "status": "Frozen20260301NotCurrentBoundary", "geometry": False}
    return relations, ref


def latest_rows(rows, code_column):
    periods = {r.get("기준_년분기_코드", "") for r in rows}
    if not periods or any(not re.fullmatch(r"20\d{2}[1-4]", p) for p in periods):
        raise ContextError("population_period_invalid")
    period = max(periods)
    selected = {}
    for row in rows:
        if row["기준_년분기_코드"] != period:
            continue
        code = row.get(code_column, "")
        if code_column == "행정동_코드" and not re.fullmatch(r"\d{8}", code):
            raise ContextError("administrative_code_invalid")
        if not code or code in selected:
            raise ContextError("population_code_invalid_or_duplicate")
        selected[code] = row
    return period, selected


def build_relations(jurisdictions, administrative_population, walk_rows):
    """References retain many-to-many jurisdiction; no population sums or shares."""
    legal = {}
    unresolved = []
    for relation in jurisdictions:
        a, b = relation["administrativeCode"], relation["legalCode"]
        if not re.fullmatch(r"\d{8}", a) or not re.fullmatch(r"\d{10}", b):
            raise ContextError("relation_code_invalid")
        entry = legal.setdefault(b, {"legalDongCode": b, "legalDongName": relation["legalName"],
                                    "administrativeContextCodes": [], "availableWalkLinkCount": 0,
                                    "legalDongPopulation": None, "populationAllocation": "NotAllocated",
                                    "relationStatus": "FrozenJurisdictionCandidate"})
        if a not in entry["administrativeContextCodes"]:
            entry["administrativeContextCodes"].append(a)
        if a not in administrative_population:
            unresolved.append({"kind": "jurisdictionAdministrativeCodeNotInLatestPopulation", "administrativeCode": a, "legalCode": b})
    links = []
    ids = set()
    for row in walk_rows:
        if row.get("노드링크 유형") != "LINK":
            continue
        b, link = row.get("읍면동코드", ""), row.get("링크 ID", "")
        if not re.fullmatch(r"\d{10}", b) or not link.isdigit() or link in ids:
            raise ContextError("walk_code_or_link_invalid")
        ids.add(link)
        contexts = legal.get(b, {}).get("administrativeContextCodes", [])
        if b in legal:
            legal[b]["availableWalkLinkCount"] += 1
        else:
            unresolved.append({"kind": "walkLegalCodeNotInFrozenJurisdiction", "legalCode": b, "linkId": link})
        links.append({"linkId": link, "legalDongCode": b,
                      "administrativeContextCodes": "|".join(sorted(contexts)),
                      "matchedPopulationContextCodes": "|".join(sorted(a for a in contexts if a in administrative_population)),
                      "contextScope": "WholeAdministrativeDongNotRoad",
                      "roadPopulation": None, "populationAllocation": "NotAllocated",
                      "relationStatus": "Candidate" if contexts else "Unresolved"})
    seen_admin = {r["administrativeCode"] for r in jurisdictions}
    unresolved.extend({"kind": "latestAdministrativeCodeNotInFrozenJurisdiction", "administrativeCode": a}
                      for a in sorted(set(administrative_population) - seen_admin))
    for entry in legal.values():
        entry["administrativeContextCodes"].sort()
    return sorted(legal.values(), key=lambda r: r["legalDongCode"]), links, unresolved


def build_trading_contexts(area_rows, population, period, jurisdictions):
    """Join by published trading code; legal candidates are jurisdiction only."""
    areas = {}
    for row in area_rows:
        code = row.get("상권_코드", "")
        admin = row.get("행정동_코드", "")
        if not code.isdigit() or code in areas or not re.fullmatch(r"\d{8}", admin):
            raise ContextError("trading_area_code_invalid_or_duplicate")
        areas[code] = row
    legal_by_admin = {}
    for row in jurisdictions:
        legal_by_admin.setdefault(row["administrativeCode"], set()).add(row["legalCode"])
    contexts = []
    for code in sorted(set(areas) | set(population)):
        area, pop = areas.get(code), population.get(code)
        admin = area.get("행정동_코드") if area else None
        candidates = sorted(legal_by_admin.get(admin, set()))
        contexts.append({
            "tradingAreaCode": code,
            "tradingAreaName": area.get("상권_코드_명") if area else pop.get("상권_코드_명"),
            "populationSourceName": pop.get("상권_코드_명") if pop else None,
            "populationPeriod": period if pop else None,
            "sourceReportedTotal": pop.get("총_유동인구_수") if pop else None,
            "populationScope": "WholeTradingAreaNotLegalDongOrRoad",
            "representativeX": area.get("엑스좌표_값") if area else None,
            "representativeY": area.get("와이좌표_값") if area else None,
            "representativeCoordinateReferenceSystem": "UnverifiedForThisCsv" if area else None,
            "administrativeCode": admin,
            "legalDongCandidates": candidates,
            "legalCandidateMethod": "Frozen20260301AdministrativeJurisdictionNotSpatialIntersection",
            "joinStatus": "Matched" if area and pop else "AreaOnly" if area else "PopulationOnly",
            "legalCandidateStatus": "Candidate" if candidates else "Unresolved",
            "legalDongPopulation": None, "roadPopulation": None,
            "populationAllocation": "NotAllocated", "geometryIntersectionPerformed": False,
        })
    counts = {"tradingAreaMatchedCodes": len(set(areas) & set(population)),
              "tradingAreaOnlyCodes": len(set(areas) - set(population)),
              "tradingPopulationOnlyCodes": len(set(population) - set(areas)),
              "tradingAreaLegalCandidateUnresolvedCodes": sum(not r["legalDongCandidates"] for r in contexts)}
    return contexts, counts


def build(root, output_dir):
    root = Path(root).resolve()
    output_dir = Path(output_dir).resolve()
    allowed = root / "artifacts/local"
    if not output_dir.is_relative_to(allowed) or output_dir == allowed:
        raise ContextError("output_must_be_new_artifacts_subdirectory")
    if output_dir.exists():
        raise ContextError("output_already_exists")
    snapshots = {name: read_snapshot(root / "artifacts/local/data-collection-runs/city-activity" / name) for name in SOURCES}
    admin_period, population = latest_rows(snapshots["seoul-administrative-population"][0], "행정동_코드")
    trading_period, trading = latest_rows(snapshots["seoul-trading-population"][0], "상권_코드")
    jurisdictions, jurisdiction_ref = read_jurisdictions(root / JURISDICTION)
    receipt = json.loads((root / WALK_RECEIPT).read_text(encoding="utf-8-sig"))
    walk_meta = next((r for r in receipt["sources"] if r.get("storedFileName") == WALK.name), None)
    if walk_meta is None:
        raise ContextError("walk_receipt_missing")
    walk_body = checked_body(root / WALK, walk_meta["sha256"])
    walk_rows = list(csv.DictReader(io.StringIO(walk_body.decode("cp949"), newline="")))
    if len(walk_rows) != walk_meta["rowCount"]:
        raise ContextError("walk_record_count_mismatch")
    contexts, roads, unresolved = build_relations(jurisdictions, population, walk_rows)
    trading_contexts, trading_join_counts = build_trading_contexts(
        snapshots["seoul-trading-areas"][0], trading, trading_period, jurisdictions)
    refs = {name: ref for name, (_, ref) in snapshots.items()}
    refs["jurisdictions"] = jurisdiction_ref
    refs["walkNetwork"] = {"sourceId": "seoul-open-data", "datasetId": "OA-21208", "path": str(root / WALK),
                           "sha256": digest(walk_body), "evidenceAsOf": receipt["sourceReferenceVintage"],
                           "collectedAt": receipt["acquiredAtUtc"], "crs": "EPSG:4326",
                           "unit": "보행 네트워크 NODE/LINK; 유동인구 아님", "rowCount": len(walk_rows),
                           "sourceReferenceDateExact": None, "coverage": "JungnangBoroughFilteredHistoricalCandidate"}
    admin_contexts = [{"administrativeCode": a, "administrativeName": r.get("행정동_코드_명"),
                       "period": admin_period, "sourceReportedTotal": r.get("총_유동인구_수"),
                       "populationScope": "WholeAdministrativeDong", "allocationToLegalDongOrRoad": "NotAllocated"}
                      for a, r in sorted(population.items())]
    summary = {"schema": "seoul-activity-reference-context.v1", "generatedAt": datetime.now(timezone.utc).isoformat(),
               "status": "CandidateReferencesNotPopulationAllocation", "sourceRefs": refs,
               "administrativePopulationPeriod": admin_period, "administrativePopulationContexts": len(population),
               "tradingPopulationPeriod": trading_period, "latestTradingPopulationRows": len(trading),
               "tradingAreaRows": len(snapshots["seoul-trading-areas"][0]),
               "administrativeAreaRows": len(snapshots["seoul-administrative-areas"][0]),
               **trading_join_counts,
               "legalDongContexts": len(contexts), "walkLinkReferences": len(roads),
               "unresolvedCount": len(unresolved), "unresolved": unresolved,
               "roadPopulationAvailable": False, "legalDongPopulationAllocated": False,
               "limitations": ["Frozen20260301JurisdictionMayDifferFromCurrentPopulationCodes",
                               "NoRoadMeasuredPopulation", "NoPopulationDuplicationOrDistribution",
                               "NoGeometryIntersectionPerformed", "NoDatabaseOrGameImport"]}
    output_dir.mkdir(parents=True)
    for name, value in (("index.json", summary), ("legal-dong-contexts.json", contexts),
                        ("trading-area-contexts.json", trading_contexts),
                        ("administrative-population-contexts.json", admin_contexts)):
        (output_dir / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (output_dir / "road-context-links.csv").open("w", encoding="utf-8-sig", newline="") as f:
        fields = ["linkId", "legalDongCode", "administrativeContextCodes", "matchedPopulationContextCodes", "contextScope", "roadPopulation", "populationAllocation", "relationStatus"]
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        writer.writerows(roads)
    return summary


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=ROOT)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        result = build(args.repo_root, args.output_dir)
    except (ContextError, OSError, ValueError, KeyError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "failed", "reason": str(exc)}, ensure_ascii=False))
        return 1
    print(json.dumps({k: result[k] for k in ("status", "legalDongContexts", "walkLinkReferences", "unresolvedCount", "latestTradingPopulationRows")}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

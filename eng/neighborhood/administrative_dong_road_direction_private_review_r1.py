#!/usr/bin/env python3
"""Generate private OA-15536 pavement-symbol candidates for the exact historical 30-dong scope.

The symbol angle is a depiction attribute, never approach, driving, lane, signal,
stop-line, access, traversal, or gameplay authority. LENX has no verified unit.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import io
import json
import math
import os
import shutil
import sys
import uuid
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path("artifacts/local/public-data/admin-dong-road-direction-20260926-r1/raw/A055_P_방향표시_20260910.zip")
RECEIPT = SOURCE.parent / "receipt.json"
BOUNDARY = Path("artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip")
SCOPE = Path("eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json")
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
OUTPUT = Path("artifacts/local/validation/admin-dong-road-direction-private-review/r1/input")
SOURCE_SHA = "641432B09B0C4A7AE38909873287BEE3E553B5C4F2CC0CD5394E03E7C36CCBAD"
RECEIPT_SHA = "2BD67989B5ED1139BAB98F1CF84E7F4B1105504B854AF249DEB99E568D0881C1"
BOUNDARY_SHA = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
SCOPE_SHA = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"
SOURCE_COUNT = 158_373
SELECTED_COUNT = 8_415
AREA_COUNT = 30
FIXED_AT = "2026-09-26T00:00:00Z"


class DirectionReviewError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise DirectionReviewError(code)


def path(root: Path, relative: Path) -> Path:
    result = (root / relative).resolve()
    require(result == root or root in result.parents, "PathEscapesRepository")
    return result


def digest(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def file_digest(file: Path) -> str:
    h = hashlib.sha256()
    with file.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")


def source_file(root: Path, relative: Path, expected: str) -> Path:
    candidate = path(root, relative)
    require(candidate.is_file() and not candidate.is_symlink(), "SourceMissingOrUnsafe:" + candidate.name)
    require(file_digest(candidate) == expected, "SourceHashChanged:" + candidate.name)
    return candidate


def context(root: Path) -> tuple[Any, Any, Any, list[Any]]:
    spec = importlib.util.spec_from_file_location("road_direction_boundary_batch", source_file(root, BATCH, file_digest(path(root, BATCH))))
    require(spec is not None and spec.loader is not None, "BoundaryLoaderUnavailable")
    batch = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = batch
    spec.loader.exec_module(batch)
    scope = batch.load_scope(root, source_file(root, SCOPE, SCOPE_SHA))
    deps = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, audit = batch.read_boundaries(root, scope, frame, deps)
    require(len(boundaries) == AREA_COUNT and audit["historicalBootstrapOnly"] is True, "HistoricalScopeChanged")
    return deps, frame, scope, boundaries


def authority() -> dict[str, bool]:
    return {
        "privateLocalCandidateOnly": True,
        "historicalBoundaryOnly": True,
        "angleSemanticVerified": False,
        "directionResolved": False,
        "renderedArrowAuthorized": False,
        "sourceLengthUnitVerified": False,
        "publicDisplayApproved": False,
        "databaseWritePerformed": False,
        "mongoWritePerformed": False,
        "currentPointerUsed": False,
        "currentPointerChanged": False,
        "unityApplyPerformed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "approachDirectionEstablished": False,
        "drivingDirectionEstablished": False,
        "laneAuthorityEstablished": False,
        "stopLineEstablished": False,
        "signalAuthorityEstablished": False,
        "passageAuthorized": False,
    }


def read_candidates(root: Path, deps: Any, frame: Any, boundaries: list[Any]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    archive_path = source_file(root, SOURCE, SOURCE_SHA)
    source_file(root, RECEIPT, RECEIPT_SHA)
    source_file(root, BOUNDARY, BOUNDARY_SHA)
    receipt = json.loads(path(root, RECEIPT).read_text(encoding="utf-8"))
    require(receipt.get("datasetId") == "OA-15536", "ReceiptDatasetChanged")
    require(receipt["internalDataset"]["recordCount"] == SOURCE_COUNT, "ReceiptCountChanged")
    require(receipt["internalDataset"]["sourceDeclaredCrs"] == "EPSG:5186", "ReceiptCrsChanged")
    require(any(item.get("sha256") == SOURCE_SHA and item.get("bytes") == archive_path.stat().st_size for item in receipt["files"]), "ReceiptSourceBindingChanged")
    to_wgs = deps.Transformer.from_crs(5186, 4326, always_xy=True)
    items: list[dict[str, Any]] = []
    per_area: Counter[str] = Counter()
    per_kind: Counter[str] = Counter()
    length_present = 0
    angle_missing = 0
    with zipfile.ZipFile(archive_path) as archive:
        require(len(archive.namelist()) == len(set(archive.namelist())), "SourceArchiveDuplicateEntry")
        require(set(("A055_P.shp", "A055_P.shx", "A055_P.dbf.dbf", "A055_P.prj", "A055_P.cpg")).issubset(archive.namelist()), "SourceArchiveMembersChanged")
        require(archive.read("A055_P.cpg").decode("ascii").strip().upper() == "EUC-KR", "SourceEncodingChanged")
        require(deps.CRS.from_wkt(archive.read("A055_P.prj").decode("utf-8-sig")).to_authority() == ("EPSG", "5186"), "SourceProjectionChanged")
        reader = deps.shapefile.Reader(
            shp=io.BytesIO(archive.read("A055_P.shp")),
            shx=io.BytesIO(archive.read("A055_P.shx")),
            dbf=io.BytesIO(archive.read("A055_P.dbf.dbf")),
            encoding="euc-kr", encodingErrors="strict",
        )
        try:
            require(len(reader) == SOURCE_COUNT, "SourceRecordCountChanged")
            fields = {value[0] for value in reader.fields[1:]}
            require({"MGRNU", "A055_KND_C", "DRN", "LENX"}.issubset(fields), "SourceFieldsMissing")
            for index, shape_record in enumerate(reader.iterShapeRecords()):
                shape = shape_record.shape
                require(shape.shapeType == 1 and len(shape.points) == 1, "SourceGeometryInvalid")
                x, y = shape.points[0]
                require(math.isfinite(x) and math.isfinite(y), "SourcePointNonFinite")
                longitude, latitude = to_wgs.transform(x, y)
                east, north = frame.wgs84_to_local(longitude, latitude)
                matches = [boundary for boundary in boundaries
                           if boundary.geometry.bounds[0] <= east <= boundary.geometry.bounds[2]
                           and boundary.geometry.bounds[1] <= north <= boundary.geometry.bounds[3]
                           and boundary.geometry.covers(deps.Point(east, north))]
                require(len(matches) <= 1, "MultipleHistoricalBoundaries")
                if not matches:
                    continue
                values = shape_record.record.as_dict()
                management = str(values["MGRNU"]).strip()
                require(bool(management), "SourceManagementNumberMissing")
                kind = str(values["A055_KND_C"]).strip()
                require(len(kind) == 3 and kind.isdigit() and 1 <= int(kind) <= 24, "OfficialKindCodeInvalid")
                angle = values["DRN"]
                if angle is None:
                    angle_missing += 1
                else:
                    require(isinstance(angle, (int, float)) and math.isfinite(angle), "DirectionAngleInvalid")
                if values["LENX"] is not None:
                    length_present += 1
                area = matches[0].stable_id
                stable_hash = digest((SOURCE_SHA + "|" + management + "|" + str(index)).encode("utf-8"))
                candidate = {
                    "administrativeAreaStableId": area,
                    "candidateStableId": "road-direction-symbol:sha256:" + stable_hash.lower(),
                    "commonEnuMillimeters": {"x": round(east * 1000), "z": round(north * 1000)},
                    "A055_KND_C": kind,
                    "DRN": angle,
                }
                items.append(candidate)
                per_area[area] += 1
                per_kind[kind] += 1
        finally:
            reader.close()
    require(len(items) == SELECTED_COUNT and len(per_area) == AREA_COUNT, "HistoricalSelectionCountChanged")
    require(len({item["candidateStableId"] for item in items}) == SELECTED_COUNT, "CandidateStableIdCollision")
    items.sort(key=lambda item: (item["administrativeAreaStableId"], item["candidateStableId"]))
    audit = {
        "sourcePointRows": SOURCE_COUNT,
        "uniqueHistoricalAssignedRows": len(items),
        "outsideScopeRows": SOURCE_COUNT - len(items),
        "multipleHistoricalBoundaryRows": 0,
        "administrativeAreasCovered": len(per_area),
        "areaCounts": dict(sorted(per_area.items())),
        "officialKindCounts": dict(sorted(per_kind.items())),
        "directionAngleMissingCount": angle_missing,
        "sourceLengthValuePresentButUnitUnverified": length_present,
    }
    return items, audit


def forbidden_scan(files: dict[str, bytes]) -> dict[str, int]:
    forbidden = {"MGRNU", "LENX", "managementNumber", "sourceFeatureKey", "sourceRow", "longitude", "latitude", "approachDirection", "drivingDirection", "lane", "stopLine", "signalTiming", "passageAllowed"}
    scanned = 0
    for name, payload in files.items():
        if not name.endswith(".ndjson"):
            continue
        for line in payload.splitlines():
            item = json.loads(line)
            require(set(item) == {"administrativeAreaStableId", "candidateStableId", "commonEnuMillimeters", "A055_KND_C", "DRN"}, "CandidateSchemaChanged")
            require(not (set(item) & forbidden), "ForbiddenCandidateField")
            require(set(item["commonEnuMillimeters"]) == {"x", "z"}, "CandidateCoordinateSchemaChanged")
            scanned += 1
    return {"candidateRowsScanned": scanned, "forbiddenFieldHits": 0}


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    deps, frame, scope, boundaries = context(root)
    items, counts = read_candidates(root, deps, frame, boundaries)
    payload = b"".join(canonical(item) for item in items)
    source_binding = {
        "datasetId": "OA-15536", "sourceRevision": "2026-09-10",
        "sourceArchiveSha256": SOURCE_SHA, "sourceReceiptSha256": RECEIPT_SHA,
        "historicalBoundaryDatasetId": "OA-22160", "historicalBoundarySha256": BOUNDARY_SHA,
        "scopeDefinitionSha256": SCOPE_SHA,
        "boundaryLoaderSha256": file_digest(path(root, BATCH)),
        "generatorSha256": file_digest(Path(__file__).resolve()),
    }
    generation_hash = digest(canonical({"sourceBinding": source_binding, "candidatesSha256": digest(payload)}))
    audit = {
        "schemaVersion": "administrative-dong-road-direction-private-audit.v1",
        "generationHashSha256": generation_hash, "generatedAtUtc": FIXED_AT,
        "sourceBinding": source_binding, "sourceCrs": "EPSG:5186",
        "historicalBoundaryCrs": "EPSG:5181", "scopeStableId": scope["scopeStableId"],
        "coordinateFrame": scope["coordinateFrame"], "counts": counts,
        "anglePolicy": {
            "DRN": "sourceNumericCandidateOnly",
            "zeroAxisVerified": False,
            "rotationDirectionVerified": False,
            "unitVerified": False,
            "renderedArrowAuthorized": False,
        },
        "lengthPolicy": "Source value counted only; unit unverified; no candidate length or render size",
        "authority": authority(),
    }
    files = {"candidates.ndjson": payload, "audit.json": canonical(audit)}
    scan = forbidden_scan(files)
    require(scan["candidateRowsScanned"] == SELECTED_COUNT, "CandidateScanCountChanged")
    manifest = {
        "schemaVersion": "administrative-dong-road-direction-private-manifest.v1",
        "generationHashSha256": generation_hash, "generatedAtUtc": FIXED_AT,
        "revision": "northeast-seoul-road-direction-private-review.r1",
        "scopeStableId": scope["scopeStableId"], "sourceBinding": source_binding,
        "fileDescriptors": [{"path": name, "bytes": len(content), "sha256": digest(content)} for name, content in sorted(files.items())],
        "candidateCount": len(items), "areaCount": AREA_COUNT,
        "anglePolicy": {"DRN": "sourceNumericCandidateOnly", "angleSemanticVerified": False,
                        "zeroAxisVerified": False, "rotationDirectionVerified": False,
                        "unitVerified": False, "renderedArrowAuthorized": False},
        "sourceLengthUnitVerified": False,
        "forbiddenFieldScan": scan, "authority": authority(),
    }
    files["manifest.json"] = canonical(manifest)
    completion = {
        "schemaVersion": "administrative-dong-road-direction-private-completion.v1",
        "generationHashSha256": generation_hash, "status": "LocalPrivateCandidateGenerated",
        "manifestSha256": digest(files["manifest.json"]), "candidateCount": len(items),
        "authority": authority(),
    }
    files["complete.json"] = canonical(completion)
    return generation_hash, files, counts


def validate(target: Path, expected: dict[str, bytes]) -> None:
    require(target.is_dir() and not target.is_symlink(), "GenerationMissingOrUnsafe")
    actual = {item.relative_to(target).as_posix() for item in target.rglob("*") if item.is_file()}
    require(actual == set(expected), "GenerationFileSetChanged")
    for name, payload in expected.items():
        file = target / name
        require(file.is_file() and not file.is_symlink() and file.read_bytes() == payload, "GenerationFileChanged:" + name)
    forbidden_scan(expected)


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        sample = {"administrativeAreaStableId": "region:kr:hjd:0000000000", "candidateStableId": "road-direction-symbol:sha256:" + "0" * 64, "commonEnuMillimeters": {"x": 1, "z": -2}, "A055_KND_C": "001", "DRN": 90}
        require(canonical(sample) == canonical(json.loads(canonical(sample))), "CanonicalJsonUnstable")
        require(forbidden_scan({"candidates.ndjson": canonical(sample)})["candidateRowsScanned"] == 1, "CandidateSchemaSelfTestFailed")
        require(all(value is False for key, value in authority().items() if key not in {"privateLocalCandidateOnly", "historicalBoundaryOnly"}), "AuthoritySelfTestFailed")
        require(digest(b"a") != digest(b"b"), "DigestSelfTestFailed")
        return {"status": "PASS", "selfTestsPassed": 4, "generatorSha256": file_digest(Path(__file__).resolve())}
    generation_hash, files, counts = prepare(root)
    target = path(root, OUTPUT / "generations" / generation_hash.lower())
    if mode == "verify":
        validate(target, files)
        changed = 0
    elif target.exists():
        validate(target, files)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = target.parent / (".staging-" + uuid.uuid4().hex)
        staging.mkdir()
        try:
            for name, payload in files.items():
                (staging / name).write_bytes(payload)
            validate(staging, files)
            os.replace(staging, target)
        except Exception:
            if staging.exists():
                shutil.rmtree(staging)
            raise
        changed = len(files)
    return {"status": "PASS", "mode": mode, "generationHashSha256": generation_hash,
            "generationRelativePath": target.relative_to(root).as_posix(), "changedFiles": changed,
            "sourcePointRows": counts["sourcePointRows"], "uniqueHistoricalAssignedRows": counts["uniqueHistoricalAssignedRows"],
            "outsideScopeRows": counts["outsideScopeRows"], "administrativeAreasCovered": counts["administrativeAreasCovered"],
            "sourceLengthValuePresentButUnitUnverified": counts["sourceLengthValuePresentButUnitUnverified"],
            "forbiddenFieldHits": 0, "authority": authority()}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        print(json.dumps(run(root, args.mode), ensure_ascii=False, sort_keys=True, indent=2))
        return 0
    except (DirectionReviewError, OSError, ValueError, KeyError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

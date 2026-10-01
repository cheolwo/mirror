#!/usr/bin/env python3
"""보관된 방문 좌표와 동결 역사 경계의 비공개 검토 후보만 만든다.

위치 확인, Verified 상향, 방문위치결속 receipt, 운영/Unity 입력은 생성하지 않는다.
기존 전체 생성기를 실행하지 않고 고정한 경계 읽기·공통 ENU 함수만 재사용한다.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import stat
import subprocess
import sys
from typing import Any


sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path("artifacts/local/validation/visit-location-binding-private-review/r1")
INPUT = Path("artifacts/local/delivery-records/delivery-visits.v2.json")
SCOPE = Path("eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json")
BOUNDARY = Path("artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip")
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
GEOMETRY_HELPER = Path("eng/neighborhood/sagajeong_spatial_presentation.py")
GENERATOR = Path("eng/neighborhood/visit_location_binding_private_review_r1.py")
PINS = {
    INPUT: "1BFDC56363D5611DE0C1D0A5A2A5BE755B35E4004167A7816842CFD2B5719661",
    SCOPE: "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B",
    BOUNDARY: "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68",
    BATCH: "24BD8D246D2FA714A9C83065D5A8594179DED8E466F8B816FCD3BA02DB79351C",
    GEOMETRY_HELPER: "CE5D5C0AEAA219F7B42A2A3646ACE47E936C3509B465C87AB21B1605DFB42D7B",
}
FRAME = {"method": "WGS84-ECEF-ENU-at-zero-altitude", "originLatitude": 37.580912,
         "originLongitude": 127.088502, "worldOffsetX": 0, "worldOffsetZ": 0,
         "metersPerUnit": 1}
SCHEMA = "visit-location-binding-private-review.v1"
REVISION = "visit-location-binding-private-review.r1"
BOUNDARY_EPSILON_METERS = 0.000001
MAX_JSON_BYTES = 8 * 1024 * 1024


class ReviewError(ValueError):
    """오류 코드만 노출하며 원문/주소/파서 오류를 출력하지 않는다."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise ReviewError(code)


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def canonical(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"),
                      allow_nan=False).encode("utf-8")


def encoded(value: Any) -> bytes:
    return canonical(value) + b"\n"


def _pairs(values: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in values:
        require(key not in result, "JsonDuplicateProperty")
        result[key] = value
    return result


def parse_json(payload: bytes) -> dict[str, Any]:
    require(len(payload) <= MAX_JSON_BYTES, "JsonInputTooLarge")
    try:
        result = json.loads(payload.decode("utf-8-sig"), object_pairs_hook=_pairs,
                            parse_constant=lambda _: (_ for _ in ()).throw(ReviewError("JsonNonFinite")))
    except ReviewError:
        raise
    except (ValueError, UnicodeError, RecursionError) as exc:
        raise ReviewError("JsonInputInvalid") from exc
    require(isinstance(result, dict), "JsonObjectRequired")
    return result


def safe_path(root: Path, relative: Path) -> Path:
    require(not relative.is_absolute() and ".." not in relative.parts, "PathOutsideRepository")
    root = root.resolve()
    path = root / relative
    require(path.resolve().is_relative_to(root), "PathOutsideRepository")
    cursor = root
    for part in relative.parts:
        cursor = cursor / part
        if cursor.exists() or cursor.is_symlink():
            attributes = cursor.lstat()
            require(not cursor.is_symlink() and not (
                getattr(attributes, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)),
                "ReparsePathNotAllowed")
    return path


def frozen_payload(root: Path, relative: Path, expected_hash: str) -> bytes:
    path = safe_path(root, relative)
    require(path.is_file(), "FrozenSourceMissing")
    payload = path.read_bytes()
    require(sha(payload) == expected_hash, "FrozenSourceHashMismatch")
    return payload


def _text(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _number(value: Any) -> bool:
    return not isinstance(value, bool) and isinstance(value, (int, float)) and math.isfinite(value)


def validate_record(record: dict[str, Any], expected_count: int = 7) -> None:
    require(record.get("schemaVersion") == "delivery-visit-record.v2"
            and _text(record.get("recordId")) and record.get("localReviewOnly") is True
            and record.get("isOperationalState") is False, "VisitRecordBoundaryInvalid")
    visits = record.get("visits")
    require(isinstance(visits, list) and len(visits) == expected_count and 0 < expected_count <= 10000,
            "VisitCountInvalid")
    identifiers: set[str] = set()
    sequences: list[int] = []
    for visit in visits:
        require(isinstance(visit, dict) and _text(visit.get("visitId"))
                and _text(visit.get("storeId")) and _text(visit.get("address")), "VisitIdentityInvalid")
        require(visit["visitId"] not in identifiers, "DuplicateVisitId")
        identifiers.add(visit["visitId"])
        sequence = visit.get("sequence")
        require(type(sequence) is int and sequence > 0, "VisitSequenceInvalid")
        sequences.append(sequence)
        require(visit.get("eventKind") == "RecordedVisit"
                and visit.get("pickupCompletionConfirmed") is False, "VisitEventBoundaryInvalid")
        require(visit.get("coordinateStatus") == "AddressGeocodeCandidate_NotEntrance"
                and visit.get("areaBindingStatus") == "Unresolved"
                and visit.get("administrativeAreaStableId") is None
                and visit.get("stationModuleStableId") is None
                and visit.get("areaBindingEvidence") is None, "VisitSourceStatusChanged")
        require(_number(visit.get("latitude")) and -90 <= visit["latitude"] <= 90
                and _number(visit.get("longitude")) and -180 <= visit["longitude"] <= 180,
                "VisitCoordinateInvalid")
    require(sorted(sequences) == list(range(1, expected_count + 1)), "VisitSequenceGapOrDuplicate")


def classify_point(point: Any, boundaries: list[Any]) -> tuple[str, list[dict[str, str]]]:
    hits: list[dict[str, str]] = []
    for boundary in boundaries:
        on_boundary = boundary.geometry.boundary.distance(point) <= BOUNDARY_EPSILON_METERS
        if on_boundary or boundary.geometry.covers(point):
            hits.append({"administrativeAreaStableId": boundary.stable_id,
                         "relation": "Boundary" if on_boundary else "Interior"})
    hits.sort(key=lambda value: value["administrativeAreaStableId"])
    require(len({item["administrativeAreaStableId"] for item in hits}) == len(hits), "BoundaryIdentityDuplicate")
    if len(hits) > 1:
        return "Multiple", hits
    if not hits:
        return "OutsideScope", hits
    return ("Boundary" if hits[0]["relation"] == "Boundary" else "SingleInterior"), hits


def make_report(record: dict[str, Any], frame: Any, boundaries: list[Any], point_factory: Any,
                provenance: dict[str, Any], expected_count: int = 7) -> dict[str, Any]:
    validate_record(record, expected_count)
    require(provenance.get("coordinateFrame") == FRAME, "CoordinateFrameChanged")
    rows: list[dict[str, Any]] = []
    for visit in sorted(record["visits"], key=lambda value: value["sequence"]):
        x, z = frame.wgs84_to_local(visit["longitude"], visit["latitude"])
        require(_number(x) and _number(z), "CommonCoordinateNonFinite")
        classification, hits = classify_point(point_factory(x, z), boundaries)
        # 위치 증거와 메뉴 검색 근거는 다른 계보다. sourceUrls는 복제하거나 승인에 사용하지 않는다.
        rows.append({
            "visitId": visit["visitId"], "storeId": visit["storeId"], "sequence": visit["sequence"],
            "sourceAddressSha256": sha(visit["address"].encode("utf-8")),
            "sourceStatuses": {"addressStatus": visit.get("addressStatus"),
                               "coordinateStatus": visit["coordinateStatus"],
                               "areaBindingStatus": visit["areaBindingStatus"]},
            "coordinateCandidate": {"longitude": visit["longitude"], "latitude": visit["latitude"],
                                    "interpretation": "ExistingGeocodeCandidateAsWgs84_NotVerified"},
            "commonEnuCandidate": {"x": x, "z": z},
            "classification": classification, "historicalBoundaryCandidates": hits,
            "reviewStatus": "PendingReview", "locationEvidenceReferences": [],
            "missingEvidence": ["StoreIdentityAndAddressCoordinateReviewRequired",
                                "CoordinateCandidateNotIndependentlyVerified",
                                "CurrentAdministrativeBoundaryUnavailable",
                                "ReviewedLocationBindingNotIssued"],
        })
    counts = Counter(row["classification"] for row in rows)
    report = {
        "schemaVersion": SCHEMA, "revision": REVISION, "recordId": record["recordId"],
        "privateReviewOnly": True, "reviewStatus": "PendingReview", "approvalGranted": False,
        "canCreatePlaybackReceipt": False, "runtimeAuthorized": False, "distributionApproved": False,
        "currentAdministrativeBoundaryEstablished": False,
        "sourceStatusesPreserved": True, "menuSourceUrlsUsedAsLocationEvidence": False,
        "provenance": provenance, "boundaryToleranceMeters": BOUNDARY_EPSILON_METERS,
        "summary": {"visitCount": len(rows), "pendingReviewCount": len(rows),
                    "singleInteriorCount": counts["SingleInterior"], "boundaryCount": counts["Boundary"],
                    "multipleCount": counts["Multiple"], "outsideScopeCount": counts["OutsideScope"],
                    "boundaryTouchVisitCount": sum(any(hit["relation"] == "Boundary" for hit in row["historicalBoundaryCandidates"])
                                                   for row in rows),
                    "sourceRowsChanged": 0, "approvalCount": 0, "playbackReceiptCount": 0},
        "visits": rows,
    }
    report["contentHashSha256"] = sha(canonical(report))
    return report


def load_batch(root: Path) -> Any:
    os.environ["PROJ_NETWORK"] = "OFF"
    spatial = safe_path(root, Path("artifacts/local/python-packages/spatial"))
    if str(spatial) not in sys.path:
        sys.path.insert(0, str(spatial))
    spec = importlib.util.spec_from_file_location("visit_review_frozen_batch", safe_path(root, BATCH))
    require(spec is not None and spec.loader is not None, "BoundaryReaderUnavailable")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def source_snapshots(root: Path) -> dict[Path, bytes]:
    return {path: frozen_payload(root, path, expected) for path, expected in PINS.items()}


def prepare_actual(root: Path) -> tuple[dict[str, Any], dict[Path, bytes]]:
    require(sys.version_info[:2] == (3, 12), "BundledPython312Required")
    snapshots = source_snapshots(root)
    record = parse_json(snapshots[INPUT])
    scope = parse_json(snapshots[SCOPE])
    require(scope.get("coordinateFrame") == FRAME, "CoordinateFrameChanged")
    boundary_sources = [item for item in scope.get("sources", []) if item.get("datasetId") == "OA-22160"]
    require(len(boundary_sources) == 1, "HistoricalBoundarySourceRequired")
    source = boundary_sources[0]
    require(source.get("repositoryRelativePath") == BOUNDARY.as_posix()
            and source.get("contentHashSha256") == PINS[BOUNDARY]
            and source.get("recordCount") == 425 and source.get("byteLength") == len(snapshots[BOUNDARY]),
            "HistoricalBoundaryContractChanged")
    batch = load_batch(root)
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, audit = batch.read_boundaries(root, scope, frame, dependencies)
    require(len(boundaries) == 30 and audit.get("historicalBootstrapOnly") is True
            and audit.get("currentAuthoritativeBoundaryAvailable") is False, "HistoricalBoundaryAuditInvalid")
    generator_hash = sha(safe_path(root, GENERATOR).read_bytes())
    provenance = {
        "sources": [{"relativePath": path.as_posix(), "sha256": sha(payload), "byteLength": len(payload)}
                    for path, payload in sorted(snapshots.items(), key=lambda item: item[0].as_posix())],
        "generatorRevision": REVISION, "generatorSha256": generator_hash,
        "recordRevisionReference": "sha256:" + PINS[INPUT],
        "boundaryRevision": source["sourceRevision"], "boundaryLimitation": source["limitationCode"],
        "scopeRevision": scope["revision"], "coordinateFrame": scope["coordinateFrame"],
        "coordinateFrameKey": "enu-sha256:" + sha(canonical(scope["coordinateFrame"])),
        "boundaryAudit": audit,
        "toolchain": {"python": ".".join(str(v) for v in sys.version_info[:3]),
                      "pyshp": dependencies.pyshp_version, "pyproj": dependencies.pyproj_version,
                      "shapely": dependencies.shapely_version, "geos": dependencies.geos_version,
                      "proj": dependencies.proj_version, "epsgDatabase": dependencies.epsg_database_version},
    }
    return make_report(record, frame, boundaries, dependencies.Point, provenance), snapshots


def assert_sources_unchanged(root: Path, snapshots: dict[Path, bytes]) -> None:
    for relative, original in snapshots.items():
        require(sha(safe_path(root, relative).read_bytes()) == sha(original), "FrozenSourceChangedDuringReview")


def ensure_private_output(root: Path, relative: Path) -> None:
    checked = subprocess.run(["git", "-C", str(root), "check-ignore", "--quiet", relative.as_posix()],
                             capture_output=True, check=False)
    require(checked.returncode == 0, "PrivateOutputMustBeGitIgnored")


def persist(root: Path, report: dict[str, Any], mode: str) -> dict[str, Any]:
    require(mode in {"build", "verify"}, "ModeInvalid")
    require(report.get("schemaVersion") == SCHEMA and report.get("approvalGranted") is False
            and report.get("reviewStatus") == "PendingReview"
            and report.get("canCreatePlaybackReceipt") is False
            and report.get("runtimeAuthorized") is False, "ReviewAuthorityBoundaryChanged")
    require(report.get("contentHashSha256") == sha(canonical({k: v for k, v in report.items() if k != "contentHashSha256"})),
            "ReviewContentHashMismatch")
    generation = report["contentHashSha256"].lower()
    relative = OUTPUT / "generations" / generation
    ensure_private_output(root, relative / "report.json")
    directory = safe_path(root, relative)
    report_bytes = encoded(report)
    complete = {"schemaVersion": "visit-location-binding-review-completion.v1", "generation": generation,
                "privateReviewOnly": True, "approvalGranted": False,
                "files": [{"name": "report.json", "sha256": sha(report_bytes), "byteLength": len(report_bytes)}]}
    expected = {"report.json": report_bytes, "complete.json": encoded(complete)}
    changed = 0
    if directory.exists():
        require(directory.is_dir() and set(path.name for path in directory.iterdir()) == set(expected),
                "ReviewGenerationFileSetMismatch")
        for name, payload in expected.items():
            actual = safe_path(root, relative / name)
            require(actual.is_file() and actual.read_bytes() == payload, "ReviewGenerationChanged")
    else:
        require(mode == "build", "ReviewGenerationMissing")
        directory.mkdir(parents=True, exist_ok=False)
        # 새 content-addressed 세대에만 생성하며 기존 세대는 수정하지 않습니다.
        for name, payload in expected.items():
            with safe_path(root, relative / name).open("xb") as stream:
                stream.write(payload)
            changed += 1
    return {"mode": mode, "generation": generation, "contentHashSha256": report["contentHashSha256"],
            "reportSha256": sha(report_bytes), "changedFiles": changed,
            "summary": report["summary"], "sourcesUnchanged": True,
            "privateReviewOnly": True, "reviewStatus": "PendingReview"}


def run(root: Path, mode: str) -> dict[str, Any]:
    root = root.resolve()
    report, snapshots = prepare_actual(root)
    assert_sources_unchanged(root, snapshots)
    result = persist(root, report, mode)
    assert_sources_unchanged(root, snapshots)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify"))
    parser.add_argument("--root", type=Path, default=ROOT)
    args = parser.parse_args()
    try:
        result = run(args.root, args.mode)
    except ReviewError as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False))
        return 1
    except Exception:
        # 재사용 파서의 예외·개인 파일 내용·주소를 콘솔로 전파하지 않습니다.
        print(json.dumps({"ok": False, "error": "VisitLocationReviewFailed"}))
        return 1
    print(json.dumps({"ok": True, **result}, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

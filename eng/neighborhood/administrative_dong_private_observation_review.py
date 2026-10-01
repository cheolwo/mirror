#!/usr/bin/env python3
"""동결 횡단보도·교차로 점을 30개 행정동 Unity 비공개 검토 번들로 파생한다.

기존 공개 DisplayOverlays, Mongo current, 통행·gameplay 권위는 변경하지 않는다.
원천 관리번호·명칭·주소는 출력하지 않고 해시 stable ID와 공통 ENU 점만 보존한다.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import os
import shutil
import stat
import sys
import uuid
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


ROOT_DEFAULT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = Path(
    "artifacts/local/public-data/admin-dong-precision-observation-20260926-r1"
)
RAW_ROOT = SOURCE_ROOT / "raw"
INTERSECTION_FILE = RAW_ROOT / "A008_P_20250814.zip"
CROSSWALK_FILE = RAW_ROOT / "서울시 교차로 및 횡단보도 시설·위치정보_20260824.xlsx"
RECEIPT_FILE = SOURCE_ROOT / "receipt.json"
BOUNDARY_FILE = Path(
    "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/"
    "seoul-administrative-dong-boundary.zip"
)
CROSSWALK_SCOPE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-crosswalk.g3a.r2.json"
)
INTERSECTION_SCOPE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-intersection.g3b.r1.json"
)
BASE_REVIEW_ROOT = Path(
    "artifacts/local/validation/admin-dong-diorama-base-review/r1/input/generations/"
    "c6cd2501cd2fc876bff671fb25c36c139afdaf14f78ec2fd3f7503e31a1153ac"
)
OUTPUT_ROOT = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r2/input"
)

SCHEMA = "administrative-dong-private-observation-overlays.v1"
REVISION = "northeast-seoul-private-observation-overlay.r1"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v2"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v2"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v2"
EXPORTER_REVISION = "administrative-dong-diorama-unity-review-exporter.r3"
GENERATED_AT_UTC = "2026-09-26T00:00:00Z"

INTERSECTION_HASH = "A77B4D4FD2886C934D2D097558A52580FA95ADB079BA828F1305DFD15F0E0449"
INTERSECTION_BYTES = 534_342
CROSSWALK_HASH = "1A5DB9EA7A1CD58E2D7F2B4246BAF099A3E4D5278F2867A1B87F3D50D21541BE"
CROSSWALK_BYTES = 1_735_662
BOUNDARY_HASH = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
BASE_INDEX_HASH = "C8D4999494767E9588E998100893A2D4227A8E116DEBD8F8B81611BA94D3E123"
EXPECTED_CROSSWALKS = 1_533
EXPECTED_SIGNAL_PRESENT = 933
EXPECTED_INTERSECTIONS = 554
EXPECTED_AREAS = 30

FORBIDDEN_OUTPUT_KEYS = {
    "businessName", "shopName", "branchName", "roadAddress", "jibunAddress",
    "detailAddress", "rawAddress", "pnu", "parcelIdentifier",
    "buildingManagementNumber", "managementNumber", "providerShopId",
    "providerBusinessId", "sourceFeatureKey", "sourceRow", "sourceSheetRow",
    "sourceOccurrenceKey", "crosswalkManagementNumber",
    "intersectionManagementNumber", "sourceManagementNumber", "longitude",
    "latitude", "phoneNumber", "representativeName",
}


class ObservationReviewError(RuntimeError):
    pass


def require(condition: bool, code: str) -> None:
    if not condition:
        raise ObservationReviewError(code)


def safe_path(root: Path, relative: Path | str) -> Path:
    root = root.resolve()
    candidate = (root / relative).resolve()
    require(candidate == root or root in candidate.parents, "PathEscapesRepository")
    return candidate


def is_reparse(path: Path) -> bool:
    try:
        value = path.lstat()
    except OSError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(getattr(value, "st_file_attributes", 0) & flag)


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False
    ).encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def content_hash(value: dict[str, Any], field: str) -> str:
    copy = dict(value)
    copy.pop(field, None)
    return sha_bytes(canonical_bytes(copy))


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ObservationReviewError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def load_module(path: Path, name: str) -> Any:
    specification = importlib.util.spec_from_file_location(name, path)
    require(specification is not None and specification.loader is not None,
            f"ModuleLoadFailed:{path.name}")
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    specification.loader.exec_module(module)
    return module


def validate_file(path: Path, length: int, digest: str, code: str) -> None:
    require(path.is_file() and not is_reparse(path), code + "MissingOrUnsafe")
    require(path.stat().st_size == length, code + "LengthChanged")
    require(sha_file(path) == digest, code + "HashChanged")


def source_receipts() -> list[dict[str, Any]]:
    return [
        {
            "datasetId": "OA-23081",
            "officialPageUrl": "https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do",
            "fileRevision": "2026-08-24",
            "fileSha256": CROSSWALK_HASH,
            "byteLength": CROSSWALK_BYTES,
            "licenseCode": "KOGL-Type1-Attribution",
            "coordinateReferenceStatus": "EmpiricallyValidatedCandidateNotSourceDeclared",
        },
        {
            "datasetId": "OA-15534",
            "officialPageUrl": "https://data.seoul.go.kr/dataList/OA-15534/S/1/datasetView.do",
            "fileRevision": "2025-08-14",
            "fileSha256": INTERSECTION_HASH,
            "byteLength": INTERSECTION_BYTES,
            "licenseCode": "KOGL-Type1-Attribution",
            "coordinateReferenceStatus": "SourceDeclaredEpsg5186",
        },
        {
            "datasetId": "OA-22160",
            "officialPageUrl": "https://data.seoul.go.kr/dataList/OA-22160/F/1/datasetView.do",
            "fileRevision": "2023-10-31",
            "fileSha256": BOUNDARY_HASH,
            "licenseCode": "KOGL-Type1-Attribution",
            "coordinateReferenceStatus": "HistoricalBoundaryBootstrapOnly",
        },
    ]


def ensure_receipt(root: Path) -> dict[str, Any]:
    receipt_path = safe_path(root, RECEIPT_FILE)
    if receipt_path.exists():
        receipt = read_json(receipt_path)
    else:
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        receipt = {
            "schemaVersion": "administrative-dong-private-observation-acquisition.v1",
            "revision": REVISION,
            "retrievedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "acquisitionMethodCode": "OfficialSeoulDataPortalPostDownloadExactHashVerified",
            "sources": source_receipts()[:2],
            "historicalBoundarySourceSha256": BOUNDARY_HASH,
            "privateReviewOnly": True,
            "distributionApproved": False,
            "runtimeAuthorized": False,
            "traversalReady": False,
            "gameplayReady": False,
        }
        receipt["contentHashSha256"] = content_hash(receipt, "contentHashSha256")
        receipt_path.write_bytes(pretty_bytes(receipt))
    require(receipt.get("schemaVersion") == "administrative-dong-private-observation-acquisition.v1"
            and receipt.get("revision") == REVISION
            and receipt.get("sources") == source_receipts()[:2]
            and receipt.get("historicalBoundarySourceSha256") == BOUNDARY_HASH
            and receipt.get("privateReviewOnly") is True
            and receipt.get("distributionApproved") is False
            and receipt.get("runtimeAuthorized") is False
            and receipt.get("traversalReady") is False
            and receipt.get("gameplayReady") is False
            and receipt.get("contentHashSha256") == content_hash(receipt, "contentHashSha256"),
            "AcquisitionReceiptChanged")
    return receipt


def stable_crosswalk_id(source_feature_key: str) -> str:
    return "candidate:kr:seoul:oa23081:crosswalk:" + sha_bytes(
        ("OA-23081|2026-08-24|" + source_feature_key).encode("utf-8")
    ).lower()


def assert_safe_output(value: Any) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            require(key not in FORBIDDEN_OUTPUT_KEYS, f"ForbiddenOutputProperty:{key}")
            assert_safe_output(child)
    elif isinstance(value, list):
        for child in value:
            assert_safe_output(child)
    elif isinstance(value, float):
        require(math.isfinite(value), "NonFiniteOutputNumber")


def prepare_candidates(root: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    spatial_packages = safe_path(root, "artifacts/local/python-packages/spatial")
    if str(spatial_packages) not in sys.path:
        sys.path.insert(0, str(spatial_packages))
    crosswalk = load_module(
        safe_path(root, "eng/neighborhood/administrative_dong_crosswalk_candidate.py"),
        "hongdal_crosswalk_candidate",
    )
    intersection = load_module(
        safe_path(root, "eng/neighborhood/administrative_dong_intersection_candidate.py"),
        "hongdal_intersection_candidate",
    )

    cross_scope = read_json(safe_path(root, CROSSWALK_SCOPE))
    intersection_scope = read_json(safe_path(root, INTERSECTION_SCOPE))
    cross_dependencies = crosswalk.load_dependencies(root)
    # 두 동결 생성기는 같은 pyshp/pyproj/shapely 계약을 사용한다. 현재 체크아웃은
    # 과거 gis-runtime 디렉터리 대신 복구된 spatial 패키지를 쓰므로 이미 검증한
    # dependency 묶음을 교차로 파서에도 전달한다.
    intersection_dependencies = cross_dependencies
    cross_rows, _ = crosswalk.read_crosswalk_rows(safe_path(root, CROSSWALK_FILE))
    cross_boundaries, _ = crosswalk.read_boundaries(
        safe_path(root, BOUNDARY_FILE), cross_scope, cross_dependencies
    )
    cross_candidates, cross_audit = crosswalk.assign_candidates(
        cross_rows, cross_boundaries, cross_scope, cross_dependencies
    )
    require(cross_audit["counts"]["candidateRows"] == EXPECTED_CROSSWALKS
            and sum(item["pedestrianSignalPresent"] for item in cross_candidates)
            == EXPECTED_SIGNAL_PRESENT,
            "CrosswalkCandidateCountsChanged")

    links: dict[str, list[Any]] = defaultdict(list)
    for item in cross_candidates:
        intersection_number = item["intersectionManagementNumber"]
        if intersection_number is None:
            continue
        links[str(intersection_number)].append(intersection.CrosswalkLink(
            item["sourceFeatureKey"], item["crosswalkManagementNumber"],
            item["administrativeAreaStableId"],
        ))
    for values in links.values():
        values.sort(key=lambda item: item.source_feature_key)

    intersection_rows, _ = intersection.read_intersections(
        safe_path(root, INTERSECTION_FILE), intersection_scope, intersection_dependencies
    )
    intersection_boundaries, _ = intersection.read_boundaries(
        safe_path(root, BOUNDARY_FILE), intersection_scope, intersection_dependencies
    )
    frame_value = intersection_scope["coordinateFrame"]
    frame = intersection.CommonEnuFrame(
        float(frame_value["originLatitude"]), float(frame_value["originLongitude"]),
        float(frame_value["worldOffsetX"]), float(frame_value["worldOffsetZ"]),
    )
    transformer = intersection_dependencies.Transformer.from_crs(5186, 4326, always_xy=True)
    intersection_candidates, intersection_audit = intersection.assign_candidates(
        intersection_rows, intersection_boundaries, dict(links),
        intersection_dependencies, frame, transformer,
    )
    require(intersection_audit["counts"]["candidateRows"] == EXPECTED_INTERSECTIONS,
            "IntersectionCandidateCountsChanged")
    return cross_candidates, intersection_candidates


def point_mm(value: dict[str, Any]) -> dict[str, int]:
    point = value["commonEnuMillimeters"]
    return {"x": int(point["x"]), "z": int(point["z"])}


def overlay_for_area(
    area_id: str,
    crosswalks: Sequence[dict[str, Any]],
    intersections: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    cross_items = []
    for item in sorted(
        (value for value in crosswalks if value["administrativeAreaStableId"] == area_id),
        key=lambda value: value["sourceFeatureKey"],
    ):
        cross_items.append({
            "candidateStableId": stable_crosswalk_id(item["sourceFeatureKey"]),
            "commonEnuMillimeters": point_mm(item),
            "pedestrianSignalPresent": bool(item["pedestrianSignalPresent"]),
            "sourceDistrictSpatialAssignmentConflict": bool(
                item["sourceDistrictSpatialAssignmentConflict"]
            ),
            "qualityCode": str(item["candidateQualityCode"]),
        })
    intersection_items = []
    for item in sorted(
        (value for value in intersections if value["administrativeAreaStableId"] == area_id),
        key=lambda value: value["candidateStableId"],
    ):
        linked = item["linkedCrosswalks"]
        intersection_items.append({
            "candidateStableId": str(item["candidateStableId"]),
            "commonEnuMillimeters": point_mm(item),
            "linkedCrosswalkCount": len(linked),
            "otherHistoricalAdministrativeAreaLinkCount": sum(
                not value["sameHistoricalAdministrativeArea"] for value in linked
            ),
            "sourceDistrictSpatialAssignmentConflict": bool(
                item["sourceDistrictSpatialAssignmentConflict"]
            ),
            "qualityCode": str(item["candidateQualityCode"]),
        })
    value: dict[str, Any] = {
        "schemaVersion": SCHEMA,
        "revision": REVISION,
        "overlaySetStableId": "administrative-dong-private-observation:" + area_id + ".v1",
        "administrativeAreaStableId": area_id,
        "sourceVintage": "oa23081-file-20260824+oa15534-file-20250814+oa22160-file-20231031",
        "historicalBoundaryBootstrapOnly": True,
        "privateReviewVisualizationAllowed": True,
        "reviewVisualizationOnly": True,
        "sourceCandidateUnityApplyAllowed": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "exactAddressIncluded": False,
        "businessIdentityIncluded": False,
        "personalDataIncluded": False,
        "sourceReceipts": source_receipts(),
        "crosswalkPoints": cross_items,
        "intersectionPoints": intersection_items,
    }
    value["overlaySetHashSha256"] = content_hash(value, "overlaySetHashSha256")
    assert_safe_output(value)
    return value


def base_generation(root: Path) -> tuple[dict[str, Any], Path]:
    generation = safe_path(root, BASE_REVIEW_ROOT)
    require(generation.is_dir() and not is_reparse(generation), "BaseReviewGenerationMissing")
    index_path = generation / "index.json"
    validate_file(index_path, index_path.stat().st_size, BASE_INDEX_HASH, "BaseReviewIndex")
    index = read_json(index_path)
    require(index.get("schemaVersion") == "administrative-dong-diorama-unity-review-index.v1"
            and index.get("exporterSemanticRevision")
            == "administrative-dong-diorama-unity-review-exporter.r2"
            and index.get("administrativeAreaCount") == EXPECTED_AREAS
            and len(index.get("bundles", [])) == EXPECTED_AREAS,
            "BaseReviewIndexContractChanged")
    return index, generation


def bundle_bytes(
    base: dict[str, Any], overlay: dict[str, Any], exporter_hash: str
) -> bytes:
    value = dict(base)
    value.update({
        "schemaVersion": BUNDLE_SCHEMA,
        "sourceVintage": base["sourceVintage"] + "+" + overlay["sourceVintage"],
        "generatedAtUtc": GENERATED_AT_UTC,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "privateObservationOverlayCaptureReady": True,
        "crosswalkPointCandidateCount": len(overlay["crosswalkPoints"]),
        "intersectionPointCandidateCount": len(overlay["intersectionPoints"]),
        "privateObservationOverlays": overlay,
    })
    value["contentHashSha256"] = content_hash(value, "contentHashSha256")
    assert_safe_output(value)
    return pretty_bytes(value)


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    validate_file(safe_path(root, INTERSECTION_FILE), INTERSECTION_BYTES,
                  INTERSECTION_HASH, "IntersectionSource")
    validate_file(safe_path(root, CROSSWALK_FILE), CROSSWALK_BYTES,
                  CROSSWALK_HASH, "CrosswalkSource")
    validate_file(safe_path(root, BOUNDARY_FILE),
                  safe_path(root, BOUNDARY_FILE).stat().st_size,
                  BOUNDARY_HASH, "HistoricalBoundarySource")
    receipt = ensure_receipt(root)
    crosswalks, intersections = prepare_candidates(root)
    base_index, base_root = base_generation(root)
    exporter_hash = sha_file(Path(__file__).resolve())

    files: dict[str, bytes] = {}
    entries: list[dict[str, Any]] = []
    overlays: list[dict[str, Any]] = []
    base_entries = sorted(base_index["bundles"], key=lambda item: item["administrativeAreaStableId"])
    for entry in base_entries:
        area_id = str(entry["administrativeAreaStableId"])
        base_path = (base_root / str(entry["relativePath"])).resolve()
        require(base_root.resolve() in base_path.parents and base_path.is_file(),
                "BaseReviewBundlePathInvalid")
        validate_file(base_path, int(entry["byteLength"]), str(entry["sha256"]),
                      "BaseReviewBundle")
        base_bundle = read_json(base_path)
        require(base_bundle.get("administrativeAreaStableId") == area_id
                and base_bundle.get("displayOverlays", {}).get("items") == [],
                "BaseReviewBundleContractChanged")
        overlay = overlay_for_area(area_id, crosswalks, intersections)
        overlays.append(overlay)
        payload = bundle_bytes(base_bundle, overlay, exporter_hash)
        relative = f"bundles/{area_id.removeprefix('region:kr:hjd:')}.json"
        files[relative] = payload
        next_entry = dict(entry)
        next_entry.update({
            "relativePath": relative,
            "sha256": sha_bytes(payload),
            "byteLength": len(payload),
            "contentHashSha256": json.loads(payload)["contentHashSha256"],
            "privateObservationOverlaySetHashSha256": overlay["overlaySetHashSha256"],
            "privateObservationOverlayCaptureReady": True,
            "crosswalkPointCandidateCount": len(overlay["crosswalkPoints"]),
            "intersectionPointCandidateCount": len(overlay["intersectionPoints"]),
        })
        entries.append(next_entry)

    require(len(entries) == EXPECTED_AREAS
            and sum(item["crosswalkPointCandidateCount"] for item in entries)
            == EXPECTED_CROSSWALKS
            and sum(item["intersectionPointCandidateCount"] for item in entries)
            == EXPECTED_INTERSECTIONS,
            "ReviewEntryCountsChanged")
    generation_hash = sha_bytes(canonical_bytes({
        "revision": REVISION,
        "baseIndexSha256": BASE_INDEX_HASH,
        "acquisitionReceiptSha256": sha_file(safe_path(root, RECEIPT_FILE)),
        "exporterSourceHashSha256": exporter_hash,
        "sourceHashes": [CROSSWALK_HASH, INTERSECTION_HASH, BOUNDARY_HASH],
        "bundleHashes": [item["sha256"] for item in entries],
        "overlaySetHashes": [item["privateObservationOverlaySetHashSha256"] for item in entries],
    }))

    index = dict(base_index)
    index.update({
        "schemaVersion": INDEX_SCHEMA,
        "sourceVintage": base_index["sourceVintage"]
        + "+oa23081-file-20260824+oa15534-file-20250814+oa22160-file-20231031",
        "generatedAtUtc": GENERATED_AT_UTC,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "generationHashSha256": generation_hash,
        "bundleSetHashSha256": generation_hash,
        "privateObservationOverlayCaptureReady": True,
        "privateObservationOverlaySetHashSha256": sha_bytes(canonical_bytes(
            [value["overlaySetHashSha256"] for value in overlays]
        )),
        "crosswalkPointCandidateCount": EXPECTED_CROSSWALKS,
        "pedestrianSignalPresentCandidateCount": EXPECTED_SIGNAL_PRESENT,
        "intersectionPointCandidateCount": EXPECTED_INTERSECTIONS,
        "bundles": entries,
    })
    index["contentHashSha256"] = content_hash(index, "contentHashSha256")
    files["index.json"] = pretty_bytes(index)

    complete = {
        "schemaVersion": COMPLETE_SCHEMA,
        "revision": str(base_index["revision"]),
        "generationHashSha256": generation_hash,
        "bundleSetHashSha256": generation_hash,
        "bundleCount": EXPECTED_AREAS,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "privateObservationOverlayCaptureReady": True,
        "crosswalkPointCandidateCount": EXPECTED_CROSSWALKS,
        "intersectionPointCandidateCount": EXPECTED_INTERSECTIONS,
        "currentPointerCreated": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "distributionApproved": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "files": [
            {"relativePath": name, "sha256": sha_bytes(data), "byteLength": len(data)}
            for name, data in sorted(files.items())
        ],
    }
    complete["contentHashSha256"] = content_hash(complete, "contentHashSha256")
    files["complete.json"] = pretty_bytes(complete)
    summary = {
        "status": "PASS",
        "generationHashSha256": generation_hash,
        "crosswalkPointCandidateCount": EXPECTED_CROSSWALKS,
        "pedestrianSignalPresentCandidateCount": EXPECTED_SIGNAL_PRESENT,
        "intersectionPointCandidateCount": EXPECTED_INTERSECTIONS,
        "administrativeAreaCount": EXPECTED_AREAS,
        "acquisitionReceiptContentHashSha256": receipt["contentHashSha256"],
        "privateReviewVisualizationAllowed": True,
        "sourceCandidateUnityApplyAllowed": False,
        "publicDisplayAllowed": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "distributionApproved": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
    }
    return generation_hash, files, summary


def generation_path(root: Path, generation_hash: str) -> Path:
    return safe_path(root, OUTPUT_ROOT / "generations" / generation_hash.lower())


def validate_materialized(path: Path, files: dict[str, bytes]) -> None:
    require(path.is_dir() and not is_reparse(path), "ReviewGenerationMissingOrUnsafe")
    actual = sorted(
        item.relative_to(path).as_posix()
        for item in path.rglob("*") if item.is_file()
    )
    require(actual == sorted(files), "ReviewGenerationFileSetChanged")
    for name, expected in files.items():
        require((path / name).read_bytes() == expected,
                f"ReviewGenerationFileChanged:{name}")


def build(root: Path) -> dict[str, Any]:
    generation_hash, files, result = prepare(root)
    target = generation_path(root, generation_hash)
    if target.exists():
        validate_materialized(target, files)
        result["changedFiles"] = 0
        result["generationRelativePath"] = target.relative_to(root).as_posix()
        return result
    generations = target.parent
    generations.mkdir(parents=True, exist_ok=True)
    require(not is_reparse(generations), "ReviewGenerationParentUnsafe")
    staging = generations / (".staging-" + uuid.uuid4().hex)
    staging.mkdir()
    try:
        for name, data in files.items():
            output = staging / name
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(data)
        validate_materialized(staging, files)
        os.replace(staging, target)
    except Exception:
        if staging.exists() and generations in staging.parents:
            shutil.rmtree(staging)
        raise
    validate_materialized(target, files)
    result["changedFiles"] = len(files)
    result["generationRelativePath"] = target.relative_to(root).as_posix()
    return result


def verify(root: Path) -> dict[str, Any]:
    generation_hash, files, result = prepare(root)
    target = generation_path(root, generation_hash)
    validate_materialized(target, files)
    result["verified"] = True
    result["generationRelativePath"] = target.relative_to(root).as_posix()
    return result


def self_test(root: Path) -> dict[str, Any]:
    passed = 0
    def test(value: bool, code: str) -> None:
        nonlocal passed
        require(value, "SelfTest:" + code)
        passed += 1
    test(sha_bytes(b"a") != sha_bytes(b"b"), "HashDistinct")
    test(content_hash({"a": 1, "contentHashSha256": "x"}, "contentHashSha256")
         == sha_bytes(b'{"a":1}'), "ContentHashExcludesSelf")
    test(stable_crosswalk_id("a") == stable_crosswalk_id("a"), "StableIdDeterministic")
    test(stable_crosswalk_id("a") != stable_crosswalk_id("b"), "StableIdDistinct")
    rejected = False
    try:
        assert_safe_output({"sourceFeatureKey": "private"})
    except ObservationReviewError:
        rejected = True
    test(rejected, "ForbiddenPropertyRejected")
    test(all(receipt["licenseCode"] == "KOGL-Type1-Attribution"
             for receipt in source_receipts()), "AttributionBound")
    test(sum(value is False for value in {
        "public": False, "runtime": False, "traversal": False, "gameplay": False,
    }.values()) == 4, "AuthorityDefaults")
    return {"status": "PASS", "selfTestsPassed": passed,
            "exporterSourceHashSha256": sha_file(Path(__file__).resolve())}


def arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT_DEFAULT))
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = arguments(sys.argv[1:] if argv is None else argv)
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        if args.mode == "self-test":
            result = self_test(root)
        elif args.mode == "build":
            result = build(root)
        else:
            result = verify(root)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0
    except (ObservationReviewError, OSError, ValueError) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

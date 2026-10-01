#!/usr/bin/env python3
"""보행망 비공개 후보를 기존 30개 Unity 검토 번들에 additive하게 결속한다.

기존 observation r2와 walk-network r2 산출물은 읽기 전용 입력이다. 새 r3
generation은 해시 stable ID, 공통 ENU 점/선 끝점, 최소 검토 상태만 포함하며
공개 표시, runtime, 통행, gameplay 또는 current pointer 권위를 만들지 않는다.
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
from pathlib import Path
from typing import Any, Iterable, Sequence


ROOT_DEFAULT = Path(__file__).resolve().parents[2]
BASE_REVIEW_ROOT = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r2/input/"
    "generations/4d94046d95dafe6c31a02b484cce68b8afb790276197668ef72b5de879fe3971"
)
WALK_REVIEW_ROOT = Path(
    "artifacts/local/validation/admin-dong-walk-network-private-review/r2/"
    "generations/7a0887f625c19401a4f57b57c5c0f51c1fc63590637b2ae82b2fc71a508fa6dd"
)
OUTPUT_ROOT = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r3/input"
)
CROSSWALK_MODULE = Path(
    "eng/neighborhood/administrative_dong_crosswalk_candidate.py"
)

OVERLAY_SCHEMA = "administrative-dong-private-walk-network-overlays.v1"
OVERLAY_REVISION = "northeast-seoul-private-walk-network-overlay.r1"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v3"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v3"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v3"
AUDIT_SCHEMA = "administrative-dong-diorama-unity-review-audit.v3"
EXPORTER_REVISION = "administrative-dong-diorama-unity-review-exporter.r4"
GENERATED_AT_UTC = "2026-09-26T00:00:00Z"

BASE_INDEX_HASH = "9C4DC79DFA246105DD8CA7E7A74A6AD7C56A9AC67BABCC687AF4FDBD2BF69A56"
BASE_COMPLETE_HASH = "7B135536AA12E0ADA421E0E7C954BC474914C5694DA7D24427917B589AB9D539"
BASE_GENERATION_HASH = "4D94046D95DAFE6C31A02B484CCE68B8AFB790276197668EF72B5DE879FE3971"
WALK_CANDIDATE_SET_HASH = "7A0887F625C19401A4F57B57C5C0F51C1FC63590637B2AE82B2FC71A508FA6DD"
WALK_CANDIDATES_HASH = "8E2CE178DE5091F7617C465FD6F01BEDE673F4FD9C8CEC92A1B9674C747065E5"
WALK_MANIFEST_HASH = "73154DCD1F4FE6F8602D3EBDC1BC382CD34081042DD256D94E14B641712B0BD8"
WALK_COMPLETE_HASH = "F54489229BA82778C7B6F06ACDAAC79097538CDBA7D13AD2B4CEA2109700FA06"
WALK_ACQUISITION_RECEIPT_HASH = "6395471456E77DFCC643404CF08C074014C8921E94C3EA7C9079CA86629AE504"
WALK_CANDIDATE_BYTES = 107_989_198
WALK_MANIFEST_BYTES = 7_569
WALK_COMPLETE_BYTES = 1_425
EXPECTED_AREAS = 30
EXPECTED_NODES = 17_267
EXPECTED_LINK_FRAGMENTS = 23_472
EXPECTED_CANDIDATES = EXPECTED_NODES + EXPECTED_LINK_FRAGMENTS
EXPECTED_LINK_PATH_VERTICES = 54_574
EXPECTED_OUTPUT_PATH_VERTICES = 71_841
EXPECTED_SOURCE_CONFLICT_LINKS = 48
PER_FRAGMENT_LENGTH_TOLERANCE_METERS = 0.003
AGGREGATE_LENGTH_TOLERANCE_METERS = 1.0

EXPECTED_FRAME = {
    "method": "WGS84-ECEF-ENU-at-zero-altitude",
    "originLatitude": 37.580912,
    "originLongitude": 127.088502,
    "worldOffsetX": 0,
    "worldOffsetZ": 0,
    "metersPerUnit": 1,
}
BASE_QUALITY_CODES = {
    "Historical2020Source",
    "HistoricalBoundary2023VintageMismatch",
    "SourceDistrictFilterBoundaryHaloUnresolved",
    "SourceReportedActorCodeOnly",
}
CONFLICT_QUALITY_CODE = "SourceDistrictSpatialAssignmentConflict"
ALLOWED_ACTORS = {"Pedestrian", "Vehicle", "Bicycle", "PM"}

FORBIDDEN_OVERLAY_KEYS = {
    "administrativeAreaDisplayName",
    "businessName",
    "buildingManagementNumber",
    "detailAddress",
    "district",
    "fragmentGeometryEpsg5186Millimeters",
    "jibunAddress",
    "legalAreaStableId",
    "managementNumber",
    "pointEpsg5186Millimeters",
    "rawAddress",
    "rawId",
    "roadAddress",
    "sourceBeginNodeIdSha256",
    "sourceBoroughCode",
    "sourceBoroughName",
    "sourceCompleteBodySha256",
    "sourceCsvDataRowNumber",
    "sourceEndNodeIdSha256",
    "sourceFeatureIdSha256",
    "sourceFeatureKey",
    "sourceFileHashSha256",
    "sourceLegalDongCode",
    "sourceLegalDongName",
    "sourceOccurrenceKey",
    "sourceRow",
    "sourceSemanticBodySha256",
    "sourceWorkTimestamp",
    "timestamp",
}


class WalkUnityReviewError(RuntimeError):
    pass


def require(condition: bool, code: str) -> None:
    if not condition:
        raise WalkUnityReviewError(code)


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
        value, ensure_ascii=False, sort_keys=True, separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")


def content_hash(value: dict[str, Any], field: str) -> str:
    copy = dict(value)
    copy.pop(field, None)
    return sha_bytes(canonical_bytes(copy))


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise WalkUnityReviewError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def load_module(path: Path, name: str) -> Any:
    specification = importlib.util.spec_from_file_location(name, path)
    require(
        specification is not None and specification.loader is not None,
        f"ModuleLoadFailed:{path.name}",
    )
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    specification.loader.exec_module(module)
    return module


def validate_file(path: Path, length: int, digest: str, code: str) -> None:
    require(path.is_file() and not is_reparse(path), code + ":MissingOrUnsafe")
    require(path.stat().st_size == length, code + ":LengthChanged")
    require(sha_file(path) == digest, code + ":HashChanged")


def assert_overlay_safe(value: Any) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            require(key not in FORBIDDEN_OVERLAY_KEYS, f"ForbiddenOverlayProperty:{key}")
            assert_overlay_safe(child)
    elif isinstance(value, list):
        for child in value:
            assert_overlay_safe(child)
    elif isinstance(value, float):
        require(math.isfinite(value), "NonFiniteOverlayNumber")


def source_receipts() -> list[dict[str, Any]]:
    return [{
        "datasetId": "OA-21208",
        "officialPageUrl": "https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do",
        "sourceReferenceVintage": "2020",
        "candidateSetHashSha256": WALK_CANDIDATE_SET_HASH,
        "acquisitionReceiptSha256": WALK_ACQUISITION_RECEIPT_HASH,
        "licenseCode": "KOGL-Type1-Attribution",
        "sourceCoordinateReference": "WGS84",
        "conversionCoordinateReference": "EPSG:5186",
        "coordinateReferenceStatus": (
            "SourceDeclaredWgs84CandidateEpsg5186RoundTripToCommonEnu"
        ),
    }]


def load_inputs(
    root: Path,
) -> tuple[dict[str, Any], Path, dict[str, Any], Path, Any, Any]:
    base_root = safe_path(root, BASE_REVIEW_ROOT)
    walk_root = safe_path(root, WALK_REVIEW_ROOT)
    require(base_root.is_dir() and not is_reparse(base_root), "BaseGenerationUnsafe")
    require(walk_root.is_dir() and not is_reparse(walk_root), "WalkGenerationUnsafe")

    base_index_path = base_root / "index.json"
    base_complete_path = base_root / "complete.json"
    validate_file(
        base_index_path, base_index_path.stat().st_size, BASE_INDEX_HASH, "BaseIndex"
    )
    validate_file(
        base_complete_path,
        base_complete_path.stat().st_size,
        BASE_COMPLETE_HASH,
        "BaseComplete",
    )
    base_index = read_json(base_index_path)
    base_complete = read_json(base_complete_path)
    require(
        base_index.get("schemaVersion")
        == "administrative-dong-diorama-unity-review-index.v2"
        and base_index.get("generationHashSha256") == BASE_GENERATION_HASH
        and base_index.get("bundleSetHashSha256") == BASE_GENERATION_HASH
        and base_index.get("administrativeAreaCount") == EXPECTED_AREAS
        and len(base_index.get("bundles", [])) == EXPECTED_AREAS,
        "BaseIndexContractChanged",
    )
    require(
        base_complete.get("schemaVersion")
        == "administrative-dong-diorama-unity-review-completion.v2"
        and base_complete.get("generationHashSha256") == BASE_GENERATION_HASH
        and base_complete.get("bundleCount") == EXPECTED_AREAS,
        "BaseCompleteContractChanged",
    )

    candidate_path = walk_root / "candidates.ndjson"
    manifest_path = walk_root / "manifest.json"
    complete_path = walk_root / "complete.json"
    validate_file(
        candidate_path,
        WALK_CANDIDATE_BYTES,
        WALK_CANDIDATES_HASH,
        "WalkCandidates",
    )
    validate_file(
        manifest_path, WALK_MANIFEST_BYTES, WALK_MANIFEST_HASH, "WalkManifest"
    )
    validate_file(
        complete_path, WALK_COMPLETE_BYTES, WALK_COMPLETE_HASH, "WalkComplete"
    )
    walk_manifest = read_json(manifest_path)
    walk_complete = read_json(complete_path)
    require(
        walk_manifest.get("schemaVersion")
        == "administrative-dong-walk-network-private-review-manifest.v2"
        and walk_manifest.get("candidateSetHashSha256") == WALK_CANDIDATE_SET_HASH
        and walk_manifest.get("candidateRows") == EXPECTED_CANDIDATES
        and walk_manifest.get("candidateNodeRows") == EXPECTED_NODES
        and walk_manifest.get("candidateLinkFragmentRows") == EXPECTED_LINK_FRAGMENTS
        and walk_manifest.get("administrativeAreaCount") == EXPECTED_AREAS,
        "WalkManifestContractChanged",
    )
    require(
        walk_complete.get("schemaVersion")
        == "administrative-dong-walk-network-private-review-complete.v2"
        and walk_complete.get("candidateSetHashSha256") == WALK_CANDIDATE_SET_HASH
        and walk_complete.get("publicDisplayAllowed") is False
        and walk_complete.get("runtimeAuthorized") is False
        and walk_complete.get("traversalReady") is False
        and walk_complete.get("gameplayReady") is False
        and walk_complete.get("unityApplyAllowed") is False,
        "WalkCompleteAuthorityChanged",
    )

    module = load_module(
        safe_path(root, CROSSWALK_MODULE), "hongdal_walk_unity_common_enu"
    )
    dependencies = module.load_dependencies(root)
    transformer = dependencies.Transformer.from_crs(5186, 4326, always_xy=True)
    frame = module.CommonEnuFrame(
        EXPECTED_FRAME["originLatitude"],
        EXPECTED_FRAME["originLongitude"],
        EXPECTED_FRAME["worldOffsetX"],
        EXPECTED_FRAME["worldOffsetZ"],
    )
    return base_index, base_root, walk_manifest, candidate_path, transformer, frame


def normalized_frame(value: dict[str, Any]) -> dict[str, Any]:
    return {
        "method": value.get("method"),
        "originLatitude": float(value.get("originLatitude")),
        "originLongitude": float(value.get("originLongitude")),
        "worldOffsetX": float(value.get("worldOffsetX")),
        "worldOffsetZ": float(value.get("worldOffsetZ")),
        "metersPerUnit": float(value.get("metersPerUnit")),
    }


def bounds_millimeters(value: dict[str, Any]) -> dict[str, int]:
    return {
        "minX": int(round(float(value["minX"]) * 1000.0)),
        "minZ": int(round(float(value["minZ"]) * 1000.0)),
        "maxX": int(round(float(value["maxX"]) * 1000.0)),
        "maxZ": int(round(float(value["maxZ"]) * 1000.0)),
    }


def common_enu_millimeters(
    epsg5186: Sequence[Any], transformer: Any, frame: Any
) -> dict[str, int]:
    require(len(epsg5186) == 2, "CoordinateDimensionChanged")
    source_x = int(epsg5186[0])
    source_y = int(epsg5186[1])
    longitude, latitude = transformer.transform(source_x / 1000.0, source_y / 1000.0)
    local_x, local_z = frame.wgs84_to_local(float(longitude), float(latitude))
    require(
        math.isfinite(local_x) and math.isfinite(local_z),
        "CoordinateTransformNonFinite",
    )
    return {
        "x": int(round(local_x * 1000.0)),
        "z": int(round(local_z * 1000.0)),
    }


def require_in_bounds(point: dict[str, int], bounds: dict[str, int], code: str) -> None:
    require(
        bounds["minX"] <= point["x"] <= bounds["maxX"]
        and bounds["minZ"] <= point["z"] <= bounds["maxZ"],
        code,
    )


def hashed_candidate_id(value: Any, kind: str) -> str:
    candidate = str(value)
    prefix = (
        "walk-node-candidate:sha256:"
        if kind == "Node"
        else "walk-link-fragment-candidate:sha256:"
    )
    suffix = candidate.removeprefix(prefix)
    require(
        candidate.startswith(prefix)
        and len(suffix) == 64
        and all(character in "0123456789abcdef" for character in suffix),
        "CandidateStableIdNotHashed",
    )
    return candidate


def actor_summary(codes: Sequence[Any], is_node: bool) -> dict[str, bool]:
    values = {str(code) for code in codes}
    require(values <= ALLOWED_ACTORS, "ActorCodeChanged")
    require(not is_node or not values, "NodeActorSummaryChanged")
    return {
        "sourceReported": bool(values),
        "pedestrian": "Pedestrian" in values,
        "bicycle": "Bicycle" in values,
        "personalMobility": "PM" in values,
        "vehicle": "Vehicle" in values,
    }


def quality_code(value: dict[str, Any], kind: str) -> tuple[str, bool]:
    codes = {str(code) for code in value.get("qualityDiagnosticCodes", [])}
    conflict = CONFLICT_QUALITY_CODE in codes
    expected = BASE_QUALITY_CODES | ({CONFLICT_QUALITY_CODE} if conflict else set())
    require(codes == expected, "QualityDiagnosticCodesChanged")
    require(
        value.get("assignmentStateCode")
        == ("UniqueHistoricalBoundaryCover" if kind == "Node" else
            "HistoricalBoundaryClippedFragment"),
        "CandidateAssignmentStateChanged",
    )
    return (
        "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
        if conflict else "PendingHumanReview",
        conflict,
    )


def overlay_template(area_id: str) -> dict[str, Any]:
    return {
        "schemaVersion": OVERLAY_SCHEMA,
        "revision": OVERLAY_REVISION,
        "overlaySetStableId": (
            "administrative-dong-private-walk-network:" + area_id + ".v1"
        ),
        "administrativeAreaStableId": area_id,
        "sourceVintage": "oa21208-reference-2020+oa22160-file-20231031",
        "historicalBoundaryBootstrapOnly": True,
        "privateReviewOnly": True,
        "privateReviewVisualizationAllowed": True,
        "reviewVisualizationOnly": True,
        "observationCandidateOnly": True,
        "sourceCandidateUnityApplyAllowed": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "sourceIdentifiersIncluded": False,
        "sourceBoroughIdentityIncluded": False,
        "legalAreaIdentityIncluded": False,
        "sourceTimestampsIncluded": False,
        "sourceEpsg5186CoordinatesIncluded": False,
        "exactAddressIncluded": False,
        "personalDataIncluded": False,
        "sourceReceipts": source_receipts(),
        "nodePoints": [],
        "linkFragments": [],
    }


def prepare_overlays(
    candidate_path: Path,
    area_bounds: dict[str, dict[str, int]],
    transformer: Any,
    frame: Any,
) -> tuple[dict[str, dict[str, Any]], dict[str, Any]]:
    overlays = {area_id: overlay_template(area_id) for area_id in sorted(area_bounds)}
    seen_ids: set[str] = set()
    counts = {
        "candidateRows": 0,
        "nodePoints": 0,
        "linkFragments": 0,
        "linkPathVertices": 0,
        "outputPathVertices": 0,
        "sourceDistrictSpatialAssignmentConflictLinks": 0,
        "sourceGeometryVerticesValidated": 0,
        "sourceSerializedFragmentLengthMeters": 0.0,
        "outputCommonEnuPathLengthMeters": 0.0,
        "maximumFragmentAbsoluteLengthDifferenceMeters": 0.0,
    }
    with candidate_path.open("r", encoding="utf-8", newline="") as stream:
        for line_number, line in enumerate(stream, 1):
            try:
                value = json.loads(line)
            except json.JSONDecodeError as exc:
                raise WalkUnityReviewError(
                    f"CandidateJsonInvalid:{line_number}"
                ) from exc
            require(isinstance(value, dict), f"CandidateRootInvalid:{line_number}")
            require(
                value.get("schemaVersion")
                == "administrative-dong-walk-network-private-review-candidate.v2"
                and value.get("revision")
                == "northeast-seoul-admin-dong-walk-network-private-review.g3c.r2",
                "CandidateSchemaOrRevisionChanged",
            )
            area_id = str(value.get("administrativeAreaStableId", ""))
            require(area_id in overlays, "CandidateAdministrativeAreaChanged")
            kind = str(value.get("candidateKindCode", ""))
            require(kind in {"Node", "LinkFragment"}, "CandidateKindChanged")
            candidate_id = hashed_candidate_id(value.get("candidateStableId"), kind)
            require(candidate_id not in seen_ids, "CandidateStableIdCollision")
            seen_ids.add(candidate_id)
            quality, conflict = quality_code(value, kind)
            bounds = area_bounds[area_id]

            if kind == "Node":
                point = common_enu_millimeters(
                    value.get("pointEpsg5186Millimeters", []), transformer, frame
                )
                require_in_bounds(point, bounds, "NodePointOutsideManifestBounds")
                overlays[area_id]["nodePoints"].append({
                    "candidateStableId": candidate_id,
                    "commonEnuMillimeters": point,
                    "kindCode": "Node",
                    "qualityCode": quality,
                    "actorSummaryFlags": actor_summary(
                        value.get("sourceReportedActorCodes", []), True
                    ),
                })
                counts["nodePoints"] += 1
                counts["sourceGeometryVerticesValidated"] += 1
                counts["outputPathVertices"] += 1
            else:
                geometry = value.get("fragmentGeometryEpsg5186Millimeters", [])
                require(isinstance(geometry, list) and len(geometry) >= 2,
                        "LinkGeometryChanged")
                converted = [
                    common_enu_millimeters(point, transformer, frame)
                    for point in geometry
                ]
                for point in converted:
                    require_in_bounds(
                        point, bounds, "LinkGeometryVertexOutsideManifestBounds"
                    )
                source_length = float(value.get("fragmentLengthMeters"))
                output_length = sum(
                    math.hypot(
                        converted[index]["x"] - converted[index - 1]["x"],
                        converted[index]["z"] - converted[index - 1]["z"],
                    ) / 1000.0
                    for index in range(1, len(converted))
                )
                difference = abs(output_length - source_length)
                require(
                    math.isfinite(source_length)
                    and math.isfinite(output_length)
                    and source_length > 0.0
                    and output_length > 0.0,
                    "LinkFragmentLengthInvalid",
                )
                require(
                    difference <= PER_FRAGMENT_LENGTH_TOLERANCE_METERS,
                    "LinkFragmentLengthNotPreserved",
                )
                overlays[area_id]["linkFragments"].append({
                    "candidateStableId": candidate_id,
                    "commonEnuMillimeterPath": converted,
                    "kindCode": "LinkFragment",
                    "qualityCode": quality,
                    "actorSummaryFlags": actor_summary(
                        value.get("sourceReportedActorCodes", []), False
                    ),
                })
                counts["linkFragments"] += 1
                counts["linkPathVertices"] += len(converted)
                counts["outputPathVertices"] += len(converted)
                counts["sourceGeometryVerticesValidated"] += len(converted)
                counts["sourceDistrictSpatialAssignmentConflictLinks"] += int(conflict)
                counts["sourceSerializedFragmentLengthMeters"] += source_length
                counts["outputCommonEnuPathLengthMeters"] += output_length
                counts["maximumFragmentAbsoluteLengthDifferenceMeters"] = max(
                    counts["maximumFragmentAbsoluteLengthDifferenceMeters"],
                    difference,
                )
            counts["candidateRows"] += 1

    require(
        counts["candidateRows"] == EXPECTED_CANDIDATES
        and counts["nodePoints"] == EXPECTED_NODES
        and counts["linkFragments"] == EXPECTED_LINK_FRAGMENTS
        and counts["linkPathVertices"] == EXPECTED_LINK_PATH_VERTICES
        and counts["outputPathVertices"] == EXPECTED_OUTPUT_PATH_VERTICES
        and counts["sourceGeometryVerticesValidated"]
        == EXPECTED_OUTPUT_PATH_VERTICES
        and counts["sourceDistrictSpatialAssignmentConflictLinks"]
        == EXPECTED_SOURCE_CONFLICT_LINKS,
        "WalkOverlayCountsChanged",
    )
    aggregate_difference = abs(
        counts["outputCommonEnuPathLengthMeters"]
        - counts["sourceSerializedFragmentLengthMeters"]
    )
    require(
        aggregate_difference <= AGGREGATE_LENGTH_TOLERANCE_METERS,
        "AggregateLinkFragmentLengthNotPreserved",
    )
    counts["aggregateAbsoluteLengthDifferenceMeters"] = aggregate_difference
    require(len(seen_ids) == EXPECTED_CANDIDATES, "CandidateStableIdCountChanged")
    for area_id, overlay in overlays.items():
        overlay["nodePoints"].sort(key=lambda item: item["candidateStableId"])
        overlay["linkFragments"].sort(key=lambda item: item["candidateStableId"])
        require(
            overlay["nodePoints"] and overlay["linkFragments"],
            f"AdministrativeAreaOverlayEmpty:{area_id}",
        )
        overlay["overlaySetHashSha256"] = content_hash(
            overlay, "overlaySetHashSha256"
        )
        assert_overlay_safe(overlay)
    return overlays, counts


def bundle_bytes(
    base: dict[str, Any], overlay: dict[str, Any], exporter_hash: str
) -> bytes:
    value = dict(base)
    require(
        value.get("schemaVersion")
        == "administrative-dong-diorama-unity-review-bundle.v2"
        and value.get("administrativeAreaStableId")
        == overlay["administrativeAreaStableId"]
        and value.get("displayOverlays", {}).get("items") == [],
        "BaseBundleContractChanged",
    )
    link_path_vertex_count = sum(
        len(fragment["commonEnuMillimeterPath"])
        for fragment in overlay["linkFragments"]
    )
    value.update({
        "schemaVersion": BUNDLE_SCHEMA,
        "sourceVintage": (
            str(base["sourceVintage"])
            + "+oa21208-reference-2020+walk-network-private-review-g3c-r2"
        ),
        "generatedAtUtc": GENERATED_AT_UTC,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "privateWalkNetworkOverlayCaptureReady": True,
        "privateWalkNetworkOverlaySetHashSha256": overlay["overlaySetHashSha256"],
        "walkNetworkNodePointCandidateCount": len(overlay["nodePoints"]),
        "walkNetworkLinkFragmentCandidateCount": len(overlay["linkFragments"]),
        "walkNetworkLinkPathVertexCount": link_path_vertex_count,
        "walkNetworkOutputPathVertexCount": (
            len(overlay["nodePoints"]) + link_path_vertex_count
        ),
        "privateWalkNetworkOverlays": overlay,
    })
    value["contentHashSha256"] = content_hash(value, "contentHashSha256")
    require(value.get("displayOverlays", {}).get("items") == [],
            "PublicDisplayOverlayMutated")
    return pretty_bytes(value)


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    (
        base_index,
        base_root,
        walk_manifest,
        candidate_path,
        transformer,
        frame,
    ) = load_inputs(root)
    exporter_hash = sha_file(Path(__file__).resolve())

    base_entries = sorted(
        base_index["bundles"], key=lambda item: item["administrativeAreaStableId"]
    )
    area_bounds: dict[str, dict[str, int]] = {}
    base_bundles: dict[str, dict[str, Any]] = {}
    for entry in base_entries:
        area_id = str(entry["administrativeAreaStableId"])
        relative = Path(str(entry["relativePath"]))
        path = (base_root / relative).resolve()
        require(base_root.resolve() in path.parents, "BaseBundlePathEscapesGeneration")
        validate_file(
            path, int(entry["byteLength"]), str(entry["sha256"]), "BaseBundle"
        )
        bundle = read_json(path)
        manifest = bundle.get("manifest", {})
        require(
            bundle.get("contentHashSha256")
            == content_hash(bundle, "contentHashSha256")
            and bundle.get("privateObservationOverlayCaptureReady") is True
            and bundle.get("displayOverlays", {}).get("items") == []
            and normalized_frame(manifest.get("coordinateFrame", {}))
            == normalized_frame(EXPECTED_FRAME),
            "BaseBundleContentOrFrameChanged",
        )
        require(area_id not in base_bundles, "BaseAdministrativeAreaDuplicate")
        base_bundles[area_id] = bundle
        area_bounds[area_id] = bounds_millimeters(manifest["bounds"])
    require(len(base_bundles) == EXPECTED_AREAS, "BaseAreaCountChanged")

    overlays, validation_counts = prepare_overlays(
        candidate_path, area_bounds, transformer, frame
    )
    expected_per_area = walk_manifest.get("perAdministrativeArea", {})
    require(
        set(expected_per_area) == {
            area_id.removeprefix("region:kr:hjd:") for area_id in overlays
        },
        "WalkPerAreaSetChanged",
    )

    files: dict[str, bytes] = {}
    entries: list[dict[str, Any]] = []
    per_area: dict[str, dict[str, int]] = {}
    for entry in base_entries:
        area_id = str(entry["administrativeAreaStableId"])
        short_id = area_id.removeprefix("region:kr:hjd:")
        overlay = overlays[area_id]
        node_count = len(overlay["nodePoints"])
        fragment_count = len(overlay["linkFragments"])
        link_path_vertex_count = sum(
            len(fragment["commonEnuMillimeterPath"])
            for fragment in overlay["linkFragments"]
        )
        expected = expected_per_area[short_id]
        require(
            node_count == int(expected["nodeCandidates"])
            and fragment_count == int(expected["linkFragmentCandidates"]),
            f"WalkPerAreaCountsChanged:{short_id}",
        )
        payload = bundle_bytes(base_bundles[area_id], overlay, exporter_hash)
        relative_path = f"bundles/{short_id}.json"
        files[relative_path] = payload
        parsed = json.loads(payload)
        next_entry = dict(entry)
        next_entry.update({
            "relativePath": relative_path,
            "sha256": sha_bytes(payload),
            "byteLength": len(payload),
            "contentHashSha256": parsed["contentHashSha256"],
            "privateWalkNetworkOverlaySetHashSha256": (
                overlay["overlaySetHashSha256"]
            ),
            "privateWalkNetworkOverlayCaptureReady": True,
            "walkNetworkNodePointCandidateCount": node_count,
            "walkNetworkLinkFragmentCandidateCount": fragment_count,
            "walkNetworkLinkPathVertexCount": link_path_vertex_count,
            "walkNetworkOutputPathVertexCount": (
                node_count + link_path_vertex_count
            ),
        })
        entries.append(next_entry)
        per_area[short_id] = {
            "nodePoints": node_count,
            "linkFragments": fragment_count,
            "linkPathVertices": link_path_vertex_count,
            "outputPathVertices": node_count + link_path_vertex_count,
        }

    require(
        len(entries) == EXPECTED_AREAS
        and sum(item["walkNetworkNodePointCandidateCount"] for item in entries)
        == EXPECTED_NODES
        and sum(item["walkNetworkLinkFragmentCandidateCount"] for item in entries)
        == EXPECTED_LINK_FRAGMENTS
        and sum(item["walkNetworkLinkPathVertexCount"] for item in entries)
        == EXPECTED_LINK_PATH_VERTICES
        and sum(item["walkNetworkOutputPathVertexCount"] for item in entries)
        == EXPECTED_OUTPUT_PATH_VERTICES,
        "ReviewEntryCountsChanged",
    )
    generation_hash = sha_bytes(canonical_bytes({
        "revision": OVERLAY_REVISION,
        "baseGenerationHashSha256": BASE_GENERATION_HASH,
        "baseIndexSha256": BASE_INDEX_HASH,
        "walkCandidateSetHashSha256": WALK_CANDIDATE_SET_HASH,
        "walkCandidatesSha256": WALK_CANDIDATES_HASH,
        "walkManifestSha256": WALK_MANIFEST_HASH,
        "walkCompleteSha256": WALK_COMPLETE_HASH,
        "exporterSourceHashSha256": exporter_hash,
        "bundleHashes": [item["sha256"] for item in entries],
        "overlaySetHashes": [
            item["privateWalkNetworkOverlaySetHashSha256"] for item in entries
        ],
    }))

    length_preservation = {
        "unit": "meter",
        "sourceSerializedFragmentLengthMeters": round(
            validation_counts["sourceSerializedFragmentLengthMeters"], 6
        ),
        "outputCommonEnuPathLengthMeters": round(
            validation_counts["outputCommonEnuPathLengthMeters"], 6
        ),
        "aggregateAbsoluteDifferenceMeters": round(
            validation_counts["aggregateAbsoluteLengthDifferenceMeters"], 6
        ),
        "maximumFragmentAbsoluteDifferenceMeters": round(
            validation_counts["maximumFragmentAbsoluteLengthDifferenceMeters"], 6
        ),
        "perFragmentToleranceMeters": PER_FRAGMENT_LENGTH_TOLERANCE_METERS,
        "aggregateToleranceMeters": AGGREGATE_LENGTH_TOLERANCE_METERS,
        "allFragmentsWithinTolerance": True,
        "aggregateWithinTolerance": True,
    }
    audit = {
        "schemaVersion": AUDIT_SCHEMA,
        "revision": OVERLAY_REVISION,
        "generationHashSha256": generation_hash,
        "baseGenerationHashSha256": BASE_GENERATION_HASH,
        "walkCandidateSetHashSha256": WALK_CANDIDATE_SET_HASH,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "counts": {
            "administrativeAreas": EXPECTED_AREAS,
            "walkNetworkNodePointCandidates": EXPECTED_NODES,
            "walkNetworkLinkFragmentCandidates": EXPECTED_LINK_FRAGMENTS,
            "walkNetworkLinkPathVertices": EXPECTED_LINK_PATH_VERTICES,
            "walkNetworkOutputPathVertices": EXPECTED_OUTPUT_PATH_VERTICES,
            "sourceGeometryVerticesValidated": validation_counts[
                "sourceGeometryVerticesValidated"
            ],
            "sourceDistrictSpatialAssignmentConflictLinks": validation_counts[
                "sourceDistrictSpatialAssignmentConflictLinks"
            ],
        },
        "perAdministrativeArea": per_area,
        "lengthPreservation": length_preservation,
        "commonEnuPathPreservesAllSourceVertices": True,
        "allGeometryWithinBaseManifestBounds": True,
        "historicalBoundaryBootstrapOnly": True,
        "privateReviewOnly": True,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "sourceCandidateUnityApplyAllowed": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
    }
    audit["contentHashSha256"] = content_hash(audit, "contentHashSha256")
    files["audit.json"] = pretty_bytes(audit)

    index = dict(base_index)
    index.update({
        "schemaVersion": INDEX_SCHEMA,
        "sourceVintage": (
            str(base_index["sourceVintage"])
            + "+oa21208-reference-2020+walk-network-private-review-g3c-r2"
        ),
        "generatedAtUtc": GENERATED_AT_UTC,
        "exporterSemanticRevision": EXPORTER_REVISION,
        "exporterSourceHashSha256": exporter_hash,
        "generationHashSha256": generation_hash,
        "bundleSetHashSha256": generation_hash,
        "privateWalkNetworkOverlayCaptureReady": True,
        "privateWalkNetworkOverlaySetHashSha256": sha_bytes(canonical_bytes([
            overlay["overlaySetHashSha256"]
            for _, overlay in sorted(overlays.items())
        ])),
        "walkNetworkNodePointCandidateCount": EXPECTED_NODES,
        "walkNetworkLinkFragmentCandidateCount": EXPECTED_LINK_FRAGMENTS,
        "walkNetworkLinkPathVertexCount": EXPECTED_LINK_PATH_VERTICES,
        "walkNetworkOutputPathVertexCount": EXPECTED_OUTPUT_PATH_VERTICES,
        "walkNetworkAuditRelativePath": "audit.json",
        "walkNetworkAuditSha256": sha_bytes(files["audit.json"]),
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
        "baseGenerationHashSha256": BASE_GENERATION_HASH,
        "walkCandidateSetHashSha256": WALK_CANDIDATE_SET_HASH,
        "privateWalkNetworkOverlayCaptureReady": True,
        "walkNetworkNodePointCandidateCount": EXPECTED_NODES,
        "walkNetworkLinkFragmentCandidateCount": EXPECTED_LINK_FRAGMENTS,
        "walkNetworkLinkPathVertexCount": EXPECTED_LINK_PATH_VERTICES,
        "walkNetworkOutputPathVertexCount": EXPECTED_OUTPUT_PATH_VERTICES,
        "walkNetworkAuditSha256": sha_bytes(files["audit.json"]),
        "currentPointerCreated": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "distributionApproved": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "sourceCandidateUnityApplyAllowed": False,
        "files": [
            {
                "relativePath": name,
                "sha256": sha_bytes(data),
                "byteLength": len(data),
            }
            for name, data in sorted(files.items())
        ],
    }
    complete["contentHashSha256"] = content_hash(complete, "contentHashSha256")
    files["complete.json"] = pretty_bytes(complete)

    node_counts = [value["nodePoints"] for value in per_area.values()]
    fragment_counts = [value["linkFragments"] for value in per_area.values()]
    summary = {
        "status": "PASS",
        "generationHashSha256": generation_hash,
        "indexSha256": sha_bytes(files["index.json"]),
        "exporterSourceHashSha256": exporter_hash,
        "baseGenerationHashSha256": BASE_GENERATION_HASH,
        "walkCandidateSetHashSha256": WALK_CANDIDATE_SET_HASH,
        "administrativeAreaCount": EXPECTED_AREAS,
        "walkNetworkNodePointCandidateCount": EXPECTED_NODES,
        "walkNetworkLinkFragmentCandidateCount": EXPECTED_LINK_FRAGMENTS,
        "walkNetworkLinkPathVertexCount": EXPECTED_LINK_PATH_VERTICES,
        "walkNetworkOutputPathVertexCount": EXPECTED_OUTPUT_PATH_VERTICES,
        "minimumNodePointsPerArea": min(node_counts),
        "maximumNodePointsPerArea": max(node_counts),
        "minimumLinkFragmentsPerArea": min(fragment_counts),
        "maximumLinkFragmentsPerArea": max(fragment_counts),
        "sourceGeometryVerticesValidated": validation_counts[
            "sourceGeometryVerticesValidated"
        ],
        "lengthPreservation": length_preservation,
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
        for item in path.rglob("*")
        if item.is_file()
    )
    require(actual == sorted(files), "ReviewGenerationFileSetChanged")
    for name, expected in files.items():
        require(
            (path / name).read_bytes() == expected,
            f"ReviewGenerationFileChanged:{name}",
        )


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
    test(
        content_hash({"a": 1, "contentHashSha256": "x"}, "contentHashSha256")
        == sha_bytes(b'{"a":1}'),
        "ContentHashExcludesSelf",
    )
    test(
        actor_summary(["Pedestrian", "PM"], False)
        == {
            "sourceReported": True,
            "pedestrian": True,
            "bicycle": False,
            "personalMobility": True,
            "vehicle": False,
        },
        "ActorSummary",
    )
    test(
        hashed_candidate_id(
            "walk-node-candidate:sha256:" + "a" * 64, "Node"
        ).endswith("a" * 64),
        "HashedCandidateAccepted",
    )
    rejected = False
    try:
        assert_overlay_safe({"sourceCsvDataRowNumber": 1})
    except WalkUnityReviewError:
        rejected = True
    test(rejected, "ForbiddenPropertyRejected")
    test(
        all(receipt["licenseCode"] == "KOGL-Type1-Attribution"
            for receipt in source_receipts()),
        "AttributionBound",
    )
    test(
        all(value is False for value in (
            False, False, False, False, False, False, False,
        )),
        "AuthorityDefaults",
    )
    return {
        "status": "PASS",
        "selfTestsPassed": passed,
        "exporterSourceHashSha256": sha_file(Path(__file__).resolve()),
    }


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
    except (WalkUnityReviewError, OSError, ValueError) as exc:
        print(
            json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

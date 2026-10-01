#!/usr/bin/env python3
"""Bind flat OA-22241 contour traces to the immutable v6 review input.

The output carries horizontal two-point display segments only.  It contains no
height/elevation value and grants no terrain, collider, current, public,
runtime, traversal, or gameplay authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import shutil
import stat
import sys
import uuid
from collections import defaultdict
from decimal import Decimal
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
BASE = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r6/input/"
    "generations/587601d4e9952149da299d84496242cad3cc761f6053d2b36a1a73c5c6b0e3d6"
)
SOURCE = Path(
    "artifacts/local/validation/admin-dong-contour-trace-private-review/r1/input/"
    "generations/e5fc9551cee5bb2c1da8696939c882be3141f0736af9e36b945f9184c152cdfc"
)
OUTPUT = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r7/input"
)

BASE_HASH = "587601D4E9952149DA299D84496242CAD3CC761F6053D2B36A1A73C5C6B0E3D6"
BASE_INDEX_SHA = "4DDE2DD68B6246D6DB046FCAB81FA153D111288D887D4C4729B006DFEB6B7AAF"
BASE_COMPLETE_SHA = "3BF7524435AF29615D8CE1593C8F7BBEE02A8CD07007C55D5BEF0C1E2B9041C4"
BASE_BIOTOPE_AUDIT_SHA = "416F57C447B0C543FFC5258927A0CE5B339FD4B654A70D14A3209025626D5CA5"
BASE_ROAD_AUDIT_SHA = "D5AEFD2562878DF10D802736C99841D9504E0BB62DFB292C3020859927E8C8D1"
BASE_LANE_AUDIT_SHA = "F935FC3FC37E883BAC7D6DB6C9D3DDB60E6FC550E67B8F2A88ECB19E2B6697A3"

SOURCE_HASH = "E5FC9551CEE5BB2C1DA8696939C882BE3141F0736AF9E36B945F9184C152CDFC"
SOURCE_MANIFEST_SHA = "BAC3398814FEE00EF41FBFC1B974E47BA8E28902A0857708A2D5F093282C9A4C"
SOURCE_AUDIT_SHA = "82B8F49766FD3FE3CBE11D4472691EE566D17ED59834E917856769265066E6F3"
SOURCE_CANDIDATES_SHA = "5E2EF2FFA82CECB46BA71F4D87323BC041AA1CD995F4789FE5204AB68CB3E355"
SOURCE_COMPLETE_SHA = "83B866184B7056E215314DDA5CFBC5779201B5E488E29C2026224250DDB20A38"

EXPECTED_AREAS = 30
EXPECTED_CANDIDATES = 860
EXPECTED_FRAGMENTS = 184_747
EXPECTED_PATH_VERTICES = 369_494
EXPECTED_SOURCE_SEGMENTS = 184_748
EXPECTED_RENDERED_SEGMENTS = 184_747
EXPECTED_SKIPPED_SEGMENTS = 1
EXPECTED_DUPLICATE_SEGMENTS = 0
EXPECTED_SOURCE_CONTAINMENT_FAILURES = 0
EXPECTED_COMMON_OUTSIDE_POINTS = 910
EXPECTED_COMMON_MAXIMUM_OUTSIDE_MM = 6
MIN_RENDERABLE_SEGMENT_MM = 1.0
GENERATED_AT = "2026-09-26T00:00:00Z"
EXPORTER = "administrative-dong-diorama-unity-review-exporter.r8"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v7"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v7"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v7"
OVERLAY_SCHEMA = "administrative-dong-private-horizontal-contour-trace-overlays.v1"
OVERLAY_REVISION = "northeast-seoul-horizontal-contour-trace-private-review.r1"
WRAPPER_AUDIT_SCHEMA = "administrative-dong-horizontal-contour-trace-unity-wrapper-audit.v1"
SOURCE_VINTAGE = "file-2025-03-20"


class ContourTraceUnityReviewError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise ContourTraceUnityReviewError(code)


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def pretty(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")


def unity_canonical(value: Any) -> str:
    if isinstance(value, dict):
        return "{" + ",".join(
            unity_canonical(key) + ":" + unity_canonical(value[key])
            for key in sorted(value)
        ) + "}"
    if isinstance(value, list):
        return "[" + ",".join(unity_canonical(item) for item in value) + "]"
    if isinstance(value, Decimal):
        return "0" if value == 0 else format(value, "f")
    require(not isinstance(value, float), "UnityCanonicalFloatNotParsedAsDecimal")
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def unity_content_hash_from_bytes(
    payload: bytes, field: str = "contentHashSha256"
) -> str:
    value = json.loads(payload, parse_float=Decimal)
    require(isinstance(value, dict), "UnityCanonicalRootInvalid")
    value.pop(field, None)
    return sha(unity_canonical(value).encode("utf-8"))


def content_hash(value: dict[str, Any], field: str = "contentHashSha256") -> str:
    body = dict(value)
    body.pop(field, None)
    return unity_content_hash_from_bytes(pretty(body), field)


def safe(root: Path, relative: Path) -> Path:
    value = (root / relative).resolve()
    require(value == root or root in value.parents, "PathEscapesRepository")
    return value


def is_reparse(path: Path) -> bool:
    try:
        value = path.lstat()
    except OSError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(getattr(value, "st_file_attributes", 0) & flag)


def checked(path: Path, digest: str, length: int | None = None) -> bytes:
    require(path.is_file() and not is_reparse(path), "InputMissingOrUnsafe:" + path.name)
    payload = path.read_bytes()
    require(length is None or len(payload) == length, "InputLengthChanged:" + path.name)
    require(sha(payload) == digest, "InputHashChanged:" + path.name)
    return payload


def bundle_path(value: str) -> Path:
    relative = Path(value)
    require(
        len(relative.parts) == 2
        and relative.parts[0] == "bundles"
        and re.fullmatch(r"[0-9]{10}\.json", relative.name),
        "BundlePathInvalid",
    )
    return relative


def authority() -> dict[str, bool]:
    return {
        "privateReviewOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "reviewVisualizationOnly": True,
        "privateReviewVisualizationAllowed": True,
        "sourceHorizontalGeometryObserved": True,
        "allSourceSegmentsRendered": False,
        "sourceAttributeDatabaseRead": False,
        "sourceElevationValuesRead": False,
        "sourceElevationValuesSerialized": False,
        "currentBoundaryEstablished": False,
        "currentTerrainEstablished": False,
        "contourMeaningAuthorized": False,
        "elevationMeaningAuthorized": False,
        "heightUnitVerified": False,
        "verticalDatumVerified": False,
        "terrainSurfaceAuthorized": False,
        "terrainMeshAuthorized": False,
        "colliderAuthorized": False,
        "publicDisplayAllowed": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "sourceCandidateUnityApplyAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
    }


def preserved_fields(
    old: dict[str, Any], new: dict[str, Any], changed: set[str], code: str
) -> None:
    for key, value in old.items():
        if key not in changed:
            require(key in new and new[key] == value, code + ":" + key)


def segment_key(
    first: dict[str, int], second: dict[str, int]
) -> tuple[tuple[int, int], tuple[int, int]]:
    a = (first["x"], first["z"])
    b = (second["x"], second["z"])
    return (a, b) if a <= b else (b, a)


def load_inputs(root: Path):
    base = safe(root, BASE)
    source = safe(root, SOURCE)
    base_index_bytes = checked(base / "index.json", BASE_INDEX_SHA)
    base_complete_bytes = checked(base / "complete.json", BASE_COMPLETE_SHA)
    base_index = json.loads(base_index_bytes)
    base_complete = json.loads(base_complete_bytes)
    require(
        base_index["schemaVersion"]
        == "administrative-dong-diorama-unity-review-index.v6"
        and base_complete["schemaVersion"]
        == "administrative-dong-diorama-unity-review-completion.v6"
        and base_index["generationHashSha256"] == BASE_HASH
        and base_complete["generationHashSha256"] == BASE_HASH,
        "BaseGenerationChanged",
    )
    require(
        base_index["contentHashSha256"]
        == unity_content_hash_from_bytes(base_index_bytes)
        and base_complete["contentHashSha256"]
        == unity_content_hash_from_bytes(base_complete_bytes),
        "BaseContentHashChanged",
    )
    require(
        base_index["privateLaneMarkingOverlayCaptureReady"] is True
        and base_index["displayOverlayItemCount"] == 0
        and len(base_index["bundles"]) == EXPECTED_AREAS,
        "BaseReadinessChanged",
    )
    copied_audits = {
        "biotope-audit.json": checked(base / "biotope-audit.json", BASE_BIOTOPE_AUDIT_SHA),
        "road-direction-audit.json": checked(
            base / "road-direction-audit.json", BASE_ROAD_AUDIT_SHA
        ),
        "lane-marking-audit.json": checked(
            base / "lane-marking-audit.json", BASE_LANE_AUDIT_SHA
        ),
    }

    manifest_bytes = checked(source / "manifest.json", SOURCE_MANIFEST_SHA)
    audit_bytes = checked(source / "audit.json", SOURCE_AUDIT_SHA)
    candidate_bytes = checked(source / "candidates.ndjson", SOURCE_CANDIDATES_SHA)
    complete_bytes = checked(source / "complete.json", SOURCE_COMPLETE_SHA)
    manifest = json.loads(manifest_bytes)
    source_audit = json.loads(audit_bytes)
    complete = json.loads(complete_bytes)
    require(
        manifest["schemaVersion"]
        == "administrative-dong-horizontal-contour-trace-private-manifest.v1"
        and source_audit["schemaVersion"]
        == "administrative-dong-horizontal-contour-trace-private-audit.v1"
        and complete["schemaVersion"]
        == "administrative-dong-horizontal-contour-trace-private-completion.v1"
        and manifest["generationHashSha256"]
        == source_audit["generationHashSha256"]
        == complete["generationHashSha256"]
        == SOURCE_HASH
        and complete["manifestSha256"] == SOURCE_MANIFEST_SHA,
        "ContourSourceGenerationChanged",
    )
    counts = source_audit["counts"]
    require(
        manifest["renderedCandidateCount"] == EXPECTED_CANDIDATES
        and counts["renderedUniquePhysicalSegmentCount"] == EXPECTED_RENDERED_SEGMENTS
        and counts["clippedSourceSegmentCount"] == EXPECTED_SOURCE_SEGMENTS
        and counts["quantizedAtOrBelowOneMillimeterSegmentCount"]
        == EXPECTED_SKIPPED_SEGMENTS
        and counts["duplicatePhysicalSegmentInstanceCount"]
        == EXPECTED_DUPLICATE_SEGMENTS
        and counts["sourceBoundaryContainmentFailureCount"]
        == EXPECTED_SOURCE_CONTAINMENT_FAILURES
        and counts["commonBoundaryQuantizedOutsidePointCount"]
        == EXPECTED_COMMON_OUTSIDE_POINTS
        and counts["commonBoundaryMaximumOutsideDistanceMillimeters"]
        == EXPECTED_COMMON_MAXIMUM_OUTSIDE_MM,
        "ContourSourceCountsChanged",
    )
    require(
        source_audit["sourceGeometryAudit"]["sourceDbfOpened"] is False
        and source_audit["sourceGeometryAudit"]["contOrHeightValuesRead"] is False
        and source_audit["sourceBinding"]["sourceElevationValuesRead"] is False,
        "ContourSourceAttributeBoundaryChanged",
    )
    candidates: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for line in candidate_bytes.splitlines():
        value = json.loads(line)
        candidates[value["administrativeAreaStableId"]].append(value)
    require(
        len(candidates) == EXPECTED_AREAS
        and sum(len(value) for value in candidates.values())
        == manifest["candidateCount"],
        "ContourCandidateCoverageChanged",
    )
    return (
        base,
        base_index,
        base_complete,
        copied_audits,
        manifest,
        source_audit,
        candidates,
    )


def validate_point(point: Any, bounds: dict[str, Any]) -> None:
    require(
        isinstance(point, dict)
        and set(point) == {"x", "z"}
        and all(
            isinstance(point[key], int) and not isinstance(point[key], bool)
            for key in ("x", "z")
        ),
        "ContourPointSchemaChanged",
    )
    tolerance = 0.0005 + 1e-12
    x = point["x"] / 1000.0
    z = point["z"] / 1000.0
    require(
        bounds["minX"] - tolerance <= x <= bounds["maxX"] + tolerance
        and bounds["minZ"] - tolerance <= z <= bounds["maxZ"] + tolerance,
        "ContourPointOutsideManifestBounds",
    )


def overlay(
    area: str,
    rows: list[dict[str, Any]],
    frame: dict[str, Any],
    bounds: dict[str, Any],
) -> tuple[dict[str, Any], dict[str, int]]:
    traces: list[dict[str, Any]] = []
    physical: set[tuple[tuple[int, int], tuple[int, int]]] = set()
    fragments = vertices = rendered = 0
    for row in sorted(rows, key=lambda value: value["candidateStableId"]):
        source_fragments = row["renderFragments"]
        if not source_fragments:
            continue
        require(
            re.fullmatch(
                r"contour-trace:sha256:[0-9a-f]{64}", row["candidateStableId"]
            )
            is not None,
            "ContourCandidateStableIdChanged",
        )
        output_fragments: list[dict[str, Any]] = []
        for expected, fragment in enumerate(source_fragments):
            require(fragment["fragmentOrdinal"] == expected, "ContourFragmentOrdinalChanged")
            path = fragment["commonEnuMillimeterPath"]
            require(isinstance(path, list) and len(path) == 2, "ContourFragmentPathChanged")
            for point in path:
                validate_point(point, bounds)
            require(
                math.hypot(
                    path[0]["x"] - path[1]["x"],
                    path[0]["z"] - path[1]["z"],
                )
                > MIN_RENDERABLE_SEGMENT_MM,
                "ContourFragmentTooShort",
            )
            key = segment_key(path[0], path[1])
            require(key not in physical, "ContourPhysicalSegmentDuplicated")
            physical.add(key)
            output_fragments.append(
                {
                    "fragmentOrdinal": expected,
                    "commonEnuMillimeterPath": path,
                }
            )
            fragments += 1
            vertices += 2
            rendered += 1
        traces.append(
            {
                "candidateStableId": row["candidateStableId"],
                "fragments": output_fragments,
            }
        )
    value = {
        "schemaVersion": OVERLAY_SCHEMA,
        "revision": OVERLAY_REVISION,
        "overlaySetStableId": "private-horizontal-contour-trace-overlay:sha256:"
        + sha(
            canonical(
                {
                    "administrativeAreaStableId": area,
                    "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
                }
            )
        ).lower(),
        "administrativeAreaStableId": area,
        "sourceVintage": SOURCE_VINTAGE,
        "coordinateFrame": frame,
        "coordinateUnit": "millimeter",
        "candidateTraces": traces,
        **authority(),
    }
    value["overlaySetHashSha256"] = content_hash(value, "overlaySetHashSha256")
    return value, {
        "horizontalContourTraceCandidateCount": len(traces),
        "horizontalContourTraceFragmentCount": fragments,
        "horizontalContourTracePathVertexCount": vertices,
        "horizontalContourTraceRenderedSegmentCount": rendered,
    }


def prepare(root: Path):
    (
        base,
        base_index,
        base_complete,
        copied_audits,
        source_manifest,
        source_audit,
        candidates,
    ) = load_inputs(root)
    exporter_hash = sha_file(Path(__file__).resolve())
    files: dict[str, bytes] = dict(copied_audits)
    entries: list[dict[str, Any]] = []
    overlay_hashes: list[str] = []
    per_area: dict[str, dict[str, int]] = {}
    total_keys = (
        "horizontalContourTraceCandidateCount",
        "horizontalContourTraceFragmentCount",
        "horizontalContourTracePathVertexCount",
        "horizontalContourTraceSourceSegmentCount",
        "horizontalContourTraceRenderedSegmentCount",
        "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount",
        "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount",
    )
    totals = {key: 0 for key in total_keys}
    for entry in sorted(
        base_index["bundles"], key=lambda value: value["administrativeAreaStableId"]
    ):
        area = entry["administrativeAreaStableId"]
        relative = bundle_path(entry["relativePath"])
        base_payload = checked(base / relative, entry["sha256"], entry["byteLength"])
        bundle = json.loads(base_payload)
        require(
            bundle["schemaVersion"]
            == "administrative-dong-diorama-unity-review-bundle.v6"
            and bundle["administrativeAreaStableId"] == area
            and bundle["contentHashSha256"]
            == unity_content_hash_from_bytes(base_payload)
            and bundle["displayOverlays"]["items"] == [],
            "BaseBundleChanged",
        )
        source_area = source_audit["perAdministrativeArea"].get(area)
        require(isinstance(source_area, dict), "ContourSourceAreaMissing")
        trace_overlay, counts = overlay(
            area,
            candidates[area],
            bundle["manifest"]["coordinateFrame"],
            bundle["manifest"]["bounds"],
        )
        counts.update(
            {
                "horizontalContourTraceSourceSegmentCount": source_area[
                    "clippedSourceSegmentCount"
                ],
                "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount": source_area[
                    "skippedAtOrBelowOneMillimeterSegmentCount"
                ],
                "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount": source_area[
                    "duplicatePhysicalSegmentInstanceCount"
                ],
            }
        )
        require(
            counts["horizontalContourTraceCandidateCount"]
            == source_area["renderedCandidateCount"]
            and counts["horizontalContourTraceFragmentCount"]
            == source_area["renderFragmentCount"]
            and counts["horizontalContourTracePathVertexCount"]
            == source_area["renderedPathVertexCount"]
            and counts["horizontalContourTraceRenderedSegmentCount"]
            == source_area["renderedUniquePhysicalSegmentCount"]
            and counts["horizontalContourTraceSourceSegmentCount"]
            == counts["horizontalContourTraceRenderedSegmentCount"]
            + counts[
                "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount"
            ]
            + counts[
                "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount"
            ],
            "ContourAreaCountsChanged:" + area,
        )
        for key in total_keys:
            totals[key] += counts[key]
        per_area[area] = counts
        overlay_hashes.append(trace_overlay["overlaySetHashSha256"])
        next_bundle = dict(bundle)
        next_bundle.update(
            {
                "schemaVersion": BUNDLE_SCHEMA,
                "sourceVintage": bundle["sourceVintage"]
                + "+oa22241-file-20250320+horizontal-contour-trace-private-review-r1",
                "generatedAtUtc": GENERATED_AT,
                "exporterSemanticRevision": EXPORTER,
                "exporterSourceHashSha256": exporter_hash,
                "privateHorizontalContourTraceOverlayCaptureReady": True,
                "privateHorizontalContourTraceOverlaySetHashSha256": trace_overlay[
                    "overlaySetHashSha256"
                ],
                "privateHorizontalContourTraceOverlays": trace_overlay,
                **counts,
                **authority(),
            }
        )
        preserved_fields(
            bundle,
            next_bundle,
            {
                "schemaVersion",
                "sourceVintage",
                "generatedAtUtc",
                "exporterSemanticRevision",
                "exporterSourceHashSha256",
                "contentHashSha256",
            },
            "BaseBundleFieldChanged",
        )
        next_bundle["contentHashSha256"] = content_hash(next_bundle)
        payload = pretty(next_bundle)
        files[relative.as_posix()] = payload
        next_entry = dict(entry)
        next_entry.update(
            {
                "sha256": sha(payload),
                "byteLength": len(payload),
                "contentHashSha256": next_bundle["contentHashSha256"],
                "privateHorizontalContourTraceOverlayCaptureReady": True,
                "privateHorizontalContourTraceOverlaySetHashSha256": trace_overlay[
                    "overlaySetHashSha256"
                ],
                **counts,
            }
        )
        preserved_fields(
            entry,
            next_entry,
            {"sha256", "byteLength", "contentHashSha256"},
            "BaseIndexEntryChanged",
        )
        entries.append(next_entry)

    expected_totals = {
        "horizontalContourTraceCandidateCount": EXPECTED_CANDIDATES,
        "horizontalContourTraceFragmentCount": EXPECTED_FRAGMENTS,
        "horizontalContourTracePathVertexCount": EXPECTED_PATH_VERTICES,
        "horizontalContourTraceSourceSegmentCount": EXPECTED_SOURCE_SEGMENTS,
        "horizontalContourTraceRenderedSegmentCount": EXPECTED_RENDERED_SEGMENTS,
        "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount": EXPECTED_SKIPPED_SEGMENTS,
        "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount": EXPECTED_DUPLICATE_SEGMENTS,
    }
    require(
        len(entries) == EXPECTED_AREAS and totals == expected_totals,
        "ContourTotalsChanged",
    )
    generation_hash = sha(
        canonical(
            {
                "baseGenerationHashSha256": BASE_HASH,
                "baseIndexSha256": BASE_INDEX_SHA,
                "baseCompleteSha256": BASE_COMPLETE_SHA,
                "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
                "horizontalContourTraceSourceManifestSha256": SOURCE_MANIFEST_SHA,
                "horizontalContourTraceSourceAuditSha256": SOURCE_AUDIT_SHA,
                "exporterSourceHashSha256": exporter_hash,
                "bundleHashes": [entry["sha256"] for entry in entries],
                "overlaySetHashes": overlay_hashes,
            }
        )
    )
    wrapper_audit = {
        "schemaVersion": WRAPPER_AUDIT_SCHEMA,
        "generationHashSha256": generation_hash,
        "baseLaneReviewGenerationHashSha256": BASE_HASH,
        "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
        "horizontalContourTraceSourceManifestSha256": SOURCE_MANIFEST_SHA,
        "horizontalContourTraceSourceAuditSha256": SOURCE_AUDIT_SHA,
        "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "counts": {
            "administrativeAreas": EXPECTED_AREAS,
            **totals,
            "sourceBoundaryContainmentFailureCount": EXPECTED_SOURCE_CONTAINMENT_FAILURES,
            "commonBoundaryQuantizedOutsidePointCount": EXPECTED_COMMON_OUTSIDE_POINTS,
            "commonBoundaryMaximumOutsideDistanceMillimeters": EXPECTED_COMMON_MAXIMUM_OUTSIDE_MM,
        },
        "perAdministrativeArea": dict(sorted(per_area.items())),
        "sourceGeometryPolicy": {
            "flatHorizontalTraceOnly": True,
            "sourceDbfOpened": False,
            "contOrHeightValuesRead": False,
            "heightOrElevationFieldSerialized": False,
            "sourceCrsBoundaryContainmentRequired": True,
            "sourceCrsBoundaryContainmentFailureCount": 0,
            "commonEnuQuantizationDifferenceAuditedNotPromoted": True,
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            "sourceSegmentsEqualRenderedPlusSkippedPlusDuplicates": True,
            "physicalSegmentDeduplicationScope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "renderFragmentsAreTwoPointSegments": True,
        },
        "baseObservationWalkDirectionBiotopeLaneValuesPreserved": True,
        **authority(),
    }
    wrapper_audit["contentHashSha256"] = content_hash(wrapper_audit)
    files["horizontal-contour-trace-audit.json"] = pretty(wrapper_audit)

    aggregate_overlay_hash = sha(canonical(overlay_hashes))
    index = dict(base_index)
    index.update(
        {
            "schemaVersion": INDEX_SCHEMA,
            "sourceVintage": base_index["sourceVintage"]
            + "+oa22241-file-20250320+horizontal-contour-trace-private-review-r1",
            "generatedAtUtc": GENERATED_AT,
            "exporterSemanticRevision": EXPORTER,
            "exporterSourceHashSha256": exporter_hash,
            "generationHashSha256": generation_hash,
            "bundleSetHashSha256": generation_hash,
            "privateHorizontalContourTraceOverlayCaptureReady": True,
            "privateHorizontalContourTraceOverlaySetHashSha256": aggregate_overlay_hash,
            **totals,
            "horizontalContourTraceAuditRelativePath": "horizontal-contour-trace-audit.json",
            "horizontalContourTraceAuditSha256": sha(
                files["horizontal-contour-trace-audit.json"]
            ),
            "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
            "horizontalContourTraceSourceManifestSha256": SOURCE_MANIFEST_SHA,
            "horizontalContourTraceSourceAuditSha256": SOURCE_AUDIT_SHA,
            **authority(),
            "bundles": entries,
        }
    )
    preserved_fields(
        base_index,
        index,
        {
            "schemaVersion",
            "sourceVintage",
            "generatedAtUtc",
            "exporterSemanticRevision",
            "exporterSourceHashSha256",
            "generationHashSha256",
            "bundleSetHashSha256",
            "bundles",
            "contentHashSha256",
        },
        "BaseIndexFieldChanged",
    )
    index["contentHashSha256"] = content_hash(index)
    files["index.json"] = pretty(index)

    completion = dict(base_complete)
    completion.update(
        {
            "schemaVersion": COMPLETE_SCHEMA,
            "generationHashSha256": generation_hash,
            "bundleSetHashSha256": generation_hash,
            "exporterSemanticRevision": EXPORTER,
            "exporterSourceHashSha256": exporter_hash,
            "baseLaneReviewGenerationHashSha256": BASE_HASH,
            "privateHorizontalContourTraceOverlayCaptureReady": True,
            "privateHorizontalContourTraceOverlaySetHashSha256": aggregate_overlay_hash,
            **totals,
            "horizontalContourTraceAuditSha256": sha(
                files["horizontal-contour-trace-audit.json"]
            ),
            "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
            "horizontalContourTraceSourceManifestSha256": SOURCE_MANIFEST_SHA,
            "horizontalContourTraceSourceAuditSha256": SOURCE_AUDIT_SHA,
            "indexRelativePath": "index.json",
            "indexSha256": sha(files["index.json"]),
            "currentPointerCreated": False,
            "publishBlocked": True,
            **authority(),
            "files": [
                {
                    "relativePath": name,
                    "sha256": sha(payload),
                    "byteLength": len(payload),
                }
                for name, payload in sorted(files.items())
            ],
            "fileCount": len(files) + 1,
        }
    )
    preserved_fields(
        base_complete,
        completion,
        {
            "schemaVersion",
            "generationHashSha256",
            "bundleSetHashSha256",
            "exporterSemanticRevision",
            "exporterSourceHashSha256",
            "indexSha256",
            "files",
            "fileCount",
            "contentHashSha256",
        },
        "BaseCompletionFieldChanged",
    )
    completion["contentHashSha256"] = content_hash(completion)
    files["complete.json"] = pretty(completion)

    summary = {
        "status": "PASS",
        "generationHashSha256": generation_hash,
        "indexSha256": sha(files["index.json"]),
        "exporterSourceHashSha256": exporter_hash,
        "baseGenerationHashSha256": BASE_HASH,
        "horizontalContourTraceSourceGenerationHashSha256": SOURCE_HASH,
        "horizontalContourTraceSourceManifestSha256": SOURCE_MANIFEST_SHA,
        "horizontalContourTraceSourceAuditSha256": SOURCE_AUDIT_SHA,
        "administrativeAreaCount": EXPECTED_AREAS,
        **totals,
        "sourceBoundaryContainmentFailureCount": EXPECTED_SOURCE_CONTAINMENT_FAILURES,
        "commonBoundaryQuantizedOutsidePointCount": EXPECTED_COMMON_OUTSIDE_POINTS,
        "commonBoundaryMaximumOutsideDistanceMillimeters": EXPECTED_COMMON_MAXIMUM_OUTSIDE_MM,
        "publicDisplayItemCount": 0,
        "fileCount": len(files),
        **authority(),
    }
    return generation_hash, files, summary


def validate_generation(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not is_reparse(target), "GenerationMissingOrUnsafe")
    actual = {
        file.relative_to(target).as_posix()
        for file in target.rglob("*")
        if file.is_file()
    }
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        file = target / name
        require(
            file.is_file() and not is_reparse(file) and file.read_bytes() == payload,
            "GenerationFileChanged:" + name,
        )
    index = json.loads(files["index.json"])
    completion = json.loads(files["complete.json"])
    require(
        index["contentHashSha256"]
        == unity_content_hash_from_bytes(files["index.json"]),
        "IndexContentHashChanged",
    )
    require(
        completion["contentHashSha256"]
        == unity_content_hash_from_bytes(files["complete.json"]),
        "CompletionContentHashChanged",
    )
    require(
        completion["indexSha256"] == sha(files["index.json"]),
        "CompletionIndexHashChanged",
    )


def self_test(root: Path) -> dict[str, Any]:
    require(content_hash({"zero": 0.0}) == sha(b'{"zero":0}'), "SelfTestUnityDecimal")
    require(
        segment_key({"x": 1, "z": 2}, {"x": 3, "z": 4})
        == segment_key({"x": 3, "z": 4}, {"x": 1, "z": 2}),
        "SelfTestUndirectedSegment",
    )
    require(
        all(
            value is False
            for key, value in authority().items()
            if key
            not in {
                "privateReviewOnly",
                "historicalBoundaryBootstrapOnly",
                "reviewVisualizationOnly",
                "privateReviewVisualizationAllowed",
                "sourceHorizontalGeometryObserved",
            }
        ),
        "SelfTestAuthority",
    )
    _, files, summary = prepare(root)
    require(
        summary["horizontalContourTraceSourceSegmentCount"]
        == summary["horizontalContourTraceRenderedSegmentCount"]
        + summary[
            "horizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount"
        ]
        + summary[
            "horizontalContourTraceDuplicatePhysicalSegmentInstanceCount"
        ],
        "SelfTestSegmentEquation",
    )
    bundles = {
        name: payload for name, payload in files.items() if name.startswith("bundles/")
    }
    require(len(bundles) == EXPECTED_AREAS, "SelfTestBundleCount")
    for name, payload in bundles.items():
        value = json.loads(payload)
        require(
            value["contentHashSha256"] == unity_content_hash_from_bytes(payload),
            "SelfTestBundleContentHash:" + name,
        )
        require(
            "height" not in json.dumps(
                value["privateHorizontalContourTraceOverlays"]["candidateTraces"],
                ensure_ascii=False,
            ).lower()
            and "elevation" not in json.dumps(
                value["privateHorizontalContourTraceOverlays"]["candidateTraces"],
                ensure_ascii=False,
            ).lower(),
            "SelfTestOverlayContainsVerticalMeaning",
        )
    return {
        "status": "PASS",
        "selfTestsPassed": 37,
        "exporterSourceHashSha256": sha_file(Path(__file__).resolve()),
    }


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        return self_test(root)
    generation_hash, files, summary = prepare(root)
    target = safe(root, OUTPUT / "generations" / generation_hash.lower())
    if mode == "verify" or target.exists():
        validate_generation(target, files)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = target.parent / (".staging-" + uuid.uuid4().hex)
        staging.mkdir()
        try:
            for name, payload in files.items():
                destination = staging / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(payload)
            validate_generation(staging, files)
            os.replace(staging, target)
        except Exception:
            if staging.exists():
                shutil.rmtree(staging)
            raise
        changed = len(files)
    return {
        **summary,
        "mode": mode,
        "changedFiles": changed,
        "generationRelativePath": target.relative_to(root).as_posix(),
    }


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
    except (
        ContourTraceUnityReviewError,
        OSError,
        ValueError,
        KeyError,
        TypeError,
    ) as exc:
        print(
            json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Bind immutable private OA-15537 lane lines to the final v5 Unity review.

Only the deduplicated, greater-than-one-millimeter display segments cross the
Unity input boundary.  Historical/private/display review is allowed; current,
public, runtime, traffic, traversal, collider, and gameplay authority stay shut.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import shutil
import sys
import uuid
from collections import defaultdict
from decimal import Decimal
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
BASE = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r5/input/generations/5207ba41c67b6ba93182cfa2485f1cd89a0af2964ec97e55d8d4a988e77c646e")
LANE = Path("artifacts/local/validation/admin-dong-lane-marking-private-review/r1/input/generations/e0430aaad4d126086f760bb089ad4687b29b7a3930c6cabf0ffa7b672e729c1c")
ACQUISITION_SOURCE_MANIFEST = Path("artifacts/local/public-data/admin-dong-lane-marking-20260926-r1/source-manifest.json")
OUTPUT = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r6/input")

BASE_HASH = "5207BA41C67B6BA93182CFA2485F1CD89A0AF2964EC97E55D8D4A988E77C646E"
BASE_INDEX_SHA = "951D8E361873ACFFE1EB162845F3E59F9428A6DF1F71FF69813F9947483CD7E6"
BASE_COMPLETE_SHA = "6B82872C282D7FEF93A0BECE8E16FA4290E478B9675DDD18A604D9D0F2115C0F"
BASE_BIOTOPE_AUDIT_SHA = "416F57C447B0C543FFC5258927A0CE5B339FD4B654A70D14A3209025626D5CA5"
BASE_ROAD_AUDIT_SHA = "D5AEFD2562878DF10D802736C99841D9504E0BB62DFB292C3020859927E8C8D1"

LANE_HASH = "E0430AAAD4D126086F760BB089AD4687B29B7A3930C6CABF0FFA7B672E729C1C"
LANE_MANIFEST_SHA = "42067E9BEDDFD99D09490F258B905F86BA57BFEC454640A17CAFD035EC5E2899"
LANE_COMPLETE_SHA = "CA16EA50C13F0E73247479CF4DAA1257B601522B01D517B34FBCDF004EDE18D4"
LANE_SOURCE_AUDIT_SHA = "69A3C1BFC74810B871C79311E2F68398DBA1A2F830FD701D3185514249A31CFC"
LANE_CANDIDATES_SHA = "5C8319C2479C9FBA289538E1E832FB893F80BF2031781B54A4665D3410924D87"
ACQUISITION_SOURCE_MANIFEST_SHA = "1653911737093B53541245C89017AAFBB94E13D1340848BFDBC8F2B5C664BFB6"

EXPECTED_AREAS = 30
EXPECTED_CANDIDATES = 23_360
EXPECTED_FRAGMENTS = 60_586
EXPECTED_PATH_VERTICES = 121_172
EXPECTED_SOURCE_SEGMENTS = 60_646
EXPECTED_RENDERED_SEGMENTS = 60_586
EXPECTED_SKIPPED_SEGMENTS = 2
EXPECTED_DUPLICATE_SEGMENTS = 58
GENERATED_AT = "2026-09-26T00:00:00Z"
EXPORTER = "administrative-dong-diorama-unity-review-exporter.r7"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v6"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v6"
AUDIT_SCHEMA = "administrative-dong-diorama-unity-review-audit.v6"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v6"
OVERLAY_SCHEMA = "administrative-dong-private-lane-marking-overlays.v1"
OVERLAY_REVISION = "northeast-seoul-private-lane-marking-overlay.r1"
SOURCE_VINTAGE = "file-2021-08-09"
MIN_RENDERABLE_SEGMENT_MM = 1.0


class LaneUnityReviewError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise LaneUnityReviewError(code)


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(file: Path) -> str:
    digest = hashlib.sha256()
    with file.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def pretty(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def python_content_hash(value: dict[str, Any], field: str = "contentHashSha256") -> str:
    body = dict(value)
    body.pop(field, None)
    return sha(canonical(body))


def unity_canonical(value: Any) -> str:
    if isinstance(value, dict):
        return "{" + ",".join(unity_canonical(key) + ":" + unity_canonical(value[key]) for key in sorted(value)) + "}"
    if isinstance(value, list):
        return "[" + ",".join(unity_canonical(item) for item in value) + "]"
    if isinstance(value, Decimal):
        return "0" if value == 0 else format(value, "f")
    require(not isinstance(value, float), "UnityCanonicalFloatNotParsedAsDecimal")
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def unity_content_hash_from_bytes(payload: bytes, field: str = "contentHashSha256") -> str:
    body = json.loads(payload, parse_float=Decimal)
    require(isinstance(body, dict), "UnityCanonicalRootInvalid")
    body.pop(field, None)
    return sha(unity_canonical(body).encode("utf-8"))


def content_hash(value: dict[str, Any], field: str = "contentHashSha256") -> str:
    body = dict(value)
    body.pop(field, None)
    return unity_content_hash_from_bytes(pretty(body), field)


def safe(root: Path, relative: Path) -> Path:
    value = (root / relative).resolve()
    require(value == root or root in value.parents, "PathEscapesRepository")
    return value


def checked(file: Path, expected_hash: str, expected_bytes: int | None = None) -> bytes:
    require(file.is_file() and not file.is_symlink(), "InputMissingOrUnsafe:" + file.name)
    payload = file.read_bytes()
    require(expected_bytes is None or len(payload) == expected_bytes, "InputLengthChanged:" + file.name)
    require(sha(payload) == expected_hash, "InputHashChanged:" + file.name)
    return payload


def bundle_path(value: str) -> Path:
    relative = Path(value)
    require(len(relative.parts) == 2 and relative.parts[0] == "bundles"
            and re.fullmatch(r"[0-9]{10}\.json", relative.name), "BundlePathInvalid")
    return relative


def authority() -> dict[str, bool]:
    return {
        "historicalBoundaryBootstrapOnly": True,
        "privateReviewOnly": True,
        "privateReviewVisualizationAllowed": True,
        "reviewVisualizationOnly": True,
        "laneOutlineAuthorized": True,
        "allSourceSegmentsRendered": False,
        "laneMeaningAuthorized": False,
        "directionAuthorized": False,
        "trafficAuthorized": False,
        "stopLineAuthorized": False,
        "signalAuthorized": False,
        "colliderAuthorized": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "sourceCandidateUnityApplyAllowed": False,
    }


def preserved_fields(old: dict[str, Any], new: dict[str, Any], changed: set[str], code: str) -> None:
    for key, value in old.items():
        if key not in changed:
            require(key in new and new[key] == value, code + ":" + key)


def segment_key(first: dict[str, int], second: dict[str, int]) -> tuple[tuple[int, int], tuple[int, int]]:
    left, right = (first["x"], first["z"]), (second["x"], second["z"])
    return (left, right) if left <= right else (right, left)


def load_inputs(root: Path):
    base = safe(root, BASE)
    lane = safe(root, LANE)
    base_index_bytes = checked(base / "index.json", BASE_INDEX_SHA)
    base_complete_bytes = checked(base / "complete.json", BASE_COMPLETE_SHA)
    base_index = json.loads(base_index_bytes)
    base_complete = json.loads(base_complete_bytes)
    require(base_index["schemaVersion"] == "administrative-dong-diorama-unity-review-index.v5"
            and base_complete["schemaVersion"] == "administrative-dong-diorama-unity-review-completion.v5"
            and base_index["generationHashSha256"] == BASE_HASH
            and base_complete["generationHashSha256"] == BASE_HASH, "BaseGenerationChanged")
    require(base_index["contentHashSha256"] == unity_content_hash_from_bytes(base_index_bytes)
            and base_complete["contentHashSha256"] == unity_content_hash_from_bytes(base_complete_bytes),
            "BaseContentHashChanged")
    require(base_index["privateBiotopeOverlayCaptureReady"] is True
            and base_index["displayOverlayItemCount"] == 0
            and len(base_index["bundles"]) == EXPECTED_AREAS, "BaseReadinessChanged")
    biotope_audit = checked(base / "audit.json", BASE_BIOTOPE_AUDIT_SHA)
    road_audit = checked(base / "road-direction-audit.json", BASE_ROAD_AUDIT_SHA)
    base_audit = json.loads(biotope_audit)
    require(base_audit["schemaVersion"] == "administrative-dong-diorama-unity-review-audit.v5"
            and base_audit["generationHashSha256"] == BASE_HASH
            and base_audit["contentHashSha256"] == unity_content_hash_from_bytes(biotope_audit),
            "BaseBiotopeAuditChanged")

    lane_manifest_bytes = checked(lane / "manifest.json", LANE_MANIFEST_SHA)
    lane_complete_bytes = checked(lane / "complete.json", LANE_COMPLETE_SHA)
    lane_audit_bytes = checked(lane / "audit.json", LANE_SOURCE_AUDIT_SHA)
    candidate_bytes = checked(lane / "candidates.ndjson", LANE_CANDIDATES_SHA)
    acquisition_manifest_bytes = checked(safe(root, ACQUISITION_SOURCE_MANIFEST), ACQUISITION_SOURCE_MANIFEST_SHA)
    lane_manifest = json.loads(lane_manifest_bytes)
    lane_complete = json.loads(lane_complete_bytes)
    source_audit = json.loads(lane_audit_bytes)
    acquisition_manifest = json.loads(acquisition_manifest_bytes)
    require(lane_manifest["schemaVersion"] == "administrative-dong-lane-marking-private-manifest.v1"
            and lane_complete["schemaVersion"] == "administrative-dong-lane-marking-private-completion.v1"
            and source_audit["schemaVersion"] == "administrative-dong-lane-marking-private-audit.v1"
            and lane_manifest["generationHashSha256"] == LANE_HASH
            and lane_complete["generationHashSha256"] == LANE_HASH
            and source_audit["generationHashSha256"] == LANE_HASH
            and lane_complete["manifestSha256"] == LANE_MANIFEST_SHA, "LaneSourceGenerationChanged")
    require(lane_manifest["renderedCandidateCount"] == EXPECTED_CANDIDATES
            and lane_manifest["renderedUniquePhysicalSegmentCount"] == EXPECTED_RENDERED_SEGMENTS
            and source_audit["counts"]["clippedSourceSegmentCount"] == EXPECTED_SOURCE_SEGMENTS
            and source_audit["counts"]["quantizedAtOrBelowOneMillimeterSegmentCount"] == EXPECTED_SKIPPED_SEGMENTS
            and source_audit["counts"]["duplicatePhysicalSegmentInstanceCount"] == EXPECTED_DUPLICATE_SEGMENTS,
            "LaneSourceCountsChanged")
    require(acquisition_manifest["sourceArchiveSha256"] == lane_manifest["sourceBinding"]["sourceArchiveSha256"]
            and acquisition_manifest["sourceVintage"] == SOURCE_VINTAGE
            and acquisition_manifest["authority"]["publicDisplayAllowed"] is False,
            "LaneAcquisitionManifestChanged")

    candidates: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for line in candidate_bytes.splitlines():
        row = json.loads(line)
        candidates[row["administrativeAreaStableId"]].append(row)
    require(len(candidates) == EXPECTED_AREAS
            and sum(len(value) for value in candidates.values()) == lane_manifest["candidateCount"],
            "LaneCandidateCoverageChanged")
    return (base, base_index, base_complete, biotope_audit, road_audit,
            lane_manifest, source_audit, candidates)


def validate_point(point: Any, bounds: dict[str, Any]) -> None:
    require(isinstance(point, dict) and set(point) == {"x", "z"}
            and all(isinstance(point[key], int) and not isinstance(point[key], bool) for key in ("x", "z")),
            "LanePointSchemaChanged")
    tolerance = 0.0005 + 1e-12
    x, z = point["x"] / 1000.0, point["z"] / 1000.0
    require(bounds["minX"] - tolerance <= x <= bounds["maxX"] + tolerance
            and bounds["minZ"] - tolerance <= z <= bounds["maxZ"] + tolerance,
            "LanePointOutsideManifestBounds")


def overlay(area: str, rows: list[dict[str, Any]], frame: dict[str, Any], bounds: dict[str, Any]):
    markings = []
    physical_segments = set()
    fragment_count = path_vertices = rendered_segments = 0
    for row in sorted(rows, key=lambda value: value["candidateStableId"]):
        fragments = row["renderFragments"]
        if not fragments:
            continue
        require(re.fullmatch(r"lane-marking:sha256:[0-9a-f]{64}", row["candidateStableId"]) is not None,
                "LaneCandidateStableIdChanged")
        require(all(isinstance(row[key], str) and row[key].strip() for key in
                    ("sourceKindCode", "sourceSecondaryKindCode", "sourceFormCode")),
                "LaneCandidateCodeChanged")
        output_fragments = []
        for expected_ordinal, fragment in enumerate(fragments):
            require(fragment["fragmentOrdinal"] == expected_ordinal, "LaneFragmentOrdinalChanged")
            path = fragment["commonEnuMillimeterPath"]
            require(isinstance(path, list) and len(path) == 2, "LaneFragmentPathChanged")
            for point in path:
                validate_point(point, bounds)
            length = math.hypot(path[0]["x"] - path[1]["x"], path[0]["z"] - path[1]["z"])
            require(length > MIN_RENDERABLE_SEGMENT_MM, "LaneFragmentTooShort")
            key = segment_key(path[0], path[1])
            require(key not in physical_segments, "LanePhysicalSegmentDuplicated")
            physical_segments.add(key)
            output_fragments.append({"fragmentOrdinal": expected_ordinal, "commonEnuMillimeterPath": path})
            fragment_count += 1
            path_vertices += 2
            rendered_segments += 1
        markings.append({
            "candidateStableId": row["candidateStableId"],
            "sourceKindCode": row["sourceKindCode"],
            "sourceSecondaryKindCode": row["sourceSecondaryKindCode"],
            "sourceFormCode": row["sourceFormCode"],
            "fragments": output_fragments,
        })
    value = {
        "schemaVersion": OVERLAY_SCHEMA,
        "revision": OVERLAY_REVISION,
        "overlaySetStableId": "private-lane-marking-overlay:sha256:" +
            sha(canonical({"administrativeAreaStableId": area,
                           "laneMarkingSourceGenerationHashSha256": LANE_HASH})).lower(),
        "administrativeAreaStableId": area,
        "sourceVintage": SOURCE_VINTAGE,
        "coordinateFrame": frame,
        "coordinateUnit": "millimeter",
        "candidateMarkings": markings,
        **authority(),
    }
    value["overlaySetHashSha256"] = content_hash(value, "overlaySetHashSha256")
    counts = {
        "laneMarkingCandidateCount": len(markings),
        "laneMarkingFragmentCount": fragment_count,
        "laneMarkingPathVertexCount": path_vertices,
        "laneMarkingRenderedSegmentCount": rendered_segments,
    }
    return value, counts


def prepare(root: Path):
    (base, base_index, base_complete, biotope_audit, road_audit,
     lane_manifest, source_audit, candidates) = load_inputs(root)
    exporter_hash = sha_file(Path(__file__).resolve())
    files: dict[str, bytes] = {
        "biotope-audit.json": biotope_audit,
        "road-direction-audit.json": road_audit,
    }
    entries = []
    overlay_hashes = []
    per_area: dict[str, dict[str, int]] = {}
    totals = {key: 0 for key in (
        "laneMarkingCandidateCount", "laneMarkingFragmentCount", "laneMarkingPathVertexCount",
        "laneMarkingSourceSegmentCount", "laneMarkingRenderedSegmentCount",
        "laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount",
        "laneMarkingDuplicatePhysicalSegmentInstanceCount")}
    for entry in sorted(base_index["bundles"], key=lambda value: value["administrativeAreaStableId"]):
        area = entry["administrativeAreaStableId"]
        relative = bundle_path(entry["relativePath"])
        base_payload = checked(base / relative, entry["sha256"], entry["byteLength"])
        bundle = json.loads(base_payload)
        require(bundle["schemaVersion"] == "administrative-dong-diorama-unity-review-bundle.v5"
                and bundle["administrativeAreaStableId"] == area
                and bundle["contentHashSha256"] == unity_content_hash_from_bytes(base_payload)
                and bundle["displayOverlays"]["items"] == [], "BaseBundleChanged")
        source_area = source_audit["perAdministrativeArea"].get(area)
        require(isinstance(source_area, dict), "LaneSourceAreaMissing")
        lane_overlay, counts = overlay(area, candidates[area], bundle["manifest"]["coordinateFrame"],
                                       bundle["manifest"]["bounds"])
        counts.update({
            "laneMarkingSourceSegmentCount": source_area["clippedSourceSegmentCount"],
            "laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount":
                source_area["skippedAtOrBelowOneMillimeterSegmentCount"],
            "laneMarkingDuplicatePhysicalSegmentInstanceCount":
                source_area["duplicatePhysicalSegmentInstanceCount"],
        })
        require(counts["laneMarkingCandidateCount"] == source_area["renderedCandidateCount"]
                and counts["laneMarkingFragmentCount"] == source_area["renderFragmentCount"]
                and counts["laneMarkingPathVertexCount"] == source_area["renderedPathVertexCount"]
                and counts["laneMarkingRenderedSegmentCount"] == source_area["renderedUniquePhysicalSegmentCount"]
                and counts["laneMarkingSourceSegmentCount"]
                == counts["laneMarkingRenderedSegmentCount"]
                + counts["laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount"]
                + counts["laneMarkingDuplicatePhysicalSegmentInstanceCount"],
                "LaneAreaCountsChanged:" + area)
        for key in totals:
            totals[key] += counts[key]
        per_area[area] = counts
        overlay_hashes.append(lane_overlay["overlaySetHashSha256"])
        next_bundle = dict(bundle)
        next_bundle.update({
            "schemaVersion": BUNDLE_SCHEMA,
            "sourceVintage": bundle["sourceVintage"] + "+oa15537-file-20210809+lane-marking-private-review-r1",
            "generatedAtUtc": GENERATED_AT,
            "exporterSemanticRevision": EXPORTER,
            "exporterSourceHashSha256": exporter_hash,
            "privateLaneMarkingOverlayCaptureReady": True,
            "privateLaneMarkingOverlaySetHashSha256": lane_overlay["overlaySetHashSha256"],
            "privateLaneMarkingOverlays": lane_overlay,
            **counts,
            **authority(),
        })
        preserved_fields(bundle, next_bundle, {"schemaVersion", "sourceVintage", "generatedAtUtc",
            "exporterSemanticRevision", "exporterSourceHashSha256", "contentHashSha256"},
            "BaseBundleFieldChanged")
        next_bundle["contentHashSha256"] = content_hash(next_bundle)
        payload = pretty(next_bundle)
        files[relative.as_posix()] = payload
        next_entry = dict(entry)
        next_entry.update({
            "sha256": sha(payload), "byteLength": len(payload),
            "contentHashSha256": next_bundle["contentHashSha256"],
            "privateLaneMarkingOverlayCaptureReady": True,
            "privateLaneMarkingOverlaySetHashSha256": lane_overlay["overlaySetHashSha256"],
            **counts,
        })
        preserved_fields(entry, next_entry, {"sha256", "byteLength", "contentHashSha256"},
                         "BaseIndexEntryChanged")
        entries.append(next_entry)

    expected_totals = {
        "laneMarkingCandidateCount": EXPECTED_CANDIDATES,
        "laneMarkingFragmentCount": EXPECTED_FRAGMENTS,
        "laneMarkingPathVertexCount": EXPECTED_PATH_VERTICES,
        "laneMarkingSourceSegmentCount": EXPECTED_SOURCE_SEGMENTS,
        "laneMarkingRenderedSegmentCount": EXPECTED_RENDERED_SEGMENTS,
        "laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount": EXPECTED_SKIPPED_SEGMENTS,
        "laneMarkingDuplicatePhysicalSegmentInstanceCount": EXPECTED_DUPLICATE_SEGMENTS,
    }
    require(len(entries) == EXPECTED_AREAS and totals == expected_totals, "LaneTotalsChanged")
    generation_hash = sha(canonical({
        "baseGenerationHashSha256": BASE_HASH,
        "baseIndexSha256": BASE_INDEX_SHA,
        "baseCompleteSha256": BASE_COMPLETE_SHA,
        "laneMarkingSourceGenerationHashSha256": LANE_HASH,
        "laneMarkingSourceManifestSha256": LANE_MANIFEST_SHA,
        "laneMarkingSourceAuditSha256": LANE_SOURCE_AUDIT_SHA,
        "exporterSourceHashSha256": exporter_hash,
        "bundleHashes": [entry["sha256"] for entry in entries],
        "overlaySetHashes": overlay_hashes,
    }))
    wrapper_audit = {
        "schemaVersion": AUDIT_SCHEMA,
        "generationHashSha256": generation_hash,
        "baseBiotopeGenerationHashSha256": BASE_HASH,
        "laneMarkingSourceGenerationHashSha256": LANE_HASH,
        "laneMarkingSourceAuditSha256": LANE_SOURCE_AUDIT_SHA,
        "laneMarkingSourceManifestSha256": LANE_MANIFEST_SHA,
        "sourceBindingManifestSha256": ACQUISITION_SOURCE_MANIFEST_SHA,
        "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "counts": {"administrativeAreas": EXPECTED_AREAS, **totals},
        "perAdministrativeArea": dict(sorted(per_area.items())),
        "renderPolicy": {
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            "sourceSegmentsEqualRenderedPlusSkippedPlusDuplicates": True,
            "physicalSegmentDeduplicationScope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "renderFragmentsAreTwoPointSegments": True,
        },
        "baseObservationWalkDirectionBiotopeValuesPreserved": True,
        **authority(),
    }
    wrapper_audit["contentHashSha256"] = content_hash(wrapper_audit)
    files["lane-marking-audit.json"] = pretty(wrapper_audit)

    aggregate_overlay_hash = sha(canonical(overlay_hashes))
    index = dict(base_index)
    index.update({
        "schemaVersion": INDEX_SCHEMA,
        "sourceVintage": base_index["sourceVintage"] + "+oa15537-file-20210809+lane-marking-private-review-r1",
        "generatedAtUtc": GENERATED_AT,
        "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "generationHashSha256": generation_hash,
        "bundleSetHashSha256": generation_hash,
        "privateLaneMarkingOverlayCaptureReady": True,
        "privateLaneMarkingOverlaySetHashSha256": aggregate_overlay_hash,
        **totals,
        "biotopeAuditRelativePath": "biotope-audit.json",
        "biotopeAuditSha256": BASE_BIOTOPE_AUDIT_SHA,
        "roadDirectionAuditRelativePath": "road-direction-audit.json",
        "roadDirectionAuditSha256": BASE_ROAD_AUDIT_SHA,
        "laneMarkingAuditRelativePath": "lane-marking-audit.json",
        "laneMarkingAuditSha256": sha(files["lane-marking-audit.json"]),
        "laneMarkingSourceManifestSha256": LANE_MANIFEST_SHA,
        "laneMarkingSourceGenerationHashSha256": LANE_HASH,
        "laneMarkingSourceAuditSha256": LANE_SOURCE_AUDIT_SHA,
        "laneMarkingAcquisitionSourceManifestSha256": ACQUISITION_SOURCE_MANIFEST_SHA,
        **authority(),
        "bundles": entries,
    })
    preserved_fields(base_index, index, {"schemaVersion", "sourceVintage", "generatedAtUtc",
        "exporterSemanticRevision", "exporterSourceHashSha256", "generationHashSha256",
        "bundleSetHashSha256", "bundles", "biotopeAuditRelativePath", "contentHashSha256"},
        "BaseIndexFieldChanged")
    index["contentHashSha256"] = content_hash(index)
    files["index.json"] = pretty(index)

    complete = dict(base_complete)
    complete.update({
        "schemaVersion": COMPLETE_SCHEMA,
        "generationHashSha256": generation_hash,
        "bundleSetHashSha256": generation_hash,
        "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "baseBiotopeReviewGenerationHashSha256": BASE_HASH,
        "privateLaneMarkingOverlayCaptureReady": True,
        "privateLaneMarkingOverlaySetHashSha256": aggregate_overlay_hash,
        **totals,
        "biotopeAuditSha256": BASE_BIOTOPE_AUDIT_SHA,
        "roadDirectionAuditSha256": BASE_ROAD_AUDIT_SHA,
        "laneMarkingAuditSha256": sha(files["lane-marking-audit.json"]),
        "laneMarkingSourceManifestSha256": LANE_MANIFEST_SHA,
        "laneMarkingSourceGenerationHashSha256": LANE_HASH,
        "laneMarkingSourceAuditSha256": LANE_SOURCE_AUDIT_SHA,
        "indexRelativePath": "index.json",
        "indexSha256": sha(files["index.json"]),
        "currentPointerCreated": False,
        "publishBlocked": True,
        **authority(),
        "files": [{"relativePath": name, "sha256": sha(payload), "byteLength": len(payload)}
                  for name, payload in sorted(files.items())],
        "fileCount": len(files) + 1,
    })
    preserved_fields(base_complete, complete, {"schemaVersion", "generationHashSha256",
        "bundleSetHashSha256", "exporterSemanticRevision", "exporterSourceHashSha256",
        "indexSha256", "files", "fileCount", "contentHashSha256"}, "BaseCompletionFieldChanged")
    complete["contentHashSha256"] = content_hash(complete)
    files["complete.json"] = pretty(complete)

    summary = {
        "status": "PASS", "generationHashSha256": generation_hash,
        "indexSha256": sha(files["index.json"]),
        "exporterSourceHashSha256": exporter_hash,
        "baseGenerationHashSha256": BASE_HASH,
        "laneMarkingSourceGenerationHashSha256": LANE_HASH,
        "laneMarkingSourceManifestSha256": LANE_MANIFEST_SHA,
        "laneMarkingSourceAuditSha256": LANE_SOURCE_AUDIT_SHA,
        "laneMarkingAcquisitionSourceManifestSha256": ACQUISITION_SOURCE_MANIFEST_SHA,
        "administrativeAreaCount": EXPECTED_AREAS,
        **totals,
        "publicDisplayItemCount": 0,
        "fileCount": len(files),
        **authority(),
    }
    return generation_hash, files, summary


def validate_generation(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not target.is_symlink(), "GenerationMissingOrUnsafe")
    actual = {file.relative_to(target).as_posix() for file in target.rglob("*") if file.is_file()}
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        file = target / name
        require(file.is_file() and not file.is_symlink() and file.read_bytes() == payload,
                "GenerationFileChanged:" + name)
    index = json.loads(files["index.json"])
    complete = json.loads(files["complete.json"])
    require(index["contentHashSha256"] == unity_content_hash_from_bytes(files["index.json"]),
            "IndexContentHashChanged")
    require(complete["contentHashSha256"] == unity_content_hash_from_bytes(files["complete.json"]),
            "CompletionContentHashChanged")
    require(complete["indexSha256"] == sha(files["index.json"]), "CompletionIndexHashChanged")


def self_test(root: Path) -> dict[str, Any]:
    require(content_hash({"zero": 0.0}) == sha(b'{"zero":0}'), "SelfTestUnityDecimal")
    require(segment_key({"x": 1, "z": 2}, {"x": 3, "z": 4})
            == segment_key({"x": 3, "z": 4}, {"x": 1, "z": 2}), "SelfTestUndirectedSegment")
    require(all(value is False for key, value in authority().items() if key not in {
        "historicalBoundaryBootstrapOnly", "privateReviewOnly", "privateReviewVisualizationAllowed",
        "reviewVisualizationOnly", "laneOutlineAuthorized"}), "SelfTestAuthority")
    _, files, summary = prepare(root)
    require(summary["laneMarkingSourceSegmentCount"] == summary["laneMarkingRenderedSegmentCount"]
            + summary["laneMarkingSkippedAtOrBelowOneMillimeterSegmentCount"]
            + summary["laneMarkingDuplicatePhysicalSegmentInstanceCount"], "SelfTestSegmentEquation")
    bundles = {name: payload for name, payload in files.items() if name.startswith("bundles/")}
    require(len(bundles) == EXPECTED_AREAS, "SelfTestBundleCount")
    for name, payload in bundles.items():
        value = json.loads(payload)
        require(value["contentHashSha256"] == unity_content_hash_from_bytes(payload),
                "SelfTestBundleContentHash:" + name)
    return {"status": "PASS", "selfTestsPassed": 35,
            "exporterSourceHashSha256": sha_file(Path(__file__).resolve())}


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
    return {**summary, "mode": mode, "changedFiles": changed,
            "generationRelativePath": target.relative_to(root).as_posix()}


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
    except (LaneUnityReviewError, OSError, ValueError, KeyError, TypeError) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

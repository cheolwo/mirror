#!/usr/bin/env python3
"""Bind private 2025 biotope rings to the immutable r4 Unity review bundles.

This produces local review inputs for exterior/interior ring outlines only.
There is no filled polygon, collider, public, runtime, traversal, or gameplay authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import uuid
from decimal import Decimal
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
BASE = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r4/input/generations/3a7475c181e6be11d46f256b40150119b95c3473477f529fda7802b51053cd33")
BIOTOPE = Path("artifacts/local/validation/admin-dong-biotope-private-review/r1")
OUTPUT = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r5/input")
BASE_HASH = "3A7475C181E6BE11D46F256B40150119B95C3473477F529FDA7802B51053CD33"
BASE_INDEX_SHA = "6170476FEEB84A4584E4C2D4EE5B3EA9B6CF651BEF12FF39E21780C4F020E887"
BASE_COMPLETE_SHA = "28A08F2316CC14A257EA4AD33E6AF995858AAE2704BC6A121A72AED66FC4D974"
BIOTOPE_MANIFEST_SHA = "33347E9C4E8FBBF12A7C00E0BD09CE5898C68B4165F65244013EE3D3643609E2"
BIOTOPE_COMPLETE_SHA = "A5377A262461746A5DB75CABB9102E0ADA6D813DE851D872C5F3DADD9A32335E"
BIOTOPE_GENERATOR_SHA = "D070A131E9B4ED0AAA880B5726AA843EE9679908E0CDF5227078F288A02743B6"
SOURCE_SHA = "7FF3802116EC35BECABAAD8F5E8401C6AF8FC8C56FE1DFB526F39BF154657663"
RECEIPT_SHA = "CDF352945CF221B2850BC540D129396E32875250D8E1E8330DA78FED8E72AE0A"
BOUNDARY_SHA = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
EXPECTED = {"areas": 30, "candidates": 3181, "parts": 3408, "holes": 108,
            "ringVertices": 105731, "lineSegments": 102215,
            "subMillimeterLineSegments": 128, "renderableLineSegments": 102087,
            "strictRoundedBoundsOutsidePoints": 67}
PRESERVED_COUNTS = {
    "administrativeAreaCount": 30, "bundleCount": 30, "buildingCount": 61897,
    "roadSegmentCount": 11769, "tileCount": 297, "unresolvedBuildingCount": 27,
    "publicBusinessCount": 0, "displayOverlayItemCount": 0,
    "crosswalkPointCandidateCount": 1533, "pedestrianSignalPresentCandidateCount": 933,
    "intersectionPointCandidateCount": 554, "walkNetworkNodePointCandidateCount": 17267,
    "walkNetworkLinkFragmentCandidateCount": 23472,
    "walkNetworkLinkPathVertexCount": 54574, "walkNetworkOutputPathVertexCount": 71841,
    "roadDirectionPointCandidateCount": 8415,
}
BOUNDS_ROUNDING_TOLERANCE_MM = 0.5
MIN_RENDERABLE_SEGMENT_MM = 1.0
GENERATED_AT = "2026-09-26T00:00:00Z"
EXPORTER = "administrative-dong-diorama-unity-review-exporter.r6"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v5"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v5"
AUDIT_SCHEMA = "administrative-dong-diorama-unity-review-audit.v5"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v5"
OVERLAY_SCHEMA = "administrative-dong-private-biotope-overlays.v1"
OVERLAY_REVISION = "northeast-seoul-private-biotope-outline-overlay.r1"
STABLE_ID = re.compile(r"biotope:[0-9a-f]{32}\Z")
FORBIDDEN = re.compile(r"고유번호|관리번호|주소|전화|raw.?id|source.?id|management.?number|species|safety|passage|traversal|gameplay", re.I)


class ReviewError(RuntimeError):
    pass


def require(ok: bool, code: str) -> None:
    if not ok:
        raise ReviewError(code)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def pretty(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False).encode("utf-8") + b"\n"


def python_content_hash(value: dict[str, Any], field: str = "contentHashSha256") -> str:
    body = dict(value)
    body.pop(field, None)
    return sha(canonical(body))


def unity_canonical(value: Any) -> str:
    if isinstance(value, dict):
        return "{" + ",".join(unity_canonical(key) + ":" + unity_canonical(value[key])
                          for key in sorted(value)) + "}"
    if isinstance(value, list):
        return "[" + ",".join(unity_canonical(item) for item in value) + "]"
    if isinstance(value, Decimal):
        # Unity Mono's decimal.ToString(InvariantCulture) normalizes parsed 0.0 to 0.
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
    path = (root / relative).resolve()
    require(path == root or root in path.parents, "PathEscapesRepository")
    return path


def checked(path: Path, expected: str, length: int | None = None) -> bytes:
    require(path.is_file() and not path.is_symlink(), "InputMissingOrUnsafe:" + path.name)
    data = path.read_bytes()
    require(length is None or len(data) == length, "InputLengthChanged:" + path.name)
    require(sha(data) == expected, "InputHashChanged:" + path.name)
    return data


def relative_bundle(path: str) -> Path:
    relative = Path(path)
    require(len(relative.parts) == 2 and relative.parts[0] == "bundles"
            and relative.suffix == ".json" and re.fullmatch(r"[0-9]{10}\.json", relative.name),
            "BundlePathInvalid")
    return relative


def authority() -> dict[str, bool]:
    return {
        "privateReviewVisualizationAllowed": True,
        "biotopeOutlineAuthorized": True,
        "allSourceSegmentsRendered": False,
        "filledPolygonAuthorized": False,
        "colliderAuthorized": False,
        "legalEffectAuthorized": False,
        "speciesObservationAuthorized": False,
        "safetyAuthorized": False,
        "passageAuthorized": False,
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


def scan_candidate(value: Any) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            require(not FORBIDDEN.search(key), "ForbiddenCandidateField:" + key)
            scan_candidate(child)
    elif isinstance(value, list):
        for child in value:
            scan_candidate(child)


def load_inputs(root: Path):
    base = safe(root, BASE)
    biotope = safe(root, BIOTOPE)
    index = json.loads(checked(base / "index.json", BASE_INDEX_SHA))
    complete = json.loads(checked(base / "complete.json", BASE_COMPLETE_SHA))
    bio_manifest = json.loads(checked(biotope / "manifest.json", BIOTOPE_MANIFEST_SHA))
    bio_complete = json.loads(checked(biotope / "complete.json", BIOTOPE_COMPLETE_SHA))
    require(index["schemaVersion"] == "administrative-dong-diorama-unity-review-index.v4"
            and complete["schemaVersion"] == "administrative-dong-diorama-unity-review-completion.v4"
            and index["generationHashSha256"] == BASE_HASH
            and complete["generationHashSha256"] == BASE_HASH, "BaseGenerationChanged")
    require(index["contentHashSha256"] == python_content_hash(index)
            and complete["contentHashSha256"] == python_content_hash(complete), "BaseContentHashChanged")
    require(index["roadDirectionAuditRelativePath"] == "audit.json"
            and index["roadDirectionAuditSha256"] == complete["roadDirectionAuditSha256"],
            "BaseDirectionAuditDescriptorChanged")
    base_audit_descriptor = next((item for item in complete["files"]
                                  if item["relativePath"] == "audit.json"), None)
    require(base_audit_descriptor is not None
            and base_audit_descriptor["sha256"] == index["roadDirectionAuditSha256"],
            "BaseDirectionAuditCompletionChanged")
    base_direction_audit = checked(base / "audit.json", index["roadDirectionAuditSha256"],
                                   base_audit_descriptor["byteLength"])
    base_audit = json.loads(base_direction_audit)
    require(base_audit["schemaVersion"] == "administrative-dong-diorama-unity-review-audit.v4"
            and base_audit["generationHashSha256"] == BASE_HASH
            and base_audit["contentHashSha256"] == python_content_hash(base_audit),
            "BaseDirectionAuditChanged")
    require(all(index.get(k) == v for k, v in PRESERVED_COUNTS.items())
            and len(index["bundles"]) == EXPECTED["areas"]
            and index["displayOverlayItemCount"] == 0, "BaseCountsChanged")
    require(bio_manifest["schemaVersion"] == "administrative-dong-biotope-private-manifest.v1"
            and bio_complete["schemaVersion"] == "administrative-dong-biotope-private-complete.v1"
            and bio_complete["manifestSha256"] == BIOTOPE_MANIFEST_SHA
            and bio_manifest["sourceDatasetId"] == "OA-21145"
            and bio_manifest["sourceVintage"] == "2025"
            and bio_manifest["sourceSha256"] == SOURCE_SHA
            and bio_manifest["sourceReceiptSha256"] == RECEIPT_SHA
            and bio_manifest["historicalBoundaryDatasetId"] == "OA-22160"
            and bio_manifest["historicalBoundaryVintage"] == "2023"
            and bio_manifest["historicalBoundarySha256"] == BOUNDARY_SHA
            and bio_manifest["generatorSha256"] == BIOTOPE_GENERATOR_SHA
            and bio_manifest["administrativeAreaCount"] == EXPECTED["areas"]
            and bio_manifest["candidateCount"] == EXPECTED["candidates"]
            and bio_complete["fileCount"] == 33
            and len(bio_manifest["files"]) == 31, "BiotopeSourceGenerationChanged")
    require(all(value is False for value in bio_manifest["authority"].values()), "BiotopeSourceAuthorityChanged")
    descriptors = {entry["path"]: entry for entry in bio_manifest["files"]}
    require(len(descriptors) == 31 and "audit.json" in descriptors, "BiotopeDescriptorSetChanged")
    for name, entry in descriptors.items():
        relative = Path(name)
        require(not relative.is_absolute() and ".." not in relative.parts, "BiotopeDescriptorPathUnsafe")
        checked(safe(biotope, relative), entry["sha256"], entry["bytes"])
    require(set(descriptors) == {"audit.json"} | {f"dong/{entry['administrativeAreaStableId'].split(':')[-1]}.json" for entry in index["bundles"]}, "BiotopeDongSetChanged")
    return base, biotope, index, complete, bio_manifest, descriptors, base_direction_audit


def check_ring(ring: Any, bounds: dict[str, Any]) -> tuple[int, int, float, int]:
    require(isinstance(ring, list) and len(ring) >= 4 and ring[0] == ring[-1], "BiotopeRingNotClosed")
    strict_outside = 0
    max_excess = 0.0
    for point in ring:
        require(isinstance(point, list) and len(point) == 2
                and all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in point),
                "BiotopeRingPointInvalid")
        x, z = point
        excess = max(bounds["minX"] * 1000 - x, x - bounds["maxX"] * 1000,
                     bounds["minZ"] * 1000 - z, z - bounds["maxZ"] * 1000, 0.0)
        if excess > 0:
            strict_outside += 1
        max_excess = max(max_excess, excess)
        require(excess <= BOUNDS_ROUNDING_TOLERANCE_MM + 1e-9,
                "BiotopeRingPointOutsideManifestQuantizationEnvelope")
    non_renderable = sum(math.hypot(a[0] - b[0], a[1] - b[1]) <= MIN_RENDERABLE_SEGMENT_MM
                         for a, b in zip(ring, ring[1:]))
    return len(ring), strict_outside, max_excess, non_renderable


def inspected_candidates(rows: Any, bounds: dict[str, Any]):
    require(isinstance(rows, list) and rows, "BiotopeDongEmpty")
    scan_candidate(rows)
    ids = set()
    counts = {"candidateCount": 0, "polygonPartCount": 0, "interiorRingCount": 0,
              "ringVertexCount": 0, "sourceLineSegmentCount": 0,
              "subMillimeterLineSegmentCount": 0, "renderableLineSegmentCount": 0,
              "strictRoundedBoundsOutsidePointCount": 0}
    maximum_excess = 0.0
    for row in rows:
        require(set(row) == {"stableId", "classification", "polygons"}, "BiotopeCandidateSchemaChanged")
        stable_id = row["stableId"]
        require(isinstance(stable_id, str) and STABLE_ID.fullmatch(stable_id)
                and stable_id not in ids, "BiotopeStableIdInvalidOrDuplicate")
        ids.add(stable_id)
        classification = row["classification"]
        require(set(classification) == {"typeCode", "evaluationGrade", "legendLabel"}
                and all(isinstance(value, str) and len(value) <= 50 for value in classification.values()),
                "BiotopeClassificationSchemaChanged")
        polygons = row["polygons"]
        require(isinstance(polygons, list) and polygons, "BiotopePolygonMissing")
        counts["candidateCount"] += 1
        for polygon in polygons:
            require(set(polygon) == {"exteriorRingMm", "interiorRingsMm"}
                    and isinstance(polygon["interiorRingsMm"], list), "BiotopePolygonSchemaChanged")
            counts["polygonPartCount"] += 1
            counts["interiorRingCount"] += len(polygon["interiorRingsMm"])
            for ring in [polygon["exteriorRingMm"], *polygon["interiorRingsMm"]]:
                vertices, outside, excess, non_renderable = check_ring(ring, bounds)
                counts["ringVertexCount"] += vertices
                counts["sourceLineSegmentCount"] += vertices - 1
                counts["subMillimeterLineSegmentCount"] += non_renderable
                counts["renderableLineSegmentCount"] += vertices - 1 - non_renderable
                counts["strictRoundedBoundsOutsidePointCount"] += outside
                maximum_excess = max(maximum_excess, excess)
    return counts, round(maximum_excess, 6)


def overlay(area: str, rows: list[dict[str, Any]], counts: dict[str, int], maximum_excess: float,
            source: dict[str, Any], frame: dict[str, Any]):
    value = {
        "schemaVersion": OVERLAY_SCHEMA,
        "revision": OVERLAY_REVISION,
        "overlaySetStableId": "private-biotope-outline-overlay:sha256:" +
            sha(canonical({"area": area, "biotopeManifestSha256": BIOTOPE_MANIFEST_SHA})).lower(),
        "administrativeAreaStableId": area,
        "sourceVintage": "oa21145-file-2025+oa22160-file-20231031",
        "sourceReceipts": [
            {"datasetId": "OA-21145", "sourceRevision": "2025",
             "sourceArchiveSha256": source["sourceSha256"],
             "sourceReceiptSha256": source["sourceReceiptSha256"],
             "candidateManifestSha256": BIOTOPE_MANIFEST_SHA},
            {"datasetId": "OA-22160", "sourceRevision": "2023 historical boundary",
             "sourceArchiveSha256": source["historicalBoundarySha256"]},
        ],
        "coordinateFrame": frame,
        "coordinateUnit": "millimeter",
        "renderGeometry": "ExteriorAndInteriorRingLineSegmentsOnly",
        "shortSegmentPolicy": "SourceSegmentsAtOrBelowOneMillimeterPreservedButNotRenderedAndNeverEnlarged",
        "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
        "manifestBoundsRoundingToleranceMillimeters": BOUNDS_ROUNDING_TOLERANCE_MM,
        "maximumManifestBoundsExcessMillimeters": maximum_excess,
        "candidatePolygons": rows,
        **counts,
        **authority(),
    }
    value["overlaySetHashSha256"] = content_hash(value, "overlaySetHashSha256")
    return value


def preserved_fields(old: dict[str, Any], new: dict[str, Any], changed: set[str], code: str) -> None:
    for key, value in old.items():
        if key not in changed:
            require(key in new and new[key] == value, code + ":" + key)


def require_capture_ready(index: dict[str, Any], bundle_values: list[Any],
                          complete: dict[str, Any]) -> None:
    field = "privateBiotopeOverlayCaptureReady"
    require(index.get(field) is True, "BiotopeIndexCaptureNotReady")
    descriptors = index.get("bundles")
    require(isinstance(descriptors, list) and bool(descriptors)
            and all(isinstance(item, dict) and item.get(field) is True for item in descriptors),
            "BiotopeIndexBundleCaptureNotReady")
    require(bool(bundle_values) and all(value is True for value in bundle_values),
            "BiotopeBundleCaptureNotReady")
    require(complete.get(field) is True, "BiotopeCompletionCaptureNotReady")


def require_completion_marker(index_bytes: bytes, complete: dict[str, Any]) -> None:
    index_sha = sha(index_bytes)
    require(complete.get("indexRelativePath") == "index.json", "BiotopeCompletionIndexPathInvalid")
    require(complete.get("indexSha256") == index_sha, "BiotopeCompletionIndexHashInvalid")
    require(complete.get("publishBlocked") is True, "BiotopeCompletionPublishNotBlocked")
    descriptors = complete.get("files")
    require(isinstance(descriptors, list) and len([item for item in descriptors
            if isinstance(item, dict) and item.get("relativePath") == "index.json"
            and item.get("sha256") == index_sha and item.get("byteLength") == len(index_bytes)]) == 1,
            "BiotopeCompletionIndexDescriptorInvalid")


def prepare(root: Path):
    base, biotope, base_index, base_complete, bio_manifest, descriptors, base_direction_audit = load_inputs(root)
    exporter_hash = sha_file(Path(__file__).resolve())
    files: dict[str, bytes] = {}
    files["road-direction-audit.json"] = base_direction_audit
    entries = []
    overlays = []
    bundle_readiness = []
    area_audit = {}
    totals = {key: 0 for key in ("candidateCount", "polygonPartCount", "interiorRingCount",
                                  "ringVertexCount", "sourceLineSegmentCount",
                                  "subMillimeterLineSegmentCount", "renderableLineSegmentCount",
                                  "strictRoundedBoundsOutsidePointCount")}
    maximum_excess = 0.0
    seen_areas = set()
    for entry in sorted(base_index["bundles"], key=lambda item: item["administrativeAreaStableId"]):
        area = entry["administrativeAreaStableId"]
        require(area not in seen_areas and re.fullmatch(r"region:kr:hjd:[0-9]{10}", area), "BaseAreaDuplicateOrInvalid")
        seen_areas.add(area)
        relative = relative_bundle(entry["relativePath"])
        base_data = checked(safe(base, relative), entry["sha256"], entry["byteLength"])
        bundle = json.loads(base_data)
        require(bundle["schemaVersion"] == "administrative-dong-diorama-unity-review-bundle.v4"
                and bundle["administrativeAreaStableId"] == area
                and bundle["contentHashSha256"] == python_content_hash(bundle)
                and bundle["displayOverlays"]["items"] == [], "BaseBundleChanged")
        require(bundle["privateObservationOverlays"]["overlaySetHashSha256"]
                == entry["privateObservationOverlaySetHashSha256"], "BaseObservationHashChanged")
        require(bundle["privateWalkNetworkOverlaySetHashSha256"]
                == entry["privateWalkNetworkOverlaySetHashSha256"]
                == bundle["privateWalkNetworkOverlays"]["overlaySetHashSha256"],
                "BaseWalkHashChanged")
        require(bundle["privateRoadDirectionOverlaySetHashSha256"]
                == entry["privateRoadDirectionOverlaySetHashSha256"]
                == bundle["privateRoadDirectionOverlays"]["overlaySetHashSha256"],
                "BaseDirectionHashChanged")
        code = area.rsplit(":", 1)[-1]
        descriptor = descriptors[f"dong/{code}.json"]
        source_data = checked(safe(biotope, Path(descriptor["path"])), descriptor["sha256"], descriptor["bytes"])
        source_bundle = json.loads(source_data)
        require(source_bundle["schemaVersion"] == "administrative-dong-biotope-private-candidate.v1"
                and source_bundle["administrativeAreaStableId"] == area
                and source_bundle["sourceDatasetId"] == "OA-21145"
                and source_bundle["sourceVintage"] == "2025"
                and source_bundle["historicalBoundaryDatasetId"] == "OA-22160"
                and source_bundle["historicalBoundaryVintage"] == "2023"
                and source_bundle["coordinateUnit"] == "millimeter"
                and all(source_bundle[key] is False for key in ("publicAllowed", "databaseAllowed", "mongoAllowed",
                    "currentAllowed", "unityAllowed", "runtimeAllowed", "traversalAllowed", "gameplayAllowed")),
                "BiotopeDongSourceChanged")
        require(source_bundle["coordinateFrame"] == bundle["manifest"]["coordinateFrame"], "BiotopeFrameChanged")
        counts, excess = inspected_candidates(source_bundle["candidates"], bundle["manifest"]["bounds"])
        for key in totals:
            totals[key] += counts[key]
        maximum_excess = max(maximum_excess, excess)
        area_audit[area] = {**counts, "maximumManifestBoundsExcessMillimeters": excess}
        value = overlay(area, source_bundle["candidates"], counts, excess,
                        bio_manifest, source_bundle["coordinateFrame"])
        require(value["overlaySetHashSha256"] == content_hash(value, "overlaySetHashSha256"), "BiotopeOverlayHashChanged")
        overlays.append(value)
        next_bundle = dict(bundle)
        next_bundle.update({
            "schemaVersion": BUNDLE_SCHEMA,
            "sourceVintage": bundle["sourceVintage"] + "+oa21145-file-2025+biotope-private-review-r1",
            "generatedAtUtc": GENERATED_AT,
            "exporterSemanticRevision": EXPORTER,
            "exporterSourceHashSha256": exporter_hash,
            "privateBiotopeOverlaySetHashSha256": value["overlaySetHashSha256"],
            "privateBiotopeOverlays": value,
            "privateBiotopeOverlayCaptureReady": True,
            "biotopeCandidateCount": counts["candidateCount"],
            "biotopePolygonPartCount": counts["polygonPartCount"],
            "biotopeInteriorRingCount": counts["interiorRingCount"],
            "biotopeRingVertexCount": counts["ringVertexCount"],
            "sourceLineSegmentCount": counts["sourceLineSegmentCount"],
            "subMillimeterLineSegmentCount": counts["subMillimeterLineSegmentCount"],
            "renderableLineSegmentCount": counts["renderableLineSegmentCount"],
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            **authority(),
        })
        preserved_fields(bundle, next_bundle, {"schemaVersion", "sourceVintage", "generatedAtUtc",
                                                  "exporterSemanticRevision", "exporterSourceHashSha256",
                                                  "contentHashSha256"}, "BaseBundleFieldChanged")
        bundle_readiness.append(next_bundle.get("privateBiotopeOverlayCaptureReady"))
        next_bundle["contentHashSha256"] = content_hash(next_bundle)
        payload = pretty(next_bundle)
        files[relative.as_posix()] = payload
        next_entry = dict(entry)
        next_entry.update({
            "sha256": sha(payload), "byteLength": len(payload),
            "contentHashSha256": next_bundle["contentHashSha256"],
            "privateBiotopeOverlaySetHashSha256": value["overlaySetHashSha256"],
            "privateBiotopeOverlayCaptureReady": True,
            "biotopeCandidateCount": counts["candidateCount"],
            "biotopePolygonPartCount": counts["polygonPartCount"],
            "biotopeInteriorRingCount": counts["interiorRingCount"],
            "biotopeRingVertexCount": counts["ringVertexCount"],
            "sourceLineSegmentCount": counts["sourceLineSegmentCount"],
            "subMillimeterLineSegmentCount": counts["subMillimeterLineSegmentCount"],
            "renderableLineSegmentCount": counts["renderableLineSegmentCount"],
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
        })
        preserved_fields(entry, next_entry, {"sha256", "byteLength", "contentHashSha256"}, "BaseIndexEntryChanged")
        entries.append(next_entry)
    require(len(entries) == EXPECTED["areas"] and len(seen_areas) == EXPECTED["areas"], "BiotopeDongCoverageChanged")
    for field, expected in (("candidateCount", EXPECTED["candidates"]),
                            ("polygonPartCount", EXPECTED["parts"]),
                            ("interiorRingCount", EXPECTED["holes"]),
                            ("ringVertexCount", EXPECTED["ringVertices"]),
                            ("sourceLineSegmentCount", EXPECTED["lineSegments"]),
                            ("subMillimeterLineSegmentCount", EXPECTED["subMillimeterLineSegments"]),
                            ("renderableLineSegmentCount", EXPECTED["renderableLineSegments"]),
                            ("strictRoundedBoundsOutsidePointCount", EXPECTED["strictRoundedBoundsOutsidePoints"])):
        require(totals[field] == expected, "BiotopeTotalChanged:" + field)
    require(maximum_excess <= BOUNDS_ROUNDING_TOLERANCE_MM, "BiotopeManifestBoundsExcessChanged")
    generation_hash = sha(canonical({
        "baseGenerationHashSha256": BASE_HASH, "baseIndexSha256": BASE_INDEX_SHA,
        "baseCompleteSha256": BASE_COMPLETE_SHA,
        "biotopeManifestSha256": BIOTOPE_MANIFEST_SHA,
        "biotopeCompleteSha256": BIOTOPE_COMPLETE_SHA,
        "roadDirectionAuditSha256": sha(base_direction_audit),
        "exporterSourceHashSha256": exporter_hash,
        "bundleHashes": [item["sha256"] for item in entries],
        "overlaySetHashes": [item["overlaySetHashSha256"] for item in overlays],
    }))
    policy = {
        "renderGeometry": "ExteriorAndInteriorRingLineSegmentsOnly",
        "shortSegmentPolicy": "SourceSegmentsAtOrBelowOneMillimeterPreservedButNotRenderedAndNeverEnlarged",
        "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
        "manifestBoundsPrecision": "ExistingBoundsRoundedToOneMillimeter",
        "manifestBoundsRoundingToleranceMillimeters": BOUNDS_ROUNDING_TOLERANCE_MM,
        "strictRoundedBoundsOutsidePointCount": totals["strictRoundedBoundsOutsidePointCount"],
        "maximumManifestBoundsExcessMillimeters": round(maximum_excess, 6),
        **authority(),
    }
    audit = {
        "schemaVersion": AUDIT_SCHEMA, "generationHashSha256": generation_hash,
        "baseGenerationHashSha256": BASE_HASH, "biotopeManifestSha256": BIOTOPE_MANIFEST_SHA,
        "roadDirectionAuditRelativePath": "road-direction-audit.json",
        "roadDirectionAuditSha256": sha(base_direction_audit),
        "exporterSemanticRevision": EXPORTER, "exporterSourceHashSha256": exporter_hash,
        "counts": {"administrativeAreas": EXPECTED["areas"], **totals,
                   "publicDisplayItems": 0, "forbiddenBiotopeCandidateFields": 0},
        "perAdministrativeArea": dict(sorted(area_audit.items())),
        "allRingPointsWithinManifestBoundsQuantizationEnvelope": True,
        "baseObservationWalkDirectionValuesPreserved": True,
        **policy,
    }
    audit["contentHashSha256"] = content_hash(audit)
    files["audit.json"] = pretty(audit)
    index = dict(base_index)
    index.update({
        "schemaVersion": INDEX_SCHEMA,
        "sourceVintage": base_index["sourceVintage"] + "+oa21145-file-2025+biotope-private-review-r1",
        "generatedAtUtc": GENERATED_AT,
        "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "generationHashSha256": generation_hash, "bundleSetHashSha256": generation_hash,
        "privateBiotopeOverlaySetHashSha256": sha(canonical([o["overlaySetHashSha256"] for o in overlays])),
        "privateBiotopeOverlayCaptureReady": True,
        "biotopeCandidateCount": totals["candidateCount"],
        "biotopePolygonPartCount": totals["polygonPartCount"],
        "biotopeInteriorRingCount": totals["interiorRingCount"],
        "biotopeRingVertexCount": totals["ringVertexCount"],
        "sourceLineSegmentCount": totals["sourceLineSegmentCount"],
        "subMillimeterLineSegmentCount": totals["subMillimeterLineSegmentCount"],
        "renderableLineSegmentCount": totals["renderableLineSegmentCount"],
        "roadDirectionAuditRelativePath": "road-direction-audit.json",
        "roadDirectionAuditSha256": sha(files["road-direction-audit.json"]),
        "biotopeAuditRelativePath": "audit.json", "biotopeAuditSha256": sha(files["audit.json"]),
        "biotopeSourceManifestSha256": BIOTOPE_MANIFEST_SHA,
        "biotopePresentationPolicy": policy,
        **authority(),
        "bundles": entries,
    })
    preserved_fields(base_index, index, {"schemaVersion", "sourceVintage", "generatedAtUtc",
                                         "exporterSemanticRevision", "exporterSourceHashSha256",
                                         "generationHashSha256", "bundleSetHashSha256", "bundles",
                                         "roadDirectionAuditRelativePath",
                                         "contentHashSha256"}, "BaseIndexFieldChanged")
    require(all(index[key] == value for key, value in PRESERVED_COUNTS.items()), "PreservedCountsChanged")
    index["contentHashSha256"] = content_hash(index)
    files["index.json"] = pretty(index)
    complete = dict(base_complete)
    complete.update({
        "schemaVersion": COMPLETE_SCHEMA,
        "generationHashSha256": generation_hash, "bundleSetHashSha256": generation_hash,
        "exporterSemanticRevision": EXPORTER, "exporterSourceHashSha256": exporter_hash,
        "baseDirectionReviewGenerationHashSha256": BASE_HASH,
        "biotopeSourceManifestSha256": BIOTOPE_MANIFEST_SHA,
        "biotopeCandidateCount": totals["candidateCount"],
        "biotopePolygonPartCount": totals["polygonPartCount"],
        "biotopeInteriorRingCount": totals["interiorRingCount"],
        "biotopeRingVertexCount": totals["ringVertexCount"],
        "sourceLineSegmentCount": totals["sourceLineSegmentCount"],
        "subMillimeterLineSegmentCount": totals["subMillimeterLineSegmentCount"],
        "renderableLineSegmentCount": totals["renderableLineSegmentCount"],
        "privateBiotopeOverlaySetHashSha256": index["privateBiotopeOverlaySetHashSha256"],
        "privateBiotopeOverlayCaptureReady": True,
        "roadDirectionAuditRelativePath": "road-direction-audit.json",
        "biotopeAuditSha256": sha(files["audit.json"]),
        "indexRelativePath": "index.json",
        "indexSha256": sha(files["index.json"]),
        "publishBlocked": True,
        "biotopePresentationPolicy": policy,
        **authority(),
        "files": [{"relativePath": name, "sha256": sha(data), "byteLength": len(data)}
                  for name, data in sorted(files.items())],
        "fileCount": len(files) + 1,
    })
    require_capture_ready(index, bundle_readiness, complete)
    require_completion_marker(files["index.json"], complete)
    preserved_fields(base_complete, complete, {"schemaVersion", "generationHashSha256", "bundleSetHashSha256",
                                               "exporterSemanticRevision", "exporterSourceHashSha256",
                                               "files", "contentHashSha256"}, "BaseCompletionFieldChanged")
    complete["contentHashSha256"] = content_hash(complete)
    files["complete.json"] = pretty(complete)
    summary = {
        "status": "PASS", "generationHashSha256": generation_hash,
        "indexSha256": sha(files["index.json"]),
        "baseGenerationHashSha256": BASE_HASH,
        "biotopeSourceManifestSha256": BIOTOPE_MANIFEST_SHA,
        "administrativeAreaCount": EXPECTED["areas"], **totals,
        "maximumManifestBoundsExcessMillimeters": round(maximum_excess, 6),
        "preservedBaseCounts": PRESERVED_COUNTS,
        "publicDisplayItems": 0, "forbiddenBiotopeCandidateFields": 0,
        "fileCount": len(files), **authority(),
    }
    return generation_hash, files, summary


def validate(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not target.is_symlink(), "GenerationMissingOrUnsafe")
    actual = {p.relative_to(target).as_posix() for p in target.rglob("*") if p.is_file()}
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        require((target / name).is_file() and (target / name).read_bytes() == payload,
                "GenerationFileChanged:" + name)


def self_test(root: Path) -> dict[str, Any]:
    require(content_hash({"a": 1, "contentHashSha256": "ignored"}) == sha(b'{"a":1}'), "SelfTestHash")
    require(canonical({"b": 2, "a": 1}) == b'{"a":1,"b":2}', "SelfTestCanonical")
    require(content_hash({"zero": 0.0}) == sha(b'{"zero":0}')
            and python_content_hash({"zero": 0.0}) != content_hash({"zero": 0.0}),
            "SelfTestUnityMonoDecimalZero")
    bounds = {"minX": 0.0, "minZ": 0.0, "maxX": 1.0, "maxZ": 1.0}
    ring = [[-0.49, 0], [1000.49, 0], [1000.49, 1000], [-0.49, 0]]
    require(check_ring(ring, bounds)[1] == 4, "SelfTestQuantizedBounds")
    try:
        check_ring([[0, 0], [1000.501, 0], [0, 1000], [0, 0]], bounds)
    except ReviewError:
        pass
    else:
        raise ReviewError("SelfTestBoundsFailClosed")
    try:
        scan_candidate({"rawId": 12})
    except ReviewError:
        pass
    else:
        raise ReviewError("SelfTestForbiddenField")
    require(all(value is False for key, value in authority().items()
                if key not in {"privateReviewVisualizationAllowed", "biotopeOutlineAuthorized"}), "SelfTestAuthority")
    field = "privateBiotopeOverlayCaptureReady"
    require_capture_ready({field: True, "bundles": [{field: True}]}, [True], {field: True})
    for location in ("index", "index_bundle", "bundle", "complete"):
        for bad_value in (None, False):
            test_index, test_bundles, test_complete = {field: True, "bundles": [{field: True}]}, [True], {field: True}
            if location == "index":
                if bad_value is None:
                    test_index.pop(field)
                else:
                    test_index[field] = bad_value
            elif location == "index_bundle":
                test_index["bundles"] = [{} if bad_value is None else {field: bad_value}]
            elif location == "bundle":
                test_bundles = [bad_value]
            else:
                test_complete = {} if bad_value is None else {field: bad_value}
            try:
                require_capture_ready(test_index, test_bundles, test_complete)
            except ReviewError:
                pass
            else:
                raise ReviewError("SelfTestCaptureReadinessFailClosed:" + location)
    index_bytes = b"{}"
    marker = {"indexRelativePath": "index.json", "indexSha256": sha(index_bytes),
              "publishBlocked": True,
              "files": [{"relativePath": "index.json", "sha256": sha(index_bytes),
                         "byteLength": len(index_bytes)}]}
    require_completion_marker(index_bytes, marker)
    for key, wrong in (("indexRelativePath", "elsewhere.json"),
                       ("indexSha256", "0" * 64), ("publishBlocked", False)):
        for remove in (True, False):
            invalid = dict(marker)
            if remove:
                invalid.pop(key)
            else:
                invalid[key] = wrong
            try:
                require_completion_marker(index_bytes, invalid)
            except ReviewError:
                pass
            else:
                raise ReviewError("SelfTestCompletionMarkerFailClosed:" + key)
    _, files, _ = prepare(root)
    bundle_files = {name: payload for name, payload in files.items() if name.startswith("bundles/")}
    require(len(bundle_files) == EXPECTED["areas"], "SelfTestBundleSetChanged")
    for name, payload in bundle_files.items():
        bundle = json.loads(payload, parse_float=Decimal)
        require(bundle["contentHashSha256"] == unity_content_hash_from_bytes(payload),
                "SelfTestUnityBundleContentHash:" + name)
    return {"status": "PASS", "selfTestsPassed": 53,
            "exporterSourceHashSha256": sha_file(Path(__file__).resolve())}


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        return self_test(root)
    generation_hash, files, summary = prepare(root)
    target = safe(root, OUTPUT / "generations" / generation_hash.lower())
    if mode == "verify" or target.exists():
        validate(target, files)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = target.parent / (".staging-" + uuid.uuid4().hex)
        staging.mkdir()
        for name, data in files.items():
            path = staging / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        validate(staging, files)
        os.replace(staging, target)
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
    except (ReviewError, OSError, KeyError, ValueError, TypeError) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

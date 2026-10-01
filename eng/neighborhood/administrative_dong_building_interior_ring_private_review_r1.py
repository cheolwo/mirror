#!/usr/bin/env python3
"""Recover omitted AL_D010 interior rings as immutable private review traces.

The v1 diorama building contract retained only the largest exterior ring.  This
tool re-opens the exact frozen official AL_D010 archive, selects only buildings
already bound to the immutable r7 review generation, and emits the omitted
interior-ring geometry at the existing one-millimetre common ENU precision.

An interior ring is only source polygon structure.  It is not promoted to a
courtyard, entrance, access, occupancy, collider, current-state, public,
runtime, traversal, or gameplay fact.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import os
import re
import shutil
import stat
import struct
import sys
import uuid
import zipfile
from collections import Counter, defaultdict
from decimal import Decimal
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path("artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip")
SOURCE_RECEIPT = Path(
    "artifacts/local/neighborhood-source-acquisition/"
    "AL_D010_11_20260809.recovery-receipt.json"
)
SCOPE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider.r2.json"
)
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
BASE = Path(
    "artifacts/local/validation/admin-dong-diorama-observation-review/r7/input/"
    "generations/13a3b1c6d7854d1a4a03bfdfdd7766966d9b4b447f3e7fb3c662555707c7b753"
)
OUTPUT = Path(
    "artifacts/local/validation/"
    "admin-dong-building-interior-ring-private-review/r1/input"
)

SOURCE_SHA = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"
SOURCE_RECEIPT_SHA = "43FC3ABF60DE57ABDE33D8A6657A4FC221844F8DF40793C11E843BDF3E4A5576"
SOURCE_BYTES = 135_675_376
SOURCE_RECORDS = 695_761
SOURCE_VINTAGE = "file-2026-08-09"
SCOPE_SHA = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"
BATCH_SHA = "24BD8D246D2FA714A9C83065D5A8594179DED8E466F8B816FCD3BA02DB79351C"
BASE_HASH = "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753"
BASE_INDEX_SHA = "C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9"
BASE_COMPLETE_SHA = "26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D"
AREA_COUNT = 30
BASE_BUILDING_COUNT = 61_897
EXPECTED_CANDIDATES = 16
EXPECTED_RINGS = 18
EXPECTED_RING_VERTICES = 148
EXPECTED_RING_SEGMENTS = 130
EXPECTED_AREAS_WITH_CANDIDATES = 9
MIN_RENDERABLE_SEGMENT_MM = 1.0
FIXED_AT = "2026-09-26T00:00:00Z"

EXPECTED_ARCHIVE_MEMBERS = {
    "AL_D010_11_20260809.shp": (129_653_308, 0xF1B013CD),
    "AL_D010_11_20260809.prj": (553, 0x58AE2B5F),
    "AL_D010_11_20260809.dbf": (1_262_807_176, 0xF08C272C),
    "AL_D010_11_20260809.shx": (5_566_188, 0x6F3AE5ED),
    "AL_D010_11_20260809.fix": (8_349_145, 0xEBB51EF8),
}


class BuildingInteriorRingError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise BuildingInteriorRingError(code)


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


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def pretty(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode(
        "utf-8"
    )


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


def unity_content_hash(payload: bytes) -> str:
    value = json.loads(payload, parse_float=Decimal)
    require(isinstance(value, dict), "UnityCanonicalRootInvalid")
    value.pop("contentHashSha256", None)
    return sha(unity_canonical(value).encode("utf-8"))


def checked(path: Path, digest: str, length: int | None = None) -> Path:
    require(path.is_file() and not is_reparse(path), "InputMissingOrUnsafe:" + path.name)
    require(length is None or path.stat().st_size == length, "InputLengthChanged:" + path.name)
    require(sha_file(path) == digest, "InputHashChanged:" + path.name)
    return path


def load_module(path: Path, name: str) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    require(spec is not None and spec.loader is not None, "ModuleLoadFailed:" + path.name)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def authority() -> dict[str, bool]:
    return {
        "privateReviewOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "reviewVisualizationOnly": True,
        "privateReviewVisualizationAllowed": True,
        "sourceBuildingInteriorRingGeometryObserved": True,
        "sourceIdentityFieldRead": True,
        "currentBoundaryEstablished": False,
        "currentBuildingGeometryEstablished": False,
        "interiorVoidMeaningAuthorized": False,
        "courtyardMeaningAuthorized": False,
        "entranceMeaningAuthorized": False,
        "accessAuthorized": False,
        "occupancyAuthorized": False,
        "buildingCutoutAuthorized": False,
        "filledPolygonAuthorized": False,
        "colliderAuthorized": False,
        "legalEffectAuthorized": False,
        "publicDisplayAllowed": False,
        "distributionApproved": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "sourceCandidateUnityApplyAllowed": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
    }


def load_context(root: Path) -> tuple[Any, Any, dict[str, Any], Any]:
    for path in (
        safe(root, Path("artifacts/local/public-data/gis-runtime-r1")),
        safe(root, Path("artifacts/local/python-packages/spatial")),
        safe(root, Path("eng/neighborhood")),
    ):
        if path.is_dir() and str(path) not in sys.path:
            sys.path.insert(0, str(path))
    batch_path = checked(safe(root, BATCH), BATCH_SHA)
    batch = load_module(batch_path, "hongdal_building_interior_ring_batch")
    checked(safe(root, SCOPE), SCOPE_SHA)
    scope = batch.load_scope(root, safe(root, SCOPE))
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    return batch, dependencies, scope, frame


def load_receipt(root: Path) -> dict[str, Any]:
    path = checked(safe(root, SOURCE_RECEIPT), SOURCE_RECEIPT_SHA)
    value = json.loads(path.read_text(encoding="utf-8"))
    destination = value.get("destination", {})
    verification = value.get("verification", {})
    scope = value.get("scope", {})
    require(
        value.get("schemaVersion") == "local-source-recovery-receipt.v1"
        and value.get("operation") == "CopyVerifiedLocalSource"
        and destination.get("lengthBytes") == SOURCE_BYTES
        and destination.get("sha256") == SOURCE_SHA
        and verification.get("archiveReadableBeforeCopy") is True
        and verification.get("archiveEntryCount") == len(EXPECTED_ARCHIVE_MEMBERS)
        and verification.get("expectedArtifactMatches") is True
        and scope.get("localPrivateArtifactOnly") is True
        and scope.get("remotePublicationPerformed") is False
        and scope.get("unityApplicationPerformed") is False,
        "SourceReceiptContractChanged",
    )
    return value


def load_building_bindings(root: Path) -> tuple[dict[str, dict[str, Any]], dict[str, Any]]:
    base = safe(root, BASE)
    index_path = checked(base / "index.json", BASE_INDEX_SHA)
    complete_path = checked(base / "complete.json", BASE_COMPLETE_SHA)
    index_bytes = index_path.read_bytes()
    complete_bytes = complete_path.read_bytes()
    index = json.loads(index_bytes)
    complete = json.loads(complete_bytes)
    require(
        index.get("schemaVersion") == "administrative-dong-diorama-unity-review-index.v7"
        and complete.get("schemaVersion")
        == "administrative-dong-diorama-unity-review-completion.v7"
        and index.get("generationHashSha256") == BASE_HASH
        and complete.get("generationHashSha256") == BASE_HASH
        and index.get("contentHashSha256") == unity_content_hash(index_bytes)
        and complete.get("contentHashSha256") == unity_content_hash(complete_bytes)
        and index.get("buildingCount") == BASE_BUILDING_COUNT
        and index.get("bundleCount") == AREA_COUNT
        and index.get("privateHorizontalContourTraceOverlayCaptureReady") is True,
        "BaseGenerationChanged",
    )
    all_ids: set[str] = set()
    bindings: dict[str, dict[str, Any]] = {}
    area_ids: set[str] = set()
    common_frame: dict[str, Any] | None = None
    observed_buildings = 0
    for entry in sorted(index["bundles"], key=lambda value: value["administrativeAreaStableId"]):
        relative = Path(entry["relativePath"])
        require(
            len(relative.parts) == 2
            and relative.parts[0] == "bundles"
            and re.fullmatch(r"[0-9]{10}\.json", relative.name) is not None,
            "BaseBundlePathInvalid",
        )
        payload_path = checked(base / relative, entry["sha256"], entry["byteLength"])
        payload = payload_path.read_bytes()
        bundle = json.loads(payload)
        area = entry["administrativeAreaStableId"]
        require(
            bundle.get("schemaVersion")
            == "administrative-dong-diorama-unity-review-bundle.v7"
            and bundle.get("administrativeAreaStableId") == area
            and bundle.get("contentHashSha256") == unity_content_hash(payload)
            and bundle.get("displayOverlays", {}).get("items") == [],
            "BaseBundleChanged:" + area,
        )
        area_ids.add(area)
        frame = bundle["manifest"]["coordinateFrame"]
        common_frame = frame if common_frame is None else common_frame
        require(frame == common_frame, "BaseCoordinateFrameChanged")
        for tile in bundle["tiles"]:
            for building in tile["buildings"]:
                stable_id = building["buildingStableId"]
                require(stable_id not in all_ids, "BaseBuildingStableIdDuplicate")
                all_ids.add(stable_id)
                observed_buildings += 1
                if "LargestExteriorCandidate" not in building.get("evidenceKindCode", ""):
                    continue
                footprint = building.get("footprint")
                require(
                    re.fullmatch(r"vworld:al-d010:[0-9]{28}", stable_id) is not None
                    and isinstance(footprint, list)
                    and len(footprint) >= 3
                    and all(set(point) == {"x", "z"} for point in footprint),
                    "InteriorRingBuildingBindingInvalid",
                )
                bindings[stable_id] = {
                    "administrativeAreaStableId": area,
                    "tileStableId": tile["tileStableId"],
                    "footprint": footprint,
                    "manifestBounds": bundle["manifest"]["bounds"],
                }
    require(
        len(area_ids) == AREA_COUNT
        and observed_buildings == BASE_BUILDING_COUNT
        and len(all_ids) == BASE_BUILDING_COUNT
        and len(bindings) == EXPECTED_CANDIDATES,
        "BaseBuildingBindingCountsChanged",
    )
    return bindings, {
        "baseGenerationHashSha256": BASE_HASH,
        "baseIndexSha256": BASE_INDEX_SHA,
        "baseCompleteSha256": BASE_COMPLETE_SHA,
        "baseBuildingCount": BASE_BUILDING_COUNT,
        "interiorRingTargetBuildingCount": len(bindings),
        "coordinateFrame": common_frame,
    }


def segment_key(
    first: dict[str, int], second: dict[str, int]
) -> tuple[tuple[int, int], tuple[int, int]]:
    a = (first["x"], first["z"])
    b = (second["x"], second["z"])
    return (a, b) if a <= b else (b, a)


def ring_path(interior: Any, dependencies: Any) -> list[dict[str, int]]:
    canonical_ring = dependencies.canonical_ring(interior.coords, clockwise=False)
    path = [
        {"x": round(float(point["x"]) * 1000), "z": round(float(point["z"]) * 1000)}
        for point in canonical_ring
    ]
    require(
        len(path) >= 4
        and path[0] == path[-1]
        and len({(point["x"], point["z"]) for point in path[:-1]}) >= 3,
        "InteriorRingPathInvalid",
    )
    return path


def signed_area(path: list[dict[str, int]]) -> int:
    return sum(
        path[index]["x"] * path[index + 1]["z"]
        - path[index + 1]["x"] * path[index]["z"]
        for index in range(len(path) - 1)
    )


def read_candidates(
    root: Path,
    batch: Any,
    dependencies: Any,
    frame: Any,
    bindings: dict[str, dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    source = checked(safe(root, SOURCE), SOURCE_SHA, SOURCE_BYTES)
    load_receipt(root)
    raw_ids = {stable_id.rsplit(":", 1)[-1]: stable_id for stable_id in bindings}
    record_occurrences: Counter[str] = Counter()
    exterior_matches: Counter[str] = Counter()
    matched: dict[str, dict[str, Any]] = {}
    source_records = deleted_records = 0
    source_polygon_parts = source_rings = source_vertices = source_segments = 0
    short_segments = duplicate_segments = 0
    exterior_mismatches = containment_failures = boundary_touches = 0
    minimum_clearance_mm: int | None = None
    physical_by_area: dict[
        str, set[tuple[tuple[int, int], tuple[int, int]]]
    ] = defaultdict(set)

    with zipfile.ZipFile(source) as archive:
        infos = {info.filename: info for info in archive.infolist()}
        require(set(infos) == set(EXPECTED_ARCHIVE_MEMBERS), "SourceArchiveEntrySetChanged")
        for name, (length, crc) in EXPECTED_ARCHIVE_MEMBERS.items():
            require(
                infos[name].file_size == length and infos[name].CRC == crc,
                "SourceArchiveMemberChanged:" + name,
            )
        shp_entry = dependencies.zip_entry(archive, ".shp")
        dbf_entry = dependencies.zip_entry(archive, ".dbf")
        prj_entry = dependencies.zip_entry(archive, ".prj")
        projection_text = archive.read(prj_entry).decode("utf-8-sig")
        declared = dependencies.CRS.from_wkt(projection_text)
        normalized_wkt = projection_text.replace(" ", "")
        require(
            'AUTHORITY["EPSG","5186"]' in normalized_wkt
            and "Korea_Central_Belt_2010" in declared.name
            and declared.is_projected,
            "BuildingSourceCrsMismatch",
        )
        to_wgs = dependencies.Transformer.from_crs(declared, 4326, always_xy=True)
        with archive.open(shp_entry, "r") as shp_stream, archive.open(dbf_entry, "r") as dbf_stream:
            dependencies.read_shp_header(shp_stream)
            record_count, _, record_length, fields = dependencies.read_dbf_header(dbf_stream)
            require(
                record_count == SOURCE_RECORDS and "A1" in fields,
                "BuildingSourceRecordContractChanged",
            )
            for index in range(record_count):
                record = dependencies.read_exact(
                    dbf_stream, record_length, "BuildingDbfRecordTruncated"
                )
                header = dependencies.read_exact(
                    shp_stream, 8, "BuildingShpRecordHeaderTruncated"
                )
                record_number, content_words = struct.unpack(">II", header)
                require(record_number == index + 1, "BuildingShpRecordOrderMismatch")
                require(2 <= content_words <= 1_048_576, "BuildingShpRecordBudgetInvalid")
                body = dependencies.read_exact(
                    shp_stream, content_words * 2, "BuildingShpRecordBodyTruncated"
                )
                source_records += 1
                if record[0] == 0x2A:
                    deleted_records += 1
                    continue
                require(record[0] == 0x20, "BuildingDbfDeletionMarkerInvalid")
                feature_id = dependencies.dbf_text(record, fields["A1"], "ascii")
                if feature_id not in raw_ids:
                    continue
                stable_id = raw_ids[feature_id]
                record_occurrences[stable_id] += 1
                source_geometry = dependencies.rings_to_geometry(
                    dependencies.transform_source_rings(
                        dependencies.parse_shp_polygon(body), to_wgs, frame
                    )
                )
                polygons = batch._polygon_members(source_geometry, dependencies)
                require(bool(polygons), "InteriorRingSourceHasNoPolygon")
                polygons.sort(key=lambda polygon: (-polygon.area, polygon.wkb_hex))
                largest = dependencies.orient(polygons[0], sign=-1.0)
                outer = dependencies.canonical_ring(largest.exterior.coords, clockwise=True)
                if outer[:-1] != bindings[stable_id]["footprint"]:
                    exterior_mismatches += 1
                    continue
                exterior_matches[stable_id] += 1
                require(exterior_matches[stable_id] == 1, "InteriorRingExteriorBindingAmbiguous")
                require(len(polygons) == 1, "InteriorRingTargetMultipartUnexpected")
                rings = sorted(
                    (ring_path(interior, dependencies) for interior in largest.interiors),
                    key=lambda value: tuple((point["x"], point["z"]) for point in value),
                )
                require(bool(rings), "InteriorRingTargetHasNoInteriorRing")
                outer_polygon = dependencies.Polygon(
                    [(point["x"], point["z"]) for point in bindings[stable_id]["footprint"]]
                )
                area = bindings[stable_id]["administrativeAreaStableId"]
                ring_rows: list[dict[str, Any]] = []
                for ordinal, path in enumerate(rings):
                    require(signed_area(path) > 0, "InteriorRingOrientationChanged")
                    polygon = dependencies.Polygon(
                        [(point["x"] / 1000.0, point["z"] / 1000.0) for point in path]
                    )
                    require(
                        polygon.is_valid
                        and polygon.exterior.is_simple
                        and polygon.area > 0.000001,
                        "InteriorRingQuantizedGeometryInvalid",
                    )
                    if not outer_polygon.contains(polygon):
                        containment_failures += 1
                    if outer_polygon.boundary.intersects(polygon.boundary):
                        boundary_touches += 1
                    clearance = round(outer_polygon.boundary.distance(polygon.boundary) * 1000)
                    minimum_clearance_mm = (
                        clearance
                        if minimum_clearance_mm is None
                        else min(minimum_clearance_mm, clearance)
                    )
                    for first, second in zip(path, path[1:]):
                        length = math.hypot(
                            second["x"] - first["x"], second["z"] - first["z"]
                        )
                        if length <= MIN_RENDERABLE_SEGMENT_MM:
                            short_segments += 1
                        key = segment_key(first, second)
                        if key in physical_by_area[area]:
                            duplicate_segments += 1
                        else:
                            physical_by_area[area].add(key)
                    source_rings += 1
                    source_vertices += len(path)
                    source_segments += len(path) - 1
                    ring_rows.append(
                        {
                            "ringOrdinal": ordinal,
                            "commonEnuMillimeterPath": path,
                        }
                    )
                source_polygon_parts += len(polygons)
                source_geometry_hash = sha(source_geometry.wkb)
                candidate_hash = sha(
                    canonical(
                        {
                            "datasetId": "data-go-kr-15083092",
                            "sourceArchiveSha256": SOURCE_SHA,
                            "sourceRecordOrdinal": index,
                            "sourceGeometrySha256": source_geometry_hash,
                            "buildingStableId": stable_id,
                            "rings": ring_rows,
                        }
                    )
                ).lower()
                matched[stable_id] = {
                    "administrativeAreaStableId": area,
                    "candidateStableId": "building-interior-ring:sha256:" + candidate_hash,
                    "buildingStableId": stable_id,
                    "sourceRecordOrdinal": index,
                    "sourceGeometrySha256": source_geometry_hash,
                    "rings": ring_rows,
                }
            require(shp_stream.read(1) == b"", "BuildingShpContainsUnexpectedExtraRecords")

    require(
        source_records == SOURCE_RECORDS
        and set(exterior_matches) == set(bindings)
        and all(exterior_matches[value] == 1 for value in bindings)
        and len(matched) == EXPECTED_CANDIDATES
        and source_polygon_parts == EXPECTED_CANDIDATES
        and source_rings == EXPECTED_RINGS
        and source_vertices == EXPECTED_RING_VERTICES
        and source_segments == EXPECTED_RING_SEGMENTS
        and short_segments == 0
        and duplicate_segments == 0
        and containment_failures == 0
        and boundary_touches == 0,
        "InteriorRingSourceCountsChanged",
    )
    candidates = sorted(
        matched.values(),
        key=lambda value: (
            value["administrativeAreaStableId"],
            value["buildingStableId"],
        ),
    )
    per_area: dict[str, Counter[str]] = {
        f"region:kr:hjd:{code}": Counter(
            {
                "buildingInteriorRingCandidateCount": 0,
                "buildingInteriorRingCount": 0,
                "buildingInteriorRingPathVertexCount": 0,
                "buildingInteriorRingSourceSegmentCount": 0,
                "buildingInteriorRingRenderableSegmentCount": 0,
                "buildingInteriorRingSkippedAtOrBelowOneMillimeterSegmentCount": 0,
                "buildingInteriorRingDuplicatePhysicalSegmentInstanceCount": 0,
            }
        )
        for code in sorted(
            value["administrativeAreaStableId"].rsplit(":", 1)[-1]
            for value in bindings.values()
        )
    }
    # Include all 30 areas from the immutable base, including zero-candidate areas.
    base_index = json.loads((safe(root, BASE) / "index.json").read_text(encoding="utf-8"))
    for entry in base_index["bundles"]:
        per_area.setdefault(
            entry["administrativeAreaStableId"],
            Counter(
                {
                    "buildingInteriorRingCandidateCount": 0,
                    "buildingInteriorRingCount": 0,
                    "buildingInteriorRingPathVertexCount": 0,
                    "buildingInteriorRingSourceSegmentCount": 0,
                    "buildingInteriorRingRenderableSegmentCount": 0,
                    "buildingInteriorRingSkippedAtOrBelowOneMillimeterSegmentCount": 0,
                    "buildingInteriorRingDuplicatePhysicalSegmentInstanceCount": 0,
                }
            ),
        )
    for candidate in candidates:
        value = per_area[candidate["administrativeAreaStableId"]]
        ring_count = len(candidate["rings"])
        vertices = sum(len(ring["commonEnuMillimeterPath"]) for ring in candidate["rings"])
        segments = vertices - ring_count
        value["buildingInteriorRingCandidateCount"] += 1
        value["buildingInteriorRingCount"] += ring_count
        value["buildingInteriorRingPathVertexCount"] += vertices
        value["buildingInteriorRingSourceSegmentCount"] += segments
        value["buildingInteriorRingRenderableSegmentCount"] += segments
    counts = {
        "sourceRecordCount": source_records,
        "sourceDeletedRecordCount": deleted_records,
        "targetSourceFeatureIdOccurrenceCount": sum(record_occurrences.values()),
        "targetBuildingBindingCount": len(bindings),
        "matchedBuildingCandidateCount": len(candidates),
        "sourcePolygonPartCount": source_polygon_parts,
        "sourceInteriorRingCount": source_rings,
        "sourceInteriorRingPathVertexCount": source_vertices,
        "sourceInteriorRingSegmentCount": source_segments,
        "renderableInteriorRingSegmentCount": source_segments,
        "skippedAtOrBelowOneMillimeterSegmentCount": short_segments,
        "duplicatePhysicalSegmentInstanceCount": duplicate_segments,
        "baseExteriorBindingMismatchRecordCount": exterior_mismatches,
        "quantizedRingOutsideBoundExteriorCount": containment_failures,
        "quantizedRingTouchesBoundExteriorCount": boundary_touches,
        "minimumRingToExteriorClearanceMillimeters": minimum_clearance_mm or 0,
        "administrativeAreaCount": len(per_area),
        "administrativeAreasWithCandidates": sum(
            value["buildingInteriorRingCandidateCount"] > 0 for value in per_area.values()
        ),
    }
    require(
        counts["administrativeAreaCount"] == AREA_COUNT
        and counts["administrativeAreasWithCandidates"] == EXPECTED_AREAS_WITH_CANDIDATES,
        "InteriorRingAreaCoverageChanged",
    )
    return candidates, {
        "counts": counts,
        "perAdministrativeArea": {
            key: dict(value) for key, value in sorted(per_area.items())
        },
        "sourceGeometryAudit": {
            "sourceDbfOpened": True,
            "sourceDbfFieldsRead": ["A1"],
            "sourceAddressFieldsRead": False,
            "sourceUseOrHeightFieldsRead": False,
            "largestExteriorMustExactlyMatchExistingV1BuildingFootprint": True,
            "sourceMultipartTargetAccepted": False,
            "interiorRingCoordinatesRoundedToExistingOneMillimeterPolicy": True,
            "interiorRingMeaningInferred": False,
            "sourcePathSimplificationApplied": False,
        },
        "deduplicationAudit": {
            "scope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "duplicatePhysicalSegmentInstanceCount": duplicate_segments,
            "topologyPreservingFailurePolicy": "RejectInsteadOfDroppingRingSegments",
        },
    }


def candidate_scan(payload: bytes) -> dict[str, int]:
    candidates = rings = vertices = segments = 0
    candidate_ids: set[str] = set()
    building_ids: set[str] = set()
    physical_by_area: dict[
        str, set[tuple[tuple[int, int], tuple[int, int]]]
    ] = defaultdict(set)
    for line in payload.splitlines():
        row = json.loads(line)
        require(
            set(row)
            == {
                "administrativeAreaStableId",
                "candidateStableId",
                "buildingStableId",
                "sourceRecordOrdinal",
                "sourceGeometrySha256",
                "rings",
            },
            "CandidateSchemaChanged",
        )
        require(
            re.fullmatch(r"building-interior-ring:sha256:[0-9a-f]{64}", row["candidateStableId"])
            is not None
            and row["candidateStableId"] not in candidate_ids
            and re.fullmatch(r"vworld:al-d010:[0-9]{28}", row["buildingStableId"])
            is not None
            and row["buildingStableId"] not in building_ids
            and isinstance(row["sourceRecordOrdinal"], int)
            and 0 <= row["sourceRecordOrdinal"] < SOURCE_RECORDS
            and re.fullmatch(r"[0-9A-F]{64}", row["sourceGeometrySha256"])
            is not None,
            "CandidateIdentityInvalidOrDuplicate",
        )
        candidate_ids.add(row["candidateStableId"])
        building_ids.add(row["buildingStableId"])
        require(bool(row["rings"]), "CandidateContainsNoRing")
        for ordinal, ring in enumerate(row["rings"]):
            require(
                set(ring) == {"ringOrdinal", "commonEnuMillimeterPath"}
                and ring["ringOrdinal"] == ordinal,
                "CandidateRingSchemaChanged",
            )
            path = ring["commonEnuMillimeterPath"]
            require(
                isinstance(path, list)
                and len(path) >= 4
                and path[0] == path[-1]
                and all(
                    set(point) == {"x", "z"}
                    and isinstance(point["x"], int)
                    and not isinstance(point["x"], bool)
                    and isinstance(point["z"], int)
                    and not isinstance(point["z"], bool)
                    for point in path
                )
                and signed_area(path) > 0,
                "CandidateRingPathChanged",
            )
            for first, second in zip(path, path[1:]):
                require(
                    math.hypot(second["x"] - first["x"], second["z"] - first["z"])
                    > MIN_RENDERABLE_SEGMENT_MM,
                    "CandidateRingSegmentTooShort",
                )
                key = segment_key(first, second)
                area = row["administrativeAreaStableId"]
                require(key not in physical_by_area[area], "CandidatePhysicalSegmentDuplicated")
                physical_by_area[area].add(key)
            rings += 1
            vertices += len(path)
            segments += len(path) - 1
        candidates += 1
    return {
        "candidateRowsScanned": candidates,
        "ringRowsScanned": rings,
        "ringPathVerticesScanned": vertices,
        "ringSegmentsScanned": segments,
    }


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    batch, dependencies, scope, frame = load_context(root)
    bindings, base_binding = load_building_bindings(root)
    candidates, source_audit = read_candidates(
        root, batch, dependencies, frame, bindings
    )
    candidate_payload = b"".join(canonical(value) for value in candidates)
    scan = candidate_scan(candidate_payload)
    counts = source_audit["counts"]
    require(
        scan["candidateRowsScanned"] == counts["matchedBuildingCandidateCount"]
        and scan["ringRowsScanned"] == counts["sourceInteriorRingCount"]
        and scan["ringPathVerticesScanned"] == counts["sourceInteriorRingPathVertexCount"]
        and scan["ringSegmentsScanned"] == counts["sourceInteriorRingSegmentCount"],
        "CandidateScanCountChanged",
    )
    generator_bytes = Path(__file__).resolve().read_bytes()
    source_binding = {
        "sourceId": "source:data-go-kr",
        "datasetId": "data-go-kr-15083092",
        "officialDatasetName": "국토교통부 GIS건물통합정보",
        "officialSourceUrl": "https://www.data.go.kr/data/15083092/fileData.do",
        "sourceRevision": "AL_D010:Seoul:20260809",
        "sourceVintage": SOURCE_VINTAGE,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceArchiveBytes": SOURCE_BYTES,
        "sourceReceiptSha256": SOURCE_RECEIPT_SHA,
        "sourceCoordinateReferenceSystem": "EPSG:5186",
        "sourceGeometryType": "Polygon",
        "sourceDbfFieldsRead": ["A1"],
        "sourceAddressFieldsRead": False,
        "licenseCode": "RightsConflictUnresolved",
        "limitationCode": (
            "OfficialGisBuildingGeometry;EPSG5186;InteriorRingReviewOnly;"
            "DistributionNotApproved"
        ),
        "scopeDefinitionSha256": SCOPE_SHA,
        "buildingLoaderSha256": BATCH_SHA,
        "generatorSha256": sha(generator_bytes),
    }
    generation_hash = sha(
        canonical(
            {
                "sourceBinding": source_binding,
                "baseBuildingBinding": base_binding,
                "candidatesSha256": sha(candidate_payload),
                "auditCounts": counts,
            }
        )
    )
    audit = {
        "schemaVersion": "administrative-dong-building-interior-ring-private-audit.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "baseBuildingBinding": base_binding,
        "coordinateFrame": scope["coordinateFrame"],
        **source_audit,
        "candidateScan": scan,
        "renderPolicy": {
            "flatInteriorRingTraceOnly": True,
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            "closedRingRequired": True,
            "counterClockwiseInteriorRingRequired": True,
            "physicalSegmentDeduplicationScope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "sourcePathSimplificationApplied": False,
            "buildingFillOrCutoutApplied": False,
        },
        "authority": authority(),
    }
    files: dict[str, bytes] = {
        "candidates.ndjson": candidate_payload,
        "audit.json": pretty(audit),
        "generator-source.py": generator_bytes,
    }
    manifest = {
        "schemaVersion": "administrative-dong-building-interior-ring-private-manifest.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "revision": "northeast-seoul-building-interior-ring-private-review.r1",
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "baseBuildingBinding": base_binding,
        "candidateCount": counts["matchedBuildingCandidateCount"],
        "interiorRingCount": counts["sourceInteriorRingCount"],
        "interiorRingPathVertexCount": counts["sourceInteriorRingPathVertexCount"],
        "interiorRingSegmentCount": counts["sourceInteriorRingSegmentCount"],
        "administrativeAreaCount": AREA_COUNT,
        "administrativeAreasWithCandidates": counts["administrativeAreasWithCandidates"],
        "fileDescriptors": [
            {"path": name, "bytes": len(payload), "sha256": sha(payload)}
            for name, payload in sorted(files.items())
        ],
        "authority": authority(),
    }
    files["manifest.json"] = pretty(manifest)
    completion = {
        "schemaVersion": "administrative-dong-building-interior-ring-private-completion.v1",
        "generationHashSha256": generation_hash,
        "status": "LocalPrivateBuildingInteriorRingReviewCandidateGenerated",
        "manifestSha256": sha(files["manifest.json"]),
        "candidateCount": counts["matchedBuildingCandidateCount"],
        "interiorRingCount": counts["sourceInteriorRingCount"],
        "interiorRingSegmentCount": counts["sourceInteriorRingSegmentCount"],
        "administrativeAreaCount": AREA_COUNT,
        "fileCount": len(files) + 1,
        "authority": authority(),
    }
    files["complete.json"] = pretty(completion)
    return generation_hash, files, counts


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
    audit = json.loads(files["audit.json"])
    manifest = json.loads(files["manifest.json"])
    completion = json.loads(files["complete.json"])
    require(
        audit["generationHashSha256"]
        == manifest["generationHashSha256"]
        == completion["generationHashSha256"]
        and completion["manifestSha256"] == sha(files["manifest.json"]),
        "GenerationBindingChanged",
    )


def self_test(root: Path) -> dict[str, Any]:
    require(
        segment_key({"x": 1, "z": 2}, {"x": 3, "z": 4})
        == segment_key({"x": 3, "z": 4}, {"x": 1, "z": 2}),
        "SelfTestUndirectedSegment",
    )
    flags = authority()
    factual_true = {
        "privateReviewOnly",
        "historicalBoundaryBootstrapOnly",
        "reviewVisualizationOnly",
        "privateReviewVisualizationAllowed",
        "sourceBuildingInteriorRingGeometryObserved",
        "sourceIdentityFieldRead",
    }
    require(
        all(value is (key in factual_true) for key, value in flags.items()),
        "SelfTestAuthority",
    )
    _, files, counts = prepare(root)
    require(
        counts["matchedBuildingCandidateCount"] == EXPECTED_CANDIDATES
        and counts["sourceInteriorRingCount"] == EXPECTED_RINGS
        and counts["sourceInteriorRingPathVertexCount"] == EXPECTED_RING_VERTICES
        and counts["sourceInteriorRingSegmentCount"] == EXPECTED_RING_SEGMENTS
        and counts["sourceInteriorRingSegmentCount"]
        == counts["renderableInteriorRingSegmentCount"]
        + counts["skippedAtOrBelowOneMillimeterSegmentCount"]
        + counts["duplicatePhysicalSegmentInstanceCount"],
        "SelfTestCounts",
    )
    geometry_audit = json.loads(files["audit.json"])["sourceGeometryAudit"]
    require(
        geometry_audit["sourceDbfFieldsRead"] == ["A1"]
        and geometry_audit["sourceAddressFieldsRead"] is False
        and geometry_audit["interiorRingMeaningInferred"] is False,
        "SelfTestAttributeBoundary",
    )
    return {
        "status": "PASS",
        "selfTestsPassed": 12,
        "generatorSourceHashSha256": sha_file(Path(__file__).resolve()),
        **counts,
    }


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        return self_test(root)
    generation_hash, files, counts = prepare(root)
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
        "status": "PASS",
        "mode": mode,
        "generationHashSha256": generation_hash,
        "generationRelativePath": target.relative_to(root).as_posix(),
        "changedFiles": changed,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptSha256": SOURCE_RECEIPT_SHA,
        "baseGenerationHashSha256": BASE_HASH,
        **counts,
        "authority": authority(),
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
        BuildingInteriorRingError,
        OSError,
        ValueError,
        KeyError,
        TypeError,
        zipfile.BadZipFile,
    ) as exc:
        print(
            json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

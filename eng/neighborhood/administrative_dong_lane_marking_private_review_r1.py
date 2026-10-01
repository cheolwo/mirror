#!/usr/bin/env python3
"""Freeze and clip OA-15537 lane-marking lines for private 30-dong review.

The source is an incomplete multi-source acquisition artifact whose bytes were
already hash-verified by tracked code.  This generator never edits that source.
It creates a lane-only immutable local receipt, then intersects the source with
the exact 2023 historical administrative-dong scope.  The result authorizes only
private display review of line work; it is not current lane, traffic, traversal,
runtime, or gameplay authority.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import io
import json
import math
import os
import re
import shutil
import sys
import uuid
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
ORIGIN = Path("artifacts/local/public-data/sagajeong-static-traffic-20260913-r1.acquiring-abc725645f844e10929b2b346ec3d73d/A058_L_차선.zip")
SOURCE_ROOT = Path("artifacts/local/public-data/admin-dong-lane-marking-20260926-r1")
SOURCE = SOURCE_ROOT / "raw/A058_L_차선.zip"
RECEIPT = SOURCE_ROOT / "receipt.json"
SOURCE_MANIFEST = SOURCE_ROOT / "source-manifest.json"
BOUNDARY = Path("artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip")
SCOPE = Path("eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json")
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
TRACKED_ACQUISITION_CONTRACT = Path("eng/Ssalddel.PublicDataPortalImport/사가정차선신호자료.cs")
OUTPUT = Path("artifacts/local/validation/admin-dong-lane-marking-private-review/r1/input")

SOURCE_SHA = "6A930A8B70B76F6FD0E2EE5576F2CA666620396AC3653E9A7CA28BDEC8A00120"
SOURCE_BYTES = 27_185_849
BOUNDARY_SHA = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
SCOPE_SHA = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"
SOURCE_ROWS = 341_957
AREA_COUNT = 30
FIXED_AT = "2026-09-26T00:00:00Z"
MIN_RENDERABLE_SEGMENT_MM = 1.0

EXPECTED = {
    "sourceBoundingBoxCandidateRows": 30_357,
    "sourceFeaturesIntersectingHistoricalScope": 22_447,
    "distinctSourceManagementNumbers": 22_423,
    "repeatedSourceManagementNumberValues": 22,
    "extraDuplicateSourceManagementNumberOccurrences": 24,
    "repeatedSourceManagementNumbersWithDifferentGeometry": 10,
    "maximumSourceManagementNumberOccurrences": 3,
    "sourcePartCount": 22_447,
    "sourcePointCount": 100_248,
    "sourceSegmentCount": 77_801,
    "sourceLengthMillimeters": 945_575_757,
    "sourceFeaturesAssignedToMultipleAdministrativeAreas": 890,
    "administrativeAreaFeatureAssignments": 23_379,
    "clipFragmentCount": 23_632,
    "clippedPathVertexCount": 84_278,
    "clippedSourceSegmentCount": 60_646,
    "clippedLengthMillimeters": 932_332_714,
    "nonLineIntersectionCount": 0,
    "quantizedAtOrBelowOneMillimeterSegmentCount": 2,
    "quantizedZeroLengthSegmentCount": 2,
    "quantizedAtOrBelowTenMillimeterSegmentCount": 16,
    "duplicatePhysicalSegmentInstanceCount": 58,
    "renderedUniquePhysicalSegmentCount": 60_586,
}

EXPECTED_KIND_COUNTS = {
    "001": 1751, "003": 130, "004": 2790, "005": 90, "006": 2715,
    "007": 508, "008": 1629, "009": 6911, "010": 2568, "011": 1803,
    "012": 756, "013": 322, "014": 175, "015": 166, "017": 120,
    "018": 13,
}
EXPECTED_FORM_COUNTS = {
    "001": 1344, "003": 10, "004": 11971, "005": 5269, "006": 2,
    "009": 3, "010": 201, "UNSPECIFIED": 3647,
}
EXPECTED_ROAD_DIVISION_COUNTS = {"001": 11561, "002": 10886}


class LaneReviewError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise LaneReviewError(code)


def safe(root: Path, relative: Path) -> Path:
    value = (root / relative).resolve()
    require(value == root or root in value.parents, "PathEscapesRepository")
    return value


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(file: Path) -> str:
    digest = hashlib.sha256()
    with file.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")


def pretty(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def checked(file: Path, expected_hash: str, expected_bytes: int | None = None) -> Path:
    require(file.is_file() and not file.is_symlink(), "InputMissingOrUnsafe:" + file.name)
    require(expected_bytes is None or file.stat().st_size == expected_bytes, "InputLengthChanged:" + file.name)
    require(sha_file(file) == expected_hash, "InputHashChanged:" + file.name)
    return file


def source_authority() -> dict[str, bool]:
    return {
        "privateLocalReviewOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "displayReviewPreparationOnly": True,
        "officialPageReverifiedForThisReceipt": False,
        "currentBoundaryEstablished": False,
        "currentLaneStateEstablished": False,
        "publicDisplayAllowed": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "unityApplyAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "laneMeaningAuthorized": False,
        "directionAuthorized": False,
        "trafficAuthorized": False,
        "stopLineAuthorized": False,
        "signalAuthorized": False,
    }


def review_authority() -> dict[str, bool]:
    return {
        "privateReviewOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "reviewVisualizationOnly": True,
        "laneOutlineAuthorized": True,
        "currentBoundaryEstablished": False,
        "currentLaneStateEstablished": False,
        "publicDisplayAllowed": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "sourceCandidateUnityApplyAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "laneMeaningAuthorized": False,
        "directionAuthorized": False,
        "trafficAuthorized": False,
        "stopLineAuthorized": False,
        "signalAuthorized": False,
        "colliderAuthorized": False,
    }


def source_documents() -> tuple[bytes, bytes]:
    members = [
        {"path": "A058_L.cpg", "bytes": 6, "sha256": "EC0FED838F6249DFC243F5AD3EACB38C28AED69F7C66BD9103CBB66DF9A9560B"},
        {"path": "A058_L.dbf", "bytes": 75_573_235, "sha256": "48B944CF44F82272E9C5422F14C9C3CE2AEDAC9891962606541CD62E12E73A16"},
        {"path": "A058_L.prj", "bytes": 422, "sha256": "DCFA42CFD392417D954AEB5038D12FDAA32A30879B3F5D611F207C7115DC9E7E"},
        {"path": "A058_L.shp", "bytes": 44_763_468, "sha256": "6C9F193A8034174FBFEE46BE155DA3EBF5EE36B485430038CCAD9B638567C63A"},
        {"path": "A058_L.shx", "bytes": 2_735_756, "sha256": "ECE2D8E762965F58BE188C2DD3E5E84FC51A2892BD4053B34B94DF9794441BA4"},
    ]
    receipt = {
        "schemaVersion": "administrative-dong-lane-marking-source-receipt.v1",
        "datasetId": "OA-15537",
        "title": "서울시 차선 관련 정보",
        "frozenAtUtc": FIXED_AT,
        "sourceArchive": {"filename": "A058_L_차선.zip", "bytes": SOURCE_BYTES, "sha256": SOURCE_SHA},
        "recovery": {
            "method": "ExactByteCopyFromIncompleteMultiSourceAcquisition",
            "originRelativePath": ORIGIN.as_posix(),
            "originBytesUnchanged": True,
            "originArchiveSha256": SOURCE_SHA,
            "recoveredLaneOnly": True,
        },
        "portalContractEvidence": {
            "officialDatasetUrl": "https://data.seoul.go.kr/dataList/OA-15537/S/1/datasetView.do",
            "trackedContractRelativePath": TRACKED_ACQUISITION_CONTRACT.as_posix(),
            "trackedExpectedFileModifiedDate": "2021-08-09",
            "trackedExpectedRecordCount": SOURCE_ROWS,
            "trackedExpectedCrs": "EPSG:5186",
            "trackedExpectedLicenseCode": "KOGL-Type1-Attribution",
            "officialPageReverifiedForThisReceipt": False,
            "freshDownloadPerformedForThisReceipt": False,
        },
        "internalDataset": {
            "geometryType": "ESRI Shapefile PolyLine",
            "recordCount": SOURCE_ROWS,
            "sourceDeclaredCrs": "EPSG:5186",
            "encoding": "EUC-KR",
            "members": members,
            "attributeFields": [
                "MGRNU", "A058_KND_C", "EVE_CDE", "OD_PE_CDE", "GU_CDE", "NW_PE_CDE",
                "WORK_CDE", "VIEW_CDE", "ROD_GBN_CD", "TFC_BSS_CD", "SIXID", "ESB_YMD",
                "CAE_YMD", "HISID", "CTK_MGRNU", "LINE_MGRNU", "A058_KND2_", "FRM_CDE",
                "LENX", "RN_CDE", "MNG_AGEN",
            ],
        },
        "interpretationBoundary": "Line geometry and source classification are historical private display candidates only; no current lane, lane meaning, driving direction, traffic control, traversal, or gameplay authority.",
        "authority": source_authority(),
    }
    receipt_bytes = pretty(receipt)
    manifest = {
        "schemaVersion": "administrative-dong-lane-marking-source-manifest.v1",
        "datasetId": "OA-15537",
        "sourceVintage": "file-2021-08-09",
        "frozenAtUtc": FIXED_AT,
        "sourceArchiveRelativePath": SOURCE.relative_to(SOURCE_ROOT).as_posix(),
        "sourceArchiveBytes": SOURCE_BYTES,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptRelativePath": RECEIPT.relative_to(SOURCE_ROOT).as_posix(),
        "sourceReceiptSha256": sha(receipt_bytes),
        "sourceRecordCount": SOURCE_ROWS,
        "sourceGeometryType": "PolyLine",
        "sourceCoordinateReferenceSystem": "EPSG:5186",
        "sourceEncoding": "EUC-KR",
        "officialPageReverified": False,
        "attributionClaimFreshlyReverified": False,
        "recoveredFromIncompleteMultiSourceAcquisition": True,
        "authority": source_authority(),
    }
    return receipt_bytes, pretty(manifest)


def validate_source_files(root: Path, receipt_bytes: bytes, manifest_bytes: bytes) -> None:
    checked(safe(root, SOURCE), SOURCE_SHA, SOURCE_BYTES)
    require(safe(root, RECEIPT).read_bytes() == receipt_bytes, "SourceReceiptChanged")
    require(safe(root, SOURCE_MANIFEST).read_bytes() == manifest_bytes, "SourceManifestChanged")
    source = safe(root, SOURCE)
    with zipfile.ZipFile(source) as archive:
        require(archive.testzip() is None, "SourceArchiveCrcFailure")
        require(archive.namelist() == ["A058_L.cpg", "A058_L.dbf", "A058_L.prj", "A058_L.shp", "A058_L.shx"], "SourceArchiveMemberSetChanged")


def ensure_source_files(root: Path, mode: str) -> int:
    receipt_bytes, manifest_bytes = source_documents()
    source_root = safe(root, SOURCE_ROOT)
    if source_root.exists():
        require(source_root.is_dir() and not source_root.is_symlink(), "SourceRootUnsafe")
        validate_source_files(root, receipt_bytes, manifest_bytes)
        return 0
    require(mode == "build", "SourceReceiptGenerationMissing")
    origin = checked(safe(root, ORIGIN), SOURCE_SHA, SOURCE_BYTES)
    source_root.parent.mkdir(parents=True, exist_ok=True)
    staging = source_root.parent / (".staging-lane-source-" + uuid.uuid4().hex)
    staging.mkdir()
    try:
        (staging / "raw").mkdir()
        shutil.copyfile(origin, staging / "raw" / SOURCE.name)
        (staging / "receipt.json").write_bytes(receipt_bytes)
        (staging / "source-manifest.json").write_bytes(manifest_bytes)
        require(sha_file(origin) == SOURCE_SHA, "OriginBytesChangedDuringCopy")
        require(sha_file(staging / "raw" / SOURCE.name) == SOURCE_SHA, "RecoveredSourceHashChanged")
        os.replace(staging, source_root)
    except Exception:
        if staging.exists():
            shutil.rmtree(staging)
        raise
    validate_source_files(root, receipt_bytes, manifest_bytes)
    return 3


def load_context(root: Path) -> tuple[Any, Any, dict[str, Any], list[Any]]:
    batch_file = safe(root, BATCH)
    require(batch_file.is_file() and not batch_file.is_symlink(), "BoundaryLoaderMissing")
    spec = importlib.util.spec_from_file_location("lane_marking_boundary_batch", batch_file)
    require(spec is not None and spec.loader is not None, "BoundaryLoaderUnavailable")
    batch = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = batch
    spec.loader.exec_module(batch)
    checked(safe(root, SCOPE), SCOPE_SHA)
    checked(safe(root, BOUNDARY), BOUNDARY_SHA)
    scope = batch.load_scope(root, safe(root, SCOPE))
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, boundary_audit = batch.read_boundaries(root, scope, frame, dependencies)
    require(len(boundaries) == AREA_COUNT and boundary_audit["historicalBootstrapOnly"] is True, "HistoricalScopeChanged")
    return dependencies, frame, scope, boundaries


def line_parts(dependencies: Any, shape: Any) -> list[Any]:
    starts = list(shape.parts) + [len(shape.points)]
    parts = []
    for start, end in zip(starts, starts[1:]):
        if end - start >= 2:
            parts.append(dependencies.LineString(shape.points[start:end]))
    require(bool(parts), "SourceLineHasNoRenderablePart")
    return parts


def local_line(dependencies: Any, frame: Any, to_wgs: Any, line: Any) -> Any:
    coordinates = list(line.coords)
    xs, ys = zip(*coordinates)
    longitudes, latitudes = to_wgs.transform(xs, ys)
    return dependencies.LineString([frame.wgs84_to_local(float(lon), float(lat)) for lon, lat in zip(longitudes, latitudes)])


def source_boundary_geometries(root: Path, dependencies: Any, boundaries: list[Any]) -> list[tuple[Any, Any]]:
    by_short_code = {boundary.stable_id.rsplit(":", 1)[-1][:-2]: boundary for boundary in boundaries}
    selected: dict[str, Any] = {}
    with zipfile.ZipFile(checked(safe(root, BOUNDARY), BOUNDARY_SHA)) as archive:
        entries = {}
        for extension in (".cpg", ".dbf", ".prj", ".shp", ".shx"):
            matches = [name for name in archive.namelist() if name.lower().endswith(extension)]
            require(len(matches) == 1, "HistoricalBoundaryArchiveMemberChanged:" + extension)
            entries[extension] = matches[0]
        source_crs = dependencies.CRS.from_wkt(archive.read(entries[".prj"]).decode("utf-8-sig"))
        require(source_crs.to_authority() == ("EPSG", "5181"), "HistoricalBoundaryProjectionChanged")
        to_lane_source = dependencies.Transformer.from_crs(source_crs, 5186, always_xy=True)
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])), shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])), encoding="utf-8", encodingErrors="strict")
        try:
            for shape_record in reader.iterShapeRecords():
                short_code = str(shape_record.record.as_dict().get("ADSTRD_CD", ""))
                if short_code not in by_short_code:
                    continue
                starts = list(shape_record.shape.parts) + [len(shape_record.shape.points)]
                rings = [shape_record.shape.points[start:end] for start, end in zip(starts, starts[1:])]
                geometry = dependencies.rings_to_geometry(rings)
                geometry = dependencies.transform(to_lane_source.transform, geometry)
                require(isinstance(geometry, dependencies.Polygon) and not geometry.is_empty,
                        "HistoricalBoundarySourceGeometryChanged:" + short_code)
                selected[short_code] = geometry
        finally:
            reader.close()
    require(set(selected) == set(by_short_code), "HistoricalBoundarySourceCoverageChanged")
    return [(by_short_code[code], selected[code]) for code in sorted(selected)]


def line_fragments(dependencies: Any, geometry: Any) -> tuple[list[Any], int]:
    if geometry.is_empty:
        return [], 0
    if geometry.geom_type == "LineString":
        return ([geometry] if len(geometry.coords) >= 2 and geometry.length > 0 else []), 0
    if geometry.geom_type == "MultiLineString":
        return [part for part in geometry.geoms if len(part.coords) >= 2 and part.length > 0], 0
    if geometry.geom_type == "GeometryCollection":
        lines: list[Any] = []
        non_line = 0
        for part in geometry.geoms:
            nested, count = line_fragments(dependencies, part)
            lines.extend(nested)
            non_line += count
            if part.geom_type not in {"LineString", "MultiLineString", "GeometryCollection", "Point", "MultiPoint"}:
                non_line += 1
        return lines, non_line
    if geometry.geom_type in {"Point", "MultiPoint"}:
        return [], 0
    return [], 1


def normalized_code(value: Any) -> str:
    text = "" if value is None else str(value).strip()
    return text if text else "UNSPECIFIED"


def quantized_path(line: Any) -> list[dict[str, int]]:
    return [{"x": round(float(x) * 1000), "z": round(float(z) * 1000)} for x, z in line.coords]


def segment_key(a: dict[str, int], b: dict[str, int]) -> tuple[tuple[int, int], tuple[int, int]]:
    first, second = (a["x"], a["z"]), (b["x"], b["z"])
    return (first, second) if first <= second else (second, first)


def boundary_ring_audit(boundaries: list[Any]) -> dict[str, int]:
    polygon_parts = exterior = interior = vertices = segments = 0
    for boundary in boundaries:
        polygons = [boundary.geometry] if boundary.geometry.geom_type == "Polygon" else list(boundary.geometry.geoms)
        for polygon in polygons:
            polygon_parts += 1
            exterior += 1
            vertices += len(polygon.exterior.coords)
            segments += len(polygon.exterior.coords) - 1
            for ring in polygon.interiors:
                interior += 1
                vertices += len(ring.coords)
                segments += len(ring.coords) - 1
    return {
        "historicalBoundaryPolygonPartCount": polygon_parts,
        "historicalBoundaryExteriorRingCount": exterior,
        "historicalBoundaryInteriorRingCount": interior,
        "historicalBoundaryRingCount": exterior + interior,
        "historicalBoundaryRingVertexCount": vertices,
        "historicalBoundaryRingSegmentCount": segments,
    }


def read_candidates(root: Path, dependencies: Any, frame: Any, boundaries: list[Any]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    to_wgs = dependencies.Transformer.from_crs(5186, 4326, always_xy=True)
    # The transformed common-frame union envelope is only an early rejection gate.
    # Exact inclusion and clipping always use the 30 historical polygons below.
    source_boundaries = source_boundary_geometries(root, dependencies, boundaries)
    source_envelope = dependencies.unary_union([geometry for _, geometry in source_boundaries]).bounds

    candidates: list[dict[str, Any]] = []
    area_counts: dict[str, Counter[str]] = defaultdict(Counter)
    kind_counts: Counter[str] = Counter()
    form_counts: Counter[str] = Counter()
    road_counts: Counter[str] = Counter()
    management_occurrences: Counter[str] = Counter()
    management_geometries: dict[str, set[str]] = defaultdict(set)
    geometry_occurrences: Counter[str] = Counter()
    selected_feature_rows = set()
    multi_area_feature_rows = 0
    source_bbox_rows = 0
    source_parts = source_points = source_segments = 0
    source_length = clipped_length = 0.0
    clipped_fragments = clipped_points = clipped_segments = 0
    non_line_intersections = 0

    source_path = checked(safe(root, SOURCE), SOURCE_SHA, SOURCE_BYTES)
    with zipfile.ZipFile(source_path) as archive:
        require(archive.testzip() is None, "SourceArchiveCrcFailure")
        require(archive.read("A058_L.cpg").decode("ascii").strip().upper() == "EUC-KR", "SourceEncodingChanged")
        require(dependencies.CRS.from_wkt(archive.read("A058_L.prj").decode("utf-8-sig")).to_epsg() == 5186, "SourceProjectionChanged")
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read("A058_L.shp")), shx=io.BytesIO(archive.read("A058_L.shx")),
            dbf=io.BytesIO(archive.read("A058_L.dbf")), encoding="euc-kr", encodingErrors="strict")
        try:
            require(len(reader) == SOURCE_ROWS, "SourceRecordCountChanged")
            fields = {field[0] for field in reader.fields[1:]}
            require({"MGRNU", "A058_KND_C", "A058_KND2_", "FRM_CDE", "ROD_GBN_CD"}.issubset(fields), "SourceFieldsMissing")
            for row_index, shape_record in enumerate(reader.iterShapeRecords()):
                shape = shape_record.shape
                require(shape.shapeType == 3, "SourceGeometryTypeChanged")
                bbox = shape.bbox
                if bbox[2] < source_envelope[0] or bbox[0] > source_envelope[2] or bbox[3] < source_envelope[1] or bbox[1] > source_envelope[3]:
                    continue
                source_bbox_rows += 1
                source_line_parts = line_parts(dependencies, shape)
                geometry_payload = [[[round(float(x) * 1000), round(float(y) * 1000)] for x, y in part.coords]
                                    for part in source_line_parts]
                geometry_hash = sha(canonical(geometry_payload))
                values = shape_record.record.as_dict()
                management = normalized_code(values.get("MGRNU"))
                require(management != "UNSPECIFIED", "SourceManagementNumberMissing")
                matches: list[tuple[Any, list[Any]]] = []
                for boundary, source_boundary in source_boundaries:
                    boundary_fragments = []
                    for part in source_line_parts:
                        if not (part.bounds[2] < source_boundary.bounds[0] or part.bounds[0] > source_boundary.bounds[2]
                                or part.bounds[3] < source_boundary.bounds[1] or part.bounds[1] > source_boundary.bounds[3]):
                            fragments, non_line = line_fragments(dependencies, part.intersection(source_boundary))
                            boundary_fragments.extend(fragments)
                            non_line_intersections += non_line
                    if boundary_fragments:
                        matches.append((boundary, boundary_fragments))
                if not matches:
                    continue
                selected_feature_rows.add(row_index)
                management_occurrences[management] += 1
                management_geometries[management].add(geometry_hash)
                geometry_occurrences[geometry_hash] += 1
                source_parts += len(source_line_parts)
                source_points += sum(len(part.coords) for part in source_line_parts)
                source_segments += sum(len(part.coords) - 1 for part in source_line_parts)
                # Preserve the archive's declared projected-meter length independently
                # from the common-ENU clipping measurement below.
                source_length += sum(part.length for part in source_line_parts)
                if len(matches) > 1:
                    multi_area_feature_rows += 1
                shared_areas = sorted(boundary.stable_id for boundary, _ in matches)
                kind = normalized_code(values.get("A058_KND_C"))
                secondary = normalized_code(values.get("A058_KND2_"))
                form = normalized_code(values.get("FRM_CDE"))
                road_division = normalized_code(values.get("ROD_GBN_CD"))
                kind_counts[kind] += 1
                form_counts[form] += 1
                road_counts[road_division] += 1
                for boundary, fragments in matches:
                    clip_rows = []
                    for fragment_ordinal, fragment in enumerate(fragments):
                        points = quantized_path(local_line(dependencies, frame, to_wgs, fragment))
                        clip_rows.append({"fragmentOrdinal": fragment_ordinal, "commonEnuMillimeterPath": points})
                        clipped_fragments += 1
                        clipped_points += len(points)
                        clipped_segments += len(points) - 1
                        clipped_length += fragment.length
                    area = boundary.stable_id
                    candidate_hash = sha((SOURCE_SHA + "|" + str(row_index) + "|" + area).encode("utf-8"))
                    candidates.append({
                        "administrativeAreaStableId": area,
                        "candidateStableId": "lane-marking:sha256:" + candidate_hash.lower(),
                        "sourceRecordOrdinal": row_index,
                        "sourceManagementNumberSha256": sha(management.encode("utf-8")),
                        "sourceGeometrySha256": geometry_hash,
                        "sourceKindCode": kind,
                        "sourceSecondaryKindCode": secondary,
                        "sourceFormCode": form,
                        "sourceRoadDivisionCode": road_division,
                        "sharedAdministrativeAreaStableIds": shared_areas,
                        "sourcePartCount": len(source_line_parts),
                        "sourcePointCount": sum(len(part.coords) for part in source_line_parts),
                        "sourceSegmentCount": sum(len(part.coords) - 1 for part in source_line_parts),
                        "clipFragments": clip_rows,
                    })
                    area_counts[area]["candidateCount"] += 1
                    area_counts[area]["clipFragmentCount"] += len(clip_rows)
                    area_counts[area]["clippedPathVertexCount"] += sum(len(item["commonEnuMillimeterPath"]) for item in clip_rows)
                    area_counts[area]["clippedSourceSegmentCount"] += sum(len(item["commonEnuMillimeterPath"]) - 1 for item in clip_rows)
        finally:
            reader.close()

    candidates.sort(key=lambda item: (item["administrativeAreaStableId"], item["candidateStableId"]))
    seen_segments: dict[str, set[tuple[tuple[int, int], tuple[int, int]]]] = defaultdict(set)
    duplicate_segments = short_segments = zero_segments = at_most_ten = rendered_segments = 0
    rendered_candidates = 0
    for candidate in candidates:
        area = candidate["administrativeAreaStableId"]
        rendered = []
        for fragment in candidate["clipFragments"]:
            points = fragment["commonEnuMillimeterPath"]
            for first, second in zip(points, points[1:]):
                length = math.hypot(first["x"] - second["x"], first["z"] - second["z"])
                if length <= 10.0:
                    at_most_ten += 1
                if length <= MIN_RENDERABLE_SEGMENT_MM:
                    short_segments += 1
                    area_counts[area]["skippedAtOrBelowOneMillimeterSegmentCount"] += 1
                    if length == 0:
                        zero_segments += 1
                    continue
                key = segment_key(first, second)
                if key in seen_segments[area]:
                    duplicate_segments += 1
                    area_counts[area]["duplicatePhysicalSegmentInstanceCount"] += 1
                    continue
                seen_segments[area].add(key)
                rendered.append({"fragmentOrdinal": len(rendered), "commonEnuMillimeterPath": [first, second]})
                rendered_segments += 1
        candidate["renderFragments"] = rendered
        if rendered:
            rendered_candidates += 1
        area_counts[area]["renderedCandidateCount"] += bool(rendered)
        area_counts[area]["renderFragmentCount"] += len(rendered)
        area_counts[area]["renderedPathVertexCount"] += 2 * len(rendered)
        area_counts[area]["renderedUniquePhysicalSegmentCount"] += len(rendered)

    repeated = {key: count for key, count in management_occurrences.items() if count > 1}
    repeated_with_different_geometry = sum(len(management_geometries[key]) > 1 for key in repeated)
    duplicated_geometry_values = sum(count > 1 for count in geometry_occurrences.values())
    duplicate_geometry_extra_occurrences = sum(count - 1 for count in geometry_occurrences.values() if count > 1)
    counts = {
        "sourceRecordCount": SOURCE_ROWS,
        "sourceBoundingBoxCandidateRows": source_bbox_rows,
        "sourceFeaturesIntersectingHistoricalScope": len(selected_feature_rows),
        "distinctSourceManagementNumbers": len(management_occurrences),
        "repeatedSourceManagementNumberValues": len(repeated),
        "extraDuplicateSourceManagementNumberOccurrences": sum(count - 1 for count in repeated.values()),
        "repeatedSourceManagementNumbersWithDifferentGeometry": repeated_with_different_geometry,
        "maximumSourceManagementNumberOccurrences": max(management_occurrences.values()),
        "duplicateSourceGeometryValues": duplicated_geometry_values,
        "extraDuplicateSourceGeometryOccurrences": duplicate_geometry_extra_occurrences,
        "sourcePartCount": source_parts,
        "sourcePointCount": source_points,
        "sourceSegmentCount": source_segments,
        "sourceLengthMillimeters": round(source_length * 1000),
        "sourceFeaturesAssignedToMultipleAdministrativeAreas": multi_area_feature_rows,
        "administrativeAreaFeatureAssignments": len(candidates),
        "clipFragmentCount": clipped_fragments,
        "clippedPathVertexCount": clipped_points,
        "clippedSourceSegmentCount": clipped_segments,
        "clippedLengthMillimeters": round(clipped_length * 1000),
        "nonLineIntersectionCount": non_line_intersections,
        "quantizedAtOrBelowOneMillimeterSegmentCount": short_segments,
        "quantizedZeroLengthSegmentCount": zero_segments,
        "quantizedAtOrBelowTenMillimeterSegmentCount": at_most_ten,
        "duplicatePhysicalSegmentInstanceCount": duplicate_segments,
        "renderedCandidateCount": rendered_candidates,
        "renderedUniquePhysicalSegmentCount": rendered_segments,
        "renderedPathVertexCount": rendered_segments * 2,
        "administrativeAreasCovered": len(area_counts),
    }
    require(len({item["candidateStableId"] for item in candidates}) == len(candidates), "CandidateStableIdCollision")
    require(len(area_counts) == AREA_COUNT, "HistoricalAreaCoverageChanged")
    for key, expected in EXPECTED.items():
        require(counts[key] == expected, "LaneAuditCountChanged:" + key + ":" + str(counts[key]))
    require(dict(sorted(kind_counts.items())) == EXPECTED_KIND_COUNTS, "LaneKindCountsChanged")
    require(dict(sorted(form_counts.items())) == EXPECTED_FORM_COUNTS, "LaneFormCountsChanged")
    require(dict(sorted(road_counts.items())) == EXPECTED_ROAD_DIVISION_COUNTS, "LaneRoadDivisionCountsChanged")
    area_metric_keys = (
        "candidateCount", "clipFragmentCount", "clippedPathVertexCount",
        "clippedSourceSegmentCount", "skippedAtOrBelowOneMillimeterSegmentCount",
        "duplicatePhysicalSegmentInstanceCount", "renderedCandidateCount",
        "renderFragmentCount", "renderedPathVertexCount",
        "renderedUniquePhysicalSegmentCount",
    )
    for value in area_counts.values():
        for key in area_metric_keys:
            value.setdefault(key, 0)
    audit = {
        "counts": counts,
        "perAdministrativeArea": {key: dict(sorted(value.items())) for key, value in sorted(area_counts.items())},
        "officialKindCounts": dict(sorted(kind_counts.items())),
        "officialFormCounts": dict(sorted(form_counts.items())),
        "officialRoadDivisionCounts": dict(sorted(road_counts.items())),
        "duplicateManagementNumberAudit": {
            "rawManagementNumbersExcludedFromOutput": True,
            "repeatedValueCount": len(repeated),
            "extraOccurrenceCount": sum(count - 1 for count in repeated.values()),
            "differentGeometryValueCount": repeated_with_different_geometry,
            "maximumOccurrences": max(management_occurrences.values()),
            "hashedRepeatedValues": sorted(sha(value.encode("utf-8")) for value in repeated),
        },
        "duplicateGeometryAudit": {
            "duplicateSourceGeometryValueCount": duplicated_geometry_values,
            "extraSourceGeometryOccurrences": duplicate_geometry_extra_occurrences,
            "duplicatePhysicalSegmentInstanceCountWithinAdministrativeArea": duplicate_segments,
            "physicalSegmentDirectionIgnoredForDeduplication": True,
        },
        "historicalBoundaryRings": boundary_ring_audit(boundaries),
    }
    return candidates, audit


def candidate_scan(payload: bytes) -> dict[str, int]:
    candidate_count = clip_fragments = render_fragments = render_segments = 0
    ids = set()
    physical_segments: dict[str, set[tuple[tuple[int, int], tuple[int, int]]]] = defaultdict(set)
    for line in payload.splitlines():
        row = json.loads(line)
        require(set(row) == {
            "administrativeAreaStableId", "candidateStableId", "sourceRecordOrdinal",
            "sourceManagementNumberSha256", "sourceGeometrySha256", "sourceKindCode",
            "sourceSecondaryKindCode", "sourceFormCode", "sourceRoadDivisionCode",
            "sharedAdministrativeAreaStableIds", "sourcePartCount", "sourcePointCount",
            "sourceSegmentCount", "clipFragments", "renderFragments"}, "CandidateSchemaChanged")
        require(re.fullmatch(r"lane-marking:sha256:[0-9a-f]{64}", row["candidateStableId"]) is not None
                and row["candidateStableId"] not in ids, "CandidateStableIdInvalidOrDuplicate")
        ids.add(row["candidateStableId"])
        require(all(row[key] != "" for key in ("sourceKindCode", "sourceSecondaryKindCode", "sourceFormCode")), "CandidateBlankCode")
        for key in ("clipFragments", "renderFragments"):
            fragments = row[key]
            require([item["fragmentOrdinal"] for item in fragments] == list(range(len(fragments))), "FragmentOrdinalChanged")
            for fragment in fragments:
                require(set(fragment) == {"fragmentOrdinal", "commonEnuMillimeterPath"}, "FragmentSchemaChanged")
                path = fragment["commonEnuMillimeterPath"]
                require(len(path) >= 2 and all(set(point) == {"x", "z"} and isinstance(point["x"], int)
                        and isinstance(point["z"], int) for point in path), "FragmentPathChanged")
                if key == "renderFragments":
                    require(len(path) == 2, "RenderFragmentMustBeSingleSegment")
                    require(math.hypot(path[0]["x"] - path[1]["x"], path[0]["z"] - path[1]["z"])
                            > MIN_RENDERABLE_SEGMENT_MM, "RenderFragmentTooShort")
                    physical = segment_key(path[0], path[1])
                    require(physical not in physical_segments[row["administrativeAreaStableId"]],
                            "RenderPhysicalSegmentDuplicated")
                    physical_segments[row["administrativeAreaStableId"]].add(physical)
        candidate_count += 1
        clip_fragments += len(row["clipFragments"])
        render_fragments += len(row["renderFragments"])
        render_segments += sum(len(item["commonEnuMillimeterPath"]) - 1 for item in row["renderFragments"])
    return {"candidateRowsScanned": candidate_count, "clipFragmentsScanned": clip_fragments,
            "renderFragmentsScanned": render_fragments, "renderSegmentsScanned": render_segments}


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    dependencies, frame, scope, boundaries = load_context(root)
    candidates, candidate_audit = read_candidates(root, dependencies, frame, boundaries)
    candidate_payload = b"".join(canonical(item) for item in candidates)
    scan = candidate_scan(candidate_payload)
    source_manifest_bytes = safe(root, SOURCE_MANIFEST).read_bytes()
    source_receipt_bytes = safe(root, RECEIPT).read_bytes()
    generator_bytes = Path(__file__).resolve().read_bytes()
    source_binding = {
        "datasetId": "OA-15537",
        "sourceVintage": "file-2021-08-09",
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptSha256": sha(source_receipt_bytes),
        "sourceManifestSha256": sha(source_manifest_bytes),
        "historicalBoundaryDatasetId": "OA-22160",
        "historicalBoundaryVintage": "2023-10-31",
        "historicalBoundarySha256": BOUNDARY_SHA,
        "scopeDefinitionSha256": SCOPE_SHA,
        "boundaryLoaderSha256": sha_file(safe(root, BATCH)),
        "generatorSha256": sha(generator_bytes),
    }
    generation_hash = sha(canonical({"sourceBinding": source_binding,
                                     "candidatesSha256": sha(candidate_payload),
                                     "auditCounts": candidate_audit["counts"]}))
    audit = {
        "schemaVersion": "administrative-dong-lane-marking-private-audit.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "sourceCrs": "EPSG:5186",
        "historicalBoundaryCrs": "EPSG:5181",
        "coordinateFrame": scope["coordinateFrame"],
        **candidate_audit,
        "candidateScan": scan,
        "renderPolicy": {
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            "skipAtOrBelowThreshold": True,
            "physicalSegmentDeduplicationScope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "renderFragmentsAreTwoPointSegments": True,
        },
        "authority": review_authority(),
    }
    files = {
        "candidates.ndjson": candidate_payload,
        "audit.json": pretty(audit),
        "generator-source.py": generator_bytes,
    }
    manifest = {
        "schemaVersion": "administrative-dong-lane-marking-private-manifest.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "revision": "northeast-seoul-lane-marking-private-review.r1",
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "candidateCount": len(candidates),
        "renderedCandidateCount": candidate_audit["counts"]["renderedCandidateCount"],
        "areaCount": AREA_COUNT,
        "clipFragmentCount": candidate_audit["counts"]["clipFragmentCount"],
        "clippedSourceSegmentCount": candidate_audit["counts"]["clippedSourceSegmentCount"],
        "renderedUniquePhysicalSegmentCount": candidate_audit["counts"]["renderedUniquePhysicalSegmentCount"],
        "fileDescriptors": [{"path": name, "bytes": len(payload), "sha256": sha(payload)}
                            for name, payload in sorted(files.items())],
        "authority": review_authority(),
    }
    files["manifest.json"] = pretty(manifest)
    completion = {
        "schemaVersion": "administrative-dong-lane-marking-private-completion.v1",
        "generationHashSha256": generation_hash,
        "status": "LocalPrivateHistoricalDisplayCandidateGenerated",
        "manifestSha256": sha(files["manifest.json"]),
        "candidateCount": len(candidates),
        "areaCount": AREA_COUNT,
        "renderedUniquePhysicalSegmentCount": candidate_audit["counts"]["renderedUniquePhysicalSegmentCount"],
        "fileCount": len(files) + 1,
        "authority": review_authority(),
    }
    files["complete.json"] = pretty(completion)
    return generation_hash, files, candidate_audit["counts"]


def validate_generation(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not target.is_symlink(), "GenerationMissingOrUnsafe")
    actual = {item.relative_to(target).as_posix() for item in target.rglob("*") if item.is_file()}
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        file = target / name
        require(file.is_file() and not file.is_symlink() and file.read_bytes() == payload, "GenerationFileChanged:" + name)
    scan = candidate_scan(files["candidates.ndjson"])
    require(scan["candidateRowsScanned"] == EXPECTED["administrativeAreaFeatureAssignments"], "GenerationCandidateScanChanged")
    require(scan["renderSegmentsScanned"] == EXPECTED["renderedUniquePhysicalSegmentCount"], "GenerationRenderScanChanged")


def self_test() -> dict[str, Any]:
    receipt_bytes, manifest_bytes = source_documents()
    require(json.loads(manifest_bytes)["sourceReceiptSha256"] == sha(receipt_bytes), "SelfTestSourceBinding")
    require(segment_key({"x": 1, "z": 2}, {"x": 3, "z": 4}) == segment_key({"x": 3, "z": 4}, {"x": 1, "z": 2}), "SelfTestUndirectedSegment")
    sample = {
        "administrativeAreaStableId": "region:kr:hjd:0000000000",
        "candidateStableId": "lane-marking:sha256:" + "0" * 64,
        "sourceRecordOrdinal": 0, "sourceManagementNumberSha256": "0" * 64,
        "sourceGeometrySha256": "1" * 64, "sourceKindCode": "001",
        "sourceSecondaryKindCode": "UNSPECIFIED", "sourceFormCode": "UNSPECIFIED",
        "sourceRoadDivisionCode": "001", "sharedAdministrativeAreaStableIds": ["region:kr:hjd:0000000000"],
        "sourcePartCount": 1, "sourcePointCount": 2, "sourceSegmentCount": 1,
        "clipFragments": [{"fragmentOrdinal": 0, "commonEnuMillimeterPath": [{"x": 0, "z": 0}, {"x": 2, "z": 0}]}],
        "renderFragments": [{"fragmentOrdinal": 0, "commonEnuMillimeterPath": [{"x": 0, "z": 0}, {"x": 2, "z": 0}]}],
    }
    require(candidate_scan(canonical(sample))["renderSegmentsScanned"] == 1, "SelfTestCandidateScan")
    require(all(value is False for key, value in review_authority().items()
                if key not in {"privateReviewOnly", "historicalBoundaryBootstrapOnly", "reviewVisualizationOnly", "laneOutlineAuthorized"}), "SelfTestAuthority")
    require(sha(b"a") != sha(b"b"), "SelfTestDigest")
    return {"status": "PASS", "selfTestsPassed": 5,
            "generatorSourceHashSha256": sha_file(Path(__file__).resolve())}


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        return self_test()
    source_changed = ensure_source_files(root, mode)
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
                (staging / name).write_bytes(payload)
            validate_generation(staging, files)
            os.replace(staging, target)
        except Exception:
            if staging.exists():
                shutil.rmtree(staging)
            raise
        changed = len(files)
    return {
        "status": "PASS", "mode": mode, "generationHashSha256": generation_hash,
        "generationRelativePath": target.relative_to(root).as_posix(),
        "sourceChangedFiles": source_changed, "changedFiles": changed,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptSha256": sha(safe(root, RECEIPT).read_bytes()),
        "sourceManifestSha256": sha(safe(root, SOURCE_MANIFEST).read_bytes()),
        **counts, "authority": review_authority(),
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
    except (LaneReviewError, OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

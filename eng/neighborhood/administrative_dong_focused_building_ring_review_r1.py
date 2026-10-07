#!/usr/bin/env python3
"""동결된 단일 건물의 내부 ring을 보존하는 비공개 roof 검토 사본.

원본/기존 r7 세대를 읽기만 한다. 내부 ring은 source polygon 구조이며
안뜰, 출입구, 통행 또는 현재 외관의 확정 사실이 아니다. 높이는 변경하지 않는다.
"""
from __future__ import annotations

import argparse
from decimal import Decimal
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import stat
import struct
import sys
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path("artifacts/local/validation/admin-dong-focused-building-ring-review/r1/input")
AREA = "region:kr:hjd:1126057500"
BUILDING = "vworld:al-d010:0000208470354536004400000000"
SOURCE = Path("artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip")
SOURCE_SHA = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"
RECEIPT = Path("artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.recovery-receipt.json")
RECEIPT_SHA = "43FC3ABF60DE57ABDE33D8A6657A4FC221844F8DF40793C11E843BDF3E4A5576"
SOURCE_BYTES = 135675376
SOURCE_RECORDS = 695761
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
BATCH_SHA = "24BD8D246D2FA714A9C83065D5A8594179DED8E466F8B816FCD3BA02DB79351C"
SCOPE = Path("eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json")
SCOPE_SHA = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"
GENERATION = "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753"
BASE = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r7/input/generations") / GENERATION.lower()
INDEX_SHA = "C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9"
COMPLETE_SHA = "26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D"
BUNDLE_SHA = "E1DBB3B99347B4139DE157274C84C881E9821A49BF064BB579813F347925E28A"
AREA_TOLERANCE_MM2 = 0.01
FALSE_AUTHORITIES = (
    "publicDisplayAllowed", "distributionApproved", "currentBoundaryEstablished",
    "currentBuildingEstablished", "databaseWriteAllowed", "mongoWriteAllowed",
    "runtimeAuthorized", "traversalReady", "gameplayReady", "colliderAuthorized",
    "entranceMeaningAuthorized", "courtyardMeaningAuthorized", "passageAuthorized",
    "buildingHeightPrecisionImproved", "sourceCandidateUnityApplyAllowed",
)


class FocusedBuildingRingError(ValueError):
    pass


def require(value, code):
    if not value:
        raise FocusedBuildingRingError(code)


def sha(payload):
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1048576), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical(value):
    if isinstance(value, dict):
        return "{" + ",".join(canonical(k) + ":" + canonical(value[k]) for k in sorted(value)) + "}"
    if isinstance(value, list):
        return "[" + ",".join(canonical(v) for v in value) + "]"
    if isinstance(value, Decimal):
        require(value.is_finite(), "NonFiniteOutputNumber")
        return "0" if value == 0 else format(value, "f")
    if isinstance(value, float):
        require(math.isfinite(value), "NonFiniteOutputNumber")
        return canonical(Decimal(str(value)))
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def encoded(value):
    return (canonical(value) + "\n").encode("utf-8")


def content_hash(value):
    return sha(canonical({k: v for k, v in value.items() if k != "contentHashSha256"}).encode("utf-8"))


def reject_constant(value):
    raise FocusedBuildingRingError("NonFiniteJsonNumber:" + value)


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        require(key not in value, "DuplicateJsonProperty:" + key)
        value[key] = item
    return value


def parse(payload):
    return json.loads(payload, parse_float=Decimal, parse_constant=reject_constant, object_pairs_hook=unique_object)


def safe(root, relative):
    root, relative = Path(root).resolve(), Path(relative)
    require(not relative.is_absolute() and ".." not in relative.parts, "PathEscapesRoot")
    current = root
    for part in relative.parts:
        current /= part
        if current.exists() or current.is_symlink():
            info = current.lstat()
            require(not current.is_symlink() and not (getattr(info, "st_file_attributes", 0)
                    & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)), "UnsafeReparsePath")
    require(current.resolve().is_relative_to(root), "PathEscapesRoot")
    return current


def checked(root, path, digest, length=None):
    path = safe(root, path)
    require(path.is_file(), "FrozenSourceMissing:" + path.name)
    require(length is None or path.stat().st_size == length, "FrozenSourceLengthMismatch:" + path.name)
    require(sha_file(path) == digest, "FrozenSourceHashMismatch:" + path.name)
    return path


def dependencies(root):
    for relative in ("artifacts/local/public-data/gis-runtime-r1", "artifacts/local/python-packages/spatial", "eng/neighborhood"):
        path = safe(root, Path(relative))
        if path.is_dir() and str(path) not in sys.path:
            sys.path.insert(0, str(path))
    spec = importlib.util.spec_from_file_location("focused_building_ring_batch", checked(root, BATCH, BATCH_SHA))
    batch = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = batch
    spec.loader.exec_module(batch)
    scope = batch.load_scope(root, checked(root, SCOPE, SCOPE_SHA))
    return batch, batch.load_dependencies(root), batch.frame_from_scope(scope)


def load_base(root):
    checked(root, BASE / "index.json", INDEX_SHA)
    checked(root, BASE / "complete.json", COMPLETE_SHA)
    bundle_path = checked(root, BASE / "bundles/1126057500.json", BUNDLE_SHA)
    bundle = parse(bundle_path.read_bytes())
    require(bundle["administrativeAreaStableId"] == AREA and bundle["publishBlocked"]
            and not bundle["currentPointerUsed"] and not bundle["currentPointerUpdated"]
            and not bundle["distributionApproved"] and not bundle["traversalReady"]
            and not bundle["gameplayReady"], "BaseAuthorityMismatch")
    targets = [(tile, building) for tile in bundle["tiles"] for building in tile["buildings"]
               if building["buildingStableId"] == BUILDING]
    require(len(targets) == 1, "BaseBuildingBindingAmbiguous")
    return bundle, targets[0][0], targets[0][1]


def ring_mm(ring, deps, clockwise):
    points = deps.canonical_ring(ring.coords, clockwise=clockwise)
    return [[round(float(point["x"]) * 1000), round(float(point["z"]) * 1000)] for point in points]


def read_target(root, batch, deps, frame):
    """ZIP 스트림 전부를 대조하여 원본 A1 식별자의 유일성까지 확인한다."""
    checked(root, RECEIPT, RECEIPT_SHA)
    source = checked(root, SOURCE, SOURCE_SHA, SOURCE_BYTES)
    matches = []
    with zipfile.ZipFile(source) as archive:
        shp = deps.zip_entry(archive, suffix=".shp")
        dbf = deps.zip_entry(archive, suffix=".dbf")
        prj = deps.zip_entry(archive, suffix=".prj")
        projection_text = archive.read(prj).decode("utf-8-sig")
        crs = deps.CRS.from_wkt(projection_text)
        require('AUTHORITY["EPSG","5186"]' in projection_text.replace(" ", "")
                and "Korea_Central_Belt_2010" in crs.name and crs.is_projected, "BuildingSourceCrsMismatch")
        transformer = deps.Transformer.from_crs(deps.CRS.from_epsg(5186), deps.CRS.from_epsg(4326), always_xy=True)
        with archive.open(shp) as shp_stream, archive.open(dbf) as dbf_stream:
            deps.read_shp_header(shp_stream)
            count, _, record_length, fields = deps.read_dbf_header(dbf_stream)
            require(count == SOURCE_RECORDS and "A1" in fields, "BuildingSourceRecordContractChanged")
            for index in range(count):
                record = deps.read_exact(dbf_stream, record_length, "BuildingDbfTruncated")
                ordinal, words = struct.unpack(">II", deps.read_exact(shp_stream, 8, "BuildingShpHeaderTruncated"))
                require(ordinal == index + 1 and 2 <= words <= 1048576, "BuildingShpRecordInvalid")
                body = deps.read_exact(shp_stream, words * 2, "BuildingShpRecordTruncated")
                if record[0] == 0x2A:
                    continue
                require(record[0] == 0x20, "BuildingDbfDeletionMarkerInvalid")
                if deps.dbf_text(record, fields["A1"], "ascii") != BUILDING.split(":")[-1]:
                    continue
                local = deps.rings_to_geometry(deps.transform_source_rings(deps.parse_shp_polygon(body), transformer, frame))
                polygons = batch._polygon_members(local, deps)
                require(len(polygons) == 1 and polygons[0].is_valid, "TargetPolygonInvalidOrMultipart")
                polygon = deps.orient(polygons[0], sign=-1.0)
                require(len(polygon.interiors) == 1, "TargetInteriorRingCountChanged")
                outer = ring_mm(polygon.exterior, deps, True)
                holes = sorted([ring_mm(ring, deps, False) for ring in polygon.interiors])
                require(len(outer) == 33 and len(holes[0]) == 13, "TargetRingVertexContractChanged")
                matches.append((ordinal, outer, holes))
            require(not shp_stream.read(1) and not dbf_stream.read(2).rstrip(b"\x1a\x00"), "UnexpectedSourceTrailingBytes")
    require(len(matches) == 1, "TargetSourceFeatureIdNotUnique")
    return matches[0]


def validate_rings(outer, holes):
    from shapely.geometry import Polygon
    for ring in [outer, *holes]:
        require(len(ring) >= 4 and ring[0] == ring[-1], "RingNotClosed")
        require(all(len(p) == 2 and all(isinstance(v, int) and not isinstance(v, bool) for v in p) for p in ring), "RingNotIntegerMillimeters")
        require(len(set(map(tuple, ring[:-1]))) == len(ring) - 1, "RingDuplicateVertex")
        require(all(math.dist(a, b) > 1 for a, b in zip(ring, ring[1:])), "RingSegmentAtOrBelowOneMillimeter")
    exterior = Polygon(outer)
    require(exterior.is_valid and exterior.area > 0, "ExteriorPolygonInvalid")
    for hole in holes:
        polygon = Polygon(hole)
        require(polygon.is_valid and polygon.area > 0 and exterior.contains(polygon)
                and not exterior.boundary.intersects(polygon.boundary), "InteriorRingNotStrictlyContained")
    polygon = Polygon(outer, holes)
    require(polygon.is_valid and polygon.area > 0, "QuantizedPolygonInvalid")
    return polygon


def audit_roof(outer, holes, vertices, indices):
    from shapely.geometry import Polygon
    from shapely.ops import unary_union
    polygon = validate_rings(outer, holes)
    expected_vertices = [p for ring in [outer, *holes] for p in ring[:-1]]
    require(vertices == expected_vertices, "RoofVertexBindingMismatch")
    require(bool(indices) and len(indices) % 3 == 0, "RoofIndexCountInvalid")
    triangles = []
    for offset in range(0, len(indices), 3):
        row = indices[offset:offset + 3]
        require(all(isinstance(i, int) and not isinstance(i, bool) and 0 <= i < len(vertices) for i in row), "RoofIndexOutOfRange")
        a, b, c = [vertices[i] for i in row]
        signed = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        require(signed < 0, "RoofTriangleWindingInvalid")
        triangle = Polygon([a, b, c])
        require(triangle.area > AREA_TOLERANCE_MM2 and polygon.covers(triangle), "RoofTriangleOutsideSourcePolygon")
        triangles.append(triangle)
    union = unary_union(triangles)
    difference = polygon.symmetric_difference(union).area
    overlap = sum(t.area for t in triangles) - union.area
    hole_overlap = sum(union.intersection(Polygon(hole)).area for hole in holes)
    require(difference <= AREA_TOLERANCE_MM2, "RoofCoverageMismatch")
    require(abs(overlap) <= AREA_TOLERANCE_MM2, "RoofTriangleOverlap")
    require(hole_overlap <= AREA_TOLERANCE_MM2, "RoofCoversInteriorRing")
    return {"exteriorAreaSquareMillimeters": Polygon(outer).area,
            "interiorAreaSquareMillimeters": sum(Polygon(h).area for h in holes),
            "roofAreaSquareMillimeters": polygon.area, "roofTriangleCount": len(triangles),
            "roofUnionSymmetricDifferenceSquareMillimeters": difference,
            "roofDuplicateAreaSquareMillimeters": overlap, "roofInteriorOverlapSquareMillimeters": hole_overlap,
            "minimumInteriorClearanceMillimeters": min((Polygon(outer).boundary.distance(Polygon(h).boundary) for h in holes), default=None)}


def triangulate_roof(outer, holes):
    from shapely import constrained_delaunay_triangles
    polygon = validate_rings(outer, holes)
    vertices = [p for ring in [outer, *holes] for p in ring[:-1]]
    by_point = {tuple(p): i for i, p in enumerate(vertices)}
    require(len(by_point) == len(vertices), "RoofVertexRepeatedBetweenRings")
    rows = []
    for triangle in constrained_delaunay_triangles(polygon).geoms:
        points = list(triangle.exterior.coords)[:-1]
        require(len(points) == 3 and all(tuple(p) in by_point for p in points), "TriangulationIntroducedVertex")
        row = [by_point[tuple(p)] for p in points]
        a, b, c = [vertices[i] for i in row]
        if (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]) > 0:
            row.reverse()
        start = row.index(min(row))
        rows.append(row[start:] + row[:start])
    rows.sort()
    indices = [i for row in rows for i in row]
    return vertices, indices, audit_roof(outer, holes, vertices, indices)


def build_candidate(root=ROOT):
    root = Path(root).resolve()
    bundle, tile, binding = load_base(root)
    batch, deps, frame = dependencies(root)
    ordinal, outer, holes = read_target(root, batch, deps, frame)
    existing = [[round(Decimal(str(p["x"])) * 1000), round(Decimal(str(p["z"])) * 1000)] for p in binding["footprint"]]
    require(outer[:-1] == existing, "BaseExteriorExactEqualityFailed")
    require(binding["evidenceKindCode"].endswith(":SymbolicHeightFallback")
            and binding["heightMeters"] == 4 and binding["aboveGroundFloorCount"] is None, "ExistingSymbolicHeightChanged")
    vertices, indices, audit = triangulate_roof(outer, holes)
    geometry_sha = sha(encoded({"exteriorRingMm": outer, "interiorRingsMm": holes}))
    candidate = {"schemaVersion": "administrative-dong-focused-building-ring-review.v1",
        "revision": "focused-building-ring-private-review.r1", "administrativeAreaStableId": AREA,
        "buildingStableId": BUILDING, "baseGenerationHashSha256": GENERATION,
        "baseProjectionHashSha256": bundle["projectionHashSha256"], "baseBundleSha256": BUNDLE_SHA,
        "baseTileStableId": tile["tileStableId"], "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptSha256": RECEIPT_SHA, "sourceGeometrySha256": geometry_sha,
        "sourceRecordOrdinal": ordinal, "sourceVintage": "AL_D010:Seoul:20260809",
        "sourceCrs": "EPSG:5186", "sourceRightsStatus": "RightsConflictUnresolved",
        "coordinateFrame": bundle["manifest"]["coordinateFrame"], "coordinateUnit": "CommonEnuMillimeters",
        "exteriorRingMm": outer, "interiorRingsMm": holes, "roofVerticesMm": vertices,
        "roofTriangleIndices": indices, "existingHeightMeters": 4, "existingHeightKind": "SymbolicHeightFallback4m",
        "sourcePolygonStructureObserved": True, "privateReviewVisualizationAllowed": True,
        "generatorSha256": sha_file(Path(__file__).resolve()), "geometryAudit": audit,
        **{key: False for key in FALSE_AUTHORITIES}}
    candidate["contentHashSha256"] = content_hash(candidate)
    return candidate


def artifacts(candidate):
    review = encoded(candidate)
    audit = encoded({"schemaVersion": "administrative-dong-focused-building-ring-audit.v1",
        "contentHashSha256": candidate["contentHashSha256"], "baseExteriorExactEquality": True,
        "sourceFeatureIdUnique": True, "sourceRecordOrdinal": candidate["sourceRecordOrdinal"],
        "sourcePolygonStructureOnly": True, "heightChanged": False, "baseArtifactsChanged": False,
        "geometryAudit": candidate["geometryAudit"]})
    completion = encoded({"schemaVersion": "administrative-dong-focused-building-ring-completion.v1",
        "generationHashSha256": candidate["contentHashSha256"], "files": [
            {"relativePath": "review.json", "sha256": sha(review), "byteLength": len(review)},
            {"relativePath": "audit.json", "sha256": sha(audit), "byteLength": len(audit)}]})
    return {"review.json": review, "audit.json": audit, "completion.json": completion}


def persist(root, candidate):
    folder = safe(root, OUTPUT / "generations" / candidate["contentHashSha256"].lower())
    files = artifacts(candidate)
    if folder.exists():
        require(set(p.name for p in folder.iterdir()) == set(files), "ImmutableGenerationUnexpectedFiles")
        for name, payload in files.items():
            require(safe(root, folder.relative_to(root) / name).read_bytes() == payload, "ImmutableGenerationContentMismatch:" + name)
        return folder, 0
    folder.parent.mkdir(parents=True, exist_ok=True)
    staging = safe(root, folder.parent.relative_to(root) / ("staging-" + uuid.uuid4().hex))
    staging.mkdir()
    for name, payload in files.items():
        (staging / name).write_bytes(payload)
    os.rename(staging, folder)
    return folder, len(files)


def verify(root, candidate):
    folder = safe(root, OUTPUT / "generations" / candidate["contentHashSha256"].lower())
    require(folder.is_dir(), "GenerationMissing")
    for name, expected in artifacts(candidate).items():
        require(safe(root, folder.relative_to(root) / name).read_bytes() == expected, "IndependentReconstructionMismatch:" + name)
    return folder


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify"))
    args = parser.parse_args()
    candidate = build_candidate()
    if args.mode == "build":
        folder, changed = persist(ROOT, candidate)
    else:
        folder, changed = verify(ROOT, candidate), 0
    print(json.dumps({"status": "PASS", "mode": args.mode, "changedFiles": changed,
        "generationHashSha256": candidate["contentHashSha256"], "reviewPath": str(folder / "review.json"),
        "reviewSha256": sha(encoded(candidate)), "sourceRecordOrdinal": candidate["sourceRecordOrdinal"],
        "geometryAudit": candidate["geometryAudit"]}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except (FocusedBuildingRingError, OSError, ValueError, KeyError, ImportError) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(1)

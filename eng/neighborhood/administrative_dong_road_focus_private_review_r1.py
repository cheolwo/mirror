#!/usr/bin/env python3
"""동결된 한 행정동의 완전 사본을 검증하고 작은 비공개 검토 범위를 선택한다.

원본 도형/타일을 복제하거나 변경하지 않는 참조 sidecar다. 전체 동 생성기,
interior-ring 초안, Unity, DB 또는 외부 자료에 접근하지 않는다.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from decimal import Decimal
import hashlib
import json
import math
import os
from pathlib import Path
import re
import stat
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path("artifacts/local/validation/admin-dong-road-focus-private-review/r1")
AREA = "region:kr:hjd:1126057500"
BUILDING = "vworld:al-d010:0000208470354536004400000000"
GENERATION = "13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753"
BASE = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r7/input/generations") / GENERATION.lower()
FRAME = {"method": "WGS84-ECEF-ENU-at-zero-altitude", "originLatitude": Decimal("37.580912"),
         "originLongitude": Decimal("127.088502"), "worldOffsetX": 0,
         "worldOffsetZ": 0, "metersPerUnit": 1}
EPSILON = 1e-9


@dataclass(frozen=True)
class SourcePin:
    base: Path = BASE
    generation: str = GENERATION
    index_sha: str = "C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9"
    complete_sha: str = "26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D"


class RoadFocusError(ValueError):
    pass


def require(condition, code):
    if not condition:
        raise RoadFocusError(code)


def sha(payload):
    return hashlib.sha256(payload).hexdigest().upper()


def canonical(value):
    """기존 Unity content hash의 정렬/decimal 표기를 그대로 검증한다."""
    if isinstance(value, dict):
        return "{" + ",".join(canonical(k) + ":" + canonical(value[k]) for k in sorted(value)) + "}"
    if isinstance(value, list):
        return "[" + ",".join(canonical(v) for v in value) + "]"
    if isinstance(value, Decimal):
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
    raise RoadFocusError("NonFiniteJsonNumber:" + value)


def unique_object(pairs):
    value = {}
    for key, item in pairs:
        require(key not in value, "DuplicateJsonProperty:" + key)
        value[key] = item
    return value


def parse(payload):
    return json.loads(payload, parse_float=Decimal, parse_constant=reject_constant,
                      object_pairs_hook=unique_object)


def safe(root, relative):
    root = Path(root).resolve()
    relative = Path(relative)
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


def checked_json(path, expected_sha, expected_length=None):
    require(path.is_file(), "InputMissing:" + path.name)
    payload = path.read_bytes()
    require(expected_length is None or len(payload) == expected_length, "InputLengthMismatch:" + path.name)
    require(sha(payload) == str(expected_sha).upper(), "InputHashMismatch:" + path.name)
    value = parse(payload)
    require(isinstance(value, dict) and value.get("contentHashSha256") == content_hash(value),
            "ContentHashMismatch:" + path.name)
    return value, len(payload)


def finite(value):
    require(not isinstance(value, bool) and isinstance(value, (int, float, Decimal)), "CoordinateInvalid")
    number = float(value)
    require(math.isfinite(number), "CoordinateInvalid")
    return number


def point(value):
    require(isinstance(value, dict) and set(value) == {"x", "z"}, "PointInvalid")
    return finite(value["x"]), finite(value["z"])


def authority(value):
    require(value.get("observationPresentationOnly") is True, "ObservationAuthorityRequired")
    for name in ("distributionApproved", "traversalReady", "gameplayReady"):
        require(value.get(name) is False, "AuthorityFlagInvalid:" + name)


def unique_map(rows, key, code):
    require(isinstance(rows, list), code)
    result = {}
    for row in rows:
        require(isinstance(row, dict) and isinstance(row.get(key), str) and row[key].strip(), code)
        require(row[key] not in result, code)
        result[row[key]] = row
    return result


def validate_bundle(bundle, entry, expected_frame=FRAME):
    """선택 동 전체 타일의 집합/판본/개수를 검사한다. 부분 타일은 거절한다."""
    area = entry["administrativeAreaStableId"]
    require(bundle.get("schemaVersion") == "administrative-dong-diorama-unity-review-bundle.v7"
            and bundle.get("administrativeAreaStableId") == area, "BundleIdentityMismatch")
    authority(bundle)
    require(bundle.get("publishBlocked") is True and bundle.get("currentPointerUsed") is False
            and bundle.get("currentPointerUpdated") is False, "CandidateBoundaryInvalid")
    manifest = bundle["manifest"]
    authority(manifest)
    require(manifest.get("schemaVersion") == "administrative-dong-diorama.v1"
            and manifest.get("administrativeAreaStableId") == area
            and manifest.get("dataPolicyCode") == "ObservationPresentationOnly", "ManifestIdentityMismatch")
    projection = manifest["projectionHashSha256"]
    require(bool(re.fullmatch(r"[a-fA-F0-9]{64}", projection))
            and projection == bundle.get("projectionHashSha256") == entry.get("projectionHashSha256"),
            "ProjectionVersionMismatch")
    frame = manifest.get("coordinateFrame")
    require(frame == expected_frame, "CoordinateFrameMismatch")
    require(finite(frame["metersPerUnit"]) == 1, "CoordinateUnitUnsupported")
    summaries = unique_map(manifest.get("tiles"), "tileStableId", "TileSummarySetInvalid")
    tiles = unique_map(bundle.get("tiles"), "tileStableId", "TileSetInvalid")
    require(set(summaries) == set(tiles) and len(tiles) == entry.get("tileCount") == bundle.get("tileCount"),
            "CompleteTileSetRequired")
    buildings, roads = {}, {}
    for tile_id in sorted(tiles):
        tile, summary = tiles[tile_id], summaries[tile_id]
        require(tile.get("schemaVersion") == "administrative-dong-diorama.v1"
                and tile.get("administrativeAreaStableId") == area
                and tile.get("projectionHashSha256") == projection
                and tile.get("tileHashSha256") == summary.get("tileHashSha256")
                and bool(re.fullmatch(r"[a-fA-F0-9]{64}", tile.get("tileHashSha256", ""))), "TileVersionMismatch")
        x, z = tile.get("tileIndexX"), tile.get("tileIndexZ")
        require(type(x) is int and type(z) is int and x == summary.get("tileIndexX")
                and z == summary.get("tileIndexZ")
                and tile_id == f"tile:{area}:500m:x{x}:z{z}", "TileGridMismatch")
        require(tile.get("bounds") == {"minX": x * 500, "minZ": z * 500,
                "maxX": (x + 1) * 500, "maxZ": (z + 1) * 500}, "TileBoundsMismatch")
        tile_buildings = unique_map(tile.get("buildings"), "buildingStableId", "BuildingIdentityInvalid")
        tile_roads = unique_map(tile.get("roads"), "roadStableId", "RoadIdentityInvalid")
        require(len(tile_buildings) == summary.get("buildingCount")
                and len(tile_roads) == summary.get("roadSegmentCount"), "TileObjectCountMismatch")
        for identity, building in tile_buildings.items():
            require(identity not in buildings, "BuildingIdentityDuplicate")
            polygon = [point(p) for p in building["footprint"]]
            require(len(polygon) >= 3, "BuildingFootprintInvalid")
            polygon_centroid(polygon)
            buildings[identity] = (tile_id, building, polygon)
        for identity, road in tile_roads.items():
            require(identity not in roads, "RoadIdentityDuplicate")
            a, b = point(road["from"]), point(road["to"])
            require(a != b, "RoadSegmentInvalid")
            roads[identity] = (tile_id, road, a, b)
    require(len(buildings) == entry.get("buildingCount") == bundle.get("buildingCount")
            and len(roads) == entry.get("roadSegmentCount") == bundle.get("roadSegmentCount"),
            "BundleObjectCountMismatch")
    require(bundle.get("displayOverlays", {}).get("items") == [], "PublicOverlayRejected")
    require(isinstance(manifest.get("sources"), list) and manifest["sources"], "SourceAttributionMissing")
    require(isinstance(bundle.get("missingLayerCodes"), list), "MissingLayerDiagnosticsRequired")
    return buildings, roads


def load_selected(root, area=AREA, pin=SourcePin()):
    require(bool(re.fullmatch(r"region:kr:hjd:[0-9]{10}", area)), "AreaIdInvalid")
    base = safe(root, pin.base)
    index, index_length = checked_json(safe(base, Path("index.json")), pin.index_sha)
    complete, _ = checked_json(safe(base, Path("complete.json")), pin.complete_sha)
    require(index.get("schemaVersion") == "administrative-dong-diorama-unity-review-index.v7"
            and complete.get("schemaVersion") == "administrative-dong-diorama-unity-review-completion.v7"
            and index.get("generationHashSha256") == complete.get("generationHashSha256") == pin.generation,
            "GenerationMismatch")
    authority(index)
    entries = unique_map(index.get("bundles"), "administrativeAreaStableId", "IndexAreaSetInvalid")
    require(area in entries, "SelectedAreaNotFound")
    entry = entries[area]
    require(entry.get("relativePath") == f"bundles/{area.split(':')[-1]}.json", "BundlePathInvalid")
    files = unique_map(complete.get("files"), "relativePath", "CompletionSetInvalid")
    require(len(files) == complete.get("fileCount") - 1, "CompletionFileCountMismatch")
    for relative, digest, length in (("index.json", pin.index_sha, index_length),
                                    (entry["relativePath"], entry["sha256"], entry["byteLength"])):
        require(relative in files and files[relative].get("sha256") == digest
                and files[relative].get("byteLength") == length, "CompletionBindingMismatch")
    bundle, _ = checked_json(safe(base, Path(entry["relativePath"])), entry["sha256"], entry["byteLength"])
    require(bundle.get("contentHashSha256") == entry.get("contentHashSha256"), "BundleIndexContentMismatch")
    buildings, roads = validate_bundle(bundle, entry)
    binding = {"generationHashSha256": pin.generation, "indexSha256": pin.index_sha,
               "completionSha256": pin.complete_sha, "bundleRelativePath": (pin.base / entry["relativePath"]).as_posix(),
               "bundleSha256": entry["sha256"], "bundleBytes": entry["byteLength"],
               "projectionHashSha256": entry["projectionHashSha256"], "completeSelectedDongValidated": True,
               "otherDongBundlesRead": 0, "sourceFilesRead": 3}
    return bundle, buildings, roads, binding


def polygon_centroid(polygon):
    total = sx = sz = 0.0
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        cross = a[0] * b[1] - b[0] * a[1]
        total += cross
        sx += (a[0] + b[0]) * cross
        sz += (a[1] + b[1]) * cross
    require(abs(total) > EPSILON, "BuildingFootprintDegenerate")
    return sx / (3 * total), sz / (3 * total)


def inside_box(p, box):
    return box[0] - EPSILON <= p[0] <= box[2] + EPSILON and box[1] - EPSILON <= p[1] <= box[3] + EPSILON


def segment_intersects_box(a, b, box):
    """중점 대신 전체 선분의 닫힌 범위 교차를 검사한다."""
    low, high = 0.0, 1.0
    for axis in (0, 1):
        delta = b[axis] - a[axis]
        if abs(delta) <= EPSILON:
            if a[axis] < box[axis] - EPSILON or a[axis] > box[axis + 2] + EPSILON:
                return False
            continue
        t0, t1 = (box[axis] - a[axis]) / delta, (box[axis + 2] - a[axis]) / delta
        low, high = max(low, min(t0, t1)), min(high, max(t0, t1))
        if low > high + EPSILON:
            return False
    return True


def inside_polygon(p, polygon):
    inside = False
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        if segment_distance_squared(p, a, b) <= EPSILON * EPSILON:
            return True
        if (a[1] > p[1]) != (b[1] > p[1]):
            crossing = (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]
            if p[0] < crossing:
                inside = not inside
    return inside


def polygon_intersects_box(polygon, box):
    if any(inside_box(p, box) for p in polygon):
        return True
    if any(segment_intersects_box(a, b, box) for a, b in zip(polygon, polygon[1:] + polygon[:1])):
        return True
    return any(inside_polygon(corner, polygon) for corner in
               ((box[0], box[1]), (box[0], box[3]), (box[2], box[1]), (box[2], box[3])))


def segment_distance_squared(p, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = dx * dx + dz * dz
    t = 0 if length == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dz) / length))
    return (p[0] - a[0] - t * dx) ** 2 + (p[1] - a[1] - t * dz) ** 2


def select_focus(bundle, buildings, roads, binding, building_id=BUILDING, width=250):
    width = finite(width)
    require(25 <= width <= 500, "FocusWidthOutOfRange")
    require(building_id in buildings, "AnchorBuildingNotFound")
    center = tuple(round(v, 3) for v in polygon_centroid(buildings[building_id][2]))
    half = width / 2
    box = tuple(round(v, 3) for v in (center[0] - half, center[1] - half, center[0] + half, center[1] + half))
    selected_roads = sorted(identity for identity, (_, _, a, b) in roads.items() if segment_intersects_box(a, b, box))
    selected_buildings = sorted(identity for identity, (_, _, polygon) in buildings.items() if polygon_intersects_box(polygon, box))
    require(selected_roads, "NoRoadIntersectsFocus")
    anchor_road = min(selected_roads, key=lambda identity: (segment_distance_squared(center, roads[identity][2], roads[identity][3]), identity))
    area = bundle["administrativeAreaStableId"]
    value = {"schemaVersion": "administrative-dong-road-focus-private-review.v1", "revision": "r1",
             "administrativeAreaStableId": area, "sourceBinding": binding,
             "coordinateFrame": bundle["manifest"]["coordinateFrame"],
             "selection": {"method": "CompleteSourceGeometryIntersectsClosedCommonEnuBox",
                 "widthMeters": width, "centerCommonEnuMeters": {"x": center[0], "z": center[1]},
                 "boundsCommonEnuMeters": dict(zip(("minX", "minZ", "maxX", "maxZ"), box)),
                 "anchorBuildingStableId": building_id, "anchorRoadStableId": anchor_road,
                 "centerEvidence": "AreaWeightedCentroidOfFrozenBuildingExteriorRoundedToMillimeter",
                 "anchorRoadEvidence": "NearestIntersectingSourceSegmentToBuildingCentroid;NotRouteOrEntrance",
                 "planningReference": "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/focused-area-refinement.decision.r39.md",
                 "defaultAnchorPreviouslyDocumentedDetailCandidate": area == AREA and building_id == BUILDING,
                 "selectionIsWorkingCandidate": True},
             "selectedRoads": [{"roadStableId": identity, "sourceTileStableId": roads[identity][0],
                               "evidenceKindCode": roads[identity][1]["evidenceKindCode"]} for identity in selected_roads],
             "selectedBuildings": [{"buildingStableId": identity, "sourceTileStableId": buildings[identity][0],
                                   "evidenceKindCode": buildings[identity][1]["evidenceKindCode"]} for identity in selected_buildings],
             "counts": {"sourceTiles": len(bundle["tiles"]), "sourceBuildings": len(buildings), "sourceRoads": len(roads),
                        "selectedBuildings": len(selected_buildings), "selectedRoads": len(selected_roads)},
             "sources": bundle["manifest"]["sources"], "sourceVintage": bundle["sourceVintage"],
             "missingLayerCodes": sorted(set(bundle["missingLayerCodes"])),
             "limitations": ["HistoricalBoundaryCandidateOnly", "LargestExteriorOnly;InteriorRingsNotRestored",
                             "NoRoadWidthSidewalkEntranceOrTraversalInference", "SourceGeometriesNotClippedOrCopied",
                             "SpatialSelectionPrepared;DetailedGeometryNotImproved"],
             "authority": {"privateReviewOnly": True, "referenceSidecarOnly": True, "sourceGeometryUnchanged": True,
                           "completeTilePayloadReplaced": False, "publicDisplayAllowed": False,
                           "distributionApproved": False, "runtimeAuthorized": False, "traversalReady": False,
                           "gameplayReady": False, "unityApplied": False, "databaseWriteAllowed": False,
                           "currentPointerUpdated": False, "evidenceStageClaimed": None},
             "generatorSha256": sha(Path(__file__).read_bytes())}
    value["contentHashSha256"] = content_hash(value)
    return value


def materialize(root, value, mode):
    require(mode in ("build", "verify"), "ModeInvalid")
    digest = value["contentHashSha256"]
    require(digest == content_hash(value), "SidecarContentHashMismatch")
    target = safe(root, OUTPUT / "generations" / digest.lower())
    payload = encoded(value)
    receipt = encoded({"schemaVersion": "administrative-dong-road-focus-completion.v1", "sidecarSha256": sha(payload),
                       "sidecarBytes": len(payload), "sidecarContentHashSha256": digest})
    files = {"focus.json": payload, "complete.json": receipt}
    if target.exists() or mode == "verify":
        require(target.is_dir(), "GenerationMissing")
        require({p.name for p in target.iterdir()} == set(files), "GenerationFileSetMismatch")
        for name, expected in files.items():
            path = safe(target, Path(name))
            require(path.is_file() and path.read_bytes() == expected, "GenerationContentMismatch:" + name)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = safe(root, OUTPUT / "generations" / (".staging-" + uuid.uuid4().hex))
        staging.mkdir()
        for name, data in files.items():
            (staging / name).write_bytes(data)
        os.replace(staging, target)
        changed = len(files)
    return {"status": "PASS", "mode": mode, "changedFiles": changed,
            "generationRelativePath": target.relative_to(Path(root).resolve()).as_posix(),
            "contentHashSha256": digest, "sourceFilesRead": 3, "otherDongBundlesRead": 0,
            "counts": value["counts"], "authority": value["authority"]}


def run(root, mode, area=AREA, building_id=BUILDING, width=250):
    bundle, buildings, roads, binding = load_selected(root, area)
    value = select_focus(bundle, buildings, roads, binding, building_id, width)
    return materialize(root, value, mode)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--area", default=AREA)
    parser.add_argument("--building", default=BUILDING)
    parser.add_argument("--width", type=float, default=250)
    args = parser.parse_args()
    try:
        if args.mode == "self-test":
            suite = unittest.defaultTestLoader.discover(str(Path(__file__).parent),
                      pattern="test_administrative_dong_road_focus_private_review_r1.py")
            result = unittest.TextTestRunner(verbosity=2).run(suite)
            return 0 if result.wasSuccessful() else 1
        result = run(args.root.resolve(), args.mode, args.area, args.building, args.width)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (RoadFocusError, OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(json.dumps({"status": "FAIL", "errorCode": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

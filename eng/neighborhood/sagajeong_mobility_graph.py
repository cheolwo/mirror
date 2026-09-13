from __future__ import annotations

import copy
import hashlib
import json
import math
import os
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


MANIFEST_SCHEMA_VERSION = "region-mobility-graph-manifest.v1"
TILE_SCHEMA_VERSION = "region-mobility-graph-tile.v1"
GRAPH_STABLE_ID = "mobility-graph:kr:seoul:jungnang:sagajeong.r1"
REGION_STABLE_ID = "world-region:kr:seoul:jungnang:sagajeong.r1"
ADMINISTRATIVE_AREA_STABLE_IDS = ("region:kr:hjd:1126057500",)
REVISION = "sagajeong-mobility-graph.osm-candidate.r1"
COVERAGE_CODE = "SagajeongOneKilometerWindow"
EXPECTED_SOURCE_SHA256 = "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3"
SOURCE_URL = "https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854"
ORIGIN_OSM_NODE_ID = "6046633864"
ORIGIN_LATITUDE = 37.5806971
ORIGIN_LONGITUDE = 127.0884106
WORLD_OFFSET_X = 550.0
WORLD_OFFSET_Z = 8.0
BOUNDS = (50.0, -492.0, 1050.0, 508.0)
TILE_SIZE_METERS = 500

VERTICAL_SLICE_BUILDINGS = (
    ("synthetic-place:food-route-a", "1256772531"),
    ("synthetic-place:food-route-b", "1256772606"),
)

ROAD_TAG_KEYS = (
    "access",
    "bicycle",
    "bridge",
    "foot",
    "highway",
    "junction",
    "lanes",
    "maxspeed",
    "motor_vehicle",
    "motorcycle",
    "name",
    "oneway",
    "ref",
    "service",
    "sidewalk",
    "tunnel",
    "vehicle",
)
NODE_TAG_KEYS = ("access", "barrier", "crossing", "crossing:signals", "entrance", "highway")

MOTOR_ROAD_KINDS = frozenset(
    {
        "motorway",
        "motorway_link",
        "trunk",
        "trunk_link",
        "primary",
        "primary_link",
        "secondary",
        "secondary_link",
        "tertiary",
        "tertiary_link",
        "unclassified",
        "residential",
        "living_street",
        "service",
        "road",
        "track",
    }
)
PEDESTRIAN_ROAD_KINDS = frozenset(
    {
        "primary",
        "primary_link",
        "secondary",
        "secondary_link",
        "tertiary",
        "tertiary_link",
        "unclassified",
        "residential",
        "living_street",
        "service",
        "road",
        "track",
        "footway",
        "pedestrian",
        "path",
        "steps",
    }
)
RESTRICTED_ACCESS_VALUES = frozenset({"no", "private"})
ALLOWED_ACCESS_VALUES = frozenset({"yes", "designated", "permissive", "destination"})


class MobilityGraphBlocked(RuntimeError):
    def __init__(self, reason_code: str, detail: str = "") -> None:
        super().__init__(reason_code)
        self.reason_code = reason_code
        self.detail = detail

    def report(self) -> dict[str, Any]:
        return {
            "status": "Blocked",
            "reasonCode": self.reason_code,
            "detail": self.detail,
            "networkRequested": False,
            "runtimeAuthorized": False,
        }


class MobilityGraphAuditError(RuntimeError):
    pass


@dataclass(frozen=True)
class OsmNode:
    node_id: str
    latitude: float
    longitude: float
    tags: dict[str, str]


@dataclass(frozen=True)
class OsmWay:
    way_id: str
    node_ids: tuple[str, ...]
    tags: dict[str, str]


@dataclass(frozen=True)
class Point:
    x: float
    z: float


@dataclass(frozen=True)
class Segment:
    way_id: str
    segment_index: int
    from_node_id: str
    to_node_id: str
    start: Point
    end: Point
    tags: dict[str, str]


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n"
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def content_hash(value: dict[str, Any]) -> str:
    candidate = copy.deepcopy(value)
    candidate["contentHashSha256"] = ""
    return sha256_bytes(canonical_json_bytes(candidate))


def finalize_content_hash(value: dict[str, Any]) -> dict[str, Any]:
    value["contentHashSha256"] = ""
    value["contentHashSha256"] = content_hash(value)
    return value


def _require(condition: bool, code: str) -> None:
    if not condition:
        raise MobilityGraphAuditError(code)


def _id_sort(value: str) -> tuple[int, int | str]:
    return (0, int(value)) if value.isdigit() else (1, value)


def _quantize(value: float) -> float:
    rounded = round(value, 3)
    return 0.0 if abs(rounded) < 0.0005 else rounded


def _point_json(point: Point) -> dict[str, float]:
    return {"x": _quantize(point.x), "z": _quantize(point.z)}


def _tag_list(tags: dict[str, str], keys: Iterable[str]) -> list[dict[str, str]]:
    return [{"key": key, "value": tags[key]} for key in sorted(keys) if key in tags]


def _parse_source(source_path: Path, expected_source_sha256: str) -> tuple[dict[str, OsmNode], dict[str, OsmWay], str]:
    if not source_path.is_file():
        raise MobilityGraphBlocked("MobilityGraphSourceMissing", str(source_path))
    expected = expected_source_sha256.upper()
    if len(expected) != 64 or any(character not in "0123456789ABCDEF" for character in expected):
        raise MobilityGraphBlocked("MobilityGraphExpectedSourceHashInvalid", expected_source_sha256)
    actual_hash = sha256_file(source_path)
    if actual_hash != expected:
        raise MobilityGraphBlocked("MobilityGraphSourceHashMismatch", actual_hash)
    try:
        root = ET.parse(source_path).getroot()
    except (ET.ParseError, OSError) as error:
        raise MobilityGraphBlocked("MobilityGraphSourceXmlInvalid", type(error).__name__) from error
    if root.tag != "osm" or root.attrib.get("version") != "0.6":
        raise MobilityGraphBlocked("MobilityGraphSourceSchemaUnsupported", root.tag)

    nodes: dict[str, OsmNode] = {}
    ways: dict[str, OsmWay] = {}
    for element in root:
        if element.tag == "node":
            node_id = element.attrib.get("id", "")
            if not node_id or node_id in nodes:
                raise MobilityGraphBlocked("MobilityGraphSourceNodeIdentityInvalid", node_id)
            tags = {
                tag.attrib["k"]: tag.attrib["v"]
                for tag in element.findall("tag")
                if "k" in tag.attrib and "v" in tag.attrib
            }
            try:
                latitude = float(element.attrib["lat"])
                longitude = float(element.attrib["lon"])
            except (KeyError, ValueError) as error:
                raise MobilityGraphBlocked("MobilityGraphSourceCoordinateInvalid", node_id) from error
            if not math.isfinite(latitude) or not math.isfinite(longitude):
                raise MobilityGraphBlocked("MobilityGraphSourceCoordinateInvalid", node_id)
            nodes[node_id] = OsmNode(node_id, latitude, longitude, tags)
        elif element.tag == "way":
            way_id = element.attrib.get("id", "")
            if not way_id or way_id in ways:
                raise MobilityGraphBlocked("MobilityGraphSourceWayIdentityInvalid", way_id)
            node_ids = tuple(node.attrib.get("ref", "") for node in element.findall("nd"))
            if any(not node_id for node_id in node_ids):
                raise MobilityGraphBlocked("MobilityGraphSourceWayNodeInvalid", way_id)
            tags = {
                tag.attrib["k"]: tag.attrib["v"]
                for tag in element.findall("tag")
                if "k" in tag.attrib and "v" in tag.attrib
            }
            ways[way_id] = OsmWay(way_id, node_ids, tags)
    origin = nodes.get(ORIGIN_OSM_NODE_ID)
    if origin is None:
        raise MobilityGraphBlocked("MobilityGraphOriginNodeMissing", ORIGIN_OSM_NODE_ID)
    if origin.latitude != ORIGIN_LATITUDE or origin.longitude != ORIGIN_LONGITUDE:
        raise MobilityGraphBlocked("MobilityGraphOriginCoordinateMismatch", ORIGIN_OSM_NODE_ID)
    return nodes, ways, actual_hash


def _ecef(latitude: float, longitude: float) -> tuple[float, float, float]:
    latitude_radians = math.radians(latitude)
    longitude_radians = math.radians(longitude)
    eccentricity_squared = 0.0066943799901413165
    radius = 6378137.0 / math.sqrt(
        1.0 - eccentricity_squared * math.sin(latitude_radians) * math.sin(latitude_radians)
    )
    return (
        radius * math.cos(latitude_radians) * math.cos(longitude_radians),
        radius * math.cos(latitude_radians) * math.sin(longitude_radians),
        radius * (1.0 - eccentricity_squared) * math.sin(latitude_radians),
    )


def _project(node: OsmNode) -> Point:
    origin = _ecef(ORIGIN_LATITUDE, ORIGIN_LONGITUDE)
    point = _ecef(node.latitude, node.longitude)
    latitude_radians = math.radians(ORIGIN_LATITUDE)
    longitude_radians = math.radians(ORIGIN_LONGITUDE)
    dx, dy, dz = (point[index] - origin[index] for index in range(3))
    east = -math.sin(longitude_radians) * dx + math.cos(longitude_radians) * dy
    north = (
        -math.sin(latitude_radians) * math.cos(longitude_radians) * dx
        - math.sin(latitude_radians) * math.sin(longitude_radians) * dy
        + math.cos(latitude_radians) * dz
    )
    return Point(_quantize(east + WORLD_OFFSET_X), _quantize(north + WORLD_OFFSET_Z))


def _inside(point: Point) -> bool:
    return BOUNDS[0] <= point.x <= BOUNDS[2] and BOUNDS[1] <= point.z <= BOUNDS[3]


def _point_at(start: Point, end: Point, parameter: float) -> Point:
    return Point(start.x + (end.x - start.x) * parameter, start.z + (end.z - start.z) * parameter)


def _split_parameters(start: Point, end: Point) -> list[float]:
    parameters = {0.0, 1.0}
    delta_x = end.x - start.x
    delta_z = end.z - start.z
    if delta_x != 0:
        for boundary in (BOUNDS[0], WORLD_OFFSET_X, BOUNDS[2]):
            parameter = (boundary - start.x) / delta_x
            if 0.0 < parameter < 1.0:
                parameters.add(parameter)
    if delta_z != 0:
        for boundary in (BOUNDS[1], WORLD_OFFSET_Z, BOUNDS[3]):
            parameter = (boundary - start.z) / delta_z
            if 0.0 < parameter < 1.0:
                parameters.add(parameter)
    return sorted(parameters)


def _tile_index(point: Point) -> tuple[int, int]:
    return (0 if point.x < WORLD_OFFSET_X else 1, 0 if point.z < WORLD_OFFSET_Z else 1)


def _tile_id(index_x: int, index_z: int) -> str:
    return f"mobility-tile:sagajeong:x{index_x}:z{index_z}.r1"


def _tile_filename(index_x: int, index_z: int) -> str:
    return f"tiles/tile-x{index_x}-z{index_z}.json"


def _tile_bounds(index_x: int, index_z: int) -> tuple[float, float, float, float]:
    minimum_x = BOUNDS[0] + index_x * TILE_SIZE_METERS
    minimum_z = BOUNDS[1] + index_z * TILE_SIZE_METERS
    return (minimum_x, minimum_z, minimum_x + TILE_SIZE_METERS, minimum_z + TILE_SIZE_METERS)


def _on_boundary(point: Point) -> bool:
    return point.x in (BOUNDS[0], WORLD_OFFSET_X, BOUNDS[2]) or point.z in (
        BOUNDS[1],
        WORLD_OFFSET_Z,
        BOUNDS[3],
    )


def _direction_code(oneway: str) -> str:
    normalized = oneway.strip().lower()
    if normalized in {"yes", "true", "1"}:
        return "Forward"
    if normalized in {"-1", "reverse"}:
        return "Reverse"
    if normalized in {"no", "false", "0"}:
        return "Both"
    return "Unknown"


def _candidate_modes(tags: dict[str, str]) -> list[str]:
    highway = tags.get("highway", "")
    modes: set[str] = set()
    if highway in MOTOR_ROAD_KINDS:
        modes.update(("Vehicle", "Motorcycle"))
    if highway in PEDESTRIAN_ROAD_KINDS:
        modes.add("Pedestrian")
    if tags.get("foot", "").lower() in ALLOWED_ACCESS_VALUES:
        modes.add("Pedestrian")
    if tags.get("motorcycle", "").lower() in ALLOWED_ACCESS_VALUES:
        modes.add("Motorcycle")
    if tags.get("motor_vehicle", "").lower() in ALLOWED_ACCESS_VALUES:
        modes.update(("Vehicle", "Motorcycle"))

    if tags.get("access", "").lower() in RESTRICTED_ACCESS_VALUES:
        modes.clear()
    if tags.get("vehicle", "").lower() in RESTRICTED_ACCESS_VALUES:
        modes.difference_update(("Vehicle", "Motorcycle"))
    if tags.get("motor_vehicle", "").lower() in RESTRICTED_ACCESS_VALUES:
        modes.difference_update(("Vehicle", "Motorcycle"))
    if tags.get("motorcycle", "").lower() in RESTRICTED_ACCESS_VALUES:
        modes.discard("Motorcycle")
    if tags.get("foot", "").lower() in RESTRICTED_ACCESS_VALUES:
        modes.discard("Pedestrian")
    return sorted(modes)


def _access_review_code(tags: dict[str, str]) -> str:
    restricted_keys = ("access", "vehicle", "motor_vehicle", "motorcycle", "foot")
    if any(tags.get(key, "").lower() in RESTRICTED_ACCESS_VALUES for key in restricted_keys):
        return "SourceExplicitlyRestricted"
    return "PendingHumanReview"


def _distance(start: Point, end: Point) -> float:
    return math.hypot(end.x - start.x, end.z - start.z)


def _node_roles(
    node_id: str,
    node: OsmNode | None,
    road_degree: dict[str, int],
    node_road_ways: dict[str, set[str]],
    portal: bool,
) -> list[str]:
    roles: set[str] = set()
    if node_id and road_degree.get(node_id, 0) > 0:
        roles.add("RoadVertex")
        if road_degree.get(node_id, 0) != 2 or len(node_road_ways.get(node_id, set())) > 1:
            roles.add("Junction")
    if node is not None and node.tags.get("highway") == "crossing":
        roles.add("Crossing")
    if node is not None and node.tags.get("entrance", "").lower() not in {"", "no"}:
        roles.add("Entrance")
    if portal:
        roles.add("TilePortal")
    return sorted(roles)


def _stitch_id(way_id: str, segment_index: int, point: Point, source_node_id: str) -> str:
    if source_node_id:
        return f"mobility-stitch:osm-node:{source_node_id}"
    millimeter_x = int(round(point.x * 1000.0))
    millimeter_z = int(round(point.z * 1000.0))
    return f"mobility-stitch:osm-way:{way_id}:segment:{segment_index}:at:{millimeter_x}:{millimeter_z}"


def _node_id(tile_stable_id: str, source_node_id: str, stitch_stable_id: str) -> str:
    if stitch_stable_id:
        suffix = stitch_stable_id.removeprefix("mobility-stitch:")
        return f"{tile_stable_id}:portal:{suffix}"
    return f"{tile_stable_id}:osm-node:{source_node_id}"


def _point_to_segment(point: Point, start: Point, end: Point) -> tuple[Point, float]:
    delta_x = end.x - start.x
    delta_z = end.z - start.z
    length_squared = delta_x * delta_x + delta_z * delta_z
    if length_squared == 0:
        return start, _distance(point, start)
    parameter = ((point.x - start.x) * delta_x + (point.z - start.z) * delta_z) / length_squared
    parameter = max(0.0, min(1.0, parameter))
    projected = _point_at(start, end, parameter)
    return projected, _distance(point, projected)


def _segment_connector_candidate(
    building_points: list[Point], road_segments: list[Segment], entrance: tuple[str, Point] | None
) -> tuple[Point, Point, Segment, float] | None:
    eligible = [segment for segment in road_segments if "Motorcycle" in _candidate_modes(segment.tags)]
    if not eligible or not building_points:
        return None
    best: tuple[float, str, int, float, float, Point, Point, Segment] | None = None
    if entrance is not None:
        source_points = [entrance[1]]
    else:
        source_points = building_points
    for source_point in source_points:
        for segment in eligible:
            target, distance = _point_to_segment(source_point, segment.start, segment.end)
            candidate = (
                distance,
                segment.way_id,
                segment.segment_index,
                source_point.x,
                source_point.z,
                source_point,
                target,
                segment,
            )
            if best is None or candidate[:5] < best[:5]:
                best = candidate
    if best is None:
        return None
    return best[5], best[6], best[7], best[0]


def build_package(source_path: Path, expected_source_sha256: str = EXPECTED_SOURCE_SHA256) -> dict[str, Any]:
    source_path = Path(source_path)
    nodes, ways, raw_hash = _parse_source(source_path, expected_source_sha256)
    projected = {node_id: _project(node) for node_id, node in nodes.items()}

    building_memberships: dict[str, set[str]] = {}
    for way in ways.values():
        if way.tags.get("building", "").lower() not in {"", "no"}:
            for node_id in way.node_ids:
                building_memberships.setdefault(node_id, set()).add(way.way_id)

    road_segments: list[Segment] = []
    road_degree: dict[str, int] = {}
    node_road_ways: dict[str, set[str]] = {}
    omitted_missing_node_segments = 0
    for way in sorted(ways.values(), key=lambda item: _id_sort(item.way_id)):
        highway = way.tags.get("highway", "")
        if not highway or highway in {"proposed", "construction"}:
            continue
        for index in range(1, len(way.node_ids)):
            from_node_id = way.node_ids[index - 1]
            to_node_id = way.node_ids[index]
            if from_node_id not in nodes or to_node_id not in nodes:
                omitted_missing_node_segments += 1
                continue
            start = projected[from_node_id]
            end = projected[to_node_id]
            if start == end:
                continue
            road_segments.append(
                Segment(way.way_id, index - 1, from_node_id, to_node_id, start, end, dict(way.tags))
            )
            road_degree[from_node_id] = road_degree.get(from_node_id, 0) + 1
            road_degree[to_node_id] = road_degree.get(to_node_id, 0) + 1
            node_road_ways.setdefault(from_node_id, set()).add(way.way_id)
            node_road_ways.setdefault(to_node_id, set()).add(way.way_id)

    tile_states: dict[tuple[int, int], dict[str, Any]] = {}
    for index_x in range(2):
        for index_z in range(2):
            tile_states[(index_x, index_z)] = {
                "nodes": {},
                "edges": [],
                "portalRefs": set(),
            }

    def ensure_node(
        tile_index: tuple[int, int],
        point: Point,
        source_node_id: str,
        way_id: str,
        segment_index: int,
    ) -> str:
        index_x, index_z = tile_index
        tile_stable_id = _tile_id(index_x, index_z)
        portal = _on_boundary(point)
        stitch_stable_id = _stitch_id(way_id, segment_index, point, source_node_id) if portal else ""
        stable_id = _node_id(tile_stable_id, source_node_id, stitch_stable_id)
        source_node = nodes.get(source_node_id) if source_node_id else None
        roles = _node_roles(source_node_id, source_node, road_degree, node_road_ways, portal)
        connection_status = "SourceRoadNode" if source_node_id in road_degree else "UnresolvedEntrance"
        candidate = {
            "nodeStableId": stable_id,
            "sourceOsmNodeId": source_node_id,
            "position": _point_json(point),
            "roleCodes": roles,
            "sourceTags": _tag_list(source_node.tags if source_node else {}, NODE_TAG_KEYS),
            "sourceBuildingOsmWayIds": sorted(building_memberships.get(source_node_id, set()), key=_id_sort),
            "isPortal": portal,
            "stitchStableId": stitch_stable_id,
            "connectionStatusCode": connection_status,
            "runtimeAuthorized": False,
        }
        state = tile_states[tile_index]
        existing = state["nodes"].get(stable_id)
        if existing is None:
            state["nodes"][stable_id] = candidate
        else:
            existing["roleCodes"] = sorted(set(existing["roleCodes"]) | set(candidate["roleCodes"]))
            existing["sourceBuildingOsmWayIds"] = sorted(
                set(existing["sourceBuildingOsmWayIds"]) | set(candidate["sourceBuildingOsmWayIds"]),
                key=_id_sort,
            )
        if portal:
            state["portalRefs"].add((stitch_stable_id, stable_id))
        return stable_id

    clipped_source_segments = 0
    for segment in sorted(road_segments, key=lambda item: (_id_sort(item.way_id), item.segment_index)):
        parameters = _split_parameters(segment.start, segment.end)
        source_segment_had_piece = False
        for part_index, (start_parameter, end_parameter) in enumerate(zip(parameters, parameters[1:])):
            midpoint = _point_at(segment.start, segment.end, (start_parameter + end_parameter) / 2.0)
            if not _inside(midpoint):
                continue
            start = Point(*(_quantize(value) for value in (_point_at(segment.start, segment.end, start_parameter).x,
                                                            _point_at(segment.start, segment.end, start_parameter).z)))
            end = Point(*(_quantize(value) for value in (_point_at(segment.start, segment.end, end_parameter).x,
                                                          _point_at(segment.start, segment.end, end_parameter).z)))
            if start == end:
                continue
            tile_index = _tile_index(midpoint)
            source_from = segment.from_node_id if math.isclose(start_parameter, 0.0, abs_tol=1e-12) else ""
            source_to = segment.to_node_id if math.isclose(end_parameter, 1.0, abs_tol=1e-12) else ""
            from_id = ensure_node(tile_index, start, source_from, segment.way_id, segment.segment_index)
            to_id = ensure_node(tile_index, end, source_to, segment.way_id, segment.segment_index)
            index_x, index_z = tile_index
            tile_stable_id = _tile_id(index_x, index_z)
            modes = _candidate_modes(segment.tags)
            access_review = _access_review_code(segment.tags)
            tile_states[tile_index]["edges"].append(
                {
                    "edgeStableId": (
                        f"{tile_stable_id}:osm-way:{segment.way_id}:segment:{segment.segment_index}:part:{part_index}"
                    ),
                    "edgeKindCode": "SourceRoadSegment",
                    "sourceOsmWayId": segment.way_id,
                    "sourceSegmentIndex": segment.segment_index,
                    "sourceGeometryNodeIds": [segment.from_node_id, segment.to_node_id],
                    "fromNodeStableId": from_id,
                    "toNodeStableId": to_id,
                    "geometry": [_point_json(start), _point_json(end)],
                    "lengthMeters": _quantize(_distance(start, end)),
                    "directionCode": _direction_code(segment.tags.get("oneway", "")),
                    "candidateModeCodes": modes,
                    "accessReviewCode": access_review,
                    "sourceOneway": segment.tags.get("oneway", ""),
                    "sourceAccess": segment.tags.get("access", ""),
                    "sourceVehicle": segment.tags.get("vehicle", ""),
                    "sourceMotorVehicle": segment.tags.get("motor_vehicle", ""),
                    "sourceMotorcycle": segment.tags.get("motorcycle", ""),
                    "sourceFoot": segment.tags.get("foot", ""),
                    "sourceTags": _tag_list(segment.tags, ROAD_TAG_KEYS),
                    "visualRoadStableId": f"osm:way:{segment.way_id}:segment:{segment.segment_index}",
                    "throughRouteCandidate": segment.tags.get("service", "") != "parking_aisle",
                    "boundaryClipped": start_parameter > 0.0 or end_parameter < 1.0,
                    "runtimeAuthorized": False,
                }
            )
            source_segment_had_piece = True
        if source_segment_had_piece and (not _inside(segment.start) or not _inside(segment.end)):
            clipped_source_segments += 1

    detached_entrance_count = 0
    crossing_count = 0
    for node_id in sorted(nodes, key=_id_sort):
        node = nodes[node_id]
        is_entrance = node.tags.get("entrance", "").lower() not in {"", "no"}
        is_crossing = node.tags.get("highway") == "crossing"
        if not (is_entrance or is_crossing):
            continue
        point = projected[node_id]
        if not _inside(point):
            continue
        tile_index = _tile_index(point)
        ensure_node(tile_index, point, node_id, "tagged-node", 0)
        if is_entrance and node_id not in road_degree:
            detached_entrance_count += 1
        if is_crossing:
            crossing_count += 1

    tiles: list[dict[str, Any]] = []
    tile_files: dict[str, dict[str, Any]] = {}
    stitch_references: dict[str, set[tuple[str, str]]] = {}
    for index_x in range(2):
        for index_z in range(2):
            tile_stable_id = _tile_id(index_x, index_z)
            state = tile_states[(index_x, index_z)]
            tile_nodes = sorted(state["nodes"].values(), key=lambda item: item["nodeStableId"])
            tile_edges = sorted(state["edges"], key=lambda item: item["edgeStableId"])
            tile = finalize_content_hash(
                {
                    "schemaVersion": TILE_SCHEMA_VERSION,
                    "graphStableId": GRAPH_STABLE_ID,
                    "regionStableId": REGION_STABLE_ID,
                    "revision": REVISION,
                    "coverageCode": COVERAGE_CODE,
                    "administrativeBoundaryClipped": False,
                    "tileStableId": tile_stable_id,
                    "tileIndexX": index_x,
                    "tileIndexZ": index_z,
                    "bounds": list(_tile_bounds(index_x, index_z)),
                    "sourceRawContentHashSha256": raw_hash,
                    "nodes": tile_nodes,
                    "edges": tile_edges,
                    "distributionApproved": False,
                    "traversalReady": False,
                    "runtimeAuthorized": False,
                    "contentHashSha256": "",
                }
            )
            file_name = _tile_filename(index_x, index_z)
            file_bytes = canonical_json_bytes(tile)
            tile_files[file_name] = tile
            portal_count = sum(1 for node in tile_nodes if node["isPortal"])
            tiles.append(
                {
                    "tileStableId": tile_stable_id,
                    "tileIndexX": index_x,
                    "tileIndexZ": index_z,
                    "bounds": list(_tile_bounds(index_x, index_z)),
                    "fileName": file_name,
                    "contentHashSha256": tile["contentHashSha256"],
                    "fileHashSha256": sha256_bytes(file_bytes),
                    "nodeCount": len(tile_nodes),
                    "edgeCount": len(tile_edges),
                    "portalCount": portal_count,
                }
            )
            for stitch_stable_id, node_stable_id in state["portalRefs"]:
                stitch_references.setdefault(stitch_stable_id, set()).add((tile_stable_id, node_stable_id))

    stitches: list[dict[str, Any]] = []
    node_lookup = {
        node["nodeStableId"]: node
        for tile in tile_files.values()
        for node in tile["nodes"]
    }
    for stitch_stable_id in sorted(stitch_references):
        references = sorted(stitch_references[stitch_stable_id])
        first_node = node_lookup[references[0][1]]
        stitches.append(
            {
                "stitchStableId": stitch_stable_id,
                "kindCode": "TileStitch" if len(references) > 1 else "OpenBoundaryPortal",
                "position": first_node["position"],
                "portals": [
                    {"tileStableId": tile_stable_id, "nodeStableId": node_stable_id}
                    for tile_stable_id, node_stable_id in references
                ],
                "runtimeAuthorized": False,
            }
        )

    vertical_slice_candidates: list[dict[str, Any]] = []
    for semantic_place_stable_id, building_way_id in VERTICAL_SLICE_BUILDINGS:
        building = ways.get(building_way_id)
        building_points = [projected[node_id] for node_id in building.node_ids if node_id in projected] if building else []
        entrance_ids = (
            sorted(
                {
                    node_id
                    for node_id in building.node_ids
                    if node_id in nodes and nodes[node_id].tags.get("entrance", "").lower() not in {"", "no"}
                },
                key=_id_sort,
            )
            if building
            else []
        )
        entrance = (entrance_ids[0], projected[entrance_ids[0]]) if entrance_ids else None
        nearest = _segment_connector_candidate(building_points, road_segments, entrance)
        connector_candidates: list[dict[str, Any]] = []
        if nearest is not None:
            source_point, target_point, road_segment, distance = nearest
            connector_candidates.append(
                {
                    "candidateStableId": f"mobility-connector-candidate:{semantic_place_stable_id}:motorcycle:r1",
                    "anchorCode": "TaggedEntrance" if entrance is not None else "BuildingBoundaryVertexForReview",
                    "sourceEntranceOsmNodeId": entrance[0] if entrance is not None else "",
                    "from": _point_json(source_point),
                    "to": _point_json(target_point),
                    "straightDistanceMeters": _quantize(distance),
                    "sourceRoadOsmWayId": road_segment.way_id,
                    "sourceRoadSegmentIndex": road_segment.segment_index,
                    "visualRoadStableId": (
                        f"osm:way:{road_segment.way_id}:segment:{road_segment.segment_index}"
                    ),
                    "candidateModeCode": "Motorcycle",
                    "reviewStatusCode": "PendingHumanReview",
                    "generatedAsGraphEdge": False,
                    "runtimeAuthorized": False,
                }
            )
        vertical_slice_candidates.append(
            {
                "semanticPlaceStableId": semantic_place_stable_id,
                "sourceBuildingOsmWayId": building_way_id,
                "buildingFound": building is not None,
                "entranceEvidenceCode": "TaggedEntrance" if entrance_ids else "NoTaggedEntrance",
                "taggedEntranceOsmNodeIds": entrance_ids,
                "connectorCandidates": connector_candidates,
                "bindingStatusCode": "PendingHumanReview" if building is not None else "SourceBuildingMissing",
                "runtimeAuthorized": False,
            }
        )

    graph_hash_payload = {
        "graphStableId": GRAPH_STABLE_ID,
        "revision": REVISION,
        "sourceRawContentHashSha256": raw_hash,
        "tiles": [
            {"tileStableId": tile["tileStableId"], "contentHashSha256": tile["contentHashSha256"]}
            for tile in tiles
        ],
        "stitches": stitches,
    }
    projection_hash = sha256_bytes(canonical_json_bytes(graph_hash_payload))
    total_nodes = sum(tile["nodeCount"] for tile in tiles)
    total_edges = sum(tile["edgeCount"] for tile in tiles)
    manifest = finalize_content_hash(
        {
            "schemaVersion": MANIFEST_SCHEMA_VERSION,
            "graphStableId": GRAPH_STABLE_ID,
            "regionStableId": REGION_STABLE_ID,
            "administrativeAreaStableIds": list(ADMINISTRATIVE_AREA_STABLE_IDS),
            "revision": REVISION,
            "coverageCode": COVERAGE_CODE,
            "administrativeBoundaryClipped": False,
            "readinessCode": "PendingHumanReview",
            "projectionHashSha256": projection_hash,
            "provenance": {
                "sourceId": "openstreetmap",
                "datasetId": "osm-map-sagajeong-one-kilometer",
                "sourceVersion": "osm-api-0.6-frozen",
                "sourceUrl": SOURCE_URL,
                "rawContentHashSha256": raw_hash,
                "licenseCode": "ODbL-1.0",
                "attribution": "© OpenStreetMap contributors",
                "coordinateMethod": "WGS84-ECEF-ENU-at-zero-altitude",
                "originOsmNodeId": ORIGIN_OSM_NODE_ID,
                "originLatitude": ORIGIN_LATITUDE,
                "originLongitude": ORIGIN_LONGITUDE,
            },
            "coordinates": {
                "unit": "m",
                "axisOrder": "EastingNorthing",
                "bounds": list(BOUNDS),
                "tileSizeMeters": TILE_SIZE_METERS,
                "worldOffsetX": WORLD_OFFSET_X,
                "worldOffsetZ": WORLD_OFFSET_Z,
                "metersPerUnit": 1.0,
            },
            "tiles": tiles,
            "stitches": stitches,
            "verticalSliceCandidates": vertical_slice_candidates,
            "quality": {
                "accessReviewCode": "PendingHumanReview",
                "connectorPolicyCode": "NoInferredGraphConnectors",
                "directionPolicyCode": "PreserveExplicitOsmOnewayOtherwiseUnknown",
                "parkingAislePolicyCode": "NotThroughRouteCandidate",
                "administrativeCoveragePolicyCode": "AdministrativeAreasAreAnalysisReferencesOnly",
                "omittedMissingNodeSegmentCount": omitted_missing_node_segments,
                "clippedSourceSegmentCount": clipped_source_segments,
                "detachedEntranceCount": detached_entrance_count,
                "crossingCount": crossing_count,
                "nodeCount": total_nodes,
                "edgeCount": total_edges,
            },
            "distributionApproved": False,
            "traversalReady": False,
            "runtimeAuthorized": False,
            "contentHashSha256": "",
        }
    )
    package = {"manifest": manifest, "tiles": tile_files}
    validate_package_structure(package)
    return package


def validate_package_structure(package: dict[str, Any]) -> None:
    manifest = package.get("manifest")
    tiles = package.get("tiles")
    _require(isinstance(manifest, dict), "MobilityGraphManifestMissing")
    _require(isinstance(tiles, dict), "MobilityGraphTilesMissing")
    _require(manifest.get("schemaVersion") == MANIFEST_SCHEMA_VERSION, "MobilityGraphManifestSchemaInvalid")
    _require(manifest.get("graphStableId") == GRAPH_STABLE_ID, "MobilityGraphStableIdInvalid")
    _require(manifest.get("regionStableId") == REGION_STABLE_ID, "MobilityGraphRegionInvalid")
    _require(
        manifest.get("administrativeAreaStableIds") == list(ADMINISTRATIVE_AREA_STABLE_IDS),
        "MobilityGraphAdministrativeReferencesInvalid",
    )
    _require(manifest.get("coverageCode") == COVERAGE_CODE, "MobilityGraphCoverageInvalid")
    _require(manifest.get("administrativeBoundaryClipped") is False, "MobilityGraphAdministrativeClipClaimInvalid")
    _require(manifest.get("distributionApproved") is False, "MobilityGraphDistributionMustRemainDisabled")
    _require(manifest.get("traversalReady") is False, "MobilityGraphTraversalMustRemainDisabled")
    _require(manifest.get("runtimeAuthorized") is False, "MobilityGraphRuntimeMustRemainDisabled")
    _require(manifest.get("readinessCode") == "PendingHumanReview", "MobilityGraphReadinessInvalid")
    _require(manifest.get("contentHashSha256") == content_hash(manifest), "MobilityGraphManifestHashMismatch")
    coordinates = manifest.get("coordinates")
    _require(isinstance(coordinates, dict), "MobilityGraphCoordinatesMissing")
    _require(coordinates.get("unit") == "m", "MobilityGraphCoordinateUnitInvalid")
    _require(coordinates.get("axisOrder") == "EastingNorthing", "MobilityGraphCoordinateAxisInvalid")
    _require(coordinates.get("bounds") == list(BOUNDS), "MobilityGraphCoordinateBoundsInvalid")
    _require(coordinates.get("tileSizeMeters") == TILE_SIZE_METERS, "MobilityGraphTileSizeInvalid")
    _require(coordinates.get("worldOffsetX") == WORLD_OFFSET_X, "MobilityGraphWorldOffsetInvalid")
    _require(coordinates.get("worldOffsetZ") == WORLD_OFFSET_Z, "MobilityGraphWorldOffsetInvalid")
    _require(coordinates.get("metersPerUnit") == 1.0, "MobilityGraphScaleInvalid")
    tile_summaries = manifest.get("tiles")
    _require(isinstance(tile_summaries, list) and len(tile_summaries) == 4, "MobilityGraphTileGridInvalid")

    seen_tile_ids: set[str] = set()
    portal_references: dict[str, set[tuple[str, str]]] = {}
    for summary in tile_summaries:
        file_name = summary.get("fileName")
        tile = tiles.get(file_name)
        _require(isinstance(tile, dict), "MobilityGraphTileFileMissing")
        tile_stable_id = summary.get("tileStableId")
        _require(tile_stable_id not in seen_tile_ids, "MobilityGraphDuplicateTileId")
        seen_tile_ids.add(tile_stable_id)
        _require(tile.get("schemaVersion") == TILE_SCHEMA_VERSION, "MobilityGraphTileSchemaInvalid")
        _require(tile.get("tileStableId") == tile_stable_id, "MobilityGraphTileIdentityMismatch")
        _require(tile.get("graphStableId") == GRAPH_STABLE_ID, "MobilityGraphTileGraphMismatch")
        _require(tile.get("regionStableId") == REGION_STABLE_ID, "MobilityGraphTileRegionMismatch")
        _require(tile.get("coverageCode") == COVERAGE_CODE, "MobilityGraphTileCoverageInvalid")
        _require(tile.get("administrativeBoundaryClipped") is False, "MobilityGraphTileAdministrativeClipClaimInvalid")
        _require(tile.get("distributionApproved") is False, "MobilityGraphTileDistributionMustRemainDisabled")
        _require(tile.get("traversalReady") is False, "MobilityGraphTileTraversalMustRemainDisabled")
        _require(tile.get("runtimeAuthorized") is False, "MobilityGraphTileRuntimeMustRemainDisabled")
        _require(tile.get("contentHashSha256") == content_hash(tile), "MobilityGraphTileHashMismatch")
        tile_bytes = canonical_json_bytes(tile)
        _require(summary.get("contentHashSha256") == tile["contentHashSha256"], "MobilityGraphTileSummaryHashMismatch")
        _require(summary.get("fileHashSha256") == sha256_bytes(tile_bytes), "MobilityGraphTileFileHashMismatch")
        nodes = tile.get("nodes")
        edges = tile.get("edges")
        _require(isinstance(nodes, list) and isinstance(edges, list), "MobilityGraphTileShapeInvalid")
        _require(nodes == sorted(nodes, key=lambda item: item["nodeStableId"]), "MobilityGraphNodesNotSorted")
        _require(edges == sorted(edges, key=lambda item: item["edgeStableId"]), "MobilityGraphEdgesNotSorted")
        node_lookup = {node.get("nodeStableId"): node for node in nodes}
        _require(len(node_lookup) == len(nodes) and None not in node_lookup, "MobilityGraphNodeIdentityInvalid")
        index_x = summary.get("tileIndexX")
        index_z = summary.get("tileIndexZ")
        _require(index_x in {0, 1} and index_z in {0, 1}, "MobilityGraphTileIndexInvalid")
        _require(tile.get("tileIndexX") == index_x and tile.get("tileIndexZ") == index_z, "MobilityGraphTileIndexMismatch")
        bounds = tile.get("bounds")
        expected_bounds = list(_tile_bounds(index_x, index_z))
        _require(bounds == expected_bounds, "MobilityGraphTileBoundsInvalid")
        _require(summary.get("bounds") == expected_bounds, "MobilityGraphTileSummaryBoundsInvalid")
        for node in nodes:
            position = node.get("position", {})
            _require(
                bounds[0] <= position.get("x", math.inf) <= bounds[2]
                and bounds[1] <= position.get("z", math.inf) <= bounds[3],
                "MobilityGraphNodeOutsideTile",
            )
            _require(node.get("runtimeAuthorized") is False, "MobilityGraphNodeRuntimeMustRemainDisabled")
            if node.get("isPortal"):
                stitch_id = node.get("stitchStableId")
                _require(isinstance(stitch_id, str) and stitch_id, "MobilityGraphPortalStitchMissing")
                portal_references.setdefault(stitch_id, set()).add((tile_stable_id, node["nodeStableId"]))
            else:
                _require(node.get("stitchStableId") == "", "MobilityGraphNonPortalStitchInvalid")
        seen_edges: set[str] = set()
        for edge in edges:
            edge_id = edge.get("edgeStableId")
            _require(isinstance(edge_id, str) and edge_id not in seen_edges, "MobilityGraphEdgeIdentityInvalid")
            seen_edges.add(edge_id)
            _require(edge.get("edgeKindCode") == "SourceRoadSegment", "MobilityGraphInferredConnectorForbidden")
            _require(edge.get("runtimeAuthorized") is False, "MobilityGraphEdgeRuntimeMustRemainDisabled")
            _require(edge.get("accessReviewCode") != "Reviewed", "MobilityGraphReviewedAccessForbidden")
            _require(edge.get("fromNodeStableId") in node_lookup, "MobilityGraphEdgeFromMissing")
            _require(edge.get("toNodeStableId") in node_lookup, "MobilityGraphEdgeToMissing")
            geometry = edge.get("geometry")
            _require(isinstance(geometry, list) and len(geometry) == 2, "MobilityGraphEdgeGeometryInvalid")
            _require(geometry[0] == node_lookup[edge["fromNodeStableId"]]["position"], "MobilityGraphEdgeFromMismatch")
            _require(geometry[-1] == node_lookup[edge["toNodeStableId"]]["position"], "MobilityGraphEdgeToMismatch")
            _require(edge.get("lengthMeters", 0) > 0, "MobilityGraphZeroLengthEdge")
            _require(
                edge.get("directionCode") in {"Unknown", "Forward", "Reverse", "Both"},
                "MobilityGraphDirectionInvalid",
            )
            _require(
                set(edge.get("candidateModeCodes", [])).issubset({"Vehicle", "Pedestrian", "Motorcycle"}),
                "MobilityGraphModeInvalid",
            )

    manifest_stitches = manifest.get("stitches")
    _require(isinstance(manifest_stitches, list), "MobilityGraphStitchesInvalid")
    expected_stitches = {
        stitch["stitchStableId"]: {
            (portal["tileStableId"], portal["nodeStableId"]) for portal in stitch.get("portals", [])
        }
        for stitch in manifest_stitches
    }
    _require(expected_stitches == portal_references, "MobilityGraphStitchReferenceMismatch")
    for candidate_group in manifest.get("verticalSliceCandidates", []):
        _require(candidate_group.get("runtimeAuthorized") is False, "MobilityGraphCandidateRuntimeMustRemainDisabled")
        for candidate in candidate_group.get("connectorCandidates", []):
            _require(candidate.get("reviewStatusCode") == "PendingHumanReview", "MobilityGraphConnectorReviewInvalid")
            _require(candidate.get("generatedAsGraphEdge") is False, "MobilityGraphConnectorEdgeForbidden")
            _require(candidate.get("runtimeAuthorized") is False, "MobilityGraphConnectorRuntimeMustRemainDisabled")


def write_package(package: dict[str, Any], output_directory: Path) -> dict[str, Any]:
    validate_package_structure(package)
    output_directory = Path(output_directory)
    (output_directory / "tiles").mkdir(parents=True, exist_ok=True)
    for file_name, tile in package["tiles"].items():
        target = output_directory / file_name
        temporary = target.with_suffix(target.suffix + ".tmp")
        temporary.write_bytes(canonical_json_bytes(tile))
        os.replace(temporary, target)
    manifest_target = output_directory / "manifest.json"
    temporary_manifest = manifest_target.with_suffix(".json.tmp")
    temporary_manifest.write_bytes(canonical_json_bytes(package["manifest"]))
    os.replace(temporary_manifest, manifest_target)
    return {
        "status": "GeneratedPendingHumanReview",
        "manifestPath": str(manifest_target),
        "projectionHashSha256": package["manifest"]["projectionHashSha256"],
        "tileCount": len(package["manifest"]["tiles"]),
        "nodeCount": package["manifest"]["quality"]["nodeCount"],
        "edgeCount": package["manifest"]["quality"]["edgeCount"],
        "distributionApproved": False,
        "traversalReady": False,
        "runtimeAuthorized": False,
    }


def load_package(package_directory: Path) -> dict[str, Any]:
    package_directory = Path(package_directory)
    manifest_path = package_directory / "manifest.json"
    if not manifest_path.is_file():
        raise MobilityGraphAuditError("MobilityGraphManifestFileMissing")
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise MobilityGraphAuditError("MobilityGraphManifestJsonInvalid") from error
    tiles: dict[str, Any] = {}
    for summary in manifest.get("tiles", []):
        file_name = summary.get("fileName", "")
        path = package_directory / file_name
        if not path.is_file():
            raise MobilityGraphAuditError("MobilityGraphTileFileMissing")
        raw_bytes = path.read_bytes()
        if sha256_bytes(raw_bytes) != summary.get("fileHashSha256"):
            raise MobilityGraphAuditError("MobilityGraphTileFileHashMismatch")
        try:
            tiles[file_name] = json.loads(raw_bytes.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise MobilityGraphAuditError("MobilityGraphTileJsonInvalid") from error
    package = {"manifest": manifest, "tiles": tiles}
    validate_package_structure(package)
    return package


def audit_package(
    source_path: Path,
    package_directory: Path,
    expected_source_sha256: str = EXPECTED_SOURCE_SHA256,
) -> dict[str, Any]:
    actual = load_package(package_directory)
    expected = build_package(source_path, expected_source_sha256)
    if canonical_json_bytes(actual["manifest"]) != canonical_json_bytes(expected["manifest"]):
        raise MobilityGraphAuditError("MobilityGraphManifestSourceRebuildMismatch")
    if set(actual["tiles"]) != set(expected["tiles"]):
        raise MobilityGraphAuditError("MobilityGraphTileSetSourceRebuildMismatch")
    for file_name in sorted(expected["tiles"]):
        if canonical_json_bytes(actual["tiles"][file_name]) != canonical_json_bytes(expected["tiles"][file_name]):
            raise MobilityGraphAuditError("MobilityGraphTileSourceRebuildMismatch:" + file_name)
    manifest = actual["manifest"]
    return {
        "status": "PassedPendingHumanReview",
        "projectionHashSha256": manifest["projectionHashSha256"],
        "tileCount": len(manifest["tiles"]),
        "nodeCount": manifest["quality"]["nodeCount"],
        "edgeCount": manifest["quality"]["edgeCount"],
        "distributionApproved": False,
        "traversalReady": False,
        "runtimeAuthorized": False,
    }

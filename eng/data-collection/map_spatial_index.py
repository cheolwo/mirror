"""Build a local, read-only preview index from a frozen map public-data snapshot."""
from __future__ import annotations

import argparse
from collections import defaultdict
from contextlib import closing
from hashlib import file_digest
import json
import math
from pathlib import Path
import sqlite3
import sys
from uuid import uuid4

from map_public_api import open_database
from map_public_api_transport import canonical


SCHEMA = Path(__file__).with_suffix(".sql")
LAYERS = {
    "shops": ("상가", "행정동", "업종과 점포의 관측 좌표, 확인한 건물 후보"),
    "market_areas": ("상권", "시군구", "상권 관측 도형, 관할 구"),
    "building_titles": ("건축물 표제부", "법정동", "표제부의 지역과 높이·층수, 외곽 도형은 별도"),
    "parcels_bbox": ("연속지적도", "행정동", "검증된 필지 도형"),
    "bus_cities": ("버스 도시 코드", "참조", "TAGO의 도시 코드"),
    "bus_stops": ("버스 정류소", "행정동", "취득한 정류소의 위치"),
    "bus_routes_at_stop": ("버스 경유 관계", "행정동", "경유 정류소를 위치로 참조, 노선 이동선은 별도"),
    "traffic_sites": ("교통량 측정 지점", "참조", "좌표계 확인이 필요한 지점 기록"),
    "traffic_vehicle_month": ("차종별 교통량", "참조", "지점·일자·방향별 관측"),
    "traffic_hour_month": ("시간대별 교통량", "참조", "지점·일자·방향별 관측"),
    "large_retail": ("대규모점포", "시군구", "인허가의 확인된 위치와 영업 상태"),
    "livestock_farms": ("가축사육업", "시군구", "공개 기관·업종·영업 상태 참조, 개별 위치 없음"),
    "slaughterhouses": ("도축업", "시군구", "인허가의 확인된 위치와 영업 상태"),
    "trade_sido": ("시도별 품목 무역", "시도", "관세청 시도 요청 코드, 품목·기간·수출입"),
    "trade_sigungu": ("시군구별 품목 무역", "시군구", "관세청 보고 지역·HS6·기간"),
    "port_vessels_month": ("항만 선박 입출항", "전국", "항만 보고 지역·선박 구분·월, 위치는 대표점"),
    "port_containers_month": ("컨테이너 실적", "전국", "해외 보고 지역·월, 국내 항만 핀으로 쓰지 않음"),
    "airport_transport": ("공항 수송실적", "전국", "공항·월별 관측, 확인된 공항 대표점"),
}
LAYER_ALIAS = {"shops_by_region": "shops", "shops_by_date": "shops",
               "bus_stops_near": "bus_stops", "bus_stops_city": "bus_stops"}
DIMENSION_FIELDS = {
    "shops": ("indsLclsCd", "indsLclsNm", "indsSclsCd", "indsSclsNm", "flrNo", "stdrYm"),
    "market_areas": ("trarNo", "mainTrarNm", "stdrDt"),
    "building_titles": ("mainPurpsCdNm", "roofCdNm", "useAprDay"),
    "parcels_bbox": ("stdr_year", "stdr_month"),
    "bus_cities": ("citycode", "cityname"),
    "bus_stops": ("nodeid", "nodenm", "citycode"),
    "bus_routes_at_stop": ("routeid", "routeno", "routetp"),
    "traffic_sites": ("spot", "id5"),
    "traffic_vehicle_month": ("spot_id", "direction", "day"),
    "traffic_hour_month": ("spot_id", "direction", "day"),
    "large_retail": ("STOR_SE_NM", "SALS_STTS_CD", "OPN_ATMY_GRP_CD"),
    "livestock_farms": ("PRMN_LSFBIZ_SPECS", "SALS_STTS_CD", "OPN_ATMY_GRP_CD"),
    "slaughterhouses": ("SALS_STTS_CD", "OPN_ATMY_GRP_CD"),
    "trade_sido": ("hsSgn", "korePrlstNm", "priodTitle"),
    "trade_sigungu": ("hsSgn", "korePrlstNm", "priodTitle", "sggNm"),
    "port_vessels_month": ("prtAgNm", "nm", "useYm"),
    "port_containers_month": ("areaCd", "areaNm", "useYm"),
    "airport_transport": ("Airport",),
}


def digest(path):
    with path.open("rb") as source:
        return file_digest(source, "sha256").hexdigest()


def jsonl(path):
    if path:
        with path.open(encoding="utf-8") as source:
            for line in source:
                if line.strip():
                    yield json.loads(line)


def layer_id(operation):
    return LAYER_ALIAS.get(operation, operation)


def point(lon, lat):
    if (isinstance(lon, (float, int)) and isinstance(lat, (float, int))
            and math.isfinite(lon) and math.isfinite(lat) and 124 <= lon <= 132 and 32 <= lat <= 40):
        return {"type": "Point", "coordinates": [lon, lat]}
    return None


def geometry_bounds(geometry):
    kind, coordinates = geometry["type"], geometry["coordinates"]
    if kind == "Point":
        vertices = [coordinates]
    elif kind in {"Polygon", "MultiPolygon"}:
        polygons = [coordinates] if kind == "Polygon" else coordinates
        vertices = []
        if not polygons:
            raise ValueError("empty_polygon")
        for polygon in polygons:
            if not polygon:
                raise ValueError("empty_polygon")
            for ring in polygon:
                if len(ring) < 4 or ring[0] != ring[-1]:
                    raise ValueError("open_polygon")
                vertices.extend(ring)
    else:
        raise ValueError("unsupported_geometry")
    if not all(len(pos) == 2 and point(*pos) is not None for pos in vertices):
        raise ValueError("coordinate_outside_korean_extent")
    longitudes, latitudes = zip(*vertices)
    return min(longitudes), max(longitudes), min(latitudes), max(latitudes)


def geometry_tools(path):
    if path is None:
        return None, None
    sys.path.insert(0, str(path.resolve()))
    try:
        from pyproj import Transformer
        import shapely
        return Transformer.from_crs("EPSG:5174", "EPSG:4326", always_xy=True), shapely
    finally:
        sys.path.pop(0)


def collection_parameters(request):
    parsed = json.loads(request)
    return parsed.get("params", parsed)


def scope_dimensions(layer, data, request):
    result = {key: data[key] for key in DIMENSION_FIELDS[layer] if key in data}
    parameters = collection_parameters(request)
    if layer in {"trade_sido", "trade_sigungu"}:
        result.update({"providerSidoCode": parameters.get("sidoCd"),
                       "periodFrom": parameters.get("strtYymm"), "periodTo": parameters.get("endYymm")})
    if layer.startswith("traffic_"):
        result.update({"surveyYear": parameters.get("year"), "roadType": parameters.get("dtype"),
                       "surveyMonth": parameters.get("month")})
    if layer == "bus_routes_at_stop":
        result["stopId"] = parameters.get("nodeid")
    return result


def region_rows(source, destination, boundaries):
    rows = source.execute("SELECT kind,code,name,parent_kind,parent_code,as_of FROM current_regions").fetchall()
    registry = {(kind, code): (parent_kind, parent_code) for kind, code, _, parent_kind, parent_code, _ in rows}
    names = defaultdict(list)
    for kind, code, name, _, _, _ in rows:
        if kind == "sigungu":
            names[(code[:2], name)].append(code)
    destination.executemany("INSERT INTO region VALUES (?,?,?,?,?,?)", rows)
    for row in jsonl(boundaries):
        if row["kind"] != "administrative":
            raise ValueError("unexpected_boundary_kind")
        geometry = row["geometry"]
        bounds = geometry_bounds(geometry)
        destination.execute("INSERT INTO region_boundary VALUES (?,?,?,?,?,?,?)",
                            (row["code"], canonical(geometry), *bounds, int(("administrative", row["code"]) in registry)))
    return registry, names


def add_scope_ancestors(areas, registry):
    for key in list(areas):
        seen = {key}
        parent = registry[key]
        while parent in registry:
            if parent in seen:
                raise ValueError("region_registry_cycle")
            seen.add(parent)
            areas.setdefault(parent, "registry_ancestor")
            parent = registry[parent]


def collected_regions(source, registry):
    references = defaultdict(dict)
    for record, kind, code, proof in source.execute(
            """SELECT c.record_id,h.kind,h.code,h.evidence_field FROM record_current c
               JOIN record_region_hint h ON h.revision_id=c.revision_id"""):
        if (kind, code) in registry:
            references[record][(kind, code)] = proof
    return references


def verified_reference_points(source, path):
    crosswalk = {}
    for row in jsonl(path):
        key = (row["scheme"], row["sourceIdentifier"])
        if key in crosswalk or point(row["longitude"], row["latitude"]) is None:
            raise ValueError("reference_point_invalid_or_duplicate")
        crosswalk[key] = row
    positions = {}
    for record, scheme, identifier in source.execute(
            """SELECT c.record_id,m.scheme,m.source_identifier
               FROM record_current c JOIN record_subject s ON s.revision_id=c.revision_id
               JOIN map_subject m USING(subject_id)
               WHERE m.scheme IN ('kac-airport','mof-port-reporting-area')"""):
        match = crosswalk.get((scheme, identifier))
        if match:
            if record in positions and positions[record] != match:
                raise ValueError("reference_point_ambiguous")
            positions[record] = match
    return positions


def entry_positions(source, destination, areas, location_map, transformer, registry, region_names):
    stop_positions = {}
    for record, text, lon, lat in source.execute(
            """SELECT c.record_id,v.data_json,f.longitude,f.latitude FROM record_current c
               JOIN map_record m USING(record_id) JOIN record_revision v ON v.revision_id=c.revision_id
               JOIN spatial_feature f ON f.revision_id=c.revision_id WHERE m.kind='bus_stop'"""):
        node = json.loads(text).get("nodeid")
        if node:
            stop_positions[node] = (record, point(lon, lat))
    for row in source.execute(
            """SELECT c.record_id,c.revision_id,m.portal_id,q.run_id,q.operation_id,q.finished_at,
                      r.source_updated_at,r.data_json,q.request_json,
                      f.longitude,f.latitude,f.geometry_json,f.source_crs,f.source_x,f.source_y
               FROM record_current c JOIN map_record m USING(record_id)
               JOIN record_revision r ON r.revision_id=c.revision_id JOIN collection_run q ON q.run_id=c.run_id
               LEFT JOIN spatial_feature f ON f.revision_id=c.revision_id
               WHERE COALESCE(json_extract(r.data_json,'$.chgGb'),'')<>'D' ORDER BY c.record_id"""):
        record, revision, portal, run, op, collected, updated, text, request, lon, lat, shape, crs, x, y = row
        layer = layer_id(op)
        data = json.loads(text)
        dimensions = scope_dimensions(layer, data, request)
        bound_areas = areas.pop(record, {})
        add_scope_ancestors(bound_areas, registry)
        scope_only = layer in {"trade_sido", "trade_sigungu", "port_containers_month", "livestock_farms", "bus_cities"}
        position = "region_only" if bound_areas else "provider_scope" if scope_only else "unlocated"
        if layer in {"trade_sido", "trade_sigungu"}:
            province = dimensions.get("providerSidoCode")
            if ("sido", province) in registry:
                bound_areas[("sido", province)] = "customs_requested_sido_code"
                if layer == "trade_sigungu":
                    matches = region_names.get((province, dimensions.get("sggNm")), [])
                    if len(matches) == 1:
                        bound_areas[("sigungu", matches[0])] = "customs_county_name_unique_within_requested_sido"
        geometry = None
        reference_id = None
        proof = None
        if shape is not None:
            geometry, position, proof = json.loads(shape), "api_area", "api_wgs84_area"
        elif (geometry := point(lon, lat)) is not None:
            position, proof = "api_point", "api_wgs84_point"
        elif crs == "EPSG:5174" and transformer is not None and x is not None and y is not None:
            geometry = point(*transformer.transform(x, y))
            if geometry:
                position, proof = "projected_point", "epsg_5174_to_4326"
        if geometry is None and record in location_map:
            match = location_map[record]
            geometry = point(match["longitude"], match["latitude"])
            position, proof, reference_id = "reference_point", match["evidence"], match["referenceId"]
        if geometry is None and layer == "bus_routes_at_stop" and dimensions.get("stopId") in stop_positions:
            stop, geometry = stop_positions[dimensions["stopId"]]
            if geometry:
                position, proof, reference_id = "reference_point", "tago_stop_id_match", f"record:{stop}"
        destination.execute("INSERT INTO map_entry VALUES (?,?,?,?,?,?,?,?,?,?)",
                            (record, layer, revision, op, portal, run, collected, updated, canonical(dimensions), position))
        if geometry is not None:
            bounds = geometry_bounds(geometry)
            destination.execute("INSERT INTO entry_geometry VALUES (?,?,?,?)",
                                (record, canonical(geometry), proof, reference_id))
            destination.execute("INSERT INTO entry_bounds VALUES (?,?,?,?,?)", (record, *bounds))
        destination.executemany("INSERT INTO entry_region VALUES (?,?,?,?)",
                                ((record, kind, code, reason) for (kind, code), reason in bound_areas.items()))


def locate_seoul_points(destination, shapely, registry):
    if shapely is None:
        return
    codes = []
    polygons = []
    for code, geometry in destination.execute("SELECT region_code,geometry_json FROM region_boundary WHERE registry_match=1"):
        codes.append(code)
        polygons.append(shapely.from_geojson(geometry))
    if not codes:
        return
    tree = shapely.STRtree(polygons)
    missing = destination.execute(
        """SELECT e.record_id,g.geometry_json FROM map_entry e JOIN entry_geometry g USING(record_id)
           JOIN entry_bounds b ON b.record_id=e.record_id
           WHERE e.placement IN ('api_point','projected_point') AND b.east>=126.7 AND b.west<=127.2
           AND b.north>=37.4 AND b.south<=37.75 AND NOT EXISTS (
             SELECT 1 FROM entry_region r WHERE r.record_id=e.record_id AND r.region_kind='administrative')""")
    for record, geometry in missing.fetchall():
        matches = tree.query(shapely.from_geojson(geometry), predicate="within")
        if len(matches) != 1:
            continue
        areas = {("administrative", codes[int(matches[0])]): "point_inside_single_official_seoul_boundary"}
        add_scope_ancestors(areas, registry)
        destination.executemany("INSERT INTO entry_region VALUES (?,?,?,?) ON CONFLICT DO NOTHING",
                                ((record, kind, code, reason) for (kind, code), reason in areas.items()))


def transfer_tables(source, destination, shop_pairs):
    for row in jsonl(shop_pairs):
        record, code = row["shopRecordId"], row["administrativeCode"]
        shop = destination.execute(
            """SELECT 1 FROM map_entry e JOIN entry_region r USING(record_id)
               WHERE e.record_id=? AND e.layer_id='shops' AND r.region_kind='administrative' AND r.region_code=?""",
            (record, code)).fetchone()
        if shop is None:
            raise ValueError("shop_pair_scope_changed")
        destination.execute("INSERT INTO shop_building_candidate VALUES (?,?,?,?)",
                            (record, code, row["buildingId"], row["evidenceKind"]))
    metrics = source.execute(
        """SELECT c.record_id,o.measure,o.value_decimal,o.unit,o.period_start,o.period_end
           FROM record_current c JOIN record_revision v ON v.revision_id=c.revision_id
           JOIN map_observation o ON o.revision_id=c.revision_id
           WHERE COALESCE(json_extract(v.data_json,'$.chgGb'),'')<>'D'""")
    destination.executemany("INSERT INTO map_metric VALUES (?,?,?,?,?,?)", metrics)
    allowed_subjects = source.execute(
        """SELECT c.record_id,m.scheme,m.source_identifier,s.role
           FROM record_current c JOIN record_revision v ON v.revision_id=c.revision_id
           JOIN record_subject s ON s.revision_id=c.revision_id JOIN map_subject m USING(subject_id)
           WHERE COALESCE(json_extract(v.data_json,'$.chgGb'),'')<>'D'
           AND m.scheme IN ('kict-traffic-site','tago-bus-stop','tago-bus-route','hs6','hs-chapter',
                            'customs-sido','kac-airport','mof-port-reporting-area','mof-container-area',
                            'localdata-agency')""")
    destination.executemany("INSERT INTO entry_subject VALUES (?,?,?,?)", allowed_subjects)
    scopes = source.execute(
        """SELECT run_id,operation_id,state,detail,request_json,started_at,finished_at,row_count
           FROM collection_run ORDER BY run_id""")
    destination.executemany("INSERT INTO collection_scope VALUES (?,?,?,?,?,?,?,?,?)",
                            ((row[0], layer_id(row[1]), *row[1:]) for row in scopes))


def inventory(database):
    totals = {table: database.execute(f"SELECT count(*) FROM {table}").fetchone()[0]
              for table in ("region", "region_boundary", "map_entry", "entry_region", "entry_geometry",
                            "entry_subject", "map_metric", "shop_building_candidate", "collection_scope")}
    rows = database.execute(
        "SELECT layer_id,label,map_scale,placement,indexed_records FROM layer_inventory ORDER BY layer_id,placement"
    ).fetchall()
    return {"totals": totals, "layers": [dict(zip(("id", "label", "scale", "placement", "records"), row))
                                        for row in rows]}


def build(args):
    target = args.output
    if target.exists():
        raise ValueError("existing_index")
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(f".{target.name}.{uuid4().hex}.tmp")
    paths = {"api_snapshot": args.db, "shop_building_candidates": args.shop_pairs,
             "administrative_boundary_extract": args.boundaries, "transport_reference_links": args.references,
             "administrative_boundary_receipt": args.boundary_receipt, "transport_reference_receipt": args.reference_receipt}
    files = [(role, path.name, digest(path)) for role, path in paths.items() if path]
    transformer, shapely = geometry_tools(args.proj_library)
    try:
        with closing(open_database(args.db, read_only=True)) as source, closing(sqlite3.connect(temporary)) as index:
            source.execute("BEGIN")
            if source.execute("SELECT 1 FROM collection_run WHERE state='running'").fetchone():
                raise ValueError("source_collection_running")
            index.execute("PRAGMA foreign_keys=ON")
            index.executescript(SCHEMA.read_text(encoding="utf-8"))
            with index:
                index.executemany("INSERT INTO source_file VALUES (?,?,?)", files)
                index.executemany("INSERT INTO map_layer VALUES (?,?,?,?)",
                                  ((code, *layer) for code, layer in LAYERS.items()))
                registry, region_names = region_rows(source, index, args.boundaries)
                areas = collected_regions(source, registry)
                points = verified_reference_points(source, args.references)
                entry_positions(source, index, areas, points, transformer, registry, region_names)
                locate_seoul_points(index, shapely, registry)
                transfer_tables(source, index, args.shop_pairs)
            if index.execute("PRAGMA quick_check").fetchall() != [("ok",)]:
                raise ValueError("index_integrity_failed")
            if index.execute("PRAGMA foreign_key_check").fetchone():
                raise ValueError("index_foreign_key_failed")
            if index.execute("SELECT count(*) FROM entry_geometry").fetchone() != index.execute(
                    "SELECT count(*) FROM entry_bounds").fetchone():
                raise ValueError("spatial_index_count_mismatch")
            result = inventory(index)
        for role, _, checksum in files:
            if digest(paths[role]) != checksum:
                raise ValueError("snapshot_changed_during_build")
        temporary.rename(target)
    finally:
        if temporary.exists():
            temporary.unlink()
    return {**result, "indexSha256": digest(target), "sourceFiles": files}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ("db", "output", "shop-pairs"):
        parser.add_argument(f"--{option}", type=Path, required=True)
    for option in ("boundaries", "references", "boundary-receipt", "reference-receipt", "proj-library"):
        parser.add_argument(f"--{option}", type=Path)
    print(canonical(build(parser.parse_args())))


if __name__ == "__main__":
    main()

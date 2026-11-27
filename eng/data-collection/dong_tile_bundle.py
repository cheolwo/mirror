"""Read and validate the separate local evidence layers with one tile key."""

import argparse
from contextlib import closing
import json
import sqlite3

from collect_public_snapshots import atomic_json
from dong_tile_geometry import REVISION, vertices
from dong_tile_index import DBNAME, OUTPUT, PARENT
from seoul_area_geography import check_database, read_only
from seoul_tile_population import PUBLIC, ROOT, digest


# The rows retain their native periods. These definitions are for the evidence reader.
LAYERS = {
    "foodLicenses": ("", "v_food_child", "인허가 건", "위치가 확인된 영업 인허가를 업종별로 합산. 매출과 주문은 별도."),
    "transitPoints": ("", "transport_child_position", "시설 또는 역·호선", "버스는 정류소, 철도는 역·호선 대표점. 출입구나 고객 위치와 구별."),
    "transitBoardings": ("@parent", "transport_tag", "승차·하차 건", "기간과 교통수단별 승·하차를 각각 비교. 월 누계와 일 집계, 생활인구를 더하지 않음."),
    "residentPopulation": ("@parent", "resident_month", "명·세대", "행정동과 기준월로 조회. 자식 타일마다 해당 동 총계를 복사하지 않음."),
    "presence": ("child-presence.sqlite", "child_presence_profile", "명 추정", "250m 원자료에서 수치가 보인 부분만 기존 50m 표본 규칙으로 배분. 요일과 시간대별 결측 수와 표본일 함께 표시."),
    "buildings": ("buildings/building-tile-index.sqlite", "v_building_child", "동·m·층·m²", "원본 대표점 기준으로 합산. 평균 높이와 층수는 값이 존재하는 동수로 나눔. 원본 면적의 합은 타일 내 도형 면적과 구별."),
    "terrain": ("terrain/terrain-tile-index.sqlite", "v_terrain_child", "m·도", "NASADEM 원 격자점과 인접 네 점의 경사. 평균의 분모는 유효 표본 수. 도로 경사도는 별도."),
    "spotElevation": ("terrain/terrain-tile-index.sqlite", "v_spot_child", "m", "서울 수치지도 표고점. NASADEM과 수직 기준이 확인될 때까지 별도 조회."),
    "landCover": ("ecology/dong-tile-ecology.sqlite", "v_tile_cover_reference", "%·m²", "관측이 알려진 면적에 대해 가중 비율 계산. 겹침·무관측 제외. 비율 자체를 합산하지 않음."),
    "shadeShelters": ("ecology/dong-tile-ecology.sqlite", "v_shade_tile_reference", "점", "공공데이터 표준의 실제 시설 위치 후보. 제공 구역 밖은 미관측."),
    "parksAndTrees": ("ecology/ecology-existing-point-references.sqlite", "v_tile_reference_points", "점", "공원 대표점과 용산 가로수. 공원 등록 면적을 해당 칸 점유 면적으로 해석하지 않음."),
    "roadCoverage": ("transport/tile-transport.sqlite", "v_road_tile_coverage", "방향별 m", "방향별 도로 길이와 속도자료 결합 길이 구분. 포함·누락 범위 확인에 사용."),
    "roadSpeed": ("transport/tile-transport.sqlite", "v_road_tile_speed_profile", "km/h", "실제 대조한 TOPIS 링크의 길이 가중 일평균. 시간대별 날짜·유효일·실측 길이 포함."),
    "crossings": ("transport/tile-transport.sqlite", "crossing_point", "점", "동일 관리번호와 거리 5m 이내로 대조된 좌표만 포함. 교통신호·통과 가능 여부는 별도."),
    "walkingNetwork": ("transport/tile-transport.sqlite", "walk_tile", "m", "2020 서울 실측 보행망이 포함된 길이. 현재 통행 가능 여부 근거와 구별."),
    "trafficAtJunction": ("transport/tile-transport.sqlite", "smart_count", "5분·방향별 대수", "공식 교차로 ID를 기준으로 저장. 같은 출처의 위치 관계가 확인되기 전까지 타일에는 표시하지 않음."),
    "walkingSensors": ("sensors/sensor-tile-reference.sqlite", "v_sensor_tile_reference", "센서·관측 행", "2025 설치 좌표와 2026 방문자 센서를 연결한 후보. 인구·유동인구·방문 누계로 합치지 않음."),
    "bike": ("bike/bike-child-index.sqlite", "v_bike_tile_month", "대여·반납 건", "위치와 명칭이 일치한 대여소 월별 합산. 먼저 같은 기간의 대여와 반납을 각각 비교."),
    "stationAccess": ("stations/station-access.sqlite", "v_station_access_reference", "안내 단계", "운영기관·호선·이름·역코드 대조 후 역 대표점 타일에 환승 설명 연결. 출구 좌표와는 별도."),
    "streetLighting": ("street-lighting/street-lighting-tile-index.sqlite", "v_light_child", "출처별 위치", "같은 원본에서 같은 좌표는 한 위치로 묶고 원본 행 수를 유지. 출처가 다른 위치 수는 합산하지 않음."),
    "seoulFacilities": ("facilities/tile-facilities.sqlite", "v_tile_facility_count", "시설 참고행", "그늘막·쉼터·화장실·음수대·택배함·승강기·주차 등 출처별 관측. 게시된 운영 설비·시간을 현재 작동 여부로 해석하지 않음."),
    "buildingFootprint": ("buildings/coverage/building-polygon-coverage.sqlite", "v_building_polygon_child", "m²·%", "공식 서울 범위로 잘라 원본 건물 외곽의 합집합 계산. 대표점 건물 수와 비오톱 조사 건폐율은 별도."),
    "streetSurface": ("transport/surface-coverage/street-surface-coverage.sqlite", "tile_area", "m²", "보도경계면과 실폭도로의 면 합집합·평면 교차. 높은 연석이나 실제 통행 허용 구역 판정에 쓰지 않음."),
    "bus20261121": ("transport/bus-latest/bus-tile-daily.sqlite", "v_tile_bus_day_hour", "승차·하차 건", "교통카드의 날짜·시간대 집계. 미확인 정류소는 제외. 앞선 동일 11월 20일 원행과 중복 합산 금지."),
    "waterSurface": ("ecology/water/seoul-water-authority-reference.sqlite", "v_tile_water_reference", "m²", "서울시 수면 분류와 국가 하천경계면 각각의 합집합. 하천 관리 범위를 물이 찬 면적으로 쓰지 않음."),
    "evSupport": ("transport/mobility-support/mobility-support.sqlite", "v_tile_ev_support", "시설·등록 기기", "위치 검증된 충전 장소와 기기 건수를 분리. 실제 운영, 이륜차 이용과 충전 가능 대수는 미확인."),
    "landmarks": ("landmarks/landmark-tile-index.sqlite", "landmark_tile", "공식 게시 장소", "기관의 장소 ID와 대표점을 연결. 여러 장소 기록이 같은 좌표를 쓸 수 있으며 개별 구조물의 위치는 별도."),
}

SOURCE_DETAILS = {
    "foodLicenses": ("licensed_food_position", "인허가 원본 위치", "좌표 확인 158,119건", "core-summary.json"),
    "transitPoints": ("rail_and_bus_position", "설치 위치·역/호선 대표점", "서울 11,111점", "core-summary.json"),
    "transitBoardings": ("transport_tag", "시설·월 또는 일·시각", "철도 2026-10, 버스 2026-10 및 2026-11-19·20", "source_release"),
    "residentPopulation": ("resident_month", "행정동·기준월", "서울 427동", "source_release"),
    "presence": ("OA-22784", "250m 격자·시간", "2026-11-01~21, 점심·저녁, 평일 15일·주말 6일", "presence-summary.json"),
    "buildings": ("GIS_building_and_building_register", "건물 원본 단위", "서울 427동. 모듈 원본의 판본을 건물 요약에서 확인", "buildings/building-summary.json"),
    "terrain": ("NASADEM_HGT", "1초 원래 격자", "레이더 모델 2000, EGM96. 서울 55,245칸에 표본 있음", "terrain/terrain-summary.json"),
    "spotElevation": ("OA-22241", "수치지도 표고점", "2023 기준. 서울 30,665칸에 점 있음", "terrain/terrain-summary.json"),
    "landCover": ("OA-21126", "서울 비오톱 조사 도형", "서울 조사 면적. 원본 조사 연도와 조건은 녹지 원장 확인", "ecology/ecology-source-catalog.json"),
    "shadeShelters": ("15129447", "그늘막 시설 위치", "원본 제공 서울 5개 구만 연결", "ecology/national-shade-15129447.receipt.json"),
    "parksAndTrees": ("parks_and_yongsan_street_trees", "대표점·가로수 개체 위치", "서울 대표 공원점, 용산 가로수만 포함", "ecology/ecology-source-catalog.json"),
    "roadCoverage": ("TOPIS_link", "방향성 도로 중심선", "서울 TOPIS에서 실제 대조된 도로", "transport/verification.json"),
    "roadSpeed": ("TOPIS_speed", "방향성 링크·일·시각", "2026-10. 대조된 링크에서 관측 가능한 시간만 포함", "transport/verification.json"),
    "crossings": ("OA-23081", "횡단보도 위치", "2026 파일과 이전 기록이 일치하는 점만 포함", "transport/verification.json"),
    "walkingNetwork": ("seoul_walk_network", "보행 링크", "2020 서울", "transport/verification.json"),
    "trafficAtJunction": ("TOPIS_5min", "교차로·진입·방향·5분", "2026-11-22·24. 공식 위치 대조 보류", "transport/verification.json"),
    "walkingSensors": ("OA-15964", "센서·기록 시각", "설치표 2025-11-13, 측정 2026-11-01~15", "sensors/source-receipts.json"),
    "bike": ("OA-15249_OA-13252", "대여소·월", "2026-01~06. 2025-12 및 2026-06 위치 일치", "bike/source-receipt.json"),
    "stationAccess": ("15130556", "운영기관·역·호선·환승 순서", "2026-08-11, 서울교통공사 일부 역의 대표점에 연결", "stations/station-access-summary.json"),
    "streetLighting": ("OA-22205_OA-21696_15128093_15128039", "공표 위치·등록 기준일", "서울 광역 가로등, 2022 안심귀갓길, 2024 강동·서초 보안등", "street-lighting/street-lighting-summary.json"),
    "seoulFacilities": ("seoul_civic_facilities_and_school_zone", "시설 대표점·자료별 기준일", "서울 421개 검증 동에서 확인된 11개 종류", "facilities/facilities-summary.json"),
    "buildingFootprint": ("AL_D010", "원본 GIS 건물 외곽 다각형", "2026-11-09 서울 GIS, 2026-10 공식 경계", "buildings/coverage/building-polygon-coverage-summary.json"),
    "streetSurface": ("NGII_sidewalk_AND_TL_SPRD_RW", "보도면·실폭도로면", "보도 객체 2020 및 2026-10 실폭도로. 보류 원본 열 구분", "transport/surface-coverage/verification.json"),
    "bus20261121": ("T-DATA-40", "노선·정류장·운행일·시각", "2026-11-21 신규 주말 표본. 11월 20일은 동일 원문 대조에 사용", "transport/bus-latest/bus-daily-summary.json"),
    "waterSurface": ("OA-21126_NGII-30248", "수면 분류 다각형과 하천경계면", "서울 공식 범위로 자른 원본 두 종류. 출처별 면적", "ecology/water/verification.json"),
    "evSupport": ("OA-23033", "게시 충전 장소·기기", "2026-09-30 서울시 자료. 위치를 재확인한 시설 153곳", "transport/mobility-support/verification.json"),
    "landmarks": ("public_published_landmark", "장소의 게시 대표점", "중랑구 검증 후보 4곳, 타일 3칸", "landmarks/landmark-verification.json"),
}

ROAD_SAMPLES = """
WITH daily AS (
    SELECT s.observed_date,s.day_kind,CAST(h.key AS INTEGER)+1 AS hour_ending,
           SUM(h.value*g.clipped_m)/SUM(CASE WHEN h.value IS NOT NULL THEN g.clipped_m END) AS weighted_kmh,
           SUM(CASE WHEN h.value IS NOT NULL THEN g.clipped_m END) AS observed_m
    FROM road_tile_link g JOIN speed_day s USING(link_id) JOIN json_each(s.by_hour_kmh) h
    WHERE g.q=? AND g.r=? AND g.slot=? AND h.key IN (12,18)
    GROUP BY 1,2,3
)
SELECT day_kind,hour_ending,MIN(observed_date) AS sample_from,MAX(observed_date) AS sample_to,
       COUNT(*) AS sample_days,COUNT(weighted_kmh) AS usable_days,AVG(weighted_kmh) AS mean_daily_weighted_kmh,
       MIN(weighted_kmh) AS min_daily_weighted_kmh,MAX(weighted_kmh) AS max_daily_weighted_kmh,
       AVG(observed_m) AS mean_observed_directional_m
FROM daily GROUP BY 1,2 ORDER BY 1,2
"""
CASES = {
    "sagajeong": (-32, -179, 2), "hongdae": (-75, -189, 3),
    "yeouido": (-68, -204, 2), "yongmasan_slope": (-28, -183, 2),
}
OFFICIAL_REGIONS = PUBLIC / "national-admin-stat-map-20261119-r1/official-local/national/official-admin-statistics.sqlite"
PORTABILITY = OUTPUT / "portability"
NATIONAL_POINTS = PORTABILITY / "national-points/national-point-tiles.sqlite"
MODULE_READINESS = PORTABILITY / "module-readiness/module-readiness.sqlite"


def database_path(layer):
    relative = LAYERS[layer][0]
    if relative == "@parent":
        return PARENT
    return OUTPUT / (relative or DBNAME)


def query(path, sql, params=()):
    with closing(read_only(path)) as db:
        db.row_factory = sqlite3.Row
        return [dict(row) for row in db.execute(sql, params)]


def layer_query(layer, suffix, params=()):
    table = LAYERS[layer][1]
    return query(database_path(layer), f"SELECT * FROM {table} " + suffix, params)


def tile_lookup(q, r, slot=None):
    key = q, r
    if slot is not None:
        key += (slot,)
    condition = "q=? AND r=?" + (" AND slot=?" if slot is not None else "")
    table = "child_tile" if slot is not None else "tile"
    core = OUTPUT / DBNAME
    geo = query(core, f"SELECT * FROM {table} WHERE " + condition, key)
    if not geo:
        raise ValueError(f"unknown_seoul_tile:{key}")
    geo = geo[0]
    geo.pop("vertices_json", None)
    geo["vertices"] = vertices(q, r, slot)
    result = {
        "geometryRevision": REVISION, "crs": "EPSG:5179", "distribution": "local-review",
        "geometry": geo, "geoState": "seoul_overlap" if geo["seoul_area_m2"] else "outside_seoul",
    }
    if not geo["seoul_area_m2"]:
        return result
    selectors = ("m.q=? AND m.r=?" + (" AND m.slot=?" if slot is not None else ""))
    result["adminDongs"] = query(core, f"""SELECT m.admin_code,d.name,d.district_code,
        SUM(m.overlap_m2) AS overlap_m2 FROM child_admin m JOIN admin_dong d ON d.code=m.admin_code
        WHERE {selectors} GROUP BY m.admin_code ORDER BY overlap_m2 DESC,m.admin_code""", key)
    result["foodLicenses"] = query(core, f"""SELECT c.name,SUM(licenses) AS located_license_rows
        FROM v_food_child v JOIN food_category c USING(category_id) WHERE {condition}
        GROUP BY c.category_id ORDER BY located_license_rows DESC,c.name""", key)
    result["transitPoints"] = query(core, f"""SELECT mode,place_id,name,line_or_ars,slot
        FROM transport_child_position WHERE {condition} ORDER BY mode,name,place_id""", key)
    result["transitBoardingsAt12"] = transit_boardings(core, key)
    result["presence"] = (
        query(PARENT, """SELECT * FROM v_tile_presence_profile WHERE q=? AND r=?
            ORDER BY day_kind,band""", key) if slot is None
        else layer_query("presence", "WHERE " + condition + " ORDER BY day_kind,band", key)
    )
    for layer in ("buildings", "terrain", "spotElevation", "landCover", "shadeShelters",
                  "parksAndTrees", "roadCoverage", "roadSpeed", "crossings", "walkingNetwork",
                  "walkingSensors", "bike", "stationAccess", "streetLighting", "seoulFacilities",
                  "buildingFootprint", "streetSurface", "bus20261121", "waterSurface", "evSupport",
                  "landmarks"):
        path = database_path(layer)
        if layer in ("roadCoverage", "roadSpeed", "landCover", "streetSurface", "waterSurface"):
            where, values = "q=? AND r=? AND slot=?", (q, r, slot if slot is not None else -1)
        else:
            where, values = condition, key
        if slot is None and layer in ("buildings", "terrain", "buildingFootprint"):
            result[layer] = query(path, "SELECT * FROM " + LAYERS[layer][1].replace("child", "parent")
                                  + " WHERE " + where, values)
        elif layer == "roadSpeed":
            result[layer] = query(path, ROAD_SAMPLES, values)
        elif layer == "bus20261121":
            result[layer] = layer_query(layer, "WHERE " + where +
                " AND service_date='2026-11-21' AND hour IN (12,18) ORDER BY hour,slot", values)
        else:
            result[layer] = layer_query(layer, "WHERE " + where, values)
    result["orders"] = {"state": "not_observed", "value": None}
    result["availability"] = {
        "foodLicenses": "located_licenses" if result["foodLicenses"] else "no_located_license_rows",
        "presence": ("allocated_visible_component" if slot is not None else "parent_grid_aggregate")
        if any(row.get("allocated_visible_mean" if slot is not None else "known_presence_average") is not None
               for row in result["presence"]) else "no_allocatable_public_values",
        "roadSpeed": "observed_directions_only" if any(row["usable_days"] for row in result["roadSpeed"])
        else "no_matching_speed_samples",
        "terrain": "native_points" if result["terrain"] else "no_native_points",
    }
    return result


def transit_boardings(core, key):
    with closing(read_only(PARENT)) as db:
        db.execute("ATTACH DATABASE ? AS child", (core.resolve().as_uri() + "?mode=ro",))
        db.row_factory = sqlite3.Row
        sql = """SELECT p.mode,p.place_id,o.period_type,o.period,o.hour,o.boardings,o.alightings,
                   o.scope_records,o.source_id,c.slot
                 FROM transport_tag o JOIN transport_place p USING(mode,place_id)
                 JOIN child.transport_child_position c USING(mode,place_id)
                 WHERE p.q=? AND p.r=? AND o.hour=12"""
        if len(key) == 3:
            sql += " AND c.slot=?"
        return [dict(row) for row in db.execute(sql + " ORDER BY p.mode,p.place_id,o.period_type,o.period", key)]


def dong_lookup(code):
    core = OUTPUT / DBNAME
    area = query(core, """SELECT a.code,a.name,d.code AS district_code,d.name AS district
        FROM admin_dong a JOIN district d ON d.code=a.district_code WHERE a.code=?""", (code,))
    if not area:
        raise ValueError(f"unknown_seoul_admin_dong:{code}")
    return {
        "geometryRevision": REVISION, "crs": "EPSG:5179", "distribution": "local-review",
        "adminDong": area[0],
        "legalDongReferences": query(core, """SELECT l.code,l.name,m.source_id FROM admin_legal m
            JOIN legal_dong l ON l.code=m.legal_code WHERE m.admin_code=? ORDER BY l.code""", (code,)),
        "residentRecords": layer_query("residentPopulation", "WHERE admin_code=? ORDER BY period_end", (code,)),
        "layerCoverage": query(OUTPUT / "dong-layer-coverage.sqlite", """SELECT layer_id,tiles_touching,
            intersecting_tiles_with_support,share_of_dong_in_tiles_with_support
            FROM v_dong_layer_coverage WHERE code=? ORDER BY layer_id""", (code,)),
        "childTileOverlap": query(core, """SELECT q,r,slot,overlap_m2,
            overlap_m2 / t.full_area_m2 AS share_of_child
            FROM child_admin m JOIN child_tile t USING(q,r,slot) WHERE admin_code=? ORDER BY q,r,slot""", (code,)),
    }


def national_tile_lookup(q, r, slot=None):
    condition = "q=? AND r=?" + (" AND slot=?" if slot is not None else "")
    key = (q,r) if slot is None else (q,r,slot)
    points = query(NATIONAL_POINTS, """SELECT official_dong_code,q,r,slot,source,category,positive_source_count
        FROM point_count WHERE """ + condition + " ORDER BY official_dong_code,slot,source,category", key)
    return {"geometryRevision": REVISION, "crs": "EPSG:5179", "distribution": "local-review",
            "key": key, "scope": "positive_point_observations",
            "state": "matched_point_observations" if points else "no_located_point_rows",
            "points": points}


def national_dong_lookup(code):
    reference = query(OFFICIAL_REGIONS, """SELECT code,name,district_code,topology_valid,import_warning,
            observed_date,population,households,one_person_households,join_status
        FROM dong_map WHERE code=?""", (code,))
    if not reference:
        raise ValueError(f"unknown_national_admin_dong:{code}")
    data = query(NATIONAL_POINTS, """SELECT source,category,SUM(positive_source_count) AS linked_count,COUNT(*) AS tile_rows
            FROM point_count WHERE official_dong_code=? GROUP BY source,category ORDER BY source,category""", (code,))
    files = query(MODULE_READINESS, """SELECT local_mesh_case,grade_in_quality_snapshot,evidence_kind,
        boundary_class,scope_revision,source_vintage,physical_state,evidence_level
        FROM v_dong_module_read WHERE code=? ORDER BY boundary_class,scope_revision,evidence_kind""", (code,))
    return {"geometryRevision": REVISION, "crs": "EPSG:5179", "distribution": "local-review",
            "officialDongAndPopulation": reference[0], "pointEvidence": data, "moduleEvidence": files}


def validate_bundle():
    core = OUTPUT / DBNAME
    paths = sorted({database_path(layer) for layer in LAYERS} | {OUTPUT / "dong-layer-coverage.sqlite"})
    catalog = []
    for name, (_, relation, unit, calculation) in LAYERS.items():
        path = database_path(name)
        with closing(read_only(path)) as db:
            columns = [row[1] for row in db.execute(f"PRAGMA table_info({relation})")]
        if not columns:
            raise ValueError(f"missing_layer_relation:{name}")
        source, grain, coverage, lineage = SOURCE_DETAILS[name]
        catalog.append({"fieldKey": name, "database": path.relative_to(ROOT).as_posix(),
                        "relation": relation, "columns": columns, "unit": unit, "basis": calculation,
                        "sourceIdOrGroup": source, "sourceGrain": grain, "scopeAndPeriod": coverage, "lineage": lineage,
                        "missing": "그 층의 관측 없음. 누락을 수치 0으로 간주하지 않음."})
    paths_by_name = {path: path.relative_to(ROOT).as_posix() for path in paths}
    verification = {}
    with closing(read_only(core)) as base:
        if base.execute("SELECT value FROM metadata WHERE key='geometry_revision'").fetchone() != (REVISION,):
            raise ValueError("wrong_geometry_revision")
        parent_keys = set(base.execute("SELECT q,r FROM tile"))
        child_keys = set(base.execute("SELECT q,r,slot FROM child_tile"))
        for q, r, slot, raw in base.execute("SELECT q,r,slot,vertices_json FROM child_tile"):
            if json.loads(raw) != [list(pair) for pair in vertices(q, r, slot)]:
                raise ValueError(f"stored_tile_vertices_differ:{q}:{r}:{slot}")
    for path in paths:
        with closing(read_only(path)) as db:
            check_database(db)
            matched = {}
            for key_table in ("tile", "child", "child_tile", "tile_footprint", "building_child_by_use", "station_scope"):
                cols = {row[1] for row in db.execute(f"PRAGMA table_info({key_table})")}
                if not {"q", "r"} <= cols:
                    continue
                triples = "slot" in cols
                names = "q,r,slot" if triples else "q,r"
                keys = set(db.execute(f"SELECT {names} FROM {key_table} WHERE q IS NOT NULL"))
                allowed = child_keys | {(q, r, -1) for q, r in parent_keys} if triples else parent_keys
                if keys - allowed:
                    raise ValueError(f"unknown_tile_keys:{path.name}:{key_table}")
                matched[key_table] = len(keys)
            verification[paths_by_name[path]] = {"sha256": digest(path), "bytes": path.stat().st_size,
                                                "foreignKeysAndIntegrity": "ok", "recognizedMapKeys": matched}
    atomic_json(OUTPUT / "field-catalog.json", {
        "schema": "hongdal.dong-tile-field-catalog.v1", "geometryRevision": REVISION, "crs": "EPSG:5179",
        "distribution": "local-review", "childKey": ["q", "r", "slot"], "layers": catalog,
    })
    result = {"geometryRevision": REVISION, "parentKeys": len(parent_keys), "childKeys": len(child_keys),
              "layerCount": len(catalog), "databases": verification}
    atomic_json(OUTPUT / "verification.json", result)
    return result


def write_examples():
    result = {"geometryRevision": REVISION, "cases": {name: tile_lookup(*key) for name, key in CASES.items()}}
    result["boundaryDong"] = dong_lookup("1126058000")
    if not all(r["geometry"]["seoul_area_m2"] > 0 for r in result["cases"].values()):
        raise ValueError("representative_case_not_in_seoul")
    if result["cases"]["yongmasan_slope"]["availability"]["presence"] != "no_allocatable_public_values":
        raise ValueError("historical_no_presence_example_changed")
    if len(result["cases"]["sagajeong"]["adminDongs"]) < 2:
        raise ValueError("representative_boundary_case_not_in_multiple_dongs")
    atomic_json(OUTPUT / "query-examples.json", result)
    return {"examples": list(CASES), "boundaryDong": result["boundaryDong"]["adminDong"],
            "boundaryDongChildCount": len(result["boundaryDong"]["childTileOverlap"])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--tile", nargs=2, type=int, metavar=("Q", "R"))
    action.add_argument("--dong", metavar="CODE")
    action.add_argument("--national-tile", nargs=2, type=int, metavar=("Q", "R"))
    action.add_argument("--national-dong", metavar="CODE")
    action.add_argument("--verify", action="store_true")
    action.add_argument("--examples", action="store_true")
    parser.add_argument("--slot", type=int, choices=range(7))
    args = parser.parse_args()
    if args.slot is not None and args.tile is None and args.national_tile is None:
        parser.error("--slot requires --tile or --national-tile")
    if args.verify:
        result = validate_bundle()
    elif args.examples:
        result = write_examples()
    elif args.dong:
        result = dong_lookup(args.dong)
    elif args.national_dong:
        result = national_dong_lookup(args.national_dong)
    elif args.national_tile:
        result = national_tile_lookup(*args.national_tile, args.slot)
    else:
        result = tile_lookup(*args.tile, args.slot)
    print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))


if __name__ == "__main__":
    main()

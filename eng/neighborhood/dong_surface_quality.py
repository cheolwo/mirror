"""동별 HDIO 삼각형을 원자료에서 복원한 지도 기호의 지표 면적과 대조한다."""
import math

import numpy as np
import shapely
from pyproj import Transformer
from shapely.geometry import LineString, shape


def mesh_surfaces(xyz, rgba, indices, profile):
    triangles = indices.reshape(-1, 3)
    measured = {}
    for layer, spec in profile['surfaces'].items():
        # 같은 색의 벽·지붕은 층의 화면 높이로 구분한다. 정점만 있고 연결되지 않은 층도 잡는다.
        target_y = np.float32(spec['yMetres'])
        belongs = (xyz[:, 1] == target_y) & np.all(rgba[:, :3] == spec['rgb'], axis=1)
        mask = belongs[triangles]
        if np.any(mask.any(axis=1) != mask.all(axis=1)):
            raise ValueError(f'{layer}: 서로 다른 표현층을 잇는 삼각형')
        faces = triangles[mask.all(axis=1)]
        points = xyz[faces][:, :, (0, 2)].astype(np.float64)
        if len(faces):
            left = points[:, 1] - points[:, 0]
            right = points[:, 2] - points[:, 0]
            areas = np.abs(left[:, 0] * right[:, 1] - left[:, 1] * right[:, 0]) / 2
            area = float(areas.sum())
            centre = (np.average(points.mean(axis=1), axis=0, weights=areas)
                      if area > 0 else None)
        else:
            area, centre = 0.0, None
        measured[layer] = (len(faces), int(belongs.sum()), area, centre)
    return measured


def check_surface_coverage(layers, frame, profile, measured):
    lat, lon, mz, mx = frame
    transformer = Transformer.from_crs(5186, 4326, always_xy=True)
    widths = profile['strokeHalfWidthMetres']
    def local(x, y):
        return (x - lon) * mx, (y - lat) * mz

    def projected(x, y):
        return local(*transformer.transform(x, y))

    boundary = shapely.transform(shape(layers['boundary']['geometry']), local, interleaved=False)
    def geographic(features):
        return shapely.transform([shape(f['geometry']) for f in features], local, interleaved=False)

    buildings = shapely.union_all(geographic(layers['buildings']['features']))
    roads = geographic(layers['roads']['features'])
    streets = layers['streets']
    origin = np.asarray(streets['frame']['originMetres'])
    unit = streets['frame']['integerUnitMetres']
    def street_lines(items):
        lines = [LineString(np.asarray(r['pathMm']) * unit + origin) for r in items]
        return shapely.transform(lines, projected, interleaved=False)

    def drawn_stroke(lines, half_width):
        buffers = shapely.buffer(lines, half_width, cap_style='flat', join_style='bevel')
        return shapely.union_all(buffers).intersection(boundary).difference(buildings)

    walks = [r for r in streets['walkLinks'] if all(s == '횡단보도' for s in r['structure'])]
    walk_width = [widths['walkMixed'] if r['modeGroup'] == 'pedestrianAndVehicle' else widths['walkOther']
                  for r in walks]
    park_features = layers['green']['parkPlanning2018']
    parks = shapely.transform([shape(f['geometry']) for f in park_features], projected, interleaved=False)
    carriage = drawn_stroke(roads, widths['carriage'])
    lane_rows = streets['laneMarkingFragments']
    expected = {
        'ground': (1, boundary),
        'parkPlan': (len(parks), shapely.union_all(parks).intersection(boundary)),
        'walk': (len(walks), drawn_stroke(street_lines(walks), walk_width)),
        'roadEdge': (len(roads), drawn_stroke(roads, widths['roadEdge'])),
        'carriage': (len(roads), carriage),
        'lane': (len(lane_rows), drawn_stroke(street_lines(lane_rows), widths['lane']).intersection(carriage)),
    }
    result = {}
    # HDIO 정점은 서울 공통 원점 기준 float32다. 외곽 길이와 그 동 좌표의 양자화 간격으로 면적 허용 오차를 산정한다.
    spacing = abs(float(np.spacing(np.float32(max(map(abs, boundary.bounds))))))
    for name, (count, geom) in expected.items():
        triangles, _, actual, centre = measured[name]
        area = geom.area
        tolerance = max(.00003, 3 * spacing * geom.length)
        if not math.isclose(area, actual, rel_tol=1e-7, abs_tol=tolerance):
            raise ValueError(f'{name}: 원본 지표 면적 {area:.5f} / 메시 {actual:.5f} m²')
        if (area > 0) != (triangles > 0) or (area > 0) != (actual > 0):
            raise ValueError(f'{name}: 원본 표시 면과 메시 삼각형의 유무가 다름')
        if centre is not None:
            # 면적이 작아 좌표 반올림 오차의 비중이 클 때는 그 오차가 무게중심에 미칠 최대 변위를 반영한다.
            span = max(geom.bounds[2] - geom.bounds[0], geom.bounds[3] - geom.bounds[1])
            if math.dist(centre, (geom.centroid.x, geom.centroid.y)) > 3 * spacing + span * tolerance / area:
                raise ValueError(f'{name}: 원본·메시의 무게중심이 다름')
        status = 'source-empty' if count == 0 else ('clipped-empty' if area == 0 else 'present')
        result[name] = {'sourceCount': count, 'triangles': triangles, 'sourceAreaM2': round(area, 4),
                        'meshAreaM2': round(actual, 4), 'status': status}
    return result

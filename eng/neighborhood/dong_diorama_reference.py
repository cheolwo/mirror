"""동별 HDIO 굽기와 원자료 대조에서 같은 기준동 설정을 읽는다."""
import hashlib
import json
import math
import re
from pathlib import Path


DEFAULT_PROFILE = Path(__file__).with_name("dong_diorama_reference.json")
SURFACE_ORDER = ("ground", "parkPlan", "walk", "roadEdge", "carriage", "lane")


def _members(row, keys, field):
    if not isinstance(row, dict) or set(row) != set(keys):
        raise ValueError(f"기준 설정 {field}의 필드를 확인하세요: {', '.join(keys)}")


def _unique(pairs):
    row = {}
    for key, value in pairs:
        if key in row:
            raise ValueError(f"기준 설정에 중복된 키 {key}")
        row[key] = value
    return row


def _number(value, field, *, positive=False):
    if type(value) not in (int, float) or not math.isfinite(value) or (positive and value <= 0):
        raise ValueError(f"기준 설정 {field}의 수치 범위를 확인하세요")


def _rgb(color):
    if not isinstance(color, list) or len(color) != 3 or any(type(v) is not int or not 0 <= v <= 255 for v in color):
        raise ValueError(f"기준 설정의 RGB 색상 오류: {color}")


def load_profile(path=DEFAULT_PROFILE):
    raw = Path(path).read_bytes()
    profile = json.loads(raw, object_pairs_hook=_unique)
    _members(profile, ("schema", "profileId", "referenceDongCode", "baseline", "surfaces",
                       "strokeHalfWidthMetres", "buildings"), "최상위")
    if profile["schema"] != "hongdal-dong-diorama-reference.v1":
        raise ValueError("지원하지 않는 행정동 디오라마 기준 설정 판본")
    if not isinstance(profile["profileId"], str) or not re.fullmatch(r"[a-z0-9][a-z0-9.-]*", profile["profileId"]):
        raise ValueError("기준 설정의 profileId를 확인하세요")
    if profile["referenceDongCode"] != "1126057500":
        raise ValueError("기준 행정동은 면목제3·8동(1126057500)입니다")
    _members(profile["baseline"], ("sourceModuleSha256", "meshSha256", "buildingIndexSha256"), "baseline")
    for value in profile["baseline"].values():
        if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
            raise ValueError("기준동 지문을 확인하세요")

    surfaces = profile["surfaces"]
    _members(surfaces, SURFACE_ORDER, "surfaces")
    last_y = -1.0
    for key in SURFACE_ORDER:
        surface = surfaces[key]
        _members(surface, ("yMetres", "rgb"), key)
        _number(surface["yMetres"], key)
        _rgb(surface["rgb"])
        if surface["yMetres"] < 0 or surface["yMetres"] <= last_y:
            raise ValueError(f"기준 설정 {key}의 표시면 높이 순서를 확인하세요")
        last_y = surface["yMetres"]
    widths = profile["strokeHalfWidthMetres"]
    _members(widths, ("walkMixed", "walkOther", "roadEdge", "carriage", "lane"), "strokeHalfWidthMetres")
    for key, value in widths.items():
        _number(value, key, positive=True)
    if not widths["lane"] < widths["carriage"] < widths["roadEdge"] or widths["walkMixed"] < widths["walkOther"]:
        raise ValueError("기준 설정의 차선·차도·도로 가장자리 또는 보행선의 표현 폭을 확인하세요")

    buildings = profile["buildings"]
    distances = ("floorHeightMetres", "fallbackHeightMetres", "compressAboveMetres", "compressionRatio",
                 "maxDisplayHeightMetres", "parapetMetres", "shadowMaxHeightMetres", "shadowScale", "shadowYMetres")
    lighting = ("wallAmbient", "wallDirectional", "shadowEast", "shadowNorth")
    _members(buildings, (*distances, *lighting, "roofRgbPalette", "wallRgb"), "buildings")
    for key in distances:
        _number(buildings[key], key, positive=True)
    for key in lighting:
        _number(buildings[key], key)
    if (buildings["compressAboveMetres"] > buildings["maxDisplayHeightMetres"] or
            buildings["compressionRatio"] > 1 or buildings["shadowYMetres"] <= last_y):
        raise ValueError("기준 설정의 건물 높이·그림자 높이를 확인하세요")
    if (min(buildings["wallAmbient"], buildings["wallDirectional"]) < 0 or
            buildings["wallAmbient"] + buildings["wallDirectional"] > 1 or
            not math.isclose(math.hypot(buildings["shadowEast"], buildings["shadowNorth"]), 1, abs_tol=0.002)):
        raise ValueError("기준 설정의 벽면 명암·그림자 방향을 확인하세요")
    _rgb(buildings["wallRgb"])
    if not isinstance(buildings["roofRgbPalette"], list) or not buildings["roofRgbPalette"]:
        raise ValueError("기준 설정에 지붕 색이 없습니다")
    for color in buildings["roofRgbPalette"]:
        _rgb(color)
    return profile, {"id": profile["profileId"], "referenceDongCode": profile["referenceDongCode"],
                     "sha256": hashlib.sha256(raw).hexdigest()}

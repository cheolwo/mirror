"""다운로드한 공식 교통 CSV에서 지도에 필요한 필드만 읽는다."""
from __future__ import annotations

import csv
from pathlib import Path

from map_public_api_projection import coordinate, field, valid_digits


_버스정류장_속성 = {
    "city_name": "도시명",
    "management_city_name": "관리도시명",
    "collected_on": "정보수집일",
}

_도로시설_공통속성 = {
    "route_name": "도로노선명",
    "route_number": "도로노선번호",
    "route_direction": "도로노선방향",
    "road_type": "도로종류",
    "province_name": "시도명",
    "district_name": "시군구명",
    "lane_count": "차로수",
    "observed_on": "데이터기준일자",
}

_도로시설_표기 = {"road_bridge": "교량", "road_tunnel": "터널"}


def _국내좌표(row, longitude_column, latitude_column):
    longitude = coordinate(field(row, longitude_column))
    latitude = coordinate(field(row, latitude_column))
    if longitude is not None and latitude is not None and 124 <= longitude <= 132 and 32 <= latitude <= 40:
        return longitude, latitude
    return None, None


def iter_transport_rows(path, kind):
    """CSV 헤더와 행 길이를 검사하고 자료 행 번호를 포함해 반환한다."""
    path = Path(path)
    if kind == "bus_stop":
        encoding = "cp949"
        name_column = "정류장명"
        id_column = "정류장번호"
        area_column = "도시코드"
        area_lengths = (2, 5)
        area_attribute = "city_code"
        start_columns = ("경도", "위도")
        end_columns = None
        attribute_columns = _버스정류장_속성
    elif kind in _도로시설_표기:
        facility = _도로시설_표기[kind]
        encoding = "utf-8-sig"
        name_column = f"{facility}명"
        id_column = None
        area_column = "시군구코드"
        area_lengths = (5, 10)
        area_attribute = "district_code"
        start_columns = (f"{facility}시작점경도", f"{facility}시작점위도")
        end_columns = (f"{facility}종료점경도", f"{facility}종료점위도")
        attribute_columns = {
            **_도로시설_공통속성,
            "facility_length": f"{facility}연장",
            "facility_width": f"{facility}폭",
            "facility_height": f"{facility}높이",
        }
        if kind == "road_bridge":
            attribute_columns["underpass_clearance"] = "하부통과제한높이"
        else:
            attribute_columns["tunnel_type"] = "터널종류"
    else:
        raise ValueError(f"지원하지 않는 교통 자료 유형: {kind}")

    required_columns = {name_column, area_column, *start_columns}
    if id_column:
        required_columns.add(id_column)
    if end_columns:
        required_columns.update(end_columns)

    with path.open(encoding=encoding, newline="") as stream:
        reader = csv.DictReader(stream, strict=True)
        header = reader.fieldnames
        if not header or any(not name.strip() for name in header) or len(header) != len({name.strip() for name in header}):
            raise ValueError(f"{path.name}: 빈 이름 또는 중복 열이 있는 CSV 헤더")
        if missing_columns := required_columns.difference(header):
            raise ValueError(f"{path.name}: 필수 열 누락 ({', '.join(sorted(missing_columns))})")

        for source_row, row in enumerate(reader, 1):
            if None in row or None in row.values():
                raise ValueError(f"{path.name}: {source_row}번째 자료 행의 열 수 불일치")

            longitude, latitude = _국내좌표(row, *start_columns)
            end_longitude, end_latitude = _국내좌표(row, *end_columns) if end_columns else (None, None)
            area_hint = field(row, area_column)
            if not any(valid_digits(area_hint, length) for length in area_lengths):
                area_hint = None
            attributes = {}
            for key, column in attribute_columns.items():
                if (value := field(row, column)) is not None:
                    attributes[key] = value
            if area_hint is not None:
                attributes[area_attribute] = area_hint
            yield {
                "source_row": source_row,
                "provider_id": field(row, id_column) if id_column else None,
                "name": field(row, name_column),
                "area_hint": area_hint,
                "longitude": longitude,
                "latitude": latitude,
                "end_longitude": end_longitude,
                "end_latitude": end_latitude,
                "attributes": attributes,
            }

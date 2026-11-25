"""공식 자료에 명시된 고유번호·지역코드·좌표·단위만 지도 조회에 연결한다."""
from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal, InvalidOperation
from functools import lru_cache
import math
import re


PROJECTION_VERSION = 1


def field(row, *names):
    for name in names:
        value = row.get(name)
        if value is not None and str(value).strip():
            return str(value).strip()
    return None


def valid_digits(value, length):
    return bool(value and re.fullmatch(rf"[0-9]{{{length}}}", value))


def region_hints(kind, row):
    hints = []
    for area, size, name in (("sido", 2, "ctprvnCd"), ("sigungu", 5, "signguCd"),
                             ("administrative", 10, "adongCd"), ("legal", 10, "ldongCd")):
        value = field(row, name)
        if kind in {"shop", "market_area"} and valid_digits(value, size):
            hints.append((area, value, name))
    if kind == "building":
        city, dong = field(row, "sigunguCd"), field(row, "bjdongCd")
        if valid_digits(city, 5):
            hints.append(("sigungu", city, "sigunguCd"))
            if valid_digits(dong, 5):
                hints.append(("legal", city + dong, "sigunguCd+bjdongCd"))
    if kind == "parcel":
        parcel = field(row, "pnu", "PNU")
        if valid_digits(parcel, 19):
            hints.append(("legal", parcel[:10], "pnu"))
    return hints


def subjects(kind, row, params):
    identifiers = []
    building = field(row, "bldMngNo")
    if valid_digits(building, 25):
        identifiers.append(("juso-building", building, "at_building"))
    parcel = field(row, "pnu", "PNU", "lnoCd")
    if valid_digits(parcel, 19):
        identifiers.append(("pnu", parcel, "self" if kind == "parcel" else "at_parcel"))
    title = field(row, "mgmBldrgstPk")
    if kind == "building" and title:
        identifiers.append(("building-hub-title", title, "self"))
    if kind == "bus_stop":
        stop = field(row, "nodeid", "nodeId")
        if stop:
            identifiers.append(("tago-bus-stop", stop, "self"))
    if kind == "bus_route_at_stop":
        identifiers.append(("tago-bus-stop", params["nodeid"], "calls_at"))
        route = field(row, "routeid", "routeId")
        if route:
            identifiers.append(("tago-bus-route", params["cityCode"] + ":" + route, "serves"))
    if kind in {"traffic_site", "traffic_count"}:
        site = field(row, "spot", "spot_id", "spotId")
        if site and params.get("year") and params.get("dtype"):
            identifiers.append(("kict-traffic-site", f"{params['year']}:{params['dtype']}:{site}",
                                "self" if kind == "traffic_site" else "observed_at"))
    if kind == "trade":
        hs = field(row, "hsSgn", "HsSgn", "hsCd") or params.get("HsSgn")
        if valid_digits(hs, 6):
            identifiers.append(("hs6", hs, "commodity"))
        elif valid_digits(hs, 2):
            identifiers.append(("hs-chapter", hs, "commodity_chapter"))
        if params.get("sidoCd"):
            identifiers.append(("customs-sido", params["sidoCd"], "reported_for"))
    if kind in {"retail", "livestock", "slaughter"}:
        agency = field(row, "OPN_ATMY_GRP_CD")
        if agency:
            identifiers.append(("localdata-agency", agency, "licensed_by"))
    if kind == "airport":
        airport = field(row, "Airport")
        if airport:
            identifiers.append(("kac-airport", airport, "reported_for"))
    if kind == "port":
        port = field(row, "prtAgNm")
        foreign_area = field(row, "areaCd")
        if port:
            identifiers.append(("mof-port-reporting-area", port, "reported_for"))
        if foreign_area:
            identifiers.append(("mof-container-area", foreign_area, "reported_for"))
    return identifiers


def decimal_value(value):
    if value is None or not re.fullmatch(r"[+-]?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d+)?", value):
        return None
    try:
        amount = Decimal(value.replace(",", ""))
    except InvalidOperation:
        return None
    return format(amount, "f") if amount.is_finite() else None


def coordinate(value):
    decimal = decimal_value(value)
    output = float(decimal) if decimal is not None else None
    return output if output is not None and math.isfinite(output) else None


@lru_cache(maxsize=1)
def korean_grid_transformer():
    try:
        from pyproj import Transformer
    except ImportError:
        return None
    return Transformer.from_crs("EPSG:5174", "EPSG:4326", always_xy=True)


def market_polygon(value):
    polygon = re.fullmatch(r"POLYGON\s*\(\s*(\([^()]+\)(?:\s*,\s*\([^()]+\))*)\s*\)", value or "",
                           re.IGNORECASE)
    if not polygon:
        return None
    rings = []
    for ring in re.findall(r"\(([^()]+)\)", polygon[1]):
        points = []
        for vertex in ring.split(","):
            values = vertex.split()
            if len(values) != 2:
                return None
            longitude, latitude = (coordinate(part) for part in values)
            if longitude is None or latitude is None or not (124 <= longitude <= 132 and 32 <= latitude <= 40):
                return None
            points.append([longitude, latitude])
        if len(points) < 4 or points[0] != points[-1]:
            return None
        rings.append(points)
    return {"type": "Polygon", "coordinates": rings}


def location(kind, row):
    if kind == "parcel" and row.get("geometry"):
        return ("EPSG:4326", None, None, None, None, row["geometry"])
    if kind == "market_area":
        geometry = market_polygon(field(row, "coords"))
        if geometry:
            return ("EPSG:4326", None, None, None, None, geometry)
    if kind in {"shop", "bus_stop"}:
        longitude = coordinate(field(row, "lon", "gpslong", "gpsLong"))
        latitude = coordinate(field(row, "lat", "gpslati", "gpsLati"))
        if longitude is not None and latitude is not None and 124 <= longitude <= 132 and 32 <= latitude <= 40:
            return ("EPSG:4326", longitude, latitude, longitude, latitude, None)
    if kind in {"retail", "slaughter"}:
        x, y = (coordinate(field(row, axis)) for axis in ("CRD_INFO_X", "CRD_INFO_Y"))
        if x is not None and y is not None:
            longitude = latitude = None
            transformer = korean_grid_transformer()
            if transformer is not None:
                lon, lat = transformer.transform(x, y)
                if 124 <= lon <= 132 and 32 <= lat <= 40:
                    longitude, latitude = lon, lat
            return ("EPSG:5174", x, y, longitude, latitude, None)
    if kind == "traffic_site":
        x, y = (coordinate(field(row, axis)) for axis in ("xcode", "ycode"))
        if x is not None and y is not None:
            return ("provider_crs_unverified", x, y, None, None, None)
    return None


def source_time(row, kind):
    value = field(row, "chgDt", "DAT_UPDT_PNT", "lastUpdtPnt", "lastUpdt", "updateDate", "stdrDt")
    if not value:
        return None
    if valid_digits(value, 14):
        value = f"{value[:4]}-{value[4:6]}-{value[6:8]}T{value[8:10]}:{value[10:12]}:{value[12:14]}"
    if valid_digits(value, 8):
        value = f"{value[:4]}-{value[4:6]}-{value[6:8]}"
    if not re.fullmatch(r"\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2}:\d{2})?", value):
        return None
    try:
        datetime.fromisoformat(value)
    except ValueError:
        return None
    return value.replace(" ", "T")


def period(row, params):
    month = field(row, "useYm", "yearMonth", "statYm", "priodTitle")
    if month and re.fullmatch(r"\d{4}[.-]\d{2}", month):
        month = month.replace("-", "").replace(".", "")
    if valid_digits(month, 6) and 1 <= int(month[4:]) <= 12:
        return month, month
    for start, end in (("strtYymm", "endYymm"), ("sym", "eym"), ("startDePd", "endDePd")):
        if start in params:
            return params[start], params[end]
    if "year" in params and "month" in params:
        ym = params["year"] + params["month"].zfill(2)
        day = field(row, "day")
        if day and day.isdigit():
            try:
                observed = date(int(ym[:4]), int(ym[4:]), int(day)).isoformat()
                return observed, observed
            except ValueError:
                pass
        return ym, ym
    return None, None


def observations(operation, row, params):
    start, end = period(row, params)
    fields = operation.get("measures", {})
    for name, (measure, unit) in fields.items():
        value = decimal_value(field(row, name))
        if value is not None:
            yield measure, value, unit, start, end

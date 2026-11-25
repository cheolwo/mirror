"""신청한 공개 API의 범위·호출·응답 검사. 자격 증명과 원응답 전문은 저장하지 않는다."""
from __future__ import annotations

from datetime import date
from http.client import HTTPException
import json
import math
import os
from pathlib import Path
import re
from urllib.error import HTTPError, URLError
from urllib.parse import quote, quote_plus, unquote, urlencode, urlsplit
from urllib.request import Request
from xml.etree import ElementTree

from collect_public_snapshots import opener
from map_public_api_projection import coordinate, field, valid_digits


MAX_RESPONSE_BYTES = 8 * 1024 * 1024
CATALOG = Path(__file__).with_name("map_public_api_catalog.json")
HOSTS = {"apis.data.go.kr", "api.vworld.kr"}


class IntakeError(Exception):
    def __init__(self, code, *, blocked=False):
        super().__init__(code)
        self.blocked = blocked


def require(condition, code, *, blocked=False):
    if not condition:
        raise IntakeError(code, blocked=blocked)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)


def load_catalog():
    operations = {}
    for raw in json.loads(CATALOG.read_text(encoding="utf-8"))["operations"]:
        operation_id = raw["id"]
        address = urlsplit(raw["url"])
        require(re.fullmatch(r"[a-z][a-z0-9_]+", operation_id) and operation_id not in operations,
                "catalog_operation_invalid")
        require(re.fullmatch(r"\d{8}", raw["portalId"]) and address.scheme == "https"
                and address.netloc in HOSTS and address.path.startswith("/") and not address.query
                and not address.fragment, "catalog_endpoint_invalid")
        require(raw["keyParam"] in {"key", "servicekey", "serviceKey", "ServiceKey"},
                "catalog_key_parameter_invalid")
        require(not any(str(raw["keyParam"]).lower() == k.lower()
                        for k in (*raw["defaults"], *raw["required"], *raw["optional"])),
                "catalog_duplicate_key_parameter")
        kept = set(raw["keepFields"])
        require(all(keys and set(keys) <= kept for keys in raw["keyFields"])
                and set(raw.get("measures", {})) <= kept, "catalog_fields_invalid")
        operations[operation_id] = raw
    return operations


def parse_parameters(operation, entries):
    supplied = {}
    names = set(operation["required"]) | set(operation["optional"])
    for entry in entries:
        key, separator, value = entry.partition("=")
        require(separator and key in names and key not in supplied and value and len(value) <= 240
                and not any(ord(c) < 32 for c in value), "request_parameter_invalid")
        supplied[key] = value
    require(set(operation["required"]) <= supplied.keys(), "request_parameter_missing")
    for key, length in (("sigunguCd", 5), ("bjdongCd", 5), ("sidoCd", 2), ("HsSgn", 6), ("year", 4)):
        if key in supplied:
            require(valid_digits(supplied[key], length), "request_code_invalid")
    if "dtype" in supplied:
        require(supplied["dtype"] in {"1", "2", "3", "5"}, "request_road_type_invalid")
    if "month" in supplied:
        require(supplied["month"].isdigit() and 1 <= int(supplied["month"]) <= 12, "request_month_invalid")
        supplied["month"] = supplied["month"].zfill(2)
    for start, end in (("strtYymm", "endYymm"), ("sym", "eym"), ("startDePd", "endDePd")):
        if start not in supplied:
            continue
        require(all(valid_digits(supplied[k], 6) and 1 <= int(supplied[k][4:]) <= 12 for k in (start, end))
                and supplied[start] <= supplied[end], "request_period_invalid")
    if "divId" in supplied and "key" in supplied:
        areas = {"ctprvnCd": (2,), "signguCd": (5,), "adongCd": (8, 10)}
        require(any(valid_digits(supplied["key"], size) for size in areas.get(supplied["divId"], ())),
                "request_region_code_invalid")
    if operation["id"] == "shops_by_date":
        value = supplied["key"]
        require(valid_digits(value, 8), "request_update_date_invalid")
        try:
            date.fromisoformat(value)
        except ValueError:
            raise IntakeError("request_update_date_invalid") from None
    if "gpsLati" in supplied:
        lat, lon = (coordinate(supplied[k]) for k in ("gpsLati", "gpsLong"))
        require(lat is not None and lon is not None and 32 <= lat <= 40 and 124 <= lon <= 132,
                "request_coordinates_invalid")
    if operation["shape"] == "vworld":
        bbox = re.fullmatch(r"BOX\((-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?),"
                            r"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)\)", supplied["geomFilter"])
        require(bbox is not None, "request_parcel_bbox_required")
        west, south, east, north = (float(n) for n in bbox.groups())
        require(124 <= west < east <= 132 and 32 <= south < north <= 40
                and (east - west) * (north - south) * 111.32 ** 2 <= 2, "request_parcel_bbox_too_large")
    return {**operation["defaults"], **supplied}


def existing_government_key():
    app_data = os.environ.get("APPDATA")
    if not app_data:
        return ""
    project = Path(__file__).resolve().parents[2] / "Ssalddel" / "Ssalddel.csproj"
    try:
        project_id = ElementTree.parse(project).findtext("./PropertyGroup/UserSecretsId", "")
        if not re.fullmatch(r"[a-fA-F0-9-]{36}", project_id):
            return ""
        path = Path(app_data) / "Microsoft" / "UserSecrets" / project_id / "secrets.json"
        values = json.loads(path.read_text(encoding="utf-8-sig"))
        value = values.get("PublicData:DataGoKrServiceKey") or values.get("PublicData:ServiceKey", "")
        return unquote(value.strip()) if isinstance(value, str) else ""
    except (OSError, ValueError, AttributeError, ElementTree.ParseError):
        return ""


def credentials(operation):
    vworld = operation["shape"] == "vworld"
    env, filename = ("VWORLD_KEY", "vworld_key.txt") if vworld else ("DATA_GO_KR_KEY", "data_go_kr_key.txt")
    key = os.environ.get(env, "").strip()
    if not key:
        path = Path.home() / "secrets" / filename
        if path.is_file():
            try:
                key = path.read_text(encoding="utf-8-sig").strip()
            except (OSError, UnicodeError):
                raise IntakeError("credential_file_unreadable", blocked=True) from None
    if not key and not vworld:
        key = existing_government_key()
    require(key and len(key) <= 256 and not any(c.isspace() for c in key), "credential_missing_or_invalid", blocked=True)
    require(not re.search(r"%[0-9a-fA-F]{2}", key), "use_decoding_key", blocked=True)
    secret = {operation["keyParam"]: key}
    if vworld:
        domain = os.environ.get("VWORLD_DOMAIN", "").strip()
        require(domain and len(domain) <= 240 and not any(c.isspace() for c in domain),
                "vworld_domain_missing", blocked=True)
        secret["domain"] = domain
    return secret


def request_page(operation, query, secret):
    def sent_secret(text):
        key = secret[operation["keyParam"]]
        return key in text or quote(key, safe="") in text or quote_plus(key, safe="") in text

    require(not sent_secret(canonical(query)), "credential_found_in_request_scope", blocked=True)
    address = operation["url"] + "?" + urlencode({**query, **secret})
    try:
        request = Request(address, headers={"User-Agent": "HongdalPublicMapIntake/1", "Accept-Encoding": "identity"})
        with opener().open(request, timeout=25) as response:
            body = response.read(MAX_RESPONSE_BYTES + 1)
    except HTTPError as exc:
        code = exc.code
        exc.close()
        raise IntakeError(f"http_{code}", blocked=code in {401, 403, 429}) from None
    except (URLError, OSError, ValueError, HTTPException):
        raise IntakeError("provider_unreachable", blocked=True) from None
    require(len(body) <= MAX_RESPONSE_BYTES, "response_too_large")
    return body


def ensure_no_secret(value, secret):
    serialized = canonical(value)
    for key in secret:
        require(not any(form in serialized for form in {key, quote(key, safe=""), quote_plus(key, safe="")}),
                "credential_found_in_response", blocked=True)


def xml_value(node):
    if not len(node):
        return node.text.strip() if node.text else None
    result = {}
    for child in node:
        name = child.tag.rsplit("}", 1)[-1]
        value = xml_value(child)
        if name in result:
            if not isinstance(result[name], list):
                result[name] = [result[name]]
            result[name].append(value)
        else:
            result[name] = value
    return result


def response_object(body):
    require(isinstance(body, bytes) and 0 < len(body) <= MAX_RESPONSE_BYTES, "response_size_invalid")
    try:
        if body.lstrip().startswith(b"<") or body.startswith(b"\xef\xbb\xbf<"):
            require(not re.search(br"<!\s*(?:DOCTYPE|ENTITY)", body, re.I), "response_xml_entity_forbidden")
            value = xml_value(ElementTree.fromstring(body))
        else:
            value = json.loads(body, parse_float=str)
    except (ElementTree.ParseError, ValueError, RecursionError):
        raise IntakeError("response_format_invalid") from None
    require(isinstance(value, dict), "response_container_invalid")
    return value


def at(value, *keys):
    for key in keys:
        if not isinstance(value, dict):
            return None
        value = value.get(key)
    return value


def nonnegative(value):
    text = str(value)
    return int(text) if re.fullmatch(r"\d{1,12}", text) else None


def rows_from_response(operation, value):
    root = value.get("response", value)
    require(isinstance(root, dict), "response_container_invalid")
    status = at(root, "header", "resultCode") or root.get("resultCode")
    error_code = at(root, "cmmMsgHeader", "returnReasonCode")
    shape = operation["shape"]
    if shape == "vworld":
        require(root.get("status") in {"OK", "NOT_FOUND"}, "provider_vworld_rejected", blocked=True)
        if root["status"] == "NOT_FOUND":
            return [], 0
    elif error_code is not None or status is not None:
        code = str(error_code if error_code is not None else status).strip().upper()
        if code in {"03", "NO_DATA", "NODATA"}:
            return [], 0
        require(code in {"0", "00", "200", "OK", "SUCCESS", "NORMAL_SERVICE"},
                f"provider_{code}" if re.fullmatch(r"[0-9]{1,3}", code) else "provider_rejected",
                blocked=code in {"20", "21", "22", "30", "31", "32", "403"})
    if "itemsPath" in operation:
        rows = at(value, *operation["itemsPath"])
        total = nonnegative(at(value, *operation.get("totalPath", ["totalCount"])))
    elif shape == "local" and "data" in root:
        rows = root.get("data")
        total = nonnegative(root.get("matchCount"))
        require(status is not None or nonnegative(root.get("currentCount")) is not None,
                "response_local_count_missing")
    else:
        payload = root.get("body", root)
        require(isinstance(payload, dict), "response_body_invalid")
        rows = payload.get("items")
        total = nonnegative(payload.get("totalCount"))
        if isinstance(rows, dict) and "item" in rows:
            rows = rows["item"]
        require(status is not None or total is not None, "response_success_unverified")
    if isinstance(rows, dict) and len(rows) == 1 and "item" in rows:
        rows = rows["item"]
    if rows is None or rows == {} or rows == "":
        require(total == 0, "response_items_missing")
        return [], total
    if isinstance(rows, dict):
        rows = [rows]
    require(isinstance(rows, list) and all(isinstance(row, dict) for row in rows),
            "response_items_invalid")
    return rows, total


def parcel_shape(value):
    if value is None:
        return None
    require(isinstance(value, dict) and value.get("type") in {"Polygon", "MultiPolygon"},
            "parcel_geometry_invalid")
    geom_type = value["type"]
    rings = value.get("coordinates")
    polygons = [rings] if geom_type == "Polygon" else rings
    require(isinstance(polygons, list) and polygons, "parcel_geometry_invalid")
    clean = []
    for polygon in polygons:
        require(isinstance(polygon, list) and polygon, "parcel_geometry_invalid")
        cleaned_rings = []
        for ring in polygon:
            require(isinstance(ring, list) and len(ring) >= 4, "parcel_ring_invalid")
            points = []
            for vertex in ring:
                require(isinstance(vertex, list) and len(vertex) == 2, "parcel_point_invalid")
                try:
                    lon, lat = map(float, vertex)
                except (ValueError, TypeError, OverflowError):
                    raise IntakeError("parcel_point_invalid") from None
                require(math.isfinite(lon) and math.isfinite(lat) and 124 <= lon <= 132 and 32 <= lat <= 40,
                        "parcel_point_invalid")
                points.append([lon, lat])
            require(points[0] == points[-1], "parcel_ring_not_closed")
            cleaned_rings.append(points)
        clean.append(cleaned_rings)
    return {"type": geom_type, "coordinates": clean[0] if geom_type == "Polygon" else clean}


def expanded_rows(operation, rows):
    group = operation.get("detailGroup")
    for row in rows:
        if not group or group[0] not in row:
            yield row
            continue
        details = at(row, *group)
        if isinstance(details, dict):
            details = [details]
        require(isinstance(details, list) and details and all(isinstance(d, dict) for d in details),
                "response_detail_group_missing")
        parent = {k: v for k, v in row.items() if k != group[0]}
        for detail in details:
            require(not (set(detail) & set(parent)), "response_detail_ambiguous")
            yield {**parent, **detail}


def decode_page(operation, body, private_values=()):
    rows, total = rows_from_response(operation, response_object(body))
    rows_by_key = {}
    for raw in expanded_rows(operation, rows):
        properties = raw.get("properties") if operation["shape"] == "vworld" else raw
        require(isinstance(properties, dict), "response_properties_invalid")
        clean = {}
        for name in operation["keepFields"]:
            value = properties.get(name)
            if value is not None:
                require(isinstance(value, (str, int, bool)) and len(str(value)) <= 200_000,
                        "response_field_invalid")
                clean[name] = str(value).strip()
        if operation["shape"] == "vworld":
            clean["geometry"] = parcel_shape(raw.get("geometry"))
        key = next((tuple(clean[name] for name in combination) for combination in operation["keyFields"]
                    if all(field(clean, name) for name in combination)), None)
        if key is None and operation["id"] == "trade_sido" and clean.get("priodTitle") == "총계":
            key = ("총계",)
        require(key is not None, "response_stable_key_missing")
        require(key not in rows_by_key, "response_duplicate_key")
        rows_by_key[key] = clean
    normalized = sorted(rows_by_key.items())
    ensure_no_secret(normalized, private_values)
    return normalized, len(rows), total

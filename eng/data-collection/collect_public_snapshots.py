"""Collect bounded public raw snapshots; never import into product databases.

The manifest is the explicit URL allowlist. No credentials, redirects, retries,
provider activation, subprocesses or interpretation of operating data are used.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import csv
from datetime import datetime, timedelta, timezone
import hashlib
import io
import ipaddress
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import socket
import ssl
import stat
import tempfile
import time
from urllib import error, parse, request
import uuid
import zipfile

from seoul_activity import validate_seoul_csv


UTC = timezone.utc
KST = timezone(timedelta(hours=9))
HARD_BYTES = 20 * 1024 * 1024
HARD_EXPANDED = 20 * 1024 * 1024
HARD_RECORDS = 100_000
AUTH_KEYS = {"key", "apikey", "servicekey", "authkey", "authorization", "auth",
             "accesstoken", "refreshtoken", "token", "secret", "clientsecret",
             "password", "registrationkey", "regkey"}
SLUG = re.compile(r"^[a-z0-9][a-z0-9._-]{0,79}$")
RESERVED = {"con", "prn", "aux", "nul", *(f"com{i}" for i in range(1, 10)),
            *(f"lpt{i}" for i in range(1, 10))}


class CollectionError(Exception):
    def __init__(self, code, *, blocked=False, received=0):
        super().__init__(code)
        self.code = code
        self.blocked = blocked
        self.received = received


def slug(value):
    if (not isinstance(value, str) or not SLUG.fullmatch(value) or ".." in value
            or value.split(".")[0] in RESERVED or value in {"runs", "bodies"}):
        raise CollectionError("unsafe_identifier", blocked=True)
    return value


def beneath(root, *parts):
    root = Path(root).resolve()
    result = root.joinpath(*parts).resolve()
    if not result.is_relative_to(root):
        raise CollectionError("unsafe_output_path", blocked=True)
    return result


def output_directory(manifest_path, manifest):
    workspace = manifest_path.parent
    for parent in [manifest_path.parent, *manifest_path.parents]:
        if (parent / ".git").exists() or (parent / "AGENTS.md").is_file():
            workspace = parent.resolve()
            break
    value = manifest.get("outputRoot", "artifacts/local/data-collection-runs")
    if not isinstance(value, str) or Path(value).is_absolute() or "\\" in value or ":" in value:
        raise CollectionError("unsafe_output_root", blocked=True)
    result = (manifest_path.parent / value).resolve()
    allowed = (workspace / "artifacts/local/data-collection-runs").resolve()
    if not allowed.is_relative_to(workspace) or not result.is_relative_to(allowed):
        raise CollectionError("unsafe_output_root", blocked=True)
    return result


def aware_time(value):
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (ValueError, TypeError, AttributeError):
        raise CollectionError("invalid_timestamp") from None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise CollectionError("timestamp_timezone_required")
    return parsed.astimezone(UTC)


def stamp(now):
    return now.astimezone(UTC).isoformat().replace("+00:00", "Z")


def positive_limit(value, maximum, code):
    if isinstance(value, bool) or not isinstance(value, int) or not 0 < value <= maximum:
        raise CollectionError(code, blocked=True)
    return value


def no_credentials(value):
    if isinstance(value, dict):
        for key, item in value.items():
            if re.sub(r"[^a-z0-9]", "", str(key).lower()) in AUTH_KEYS:
                raise CollectionError("credential_field_forbidden", blocked=True)
            no_credentials(item)
    elif isinstance(value, list):
        for item in value:
            no_credentials(item)


def validate_url(url, allowed_urls):
    if not isinstance(url, str) or any(ord(c) < 33 for c in url):
        raise CollectionError("unsafe_url", blocked=True)
    try:
        parsed = parse.urlsplit(url)
        port = parsed.port
    except ValueError:
        raise CollectionError("unsafe_url", blocked=True) from None
    if (parsed.scheme != "https" or not parsed.hostname or parsed.username is not None
            or parsed.password is not None or parsed.fragment or port not in (None, 443)):
        raise CollectionError("unsafe_url", blocked=True)
    host = parsed.hostname.rstrip(".").lower()
    if host in {"localhost", "localhost.localdomain"} or "." not in host:
        raise CollectionError("private_address_forbidden", blocked=True)
    try:
        if not ipaddress.ip_address(host).is_global:
            raise CollectionError("private_address_forbidden", blocked=True)
    except ValueError:
        pass
    decoded_path = parsed.path
    for _ in range(3):
        decoded_path = parse.unquote(decoded_path)
    if ("\\" in decoded_path or any(ord(c) < 32 for c in decoded_path)
            or ".." in PurePosixPath(decoded_path).parts):
        raise CollectionError("unsafe_url_path", blocked=True)
    for key, _ in parse.parse_qsl(parsed.query, keep_blank_values=True):
        for _ in range(2):
            key = parse.unquote(key)
        if re.sub(r"[^a-z0-9]", "", key.lower()) in AUTH_KEYS:
            raise CollectionError("credential_query_forbidden", blocked=True)
    if not isinstance(allowed_urls, list) or url not in allowed_urls:
        raise CollectionError("url_not_allowlisted", blocked=True)
    return parsed


def public_dns(host):
    try:
        answers = socket.getaddrinfo(host, 443, type=socket.SOCK_STREAM)
    except OSError:
        raise CollectionError("dns_failed") from None
    if not answers or any(not ipaddress.ip_address(answer[4][0]).is_global for answer in answers):
        raise CollectionError("private_address_forbidden", blocked=True)


def year_tokens(value, now):
    if isinstance(value, dict):
        return {key: year_tokens(item, now) for key, item in value.items()}
    if isinstance(value, list):
        return [year_tokens(item, now) for item in value]
    if isinstance(value, str):
        year = now.astimezone(KST).year
        if value == "{{previousYear}}":
            return str(year - 1)
        if value == "{{currentYear}}":
            return str(year)
        if "{{" in value or "}}" in value:
            raise CollectionError("unsupported_template", blocked=True)
    return value


class NoRedirect(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def opener():
    return request.build_opener(NoRedirect(), request.HTTPSHandler(context=ssl.create_default_context()))


def safe_header(headers, name):
    value = headers.get(name)
    return value if isinstance(value, str) and len(value) <= 512 and not any(ord(c) < 32 for c in value) else None


def fetch(source, previous, timeout, max_bytes, now, *, open_client=None, resolve_dns=True):
    deadline = time.monotonic() + timeout
    method = source.get("method", "GET")
    if method not in {"GET", "POST"} or (source.get("json") is not None and source.get("form") is not None):
        raise CollectionError("invalid_request_method", blocked=True)
    data = None
    headers = {"User-Agent": "Hongdal-PublicSnapshot/1.0", "Accept-Encoding": "identity"}
    no_credentials(source.get("json"))
    no_credentials(source.get("form"))
    if method == "GET" and (source.get("json") is not None or source.get("form") is not None):
        raise CollectionError("get_body_forbidden", blocked=True)
    if source.get("json") is not None:
        if not isinstance(source["json"], dict):
            raise CollectionError("invalid_request_body", blocked=True)
        data = json.dumps(year_tokens(source["json"], now), ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    elif source.get("form") is not None:
        if not isinstance(source["form"], dict):
            raise CollectionError("invalid_request_body", blocked=True)
        data = parse.urlencode(year_tokens(source["form"], now)).encode("utf-8")
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    if data and len(data) > 65536:
        raise CollectionError("request_body_too_large", blocked=True)
    if previous:
        for name, key in [("If-None-Match", "etag"), ("If-Modified-Since", "lastModified")]:
            value = safe_header({name: previous.get(key)}, name)
            if value:
                headers[name] = value
    if resolve_dns:
        public_dns(parse.urlsplit(source["url"]).hostname)
    req = request.Request(source["url"], data=data, headers=headers, method=method)
    client = open_client if open_client is not None else opener()
    if time.monotonic() >= deadline:
        raise CollectionError("request_timeout")
    try:
        response = client.open(req, timeout=max(0.001, deadline - time.monotonic()))
    except error.HTTPError as exc:
        if exc.code == 304:
            headers = exc.headers
            exc.close()
            if time.monotonic() >= deadline:
                raise CollectionError("request_timeout")
            return 304, b"", headers
        if 300 <= exc.code < 400:
            raise CollectionError("redirect_forbidden", blocked=True) from None
        raise CollectionError(f"http_{exc.code}", blocked=exc.code in {401, 403, 429}) from None
    except TimeoutError:
        raise CollectionError("request_timeout") from None
    except (error.URLError, OSError):
        raise CollectionError("network_failed") from None
    with response:
        if time.monotonic() >= deadline:
            raise CollectionError("request_timeout")
        code = response.status
        if code == 304:
            return code, b"", response.headers
        if 300 <= code < 400:
            raise CollectionError("redirect_forbidden", blocked=True)
        if code != 200:
            raise CollectionError(f"http_{code}")
        if response.geturl() != source["url"]:
            raise CollectionError("response_url_changed", blocked=True)
        encoding = response.headers.get("Content-Encoding", "identity").lower()
        if encoding not in {"", "identity"}:
            raise CollectionError("encoded_response_forbidden")
        length = response.headers.get("Content-Length")
        if length is not None:
            try:
                length = int(length)
            except (ValueError, TypeError):
                raise CollectionError("invalid_content_length") from None
            if length < 0 or length > max_bytes:
                raise CollectionError("response_too_large")
        chunks, total = [], 0
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise CollectionError("request_timeout", received=total)
            # HTTPResponse.fp.raw._sock is the underlying TLS socket. read1
            # avoids waiting to fill a full chunk while a peer drips bytes.
            socket_handle = getattr(getattr(getattr(response, "fp", None), "raw", None), "_sock", None)
            if socket_handle is not None:
                socket_handle.settimeout(remaining)
            try:
                chunk = response.read1(min(65536, max_bytes - total + 1))
            except TimeoutError:
                raise CollectionError("request_timeout", received=total) from None
            if time.monotonic() >= deadline:
                raise CollectionError("request_timeout", received=total + len(chunk))
            if not chunk:
                break
            total += len(chunk)
            if total > max_bytes:
                raise CollectionError("response_too_large", received=total)
            chunks.append(chunk)
        return code, b"".join(chunks), response.headers


def as_json(body):
    try:
        return json.loads(body.decode("utf-8-sig"))
    except (UnicodeError, ValueError):
        raise CollectionError("invalid_json") from None


def numeric(value):
    if isinstance(value, bool):
        return False
    try:
        return math.isfinite(float(value))
    except (ValueError, TypeError, OverflowError):
        return False


def evidence_date(value):
    if not isinstance(value, str):
        return None
    try:
        if re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
            datetime.strptime(value, "%Y-%m-%d")
        else:
            aware_time(value)
        return value
    except (ValueError, CollectionError):
        return None


def worldbank_contract(source):
    """허용한 한국 연간 지표의 단위 계약만 선택한다. 경지면적의 기존 기본값을 유지한다."""
    indicator = source.get("indicatorId", "AG.LND.ARBL.HA")
    contracts = {"AG.LND.ARBL.HA": ("ha", "hectares"),
                 "PA.NUS.FCRF": ("KRW_per_USD", "KRW_per_USD"),
                 "TX.VAL.MRCH.CD.WT": ("USD_current", "USD_current")}
    if not isinstance(indicator, str) or indicator not in contracts:
        raise CollectionError("worldbank_indicator_not_allowed", blocked=True)
    configured_unit, result_unit = contracts[indicator]
    unit = source.get("unit", "ha" if indicator == "AG.LND.ARBL.HA" else None)
    if unit != configured_unit:
        raise CollectionError("worldbank_unit_contract_invalid", blocked=True)
    return indicator, configured_unit, result_unit


def validate_worldbank(body, source, max_records):
    indicator, configured_unit, result_unit = worldbank_contract(source)
    data = as_json(body)
    if not (isinstance(data, list) and len(data) == 2 and isinstance(data[0], dict)
            and isinstance(data[1], list) and 0 < len(data[1]) <= max_records):
        raise CollectionError("worldbank_shape_invalid")
    updated = evidence_date(data[0].get("lastupdated"))
    if not updated:
        raise CollectionError("worldbank_asof_missing")
    valid, years, numeric_years = 0, set(), set()
    for row in data[1]:
        if (not isinstance(row, dict) or row.get("countryiso3code") != "KOR"
                or not isinstance(row.get("indicator"), dict)
                or row["indicator"].get("id") != indicator
                or not isinstance(row.get("date"), str)
                or not re.fullmatch(r"\d{4}", row["date"])):
            raise CollectionError("worldbank_identity_invalid")
        # 세계은행의 이 세 지표는 unit=""를 반환한다. 빈 단위는 고정 지표 계약으로만 보완한다.
        if row.get("unit", "") not in ("", configured_unit, result_unit):
            raise CollectionError("worldbank_unit_invalid")
        if row["date"] in years:
            raise CollectionError("worldbank_year_duplicate")
        years.add(row["date"])
        if row.get("value") is not None:
            if (not isinstance(row["value"], (int, float)) or not numeric(row["value"])
                    or row["value"] < 0):
                raise CollectionError("worldbank_value_invalid")
            valid += 1
            numeric_years.add(row["date"])
    if not valid:
        raise CollectionError("worldbank_no_numeric_observation")
    return {"recordCount": len(data[1]), "numericRecords": valid,
            "evidenceAsOf": max(numeric_years), "evidenceAsOfPrecision": "year",
            "sourceUpdatedAt": updated, "observationYears": sorted(years),
            "numericObservationYears": sorted(numeric_years), "unit": result_unit}


def validate_bls(body, source, max_records):
    data = as_json(body)
    allowed = source.get("seriesIds")
    if (not isinstance(allowed, list) or not allowed or any(not isinstance(item, str) or
            not re.fullmatch(r"APU[A-Z0-9]{5,40}", item) for item in allowed)):
        raise CollectionError("bls_series_allowlist_required")
    unit = source.get("unit")
    if not isinstance(unit, str) or not unit.strip():
        raise CollectionError("bls_unit_required")
    results = data.get("Results") if isinstance(data, dict) else None
    series = results.get("series") if isinstance(results, dict) else None
    if not isinstance(data, dict) or data.get("status") != "REQUEST_SUCCEEDED" or not isinstance(series, list) or not series:
        raise CollectionError("bls_response_invalid")
    count, numeric_count, observed, periods, numeric_periods, missing_periods = 0, 0, set(), set(), set(), set()
    for item in series:
        if not isinstance(item, dict):
            raise CollectionError("bls_series_identity_invalid")
        identity = item.get("seriesID")
        if identity not in allowed or identity in observed:
            raise CollectionError("bls_series_identity_invalid")
        observed.add(identity)
        rows = item.get("data")
        if not isinstance(rows, list) or not rows:
            raise CollectionError("bls_no_observations")
        for row in rows:
            if (not isinstance(row, dict) or not isinstance(row.get("year"), str)
                    or not re.fullmatch(r"\d{4}", row["year"])
                    or not re.fullmatch(r"M(?:0[1-9]|1[0-2])", str(row.get("period", "")))
                    or (row.get("value") != "-" and not numeric(row.get("value")))):
                raise CollectionError("bls_monthly_observation_invalid")
            period = row["year"] + "-" + row["period"][1:]
            try:
                datetime.strptime(period, "%Y-%m")
            except ValueError:
                raise CollectionError("bls_monthly_observation_invalid") from None
            periods.add(period)
            if row.get("value") == "-":
                missing_periods.add(period)
            else:
                numeric_count += 1
                numeric_periods.add(period)
            count += 1
            if count > max_records:
                raise CollectionError("record_limit_exceeded")
    if observed != set(allowed):
        raise CollectionError("bls_requested_series_missing")
    if numeric_count == 0:
        raise CollectionError("bls_no_numeric_observation")
    return {"recordCount": count, "numericRecords": numeric_count, "missingRecords": count - numeric_count,
            "missingValueMarker": "-", "seriesCount": len(observed),
            "evidenceAsOf": max(numeric_periods), "evidenceAsOfPrecision": "month",
            "sourceUpdatedAt": None, "observationPeriods": sorted(periods),
            "numericObservationPeriods": sorted(numeric_periods),
            "missingObservationPeriods": sorted(missing_periods), "unit": unit}


def validate_legal_zip(body, source, max_records):
    if len(body) > 5 * 1024 * 1024:
        raise CollectionError("zip_compressed_limit_exceeded")
    max_expanded = positive_limit(source.get("maxExpandedBytes", HARD_EXPANDED), HARD_EXPANDED,
                                  "invalid_expanded_limit")
    try:
        archive = zipfile.ZipFile(io.BytesIO(body))
        infos = archive.infolist()
        if len(infos) != 1 or infos[0].is_dir() or not infos[0].filename.lower().endswith(".txt"):
            raise CollectionError("zip_members_invalid")
        total, count, codes = 0, 0, set()
        for info in infos:
            name = info.filename.replace("\\", "/")
            if (PurePosixPath(name).is_absolute() or ".." in PurePosixPath(name).parts or ":" in name
                    or stat.S_ISLNK(info.external_attr >> 16) or info.flag_bits & 1):
                raise CollectionError("zip_unsafe_member")
            if info.is_dir():
                continue
            total += info.file_size
            if total > max_expanded:
                raise CollectionError("zip_expanded_limit_exceeded")
            with archive.open(info) as member:
                expanded = member.read(info.file_size + 1)
            if len(expanded) != info.file_size:
                raise CollectionError("zip_member_size_invalid")
            try:
                text = expanded.decode("cp949")
            except UnicodeError:
                raise CollectionError("legal_encoding_invalid") from None
            rows = csv.reader(io.StringIO(text), delimiter="\t")
            header = next(rows, [])
            header = [item.strip().lstrip("\ufeff") for item in header]
            required = ["법정동코드", "법정동명", "폐지여부"]
            if header != required:
                raise CollectionError("legal_headers_invalid")
            indexes = [header.index(key) for key in required]
            for row in rows:
                if not row or not any(value.strip() for value in row):
                    continue
                if (len(row) != 3 or not re.fullmatch(r"\d{10}", row[indexes[0]].strip())
                        or not row[indexes[1]].strip() or row[indexes[2]].strip() not in {"존재", "폐지"}):
                    raise CollectionError("legal_row_invalid")
                code = row[indexes[0]].strip()
                if code in codes:
                    raise CollectionError("legal_code_duplicate")
                codes.add(code)
                count += 1
                if count > min(max_records, HARD_RECORDS):
                    raise CollectionError("record_limit_exceeded")
        if not count:
            raise CollectionError("legal_no_records")
        return {"recordCount": count, "expandedBytes": total, "evidenceAsOf": None}
    except (zipfile.BadZipFile, RuntimeError, OSError, NotImplementedError):
        raise CollectionError("invalid_zip") from None
    finally:
        if "archive" in locals():
            archive.close()


def validate_catalog(body, source, max_records):
    data = as_json(body)
    # The official portal publishes JSON-LD Dataset metadata. Its verified URL
    # can bind the dataset identity when an identifier property is absent.
    if isinstance(data, dict) and "@type" in data:
        types = data["@type"] if isinstance(data["@type"], list) else [data["@type"]]
        dataset_id = source.get("datasetId")
        allowed_metadata = {f"https://www.data.go.kr/data/{dataset_id}/{kind}.do"
                            for kind in ["fileData", "openapi"]}
        if (not isinstance(dataset_id, str) or not re.fullmatch(r"\d{8}", dataset_id)
                or not any(isinstance(value, str) and value.rstrip("/").split("/")[-1] == "Dataset" for value in types)
                or not isinstance(data.get("name"), str) or not data["name"].strip()
                or data.get("url") not in allowed_metadata
                or data.get("identifier", dataset_id) != dataset_id):
            raise CollectionError("catalog_dataset_binding_invalid")
        return {"recordCount": 1, "evidenceAsOf": evidence_date(data.get("dateModified")),
                "catalogDatasetId": dataset_id}
    if isinstance(data, dict):
        rows = next((data[key] for key in ["datasets", "sources", "apis", "items", "records", "services", "entries", "results", "data"]
                     if isinstance(data.get(key), list)), [data])
    else:
        rows = data
    if not isinstance(rows, list) or not 0 < len(rows) <= max_records:
        raise CollectionError("catalog_shape_invalid")
    ids = ["id", "identifier", "sourceId", "datasetId", "key", "serviceId", "apiId"]
    names = ["name", "title", "sourceName", "datasetName", "serviceName"]
    for row in rows:
        if (not isinstance(row, dict) or not any(isinstance(row.get(key), (str, int)) and
                not isinstance(row[key], bool) and str(row[key]).strip() for key in ids)
                or not any(isinstance(row.get(key), str) and row[key].strip() for key in names)):
            raise CollectionError("catalog_identity_invalid")
    asof = None
    if isinstance(data, dict):
        asof = next((evidence_date(data.get(key)) for key in ["lastupdated", "lastUpdated", "updatedAt", "modifiedAt", "dateModified"]
                     if evidence_date(data.get(key))), None)
    return {"recordCount": len(rows), "evidenceAsOf": asof}


def validate_html(body, source, max_records):
    if source.get("kind") != "metadata":
        raise CollectionError("html_metadata_only")
    text = body[:65536].decode("utf-8", errors="replace").lower()
    if len(body) < 40 or not re.search(r"<!doctype\s+html|<html(?:\s|>)", text):
        raise CollectionError("html_body_invalid")
    title = re.search(r"<title[^>]*>(.*?)</title>", text, flags=re.S)
    if title and re.search(r"access denied|forbidden|robot check|captcha", title.group(1)):
        raise CollectionError("html_provider_challenge", blocked=True)
    return {"recordCount": 0, "evidenceAsOf": None, "metadataDocument": True}


def validate_activity_csv(body, source, max_records):
    return validate_seoul_csv(body, source, max_records, error_type=CollectionError)


def validate_business_license_csv(body, source, max_records):
    """인허가 원문행을 보존하며 기관·헤더를 검증하고 결측/중복을 요약한다."""
    def fail(code):
        raise CollectionError(code)

    def column_list(key, default=None):
        value = source.get(key, [] if default is None else default)
        if (not isinstance(value, list) or any(not isinstance(x, str) or not x or x != x.strip()
                                              for x in value) or len(value) != len(set(value))):
            fail("license_csv_config_invalid")
        return value

    if not isinstance(body, bytes) or not isinstance(source, dict):
        fail("license_csv_config_invalid")
    if isinstance(max_records, bool) or not isinstance(max_records, int) or not 0 < max_records <= HARD_RECORDS:
        fail("invalid_record_limit")
    exact = column_list("exactHeader")
    expected_hash = source.get("headerSha256")
    if (not exact and expected_hash is None) or (expected_hash is not None and
            (not isinstance(expected_hash, str) or not re.fullmatch(r"[0-9a-fA-F]{64}", expected_hash))):
        fail("license_csv_header_contract_required")
    required = column_list("requiredColumns")
    agency_column = source.get("agencyColumn", "개방자치단체코드")
    management = source.get("managementIdColumn", "관리번호")
    agency = source.get("expectedAgencyCode")
    if (not required or not isinstance(agency, str) or not re.fullmatch(r"[0-9]{7}", agency)
            or not all(isinstance(x, str) and x and x == x.strip() for x in [agency_column, management])):
        fail("license_csv_config_invalid")
    dates = column_list("dateColumns", ["인허가일자", "폐업일자", "최종수정일자", "데이터갱신일자"])
    statuses = column_list("statusColumns", ["영업상태코드", "영업상태명", "상세영업상태코드", "상세영업상태명"])
    numbers = column_list("numericColumns")
    pairs = source.get("coordinatePairs", [["좌표정보(X)", "좌표정보(Y)"], ["X좌표", "Y좌표"]])
    if (not isinstance(pairs, list) or any(not isinstance(pair, list) or len(pair) != 2
            or any(not isinstance(x, str) or not x or x != x.strip() for x in pair)
            or pair[0] == pair[1] for pair in pairs)):
        fail("license_csv_config_invalid")
    configured_encoding = source.get("encoding", "auto")
    if configured_encoding not in {"auto", "cp949", "utf-8", "utf-8-sig"}:
        fail("license_csv_config_invalid")
    candidates = (["utf-8-sig"] if body.startswith(b"\xef\xbb\xbf") else ["utf-8", "cp949"]) if configured_encoding == "auto" else [configured_encoding]
    for encoding in candidates:
        try:
            text = body.decode(encoding, errors="strict")
            break
        except UnicodeError:
            continue
    else:
        fail("license_csv_encoding_invalid")
    if not text.strip():
        fail("license_csv_empty")
    if "\x00" in text or text.lstrip().startswith(("<", "{", "[")):
        fail("license_csv_not_csv")
    count = blank = 0
    keys = {}
    date_summary = {k: {"missing": 0, "invalid": 0, "minimum": None, "maximum": None,
                        "invalidSourceRows": []} for k in dates}
    status_summary = {k: {"missing": 0, "values": {}} for k in statuses}
    numeric_summary = {k: {"missing": 0, "invalid": 0, "zero": 0, "negative": 0,
                           "invalidSourceRows": []} for k in sorted(set(numbers + [x for pair in pairs for x in pair]))}
    pair_summary = [{"columns": pair, "missingBoth": 0, "missingOne": 0, "invalid": 0,
                     "zeroBoth": 0, "finiteNonzero": 0} for pair in pairs]

    def number(value):
        if not value:
            return None
        try:
            parsed = float(value)
            return parsed if math.isfinite(parsed) else "invalid"
        except (ValueError, OverflowError):
            return "invalid"

    try:
        rows = csv.DictReader(io.StringIO(text, newline=""), strict=True)
        header = rows.fieldnames
        if (not header or len(header) != len(set(header)) or any(not x or x != x.strip() for x in header)):
            fail("license_csv_headers_invalid")
        digest = hashlib.sha256(json.dumps(header, ensure_ascii=False, separators=(",", ":")).encode("utf-8")).hexdigest()
        if (exact and header != exact) or (expected_hash and digest != expected_hash.lower()):
            fail("license_csv_headers_mismatch")
        configured = [*required, agency_column, management, *dates, *statuses, *numeric_summary]
        if not set(configured).issubset(header):
            fail("license_csv_required_columns_missing")
        for source_row, row in enumerate(rows, 2):
            if None in row or any(value is None for value in row.values()):
                fail("license_csv_row_shape_invalid")
            values = {k: v.strip() for k, v in row.items()}
            if not any(values.values()):
                blank += 1
                continue
            count += 1
            if count > max_records:
                fail("record_limit_exceeded")
            if values[agency_column] != agency:
                fail("license_csv_agency_mismatch")
            if not values[management]:
                fail("license_csv_management_id_empty")
            keys[values[management]] = keys.get(values[management], 0) + 1
            for k, summary in date_summary.items():
                value = values[k]
                if not value:
                    summary["missing"] += 1
                    continue
                try:
                    parsed = (datetime.strptime(value, "%Y%m%d") if re.fullmatch(r"[0-9]{8}", value)
                              else datetime.fromisoformat(value.replace("Z", "+00:00")))
                    normalized = parsed.isoformat()
                    summary["minimum"] = min(summary["minimum"] or normalized, normalized)
                    summary["maximum"] = max(summary["maximum"] or normalized, normalized)
                except (ValueError, OverflowError):
                    summary["invalid"] += 1
                    if len(summary["invalidSourceRows"]) < 5:
                        summary["invalidSourceRows"].append(source_row)
            for k, summary in status_summary.items():
                value = values[k]
                if not value:
                    summary["missing"] += 1
                else:
                    summary["values"][value] = summary["values"].get(value, 0) + 1
            for k, summary in numeric_summary.items():
                value = number(values[k])
                if value is None:
                    summary["missing"] += 1
                elif value == "invalid":
                    summary["invalid"] += 1
                    if len(summary["invalidSourceRows"]) < 5:
                        summary["invalidSourceRows"].append(source_row)
                else:
                    summary["zero"] += value == 0
                    summary["negative"] += value < 0
            for summary in pair_summary:
                x, y = [number(values[k]) for k in summary["columns"]]
                if x is None and y is None:
                    summary["missingBoth"] += 1
                elif x is None or y is None:
                    summary["missingOne"] += 1
                elif x == "invalid" or y == "invalid":
                    summary["invalid"] += 1
                elif x == 0 and y == 0:
                    summary["zeroBoth"] += 1
                else:
                    summary["finiteNonzero"] += 1
    except csv.Error:
        fail("license_csv_syntax_invalid")
    if not count:
        fail("license_csv_no_records")
    return {"recordCount": count, "evidenceAsOf": None, "observationPeriods": [],
            "unit": source.get("unit"), "spatialUnit": source.get("spatialUnit"), "columns": header,
            "encoding": encoding, "headerSha256": digest, "expectedAgencyCode": agency,
            "uniqueManagementIds": len(keys), "duplicateManagementRows": count - len(keys),
            "duplicateManagementGroups": sum(v > 1 for v in keys.values()), "blankRecordRows": blank,
            "duplicatesPreserved": True, "dateSummary": date_summary, "statusSummary": status_summary,
            "numericSummary": numeric_summary, "coordinateSummary": pair_summary,
            "coordinateCrsVerified": False, "reportedStatusIsFieldConfirmed": False,
            "missingValuesFilled": False}


VALIDATORS = {"worldbank": validate_worldbank, "bls": validate_bls,
              "legal_dong_zip": validate_legal_zip, "catalog_json": validate_catalog,
              "html": validate_html, "seoul_activity_csv": validate_activity_csv,
              "seoul_business_license_csv": validate_business_license_csv}


def policy_fingerprint(source, now):
    """Include the effective request and validation contract, preserving old hashes."""
    if source.get("validator") == "worldbank":
        worldbank_contract(source)
    value = {"sourceId": source["sourceId"], "datasetId": source["datasetId"],
             "url": source["url"], "method": source.get("method", "GET"),
             "json": year_tokens(source.get("json"), now),
             "form": year_tokens(source.get("form"), now),
             "kind": source["kind"], "validator": source["validator"],
             "seriesIds": source.get("seriesIds"), "unit": source.get("unit"),
             "validationPolicyVersion": source.get("validationPolicyVersion", "initial-v1"),
             "policyRevision": source.get("policyRevision", "initial-v1")}
    if source.get("validator") == "seoul_activity_csv":
        value["csvPolicy"] = {key: source.get(key) for key in (
            "requiredColumns", "periodColumn", "periodFormat", "expectedPeriod",
            "idColumns", "numericColumns", "spatialUnit")}
    if source.get("validator") == "worldbank" and "indicatorId" in source:
        value["worldbankPolicy"] = {"indicatorId": source["indicatorId"]}
    if source.get("validator") == "seoul_business_license_csv":
        value["licensePolicy"] = {key: source.get(key) for key in (
            "exactHeader", "headerSha256", "requiredColumns", "encoding", "agencyColumn",
            "expectedAgencyCode", "managementIdColumn", "dateColumns", "statusColumns",
            "coordinatePairs", "numericColumns", "spatialUnit", "countDiscovery",
            "attemptCooldownHours", "intervalHours", "maxRecords", "maxBytes")}
    return hashlib.sha256(json.dumps(value, sort_keys=True,
        ensure_ascii=False, separators=(",", ":")).encode("utf-8")).hexdigest()


def license_count_contract(source, allowed, max_records):
    discovery = source.get("countDiscovery")
    if not isinstance(discovery, dict) or set(discovery) != {"url", "maxBytes"}:
        raise CollectionError("license_count_discovery_required", blocked=True)
    validate_url(discovery["url"], allowed)
    parsed = parse.urlsplit(discovery["url"])
    query = parse.parse_qs(parsed.query, keep_blank_values=True)
    if (parsed.hostname != "data.seoul.go.kr" or parsed.path != "/dataList/dataView.do"
            or query.get("infId") != [source.get("datasetId")]
            or query.get("filterCol") != ["OPNSFTEAMCODE"]
            or query.get("txtFilter") != [source.get("expectedAgencyCode")]
            or query.get("onepagerow") != ["1"]):
        raise CollectionError("license_count_scope_mismatch", blocked=True)
    form = source.get("form", {})
    if (source.get("method") != "POST" or form.get("gridTotalCnt") != "__DISCOVERED_COUNT__"
            or form.get("infId") != source.get("datasetId")
            or form.get("filterCol") != "OPNSFTEAMCODE"
            or form.get("txtFilter") != source.get("expectedAgencyCode")):
        raise CollectionError("license_download_scope_mismatch", blocked=True)
    cap = positive_limit(discovery["maxBytes"], 262144, "invalid_discovery_byte_limit")
    cooldown = source.get("attemptCooldownHours", 24)
    if isinstance(cooldown, bool) or not isinstance(cooldown, (int, float)) or not math.isfinite(cooldown) or cooldown < 24:
        raise CollectionError("invalid_license_attempt_cooldown", blocked=True)
    if source.get("intervalHours", 24) < 24:
        raise CollectionError("license_interval_too_short", blocked=True)
    return cap, cooldown


def discover_license_count(source, source_dir, limits, budget, now, open_client, resolve_dns):
    remaining = limits["maxTotalBytes"] - budget["bytes"]
    if budget["requests"] + 2 > limits["maxRequests"] or remaining <= 0:
        raise CollectionError("job_request_or_byte_budget_reached", blocked=True)
    budget["requests"] += 1
    code, body, _ = fetch({"url": source["countDiscovery"]["url"], "method": "GET"}, None,
        limits["timeoutSeconds"], min(source["countDiscovery"]["maxBytes"], remaining), now,
        open_client=open_client, resolve_dns=resolve_dns)
    budget["bytes"] += len(body)
    if code != 200:
        raise CollectionError("license_count_response_invalid")
    try:
        text = body.decode("utf-8-sig")
    except UnicodeError:
        raise CollectionError("license_count_encoding_invalid") from None
    matches = re.findall(r'''(?<!\w)["']?totalCount["']?\s*:\s*([^\s,;}\]]+)''', text)
    if (len(matches) != 1 or not re.fullmatch(r"[0-9]+", matches[0])
            or not 0 < int(matches[0]) <= source.get("maxRecords", HARD_RECORDS)):
        raise CollectionError("license_count_invalid_or_exceeds_limit")
    count = int(matches[0]); body_sha = hashlib.sha256(body).hexdigest()
    path = beneath(source_dir, "count-bodies", body_sha + ".raw")
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if hashlib.sha256(path.read_bytes()).hexdigest() != body_sha:
            raise CollectionError("existing_count_body_digest_mismatch")
    else:
        with path.open("xb") as stream:
            stream.write(body)
    return count, {"url": source["countDiscovery"]["url"], "sha256": body_sha,
                   "body": "count-bodies/" + body_sha + ".raw", "bytes": len(body),
                   "recordCount": count, "checkedAt": stamp(now), "agencyCode": source["expectedAgencyCode"]}


@contextmanager
def global_lock(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    stream = path.open("a+b")
    locked = False
    try:
        stream.seek(0, 2)
        if stream.tell() == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        try:
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            locked = True
        except OSError:
            pass
        yield locked
    finally:
        if locked:
            stream.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)
        stream.close()


def atomic_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent,
                                         suffix=".tmp", delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary and temporary.exists():
            temporary.unlink()


def previous_snapshot(source_dir):
    latest = beneath(source_dir, "latest.json")
    if not latest.exists():
        return None
    try:
        previous = json.loads(latest.read_text(encoding="utf-8"))
        digest = previous["sha256"]
        if not isinstance(digest, str) or not re.fullmatch(r"[a-f0-9]{64}", digest):
            raise CollectionError("previous_digest_invalid")
        body = beneath(source_dir, "bodies", digest + ".raw")
        if not body.is_file():
            raise CollectionError("previous_body_missing")
        if hashlib.sha256(body.read_bytes()).hexdigest() != digest:
            raise CollectionError("previous_body_digest_mismatch")
        aware_time(previous["collectedAt"])
        return previous
    except (ValueError, KeyError, TypeError):
        raise CollectionError("previous_snapshot_invalid") from None


def collect_source(source, source_dir, allowed, limits, now, budget, *, dry_run=False,
                   open_client=None, resolve_dns=True):
    result = {"id": source.get("id"), "sourceId": source.get("sourceId"),
              "datasetId": source.get("datasetId"), "kind": source.get("kind"),
              "collectedAt": None, "evidenceAsOf": None}
    if source.get("status") != "ready":
        reason = source.get("reasonCode", "source_not_ready")
        if not isinstance(reason, str) or not re.fullmatch(r"[a-zA-Z0-9_-]{1,96}", reason):
            reason = "source_not_ready"
        return dict(result, status="blocked", reason=reason)
    fingerprint = None
    try:
        slug(source["id"])
        if source.get("kind") not in {"dataset", "metadata"}:
            raise CollectionError("invalid_source_kind", blocked=True)
        if not all(isinstance(source.get(key), str) and source[key].strip() for key in ["sourceId", "datasetId"]):
            raise CollectionError("source_identity_required", blocked=True)
        validate_url(source.get("url"), allowed)
        validator = VALIDATORS.get(source.get("validator"))
        if validator is None:
            raise CollectionError("validator_required", blocked=True)
        max_bytes = positive_limit(source.get("maxBytes", 1024 * 1024), HARD_BYTES, "invalid_byte_limit")
        max_records = positive_limit(source.get("maxRecords", HARD_RECORDS), HARD_RECORDS, "invalid_record_limit")
        interval = source.get("intervalHours", 24)
        if isinstance(interval, bool) or not isinstance(interval, (int, float)) or not math.isfinite(interval) or interval < 0:
            raise CollectionError("invalid_interval", blocked=True)
        no_credentials(source.get("json"))
        no_credentials(source.get("form"))
        year_tokens(source.get("json"), now)
        year_tokens(source.get("form"), now)
        license_policy = source.get("validator") == "seoul_business_license_csv"
        if license_policy:
            _, attempt_cooldown = license_count_contract(source, allowed, max_records)
        # A refused identical request stays refused until the reviewed request or
        # explicit policy revision changes. Time passing is not new permission.
        fingerprint = policy_fingerprint(source, now)
        result["requestPolicyFingerprint"] = fingerprint
        blocked_path = beneath(source_dir, "blocked-outcome.json")
        if blocked_path.exists():
            blocked = json.loads(blocked_path.read_text(encoding="utf-8"))
            if blocked.get("requestPolicyFingerprint") == fingerprint:
                return dict(result, status="blocked", reason=blocked["reason"],
                            blockedAt=blocked["blockedAt"], reusedBlockedOutcome=True)
        previous = previous_snapshot(source_dir)
        reusable = previous if previous and previous.get("requestPolicyFingerprint") == fingerprint else None
        if reusable and now < aware_time(reusable["collectedAt"]) + timedelta(hours=interval):
            return dict(result, status="not_due", previousCollectedAt=reusable["collectedAt"],
                        evidenceAsOf=reusable.get("evidenceAsOf"))
        if license_policy:
            attempt_path = beneath(source_dir, "license-last-attempt.json")
            if attempt_path.exists():
                attempt = json.loads(attempt_path.read_text(encoding="utf-8"))
                if (attempt.get("requestPolicyFingerprint") == fingerprint
                        and now < aware_time(attempt["attemptedAt"]) + timedelta(hours=attempt_cooldown)):
                    return dict(result, status="not_due", reason="license_attempt_cooldown",
                                previousAttemptedAt=attempt["attemptedAt"])
        if dry_run:
            return dict(result, status="dry_run", reason="ready_no_request")
        effective_source, expected_count = source, None
        if license_policy:
            if budget["requests"] + 2 > limits["maxRequests"] or budget["bytes"] >= limits["maxTotalBytes"]:
                raise CollectionError("job_request_or_byte_budget_reached", blocked=True)
            atomic_json(attempt_path, {"requestPolicyFingerprint": fingerprint, "attemptedAt": stamp(now)})
            expected_count, result["countDiscovery"] = discover_license_count(
                source, source_dir, limits, budget, now, open_client, resolve_dns)
            effective_source = dict(source, form={**source["form"], "gridTotalCnt": str(expected_count)})
            result["resolvedDownloadRecordCount"] = expected_count
        remaining = limits["maxTotalBytes"] - budget["bytes"]
        if budget["requests"] >= limits["maxRequests"] or remaining <= 0:
            raise CollectionError("job_request_or_byte_budget_reached", blocked=True)
        budget["requests"] += 1
        code, body, headers = fetch(effective_source, reusable, limits["timeoutSeconds"], min(max_bytes, remaining),
                                    now, open_client=open_client, resolve_dns=resolve_dns)
        budget["bytes"] += len(body)
        if code == 304:
            if reusable is None:
                raise CollectionError("not_modified_without_previous_body")
            if expected_count is not None and reusable.get("recordCount") != expected_count:
                raise CollectionError("license_discovery_count_changed_304")
            pointer = dict(reusable, collectedAt=stamp(now), httpStatus=304)
            if license_policy:
                pointer.update(countDiscovery=result["countDiscovery"], resolvedDownloadRecordCount=expected_count)
            atomic_json(beneath(source_dir, "latest.json"), pointer)
            return dict(result, status="unchanged", sha256=reusable["sha256"], httpStatus=304,
                        collectedAt=stamp(now), evidenceAsOf=reusable.get("evidenceAsOf"),
                        recordCount=reusable.get("recordCount", 0),
                        **{key: reusable[key] for key in ["sourceUpdatedAt", "evidenceAsOfPrecision",
                            "observationYears", "numericObservationYears", "numericRecords", "unit", "seriesCount",
                            "missingRecords", "missingValueMarker", "observationPeriods",
                            "numericObservationPeriods", "missingObservationPeriods",
                            "spatialUnit", "columns", "encoding", "missingNumericCells"]
                           if key in reusable})
        validated = validator(body, source, max_records)
        if expected_count is not None and validated.get("recordCount") != expected_count:
            raise CollectionError("license_discovery_record_count_mismatch")
        digest = hashlib.sha256(body).hexdigest()
        body_path = beneath(source_dir, "bodies", digest + ".raw")
        body_path.parent.mkdir(parents=True, exist_ok=True)
        if body_path.exists():
            if hashlib.sha256(body_path.read_bytes()).hexdigest() != digest:
                raise CollectionError("existing_body_digest_mismatch")
        else:
            with body_path.open("xb") as stream:
                stream.write(body)
                stream.flush()
                os.fsync(stream.fileno())
        status = "unchanged" if reusable and reusable["sha256"] == digest else "saved"
        pointer = {**result, **validated, "sha256": digest, "body": "bodies/" + digest + ".raw",
                   "collectedAt": stamp(now), "httpStatus": code, "bytes": len(body),
                   "etag": safe_header(headers, "ETag"), "lastModified": safe_header(headers, "Last-Modified"),
                   "previousSha256": previous["sha256"] if previous and status == "saved" else
                   previous.get("previousSha256") if previous else None}
        atomic_json(beneath(source_dir, "latest.json"), pointer)
        return dict(result, **validated, status=status, sha256=digest, httpStatus=code,
                    collectedAt=stamp(now), bytes=len(body))
    except CollectionError as exc:
        budget["bytes"] += exc.received
        if not dry_run and fingerprint and exc.code in {"http_401", "http_403", "http_429",
                "redirect_forbidden", "html_provider_challenge"}:
            try:
                atomic_json(beneath(source_dir, "blocked-outcome.json"),
                    {"requestPolicyFingerprint": fingerprint, "reason": exc.code,
                     "blockedAt": stamp(now), "sourceId": source.get("sourceId"),
                     "datasetId": source.get("datasetId")})
            except OSError:
                return dict(result, status="failed", reason="blocked_outcome_storage_failed")
        return dict(result, status="blocked" if exc.blocked else "failed", reason=exc.code)
    except (OSError, ValueError, TypeError, KeyError, OverflowError):
        return dict(result, status="failed", reason="collection_or_storage_failed")


def summarize(results):
    summary = {"datasetSaved": 0, "datasetUnchanged": 0, "metadataSaved": 0,
               "metadataChecked": 0, "notDue": 0, "blocked": 0, "failed": 0, "dryRun": 0}
    for result in results:
        status = result["status"]
        if status in {"saved", "unchanged"}:
            if result["kind"] == "metadata":
                summary["metadataChecked"] += 1
                if status == "saved":
                    summary["metadataSaved"] += 1
            else:
                summary["datasetSaved" if status == "saved" else "datasetUnchanged"] += 1
        elif status in {"not_due", "blocked", "failed", "dry_run"}:
            summary[{"not_due": "notDue", "dry_run": "dryRun"}.get(status, status)] += 1
    return summary


def run(manifest_path, job_id, *, dry_run=False, now=None, open_client=None, resolve_dns=True):
    manifest_path = Path(manifest_path).resolve()
    now = now or datetime.now(UTC)
    if now.tzinfo is None or now.utcoffset() is None:
        raise CollectionError("timestamp_timezone_required")
    now = now.astimezone(UTC)
    if manifest_path.stat().st_size > 2 * 1024 * 1024:
        raise CollectionError("manifest_too_large", blocked=True)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    slug(job_id)
    matches = [job for job in manifest.get("jobs", []) if job.get("id") == job_id]
    if len(matches) != 1:
        raise CollectionError("unique_job_required", blocked=True)
    job = matches[0]
    sources = job.get("sources")
    if not isinstance(sources, list) or not sources or any(not isinstance(item, dict) for item in sources):
        raise CollectionError("job_sources_required", blocked=True)
    ids = [slug(item.get("id")) for item in sources]
    if len(ids) != len(set(ids)):
        raise CollectionError("duplicate_source_id", blocked=True)
    limits = {**manifest.get("limits", {}), **job.get("limits", {})}
    limits = {"maxRequests": positive_limit(limits.get("maxRequests", 4), 100, "invalid_request_limit"),
              "maxTotalBytes": positive_limit(limits.get("maxTotalBytes", 10 * 1024 * 1024),
                                              100 * 1024 * 1024, "invalid_total_byte_limit"),
              "timeoutSeconds": positive_limit(limits.get("timeoutSeconds", 30), 60, "invalid_timeout")}
    output = output_directory(manifest_path, manifest)
    job_dir = beneath(output, job_id)
    run_id = now.strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:12]
    receipt = {"schema": "public-data-raw-snapshot-run.v1", "job": job_id, "runId": run_id,
               "checkedAt": stamp(now), "dryRun": dry_run, "sourceResults": []}
    allowed = manifest.get("security", {}).get("allowedUrls", [])
    budget = {"requests": 0, "bytes": 0}
    if dry_run:
        for source in sources:
            receipt["sourceResults"].append(collect_source(source, beneath(job_dir, source["id"]),
                allowed, limits, now, budget, dry_run=True))
        receipt["status"] = "dry_run"
    else:
        with global_lock(beneath(output, "collector.lock")) as acquired:
            if not acquired:
                receipt.update(status="skipped_locked", reason="another_collection_job_in_progress")
            else:
                for source in sources:
                    source_dir = beneath(job_dir, source["id"])
                    result = collect_source(source, source_dir, allowed, limits, now, budget,
                                            open_client=open_client, resolve_dns=resolve_dns)
                    receipt["sourceResults"].append(result)
                    atomic_json(beneath(source_dir, "runs", run_id + ".json"), result)
                counts = summarize(receipt["sourceResults"])
                receipt["status"] = "failed" if counts["failed"] else "blocked" if counts["blocked"] else "completed"
    receipt["summary"] = {**summarize(receipt["sourceResults"]), "requestCount": budget["requests"],
                          "receivedBytes": budget["bytes"]}
    if not dry_run:
        atomic_json(beneath(job_dir, "runs", run_id + ".json"), receipt)
    return receipt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--job", required=True)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)
    try:
        receipt = run(args.manifest, args.job, dry_run=args.dry_run)
    except (CollectionError, OSError, ValueError, TypeError) as exc:
        receipt = {"status": "failed", "reason": exc.code if isinstance(exc, CollectionError) else "invalid_manifest_or_storage"}
    print(json.dumps(receipt, ensure_ascii=False, indent=2))
    if receipt["status"] == "failed":
        return 1
    if receipt["status"] == "blocked":
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""서울 공개 집계 CSV의 원문 검증. 네트워크·저장·공간 재배분은 수행하지 않는다."""
from __future__ import annotations

import csv
import io
import math
import re


def validate_seoul_csv(body, source, max_records, *, error_type):
    """공급자 열과 기간·키·숫자를 확인하고 원본을 바꾸지 않은 요약을 반환한다."""
    def fail(code):
        raise error_type(code)

    if not isinstance(body, bytes):
        fail("seoul_csv_body_invalid")
    if not isinstance(source, dict):
        fail("seoul_csv_config_invalid")
    if isinstance(max_records, bool) or not isinstance(max_records, int) or max_records <= 0:
        fail("invalid_record_limit")

    def configured_columns(name, *, required=False):
        value = source.get(name, [])
        if (not isinstance(value, list) or (required and not value)
                or any(not isinstance(item, str) or not item.strip() or item != item.strip()
                       for item in value) or len(value) != len(set(value))):
            fail("seoul_csv_config_invalid")
        return value

    required = configured_columns("requiredColumns", required=True)
    ids = configured_columns("idColumns")
    numbers = configured_columns("numericColumns")
    period_column = source.get("periodColumn")
    period_format = source.get("periodFormat", "YYYYQ")
    if ((period_column is not None and (not isinstance(period_column, str) or not period_column))
            or period_format not in {"YYYYQ", "YYYYMM"}):
        fail("seoul_csv_config_invalid")

    def valid_period(value):
        pattern = r"[0-9]{4}[1-4]" if period_format == "YYYYQ" else r"[0-9]{4}(?:0[1-9]|1[0-2])"
        return isinstance(value, str) and bool(re.fullmatch(pattern, value)) and int(value[:4]) > 0

    expected = source.get("expectedPeriod")
    if expected is not None and period_column is None:
        fail("seoul_csv_config_invalid")
    if expected is not None and not valid_period(expected):
        fail("seoul_csv_expected_period_invalid")

    if body.startswith(b"\xef\xbb\xbf"):
        candidates = ["utf-8-sig"]
    else:
        candidates = ["utf-8", "cp949"]
    for encoding in candidates:
        try:
            text = body.decode(encoding, errors="strict")
            break
        except UnicodeError:
            continue
    else:
        fail("seoul_csv_encoding_invalid")
    if not text.strip():
        fail("seoul_csv_empty")
    if "\x00" in text or text.lstrip().startswith(("<", "{", "[")):
        fail("seoul_csv_not_csv")

    count = 0
    periods = set()
    keys = set()
    missing = 0
    try:
        rows = csv.DictReader(io.StringIO(text, newline=""), strict=True)
        header = rows.fieldnames
        if (not header or any(not name or not name.strip() for name in header)
                or len(header) != len(set(header))):
            fail("seoul_csv_headers_invalid")
        columns = [name.strip() for name in header]
        if len(columns) != len(set(columns)):
            fail("seoul_csv_headers_invalid")
        rows.fieldnames = columns
        configured = [*required, *ids, *numbers]
        if period_column is not None:
            configured.append(period_column)
        if not set(configured).issubset(columns):
            fail("seoul_csv_headers_invalid")
        for row in rows:
            if None in row or any(value is None for value in row.values()):
                fail("seoul_csv_row_shape_invalid")
            values = {name: value.strip() for name, value in row.items()}
            if not any(values.values()):
                continue
            count += 1
            if count > max_records:
                fail("record_limit_exceeded")
            if period_column is not None:
                period = values[period_column]
                if not valid_period(period):
                    fail("seoul_csv_period_invalid")
                if expected is not None and period != expected:
                    fail("seoul_csv_period_mismatch")
                periods.add(period)
            if ids:
                key = tuple(values[name] for name in ids)
                if any(not value for value in key):
                    fail("seoul_csv_id_empty")
                if key in keys:
                    fail("seoul_csv_id_duplicate")
                keys.add(key)
            for name in numbers:
                value = values[name]
                if not value:
                    missing += 1
                    continue
                try:
                    number = float(value)
                except (ValueError, OverflowError):
                    fail("seoul_csv_numeric_invalid")
                if not math.isfinite(number) or number < 0:
                    fail("seoul_csv_numeric_invalid")
    except csv.Error:
        fail("seoul_csv_syntax_invalid")
    if not count:
        fail("seoul_csv_no_records")
    return {"recordCount": count, "evidenceAsOf": max(periods) if periods else None,
            "observationPeriods": sorted(periods), "unit": source.get("unit"),
            "spatialUnit": source.get("spatialUnit"), "columns": columns,
            "encoding": encoding, "missingNumericCells": missing}

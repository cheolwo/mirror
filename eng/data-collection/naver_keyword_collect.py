"""네이버의 인증된 읽기 전용 키워드 API만 제한 수집한다.

키는 지정한 전용 환경변수로만 읽는다. 지도/NCP 키를 재사용하지 않는다.
401/403/429 뒤에는 자격 개선을 확인하고 manifest.authorizationRevision을
올리기 전 같은 정책을 재요청하지 않는다. 키/헤더/계정 ID/오류 본문은 저장하지 않는다.
"""
from __future__ import annotations

import argparse
import base64
from datetime import date, datetime, timedelta, timezone
import hashlib
import hmac
import json
import math
import os
from pathlib import Path
import re
from urllib import parse
import uuid

from collect_public_snapshots import CollectionError, atomic_json, aware_time, beneath, fetch, global_lock, opener, stamp

UTC = timezone.utc
KST = timezone(timedelta(hours=9))
ENDPOINTS = {"datalab": "https://naverapihub.apigw.ntruss.com/search-trend/v1/search",
             "searchad": "https://api.searchad.naver.com/keywordstool"}
ENVIRONMENTS = {"datalab": ("NAVER_API_HUB_CLIENT_ID", "NAVER_API_HUB_CLIENT_SECRET"),
                "searchad": ("NAVER_SEARCHAD_API_KEY", "NAVER_SEARCHAD_SECRET_KEY", "NAVER_SEARCHAD_CUSTOMER_ID")}
DEFAULT_MANIFEST = Path(__file__).with_name("naver-keyword.jobs.json")


def digest_json(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def policy_fingerprint(manifest, plan):
    # 안내 문구 수정만으로 인증 거절/최소 간격을 초기화하지 않는다.
    value = {key: manifest[key] for key in ("schemaVersion", "authorizationRevision",
        "minimumSuccessIntervalHours", "maximumRequests", "maximumResponseBytes", "maximumRunBytes", "timeoutSeconds")}
    value.update(validationContract="naver-keyword-validation-v1", requests=plan,
                 numericPersistenceEnabled=manifest["datalab"]["numericPersistenceEnabled"])
    return digest_json(value)


def validate_manifest(manifest):
    if not isinstance(manifest, dict) or manifest.get("schemaVersion") != "naver-keyword-intake.v1":
        raise CollectionError("manifest_invalid", blocked=True)
    for key, expected in [("minimumSuccessIntervalHours", 168), ("maximumRequests", 6),
                          ("maximumResponseBytes", 262144), ("maximumRunBytes", 2097152), ("timeoutSeconds", 30)]:
        if type(manifest.get(key)) is not int or manifest[key] != expected:
            raise CollectionError("limits_contract_invalid", blocked=True)
    revision = manifest.get("authorizationRevision")
    if type(revision) is not int or not 0 <= revision <= 10000:
        raise CollectionError("authorization_revision_invalid", blocked=True)
    for provider, endpoint in ENDPOINTS.items():
        if manifest.get(provider, {}).get("endpoint") != endpoint:
            raise CollectionError("endpoint_not_allowed", blocked=True)
    dl, ad = manifest["datalab"], manifest["searchad"]
    if type(dl.get("numericPersistenceEnabled")) is not bool:
        raise CollectionError("storage_gate_invalid", blocked=True)
    if (dl.get("clientIdEnvironment"), dl.get("clientSecretEnvironment")) != ENVIRONMENTS["datalab"]:
        raise CollectionError("credential_binding_invalid", blocked=True)
    if (ad.get("apiKeyEnvironment"), ad.get("secretKeyEnvironment"), ad.get("customerIdEnvironment")) != ENVIRONMENTS["searchad"]:
        raise CollectionError("credential_binding_invalid", blocked=True)
    try:
        start, end = date.fromisoformat(dl["startDate"]), date.fromisoformat(dl["endDate"])
    except (KeyError, TypeError, ValueError):
        raise CollectionError("query_period_invalid", blocked=True) from None
    if start < date(2016, 1, 1) or start.day != 1 or start > end or end >= datetime.now(KST).date() or dl.get("timeUnit") != "month":
        raise CollectionError("query_period_invalid", blocked=True)
    if not isinstance(dl.get("batches"), list) or len(dl["batches"]) != 3:
        raise CollectionError("batches_invalid", blocked=True)
    for batch in dl["batches"]:
        if not isinstance(batch, list) or not 1 <= len(batch) <= 5 or len(set(batch)) != len(batch):
            raise CollectionError("batches_invalid", blocked=True)
        for keyword in batch:
            keyword_text(keyword)
    seeds = ad.get("seedKeywords")
    if (not isinstance(seeds, list) or not 1 <= len(seeds) <= 15 or len(set(seeds)) != len(seeds)
            or ad.get("maximumSeedsPerRequest") != 5 or ad.get("showDetail") != 1):
        raise CollectionError("seeds_invalid", blocked=True)
    for keyword in seeds:
        keyword_text(keyword)


def keyword_text(value):
    if not isinstance(value, str) or not 1 <= len(value) <= 30 or value.strip() != value or any(ord(c) < 32 for c in value):
        raise CollectionError("keyword_invalid", blocked=True)
    return value


def build_plan(manifest):
    validate_manifest(manifest)
    result = []
    dl = manifest["datalab"]
    for index, batch in enumerate(dl["batches"], 1):
        payload = {key: dl[key] for key in ("startDate", "endDate", "timeUnit")}
        payload["keywordGroups"] = [{"groupName": term, "keywords": [term]} for term in batch]
        result.append({"id": f"datalab-{index:02d}", "provider": "datalab", "method": "POST", "url": ENDPOINTS["datalab"], "json": payload})
    ad = manifest["searchad"]
    for offset in range(0, len(ad["seedKeywords"]), 5):
        seeds = ad["seedKeywords"][offset:offset + 5]
        result.append({"id": f"searchad-{offset // 5 + 1:02d}", "provider": "searchad", "method": "GET",
                       "url": ENDPOINTS["searchad"] + "?" + parse.urlencode({"hintKeywords": ",".join(seeds), "showDetail": 1}),
                       "seedKeywords": seeds})
    return result


def signature(secret, timestamp, method="GET", uri="/keywordstool"):
    return base64.b64encode(hmac.new(secret.encode(), f"{timestamp}.{method}.{uri}".encode(), hashlib.sha256).digest()).decode()


class AuthenticatedClient:
    def __init__(self, provider, credentials, now, *, client=None):
        self.provider, self.credentials, self.now = provider, credentials, now
        self.client = client if client is not None else opener()

    def open(self, req, timeout):
        url = parse.urlsplit(req.full_url)
        expected = parse.urlsplit(ENDPOINTS[self.provider])
        if url.scheme != "https" or url.netloc != expected.netloc or url.path != expected.path or req.method != ("POST" if self.provider == "datalab" else "GET"):
            raise CollectionError("authenticated_endpoint_invalid", blocked=True)
        if self.provider == "datalab":
            headers = {"X-NCP-APIGW-API-KEY-ID": self.credentials[0], "X-NCP-APIGW-API-KEY": self.credentials[1]}
        else:
            timestamp = str(int(self.now.timestamp() * 1000))
            headers = {"X-Timestamp": timestamp, "X-API-KEY": self.credentials[0], "X-Customer": self.credentials[2],
                       "X-Signature": signature(self.credentials[1], timestamp)}
        for key, value in headers.items():
            req.add_header(key, value)
        return self.client.open(req, timeout=timeout)


def number(value, *, maximum=None):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0 or (maximum is not None and value > maximum):
        raise CollectionError("numeric_value_invalid")
    return value


def provider_number(value, *, maximum=None):
    """명세의 숫자문자열도 검사하되 저장하는 공급자 원값은 바꾸지 않는다."""
    parsed = value
    if isinstance(value, str):
        if not re.fullmatch(r"\d+(?:\.\d+)?", value):
            raise CollectionError("numeric_value_invalid")
        parsed = float(value)
    return number(parsed, maximum=maximum)


def validate_datalab(value, source):
    query = source["json"]
    if not isinstance(value, dict) or any(value.get(key) != query[key] for key in ("startDate", "endDate", "timeUnit")):
        raise CollectionError("datalab_period_contract_invalid")
    expected = {group["groupName"]: group["keywords"] for group in query["keywordGroups"]}
    groups, rows, seen_groups = value.get("results"), [], set()
    if not isinstance(groups, list) or len(groups) != len(expected):
        raise CollectionError("datalab_groups_invalid")
    for group in groups:
        if not isinstance(group, dict) or not isinstance(group.get("title"), str):
            raise CollectionError("datalab_group_identity_invalid")
        title = group["title"]
        if title in seen_groups or title not in expected or group.get("keywords") != expected[title]:
            raise CollectionError("datalab_group_identity_invalid")
        seen_groups.add(title)
        data = group.get("data")
        if not isinstance(data, list) or len(data) > 12:
            raise CollectionError("datalab_periods_invalid")
        periods = set()
        for point in data:
            if not isinstance(point, dict):
                raise CollectionError("datalab_periods_invalid")
            period = point.get("period")
            try:
                parsed = date.fromisoformat(period)
            except (TypeError, ValueError):
                raise CollectionError("datalab_periods_invalid") from None
            if parsed.day != 1 or not query["startDate"] <= period <= query["endDate"] or period in periods:
                raise CollectionError("datalab_periods_invalid")
            periods.add(period)
            ratio = point.get("ratio")
            rows.append({"batchId": source["id"], "keyword": title, "period": period,
                         "ratio": provider_number(ratio, maximum=100), "ratioAsPublished": ratio})
    return {"kind": "dataset", "unit": "request_relative_index_0_100", "normalizationScope": source["id"],
            "geography": "NAVER 통합검색 범위; 행정동 필터 없음", "rows": rows,
            "limitations": ["비율은 절대검색수·노출·주문이 아님", "서로 다른 배치/조회기간 비율을 직접 합산·비교하지 않음", "미반환 기간을 0으로 생성하지 않음"]}


def search_count(value):
    if value is None:
        return {"raw": None, "status": "missing", "value": None}
    if isinstance(value, str) and re.fullmatch(r"\s*<\s*10\s*", value):
        return {"raw": value, "status": "censored", "value": None, "upperBound": 10, "upperBoundInclusive": False}
    if isinstance(value, str) and re.fullmatch(r"\d+", value):
        parsed = int(value)
    elif type(value) is int:
        parsed = value
    else:
        raise CollectionError("searchad_search_count_invalid")
    number(parsed)
    return {"raw": value, "status": "observed", "value": parsed}


def validate_searchad(value, source):
    data = value.get("keywordList") if isinstance(value, dict) else None
    if not isinstance(data, list) or not 0 <= len(data) <= 1000:
        raise CollectionError("searchad_keyword_list_invalid")
    rows, seen = [], set()
    for item in data:
        if not isinstance(item, dict):
            raise CollectionError("searchad_keyword_row_invalid")
        keyword = keyword_text(item.get("relKeyword"))
        if keyword in seen:
            raise CollectionError("searchad_duplicate_keyword")
        seen.add(keyword)
        if any(key not in item for key in ("monthlyPcQcCnt", "monthlyMobileQcCnt")):
            raise CollectionError("searchad_search_count_missing")
        row = {"batchId": source["id"], "keyword": keyword,
               "pcSearchesLast30Days": search_count(item["monthlyPcQcCnt"]),
               "mobileSearchesLast30Days": search_count(item["monthlyMobileQcCnt"])}
        for key in ("monthlyAvePcClkCnt", "monthlyAveMobileClkCnt", "monthlyAvePcCtr", "monthlyAveMobileCtr", "plAvgDepth"):
            metric = item.get(key)
            if metric is not None:
                provider_number(metric)
            row[key] = metric
        competition = item.get("compIdx")
        if competition is not None and (not isinstance(competition, str) or len(competition) > 40):
            raise CollectionError("searchad_competition_invalid")
        row["pcAdvertisingCompetitionAsPublished"] = competition
        rows.append(row)
    return {"kind": "dataset", "rows": rows, "geography": "키워드 도구 반환범위; 동명 포함 검색어가 동별 관측을 뜻하지 않음",
            "measurementWindows": {"searchCounts": "최근30일", "averageClicksCtrAndAdDepth": "최근4주"},
            "units": {"searchCounts": "search_count_or_censored", "averageClicks": "click_count", "ctr": "provider_reported_ctr_scale_unconfirmed", "plAvgDepth": "average_ad_count"},
            "limitations": ["경쟁도는 PC광고 기준이며 전체검색 경쟁도/자연순위 아님", "평균광고지표는 특정광고주의 실제실적/현재CPC/게임전환율이 아님", "CTR배율은미확인원값이며0..1변환/게임확률적용안함", "배치간 중복연관키워드를 합산하지 않음"]}


def json_body(body):
    def unique_keys(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise CollectionError("response_duplicate_json_key")
            result[key] = value
        return result
    try:
        return json.loads(body.decode("utf-8-sig"), object_pairs_hook=unique_keys)
    except (ValueError, UnicodeError, RecursionError):
        raise CollectionError("response_json_invalid") from None


def decoded_credential_reflection(value, secrets):
    """JSON 이스케이프가 해제된 키/문자열도 검사해 반사된 자격정보를 차단한다."""
    pending, checked = [value], 0
    while pending:
        current = pending.pop()
        checked += 1
        if checked > 100000:
            raise CollectionError("response_json_too_complex")
        if isinstance(current, str):
            if any(secret in current for secret in secrets):
                return True
        elif isinstance(current, dict):
            pending.extend(current.keys())
            pending.extend(current.values())
        elif isinstance(current, list):
            pending.extend(current)
    return False


def collect(workspace, manifest, *, now=None, dry_run=False, environ=None, open_client=None, resolve_dns=True):
    now = now or datetime.now(UTC)
    if now.tzinfo is None or now.utcoffset() is None:
        raise CollectionError("timestamp_timezone_required")
    plan = build_plan(manifest)
    workspace = Path(workspace).resolve()
    output_root = beneath(workspace, "artifacts/local/data-collection-runs")
    output = beneath(output_root, "naver-keywords")
    env = os.environ if environ is None else environ
    credentials = {provider: tuple(env.get(key, "").strip() for key in keys) for provider, keys in ENVIRONMENTS.items()}
    credential_values = tuple(secret for values in credentials.values() for secret in values if secret)
    auth_ready = {provider: all(values) and all(len(value) <= 512 and not any(ord(c) < 33 for c in value) for value in values) for provider, values in credentials.items()}
    fingerprint = policy_fingerprint(manifest, plan)
    result = {"schemaVersion": "naver-keyword-collection-run.v1", "checkedAtUtc": stamp(now), "dryRun": dry_run,
              "requestPolicySha256": fingerprint, "credentialConfigured": auth_ready, "sourceResults": [],
              "requestCount": 0, "receivedBytes": 0, "observationRows": 0, "advertiserPerformanceRows": 0,
              "databaseImported": False, "gameImported": False, "redistributionApproved": False}
    state_path = beneath(output, "state.json")

    def previous_state():
        if not state_path.exists():
            return {}
        try:
            value = json.loads(state_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            raise CollectionError("state_invalid", blocked=True) from None
        if not isinstance(value, dict):
            raise CollectionError("state_invalid", blocked=True)
        return value if value.get("requestPolicySha256") == fingerprint else {}

    refused_providers = set()

    def eligibility(source, previous):
        if not auth_ready[source["provider"]]:
            return "blocked", "credential_not_configured"
        if source["provider"] == "datalab" and not manifest["datalab"]["numericPersistenceEnabled"]:
            return "blocked", "storage_terms_unconfirmed"
        if (source["provider"] in refused_providers or any(
                previous.get("sources", {}).get(other["id"], {}).get("stickyBlocked")
                for other in plan if other["provider"] == source["provider"])):
            return "blocked", "previous_auth_or_policy_refusal_requires_review"
        prior = previous.get("sources", {}).get(source["id"], {})
        if prior.get("stickyBlocked"):
            return "blocked", "previous_auth_or_policy_refusal_requires_review"
        attempt = prior.get("lastAttemptAtUtc")
        if attempt and now < aware_time(attempt) + timedelta(hours=168):
            return "not_due", "minimum_168_hours"
        return "ready", None

    if dry_run:
        previous = previous_state()
        for source in plan:
            status, reason = eligibility(source, previous)
            result["sourceResults"].append({"id": source["id"], "provider": source["provider"], "status": status, "reason": reason})
        result["status"] = "dry_run"
        return result
    with global_lock(beneath(output_root, "collector.lock")) as acquired:
        if not acquired:
            return dict(result, status="blocked", reason="collector_locked")
        previous = previous_state()
        updated = previous.get("sources", {}).copy()
        run_id = now.astimezone(UTC).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
        run_dir = beneath(output, "runs", run_id)
        run_dir.mkdir(parents=True, exist_ok=False)
        result["runId"] = run_id
        atomic_json(beneath(run_dir, "public-request-plan.json"), {"policySha256": fingerprint, "requests": plan})
        for source in plan:
            status, reason = eligibility(source, previous)
            item = {"id": source["id"], "provider": source["provider"], "status": status, "reason": reason, "observationRows": 0}
            if status != "ready":
                result["sourceResults"].append(item)
                continue
            prior = {"lastAttemptAtUtc": stamp(now), "stickyBlocked": False}
            try:
                if result["requestCount"] >= 6 or result["receivedBytes"] >= 2097152:
                    raise CollectionError("run_budget_exceeded")
                result["requestCount"] += 1
                client = AuthenticatedClient(source["provider"], credentials[source["provider"]], now, client=open_client)
                _, body, _ = fetch(source, None, 30, min(262144, 2097152 - result["receivedBytes"]), now,
                                   open_client=client, resolve_dns=resolve_dns)
                result["receivedBytes"] += len(body)
                # 성공 본문에도 자격정보가 반사되면 원문을 쓰지 않는다.
                if any(secret.encode() in body for secret in credential_values):
                    raise CollectionError("credential_reflected_in_response", blocked=True)
                value = json_body(body)
                if decoded_credential_reflection(value, credential_values):
                    raise CollectionError("credential_reflected_in_response", blocked=True)
                normalized = validate_datalab(value, source) if source["provider"] == "datalab" else validate_searchad(value, source)
                raw = beneath(run_dir, source["id"] + ".raw.json")
                raw.write_bytes(body)
                raw_hash = hashlib.sha256(body).hexdigest()
                structured = beneath(run_dir, source["id"] + ".structured.json")
                normalized.update(sourceId="naver-api-hub-search-trend" if source["provider"] == "datalab" else "naver-searchad-keyword-tool", sourceUrl=source["url"],
                                  collectedAtUtc=stamp(now), rawSha256=raw_hash, requestPolicySha256=fingerprint)
                atomic_json(structured, normalized)
                item.update(status="saved", reason=None, observationRows=len(normalized["rows"]),
                            rawPath=raw.relative_to(workspace).as_posix(), rawSha256=raw_hash, bytes=len(body),
                            structuredPath=structured.relative_to(workspace).as_posix(), collectedAtUtc=stamp(now),
                            kind="dataset", httpStatus=200)
                prior["lastSuccessAtUtc"] = stamp(now)
                result["observationRows"] += len(normalized["rows"])
            except CollectionError as exc:
                result["receivedBytes"] += exc.received
                item.update(status="blocked" if exc.blocked else "failed", reason=exc.code)
                prior["stickyBlocked"] = exc.blocked
                if exc.blocked:
                    refused_providers.add(source["provider"])
            except (ValueError, OSError, TypeError, KeyError):
                item.update(status="failed", reason="request_or_storage_invalid")
            updated[source["id"]] = prior
            result["sourceResults"].append(item)
        result["status"] = "blocked" if any(item["status"] == "blocked" for item in result["sourceResults"]) else ("failed" if any(item["status"] == "failed" for item in result["sourceResults"]) else "completed")
        atomic_json(beneath(run_dir, "receipt.json"), result)
        atomic_json(state_path, {"schemaVersion": "naver-keyword-state.v1", "requestPolicySha256": fingerprint, "sources": updated, "latestRunId": run_id})
        return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)
    try:
        manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
        result = collect(args.workspace, manifest, dry_run=args.dry_run)
    except CollectionError as exc:
        result = {"status": "blocked" if exc.blocked else "failed", "reason": exc.code, "dryRun": args.dry_run}
    except (OSError, ValueError, TypeError, KeyError):
        result = {"status": "failed", "reason": "manifest_or_state_invalid", "dryRun": args.dry_run}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 2 if result["status"] == "blocked" else (1 if result["status"] == "failed" else 0)


if __name__ == "__main__":
    raise SystemExit(main())

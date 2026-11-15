"""승인한 두 한국 무역 원문만 초기 채택한다. HTTP 요청·외부 경로 입력·기존 자료 갱신 없음.

2015~2025년 11행은 mrv=12 요청을 수행한 결과가 아니다. 이 차이와 원래
취득 시각/URL/SHA를 latest에 남겨 최초 720시간 동안 재다운로드를 피한다.
"""
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import uuid

import collect_public_snapshots as collector


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "eng/data-collection/research-trade.jobs.json"
BASE = "artifacts/local/management-research-data-20261114/trade"
BASELINES = (
    {"id": "worldbank-kor-exchange-rate", "stem": "wdi-pa-nus-fcrf-kor-2015-2025",
     "datasetId": "wdi-pa-nus-fcrf", "indicatorId": "PA.NUS.FCRF", "unit": "KRW_per_USD",
     "sha256": "df70617adf4518c6c0fa3b3f46cf0a5e2ea016145533a8f3b251719e1e384119",
     "collectedAt": "2026-11-13T23:46:11.629000+00:00"},
    {"id": "worldbank-kor-merchandise-exports", "stem": "wdi-tx-val-mrch-cd-wt-kor-2015-2025",
     "datasetId": "wdi-tx-val-mrch-cd-wt", "indicatorId": "TX.VAL.MRCH.CD.WT", "unit": "USD_current",
     "sha256": "f32554e82c7873e2e57605e0df4ed3fa3ba1f3192ae8bb7e24b680db2566e15b",
     "collectedAt": "2026-11-13T23:45:37.661694+00:00"},
)


def seed(*, now=None):
    now = now or datetime.now(collector.UTC)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
    jobs = manifest.get("jobs", [])
    if len(jobs) != 1 or jobs[0].get("id") != "research-trade":
        raise collector.CollectionError("seed_manifest_mismatch")
    sources = jobs[0].get("sources", [])
    if len(sources) != 2 or {s.get("id") for s in sources} != {s["id"] for s in BASELINES}:
        raise collector.CollectionError("seed_sources_mismatch")
    output = collector.output_directory(MANIFEST, manifest)
    prepared = []
    for baseline in BASELINES:
        source = next(s for s in sources if s["id"] == baseline["id"])
        recent_url = ("https://api.worldbank.org/v2/country/KOR/indicator/" + baseline["indicatorId"]
                      + "?format=json&mrv=12&per_page=100")
        original_url = recent_url.replace("mrv=12", "date=2015:2025")
        expected = {"sourceId": "world-bank-indicators", "datasetId": baseline["datasetId"],
                    "indicatorId": baseline["indicatorId"], "unit": baseline["unit"], "validator": "worldbank",
                    "url": recent_url, "kind": "dataset", "status": "ready", "method": "GET",
                    "intervalHours": 720, "maxBytes": 262144, "maxRecords": 12}
        if any(source.get(k) != value for k, value in expected.items()):
            raise collector.CollectionError("seed_source_contract_mismatch")
        collector.validate_url(recent_url, manifest.get("security", {}).get("allowedUrls", []))
        original = collector.beneath(ROOT, BASE, "raw", baseline["stem"] + ".json")
        acquisition = collector.beneath(ROOT, BASE, baseline["stem"] + ".acquisition.json")
        if original.stat().st_size > source["maxBytes"] or acquisition.stat().st_size > 16384:
            raise collector.CollectionError("seed_file_too_large")
        body = original.read_bytes()
        receipt = json.loads(acquisition.read_text(encoding="utf-8"))
        if (hashlib.sha256(body).hexdigest() != baseline["sha256"]
                or receipt.get("rawSha256") != baseline["sha256"]
                or receipt.get("url") != original_url or receipt.get("status") != "saved"
                or receipt.get("httpStatus") != 200
                or receipt.get("collectedAtUtc") != baseline["collectedAt"]
                or collector.aware_time(baseline["collectedAt"]) > now):
            raise collector.CollectionError("seed_provenance_mismatch")
        validated = collector.validate_worldbank(body, source, source["maxRecords"])
        if validated["observationYears"] != [str(y) for y in range(2015, 2026)]:
            raise collector.CollectionError("seed_period_mismatch")
        source_dir = collector.beneath(output, "research-trade", source["id"])
        digest = baseline["sha256"]
        pointer = {"id": source["id"], "sourceId": source["sourceId"], "datasetId": source["datasetId"],
                   "kind": source["kind"], **validated,
                   "requestPolicyFingerprint": collector.policy_fingerprint(source, now),
                   "sha256": digest, "body": "bodies/" + digest + ".raw", "bytes": len(body),
                   "collectedAt": collector.stamp(collector.aware_time(baseline["collectedAt"])),
                   "httpStatus": None, "etag": None, "lastModified": None, "previousSha256": None,
                   "seededBaseline": True, "seededAt": collector.stamp(now),
                   "baselineAdoption": {"originalRequestUrl": original_url,
                       "originalRawPath": original.relative_to(ROOT).as_posix(), "originalSha256": digest,
                       "originalCollectedAt": baseline["collectedAt"], "originalResponseHttpStatus": 200,
                       "originalPeriod": "2015:2025", "baselineRecordCount": validated["recordCount"],
                       "scheduledRequestUrl": recent_url, "requestedRecentRecords": 12,
                       "scheduledRequestCoverageVerified": False, "httpPerformed": False,
                       "purpose": "기존 11연도 원문의 명시적 초기 채택; 주기 간격만 이어받으며 새 mrv=12 응답으로 간주하지 않음"}}
        prepared.append((source_dir, body, pointer))

    result = {"schema": "research-trade-baseline-adoption.v1", "checkedAt": collector.stamp(now),
              "httpRequestCount": 0, "receivedBytes": 0, "copiedBodyBytes": 0, "sources": []}
    with collector.global_lock(collector.beneath(output, "collector.lock")) as acquired:
        if not acquired:
            raise collector.CollectionError("seed_collection_lock_busy")
        # 두 항목 모두 검증한 뒤 신규 파일만 쓴다. 나중에 받은 최신 원본을 seed로 덮지 않는다.
        for source_dir, body, pointer in prepared:
            previous = collector.previous_snapshot(source_dir)
            if previous:
                expected = {k: v for k, v in pointer.items() if k != "seededAt"}
                if any(previous.get(k) != v for k, v in expected.items()):
                    raise collector.CollectionError("seed_existing_state_conflict")
        for source_dir, body, pointer in prepared:
            latest = collector.beneath(source_dir, "latest.json")
            if latest.exists():
                result["sources"].append({"id": pointer["id"], "status": "existing_baseline", "sha256": pointer["sha256"]})
                continue
            body_path = collector.beneath(source_dir, pointer["body"])
            body_path.parent.mkdir(parents=True, exist_ok=True)
            if body_path.exists():
                if hashlib.sha256(body_path.read_bytes()).hexdigest() != pointer["sha256"]:
                    raise collector.CollectionError("seed_existing_body_mismatch")
            else:
                with body_path.open("xb") as stream:
                    stream.write(body)
                    stream.flush()
                    os.fsync(stream.fileno())
                result["copiedBodyBytes"] += len(body)
            collector.atomic_json(latest, pointer)
            result["sources"].append({"id": pointer["id"], "status": "seeded", "sha256": pointer["sha256"],
                                      "recordCount": pointer["recordCount"], "httpStatus": None})
        result["status"] = "completed"
        run_id = now.strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:12]
        collector.atomic_json(collector.beneath(output, "research-trade", "seed-runs", run_id + ".json"), result)
    return result


def main():
    try:
        result = seed()
    except (collector.CollectionError, OSError, ValueError, TypeError, KeyError) as exc:
        result = {"status": "failed", "reason": exc.code if isinstance(exc, collector.CollectionError) else "seed_validation_or_storage_failed"}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())

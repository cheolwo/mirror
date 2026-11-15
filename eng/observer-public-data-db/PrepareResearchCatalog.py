"""고정된 조사 대장을 메타데이터로 분리한다. 네트워크·DB 접근 없음."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re


SCHEMA = "observer-public-data-db.v1"
ORIGIN = "docs/AI/DataCollection/Research/2026-11-13-observer-source-catalog.json"
BASE = "artifacts/local/public-data-db-20261113"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def safe_id(value: str) -> str:
    result = re.sub(r"[^A-Za-z0-9._-]+", "-", value).strip(".-")
    if not result or result.upper() in {"CON", "PRN", "AUX", "NUL"}:
        raise ValueError("unsafe research ID for filename")
    if re.fullmatch(r"(?:COM|LPT)[1-9]", result, flags=re.IGNORECASE):
        raise ValueError("reserved filename")
    return result


def prepare(repo_root: Path, expected_origin_sha256: str) -> dict:
    repo_root = repo_root.resolve()
    origin_path = repo_root / ORIGIN
    original_bytes = origin_path.read_bytes()
    origin_hash = sha256(original_bytes)
    if origin_hash != expected_origin_sha256:
        raise ValueError("origin SHA-256 differs from reviewed baseline")
    catalog = json.loads(original_bytes.decode("utf-8-sig"))
    candidates = catalog["candidates"]
    if len(candidates) != 26:
        raise ValueError("expected exactly 26 candidates")
    ids = [candidate["researchId"] for candidate in candidates]
    if any(not isinstance(value, str) or not value for value in ids):
        raise ValueError("research IDs must be non-empty strings")
    if len(set(ids)) != 26:
        raise ValueError("duplicate research ID")
    safe_ids = [safe_id(value) for value in ids]
    if len({value.casefold() for value in safe_ids}) != 26:
        raise ValueError("safe filename collision")
    if sha256(origin_path.read_bytes()) != origin_hash:
        raise ValueError("origin changed before preparation")

    prepared_at = datetime.now(timezone.utc).isoformat(timespec="microseconds")
    output_root = repo_root / BASE
    raw_root = output_root / "research-catalog"
    manifest_path = output_root / "catalog-manifest.partial.json"
    receipt_path = output_root / "research-catalog-preparation-receipt.json"
    expected_paths = [raw_root / f"{value}.json" for value in safe_ids]
    all_output_paths = expected_paths + [manifest_path, receipt_path]
    if any(path.exists() for path in all_output_paths):
        raise FileExistsError("refusing to overwrite prepared research artifacts")
    for path in expected_paths:
        if path.resolve().parent != raw_root.resolve():
            raise ValueError("output path escaped research-catalog")

    entries = []
    planned_raws = []
    for candidate, filename_id, path in zip(candidates, safe_ids, expected_paths):
        wrapped = {
            "schemaVersion": SCHEMA,
            "catalogOriginPath": ORIGIN,
            "catalogOriginSha256": origin_hash,
            "preparedAtUtc": prepared_at,
            "preparedAtMeaning": "Metadata split and DB preparation time; not an external observation time.",
            "kind": "metadata",
            "externalObservationAcquired": False,
            "sourceId": "observer-public-data-research",
            "sourceIdMeaning": "Research registry namespace; not a provider ingestion contract.",
            "datasetId": candidate["researchId"],
            "datasetIdMeaning": "Research registry ID; not a provider dataset contract.",
            "sourceVersion": "r158",
            "candidate": candidate,
        }
        raw_bytes = json_bytes(wrapped)
        raw_hash = sha256(raw_bytes)
        planned_raws.append((path, raw_bytes, candidate))
        entries.append({
            "kind": "metadata",
            "sourceId": "observer-public-data-research",
            "datasetId": candidate["researchId"],
            "sourceVersion": "r158",
            "dataRevision": f"r158.{raw_hash[:16]}",
            "collectedAtUtc": prepared_at,
            "collectedAtMeaning": "Metadata split and DB preparation time; not an external observation time.",
            "evidenceAsOfUtc": None,
            "rawPath": path.relative_to(repo_root).as_posix(),
            "rawSha256": raw_hash,
            "contentType": "application/json",
            "officialUrl": candidate["officialUrl"],
            "recordsPath": None,
            "recordsSha256": None,
        })

    manifest = {
        "schemaVersion": SCHEMA,
        "manifestKind": "research-metadata-partial",
        "sourceId": "observer-public-data-research",
        "sourceIdMeaning": "Research registry namespace; not a provider ingestion contract.",
        "datasetIdMeaning": "Each datasetId is the original researchId, not a provider contract.",
        "sourceVersion": "r158",
        "catalogOriginPath": ORIGIN,
        "catalogOriginSha256": origin_hash,
        "preparedAtUtc": prepared_at,
        "collectedAtMeaning": "Metadata split and DB preparation time; not an external observation time.",
        "externalObservationAcquired": False,
        "entries": entries,
    }
    receipt = {
        "schemaVersion": SCHEMA,
        "status": "metadata_artifacts_prepared",
        "catalogOriginPath": ORIGIN,
        "catalogOriginSha256": origin_hash,
        "catalogOriginBytes": len(original_bytes),
        "originHashCheckedBeforePreparation": True,
        "originHashCheckedAfterPreparation": True,
        "preparedAtUtc": prepared_at,
        "candidateCount": 26,
        "uniqueResearchIdCount": len(set(ids)),
        "uniqueSafeFilenameCount": len({value.casefold() for value in safe_ids}),
        "manifestPath": manifest_path.relative_to(repo_root).as_posix(),
        "manifestSha256": sha256(json_bytes(manifest)),
        "rawFileCount": 26,
        "allRawHashesVerified": True,
        "allCandidatesPreserved": True,
        "allEntriesKindMetadata": True,
        "allEvidenceAsOfUtcNull": True,
        "allRecordsReferencesNull": True,
        "externalObservationAcquired": False,
        "networkRequests": 0,
        "dbWrites": 0,
        "productDocumentsChanged": False,
        "fileMap": [{"researchId": entry["datasetId"], "rawPath": entry["rawPath"], "rawSha256": entry["rawSha256"]} for entry in entries],
    }
    if sha256(origin_path.read_bytes()) != origin_hash:
        raise ValueError("origin changed before writes")
    raw_root.mkdir(parents=True, exist_ok=True)
    for path, raw_bytes, candidate in planned_raws:
        with path.open("xb") as stream:
            stream.write(raw_bytes)
        saved = path.read_bytes()
        parsed = json.loads(saved.decode("utf-8"))
        if sha256(saved) != sha256(raw_bytes) or parsed["candidate"] != candidate:
            raise ValueError("raw preservation verification failed")
    with manifest_path.open("xb") as stream:
        stream.write(json_bytes(manifest))
    if json.loads(manifest_path.read_text(encoding="utf-8")) != manifest:
        raise ValueError("manifest round-trip verification failed")
    if sha256(origin_path.read_bytes()) != origin_hash:
        raise ValueError("origin changed after preparation")
    with receipt_path.open("xb") as stream:
        stream.write(json_bytes(receipt))
    return {key: receipt[key] for key in ["status", "candidateCount", "uniqueResearchIdCount", "rawFileCount", "manifestPath", "manifestSha256", "catalogOriginSha256", "networkRequests", "dbWrites"]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--expected-origin-sha256", required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.repo_root, args.expected_origin_sha256), ensure_ascii=False))

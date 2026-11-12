"""Export or verify a bounded, offline public-data collaboration snapshot.

No extraction, restoration, network access, upload, database import, execution
grant or public-distribution approval is performed. Existing collector inputs
and source bytes are preserved; the caller transfers the ZIP separately.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import zipfile

import collect_public_snapshots as collector


SCHEMA = "public-data-collaboration-snapshot.v1"
INDEX = "bundle.json"
MAX_BYTES = 64 * 1024 * 1024
MAX_JSON_BYTES = 2 * 1024 * 1024
MAX_ENTRIES = 512
MAX_SOURCES = 100
DIGEST = re.compile(r"[a-f0-9]{64}")
RUN_ID = re.compile(r"[0-9]{8}T[0-9]{6}Z-[a-f0-9]{12}")
SNAPSHOT_FIELDS = {
    "id", "sourceId", "datasetId", "kind", "collectedAt", "evidenceAsOf",
    "requestPolicyFingerprint", "recordCount", "numericRecords", "unit",
    "evidenceAsOfPrecision", "sourceUpdatedAt", "observationYears",
    "numericObservationYears", "missingRecords", "missingValueMarker",
    "seriesCount", "observationPeriods", "numericObservationPeriods",
    "missingObservationPeriods", "expandedBytes", "catalogDatasetId",
    "metadataDocument", "sha256", "body", "httpStatus", "bytes", "etag",
    "lastModified", "previousSha256", "spatialUnit", "columns", "encoding", "missingNumericCells",
}
RUN_FIELDS = (SNAPSHOT_FIELDS - {"body", "etag", "lastModified", "previousSha256"}) | {
    "status", "reason", "previousCollectedAt", "blockedAt", "reusedBlockedOutcome",
}
BLOCK_FIELDS = {"requestPolicyFingerprint", "reason", "blockedAt", "sourceId", "datasetId"}


class TransferError(Exception):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


def digest(body):
    return hashlib.sha256(body).hexdigest()


def require(condition, code):
    if not condition:
        raise TransferError(code)


def _unique_object(pairs):
    value = {}
    for key, item in pairs:
        require(key not in value, "duplicate_json_key")
        value[key] = item
    return value


def decode_json(body):
    require(len(body) <= MAX_JSON_BYTES, "json_too_large")
    value = json.loads(body.decode("utf-8-sig"), object_pairs_hook=_unique_object)
    collector.no_credentials(value)
    return value


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def read_bounded(path, maximum):
    require(path.is_file(), "input_file_missing")
    require(path.stat().st_size <= maximum, "input_too_large")
    with path.open("rb") as stream:
        body = stream.read(maximum + 1)
    require(len(body) <= maximum, "input_too_large")
    return body


def contract(manifest_path, job_id):
    manifest_path = Path(manifest_path).resolve()
    body = read_bounded(manifest_path, MAX_JSON_BYTES)
    manifest = decode_json(body)
    require(isinstance(manifest, dict), "invalid_manifest")
    collector.slug(job_id)
    jobs = [j for j in manifest.get("jobs", []) if isinstance(j, dict) and j.get("id") == job_id]
    require(len(jobs) == 1, "unique_job_required")
    sources = jobs[0].get("sources")
    require(isinstance(sources, list) and 0 < len(sources) <= MAX_SOURCES, "invalid_sources")
    seen = set()
    for source in sources:
        require(isinstance(source, dict), "invalid_source")
        sid = collector.slug(source.get("id"))
        require(sid not in seen, "duplicate_source_id")
        seen.add(sid)
        require(source.get("kind") in {"dataset", "metadata"}, "invalid_source_kind")
        if source.get("status") == "ready":
            require(isinstance(source.get("sourceId"), str) and bool(source["sourceId"]), "source_identity_required")
            require(isinstance(source.get("datasetId"), str) and bool(source["datasetId"]), "source_identity_required")
            require(source.get("validator") in collector.VALIDATORS, "unsupported_validator")
            collector.validate_url(source.get("url"), manifest.get("security", {}).get("allowedUrls", []))
    return manifest_path, manifest, sources, digest(body)


def source_identity(source):
    return {key: source.get(key) for key in ("id", "sourceId", "datasetId", "kind")}


def check_identity(value, source, *, kind=True):
    require(isinstance(value, dict), "invalid_source_metadata")
    keys = ("id", "sourceId", "datasetId", "kind") if kind else ("sourceId", "datasetId")
    require(all(value.get(k) == source.get(k) for k in keys), "source_identity_mismatch")


def hash_value(value, code):
    require(isinstance(value, str) and DIGEST.fullmatch(value) is not None, code)
    return value


def policy_fingerprint(source, observed_at):
    """Match the collector's effective-request policy, including year tokens."""
    observed_at = collector.aware_time(observed_at)
    return collector.policy_fingerprint(source, observed_at)


def check_latest(body, raw, source):
    pointer = decode_json(body)
    check_identity(pointer, source)
    require(set(pointer) <= SNAPSHOT_FIELDS, "unrecognized_snapshot_field")
    sha = hash_value(pointer.get("sha256"), "invalid_body_hash")
    require(pointer.get("body") == f"bodies/{sha}.raw", "unsafe_body_reference")
    require(digest(raw) == sha, "body_digest_mismatch")
    require(type(pointer.get("bytes")) is int and pointer["bytes"] == len(raw), "body_size_mismatch")
    hash_value(pointer.get("requestPolicyFingerprint"), "invalid_policy_fingerprint")
    collector.aware_time(pointer.get("collectedAt"))
    require(pointer["requestPolicyFingerprint"] == policy_fingerprint(source, pointer["collectedAt"]), "snapshot_policy_mismatch")
    if pointer.get("previousSha256") is not None:
        hash_value(pointer["previousSha256"], "invalid_previous_hash")
    maximum = collector.positive_limit(source.get("maxBytes", collector.HARD_BYTES), collector.HARD_BYTES, "invalid_source_byte_limit")
    require(len(raw) <= maximum, "source_body_too_large")
    records = collector.positive_limit(source.get("maxRecords", collector.HARD_RECORDS), collector.HARD_RECORDS, "invalid_record_limit")
    validated = collector.VALIDATORS[source["validator"]](raw, source, records)
    require(all(type(pointer.get(key)) is type(value) and pointer.get(key) == value
                for key, value in validated.items()), "snapshot_validation_metadata_mismatch")
    return pointer


def check_block(body, source):
    value = decode_json(body)
    check_identity(value, source, kind=False)
    require(set(value) == BLOCK_FIELDS, "invalid_blocked_outcome_fields")
    hash_value(value["requestPolicyFingerprint"], "invalid_policy_fingerprint")
    collector.aware_time(value["blockedAt"])
    require(value["requestPolicyFingerprint"] == policy_fingerprint(source, value["blockedAt"]), "blocked_policy_mismatch")
    require(value["reason"] in {"http_401", "http_403", "http_429", "redirect_forbidden", "html_provider_challenge"}, "invalid_blocked_reason")
    return value


def check_run(body, source):
    value = decode_json(body)
    check_identity(value, source)
    require(set(value) <= RUN_FIELDS, "unrecognized_run_field")
    require(value.get("status") in {"saved", "unchanged", "not_due", "blocked", "failed"}, "invalid_run_status")
    for key in ("collectedAt", "previousCollectedAt", "blockedAt"):
        if value.get(key) is not None:
            collector.aware_time(value[key])
    if value.get("requestPolicyFingerprint") is not None:
        hash_value(value["requestPolicyFingerprint"], "invalid_policy_fingerprint")
    if "reason" in value:
        require(isinstance(value["reason"], str) and re.fullmatch(r"[a-zA-Z0-9_-]{1,96}", value["reason"]), "invalid_run_reason")
    return value


def member_name(name):
    require(isinstance(name, str) and name and "\\" not in name and ":" not in name and "\x00" not in name, "unsafe_archive_path")
    path = PurePosixPath(name)
    require(not path.is_absolute() and name == path.as_posix() and all(p not in {"", ".", ".."} for p in path.parts), "unsafe_archive_path")
    return name


def summary(sources):
    return {"datasetSnapshots": sum(s["snapshotStatus"] == "present" and s["kind"] == "dataset" for s in sources),
            "metadataSnapshots": sum(s["snapshotStatus"] == "present" and s["kind"] == "metadata" for s in sources),
            "missingSnapshots": sum(s["snapshotStatus"] == "missing" for s in sources),
            "policyBlockedSources": sum(s["snapshotStatus"] == "not_requested" for s in sources),
            "blockedOutcomes": sum("blockedOutcome" in s for s in sources)}


def export_bundle(manifest_path, job_id, output, *, now=None):
    manifest_path, manifest, sources, manifest_hash = contract(manifest_path, job_id)
    source_root = collector.output_directory(manifest_path, manifest)
    output = Path(output).absolute()
    require(not output.resolve().is_relative_to(source_root.resolve()), "output_inside_source_root")
    require(not output.exists() and not output.is_symlink(), "output_already_exists")
    now = now or datetime.now(collector.UTC)
    collector.aware_time(collector.stamp(now))
    blobs, entries, total = {}, [], 0

    def include(name, body):
        nonlocal total
        member_name(name)
        require(name not in blobs, "duplicate_archive_member")
        total += len(body)
        require(total <= MAX_BYTES and len(blobs) < MAX_ENTRIES - 1, "bundle_limit_exceeded")
        blobs[name] = body
        return name

    with collector.global_lock(collector.beneath(source_root, "collector.lock")) as acquired:
        require(acquired, "collector_busy")
        job_root = collector.beneath(source_root, job_id)
        for source in sources:
            entry = {**source_identity(source), "policyStatus": source.get("status"),
                     "configuredUnit": source.get("unit"), "snapshotStatus": "not_requested"}
            if source.get("status") != "ready":
                entry["reason"] = "source_not_ready"
                entries.append(entry)
                continue
            sid = source["id"]
            local = collector.beneath(job_root, sid)
            pointer_path = collector.beneath(local, "latest.json")
            entry["snapshotStatus"] = "missing"
            if pointer_path.exists():
                pointer_body = read_bounded(pointer_path, MAX_JSON_BYTES)
                pointer = decode_json(pointer_body)
                check_identity(pointer, source)
                sha = hash_value(pointer.get("sha256"), "invalid_body_hash")
                raw = read_bounded(collector.beneath(local, "bodies", sha + ".raw"), collector.HARD_BYTES)
                pointer = check_latest(pointer_body, raw, source)
                entry.update(snapshotStatus="present", latest=include(f"sources/{sid}/latest.json", pointer_body),
                             body=include(f"sources/{sid}/bodies/{sha}.raw", raw))
            blocked_path = collector.beneath(local, "blocked-outcome.json")
            if blocked_path.exists():
                blocked_body = read_bounded(blocked_path, MAX_JSON_BYTES)
                check_block(blocked_body, source)
                entry["blockedOutcome"] = include(f"sources/{sid}/blocked-outcome.json", blocked_body)
            run_root = collector.beneath(local, "runs")
            candidates = []
            if run_root.exists():
                for candidate in run_root.iterdir():
                    if candidate.is_file() and RUN_ID.fullmatch(candidate.stem) and candidate.suffix == ".json":
                        candidates.append(candidate)
                        require(len(candidates) <= 10000, "too_many_run_receipts")
            if candidates:
                recent = max(candidates, key=lambda p: p.name)
                run_body = read_bounded(collector.beneath(run_root, recent.name), MAX_JSON_BYTES)
                check_run(run_body, source)
                entry["recentRun"] = include(f"sources/{sid}/runs/{recent.name}", run_body)
            entries.append(entry)

    index = {"schema": SCHEMA, "purpose": "internal_collaboration", "publicDistributionApproved": False,
             "createdAt": collector.stamp(now), "job": job_id, "manifestSha256": manifest_hash,
             "collectorSha256": digest(Path(collector.__file__).read_bytes()),
             "meaning": "Integrity verification is not execution authority, database/game import or publication approval.",
             "sources": entries, "summary": summary(entries),
             "files": [{"path": name, "bytes": len(body), "sha256": digest(body)} for name, body in sorted(blobs.items())]}
    index_body = json_bytes(index)
    require(len(index_body) <= MAX_JSON_BYTES and total + len(index_body) <= MAX_BYTES, "bundle_limit_exceeded")
    output.parent.mkdir(parents=True, exist_ok=True)
    created_identity = None
    try:
        with output.open("xb") as stream:
            created_stat = os.fstat(stream.fileno())
            created_identity = (created_stat.st_dev, created_stat.st_ino)
            with zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_STORED) as archive:
                archive.writestr(INDEX, index_body)
                for name, body in sorted(blobs.items()):
                    archive.writestr(name, body)
            stream.flush()
            os.fsync(stream.fileno())
        verified = verify_bundle(output, manifest_path, job_id)
        return {**verified, "status": "exported", "bundle": str(output.resolve())}
    except Exception:
        if created_identity and output.is_file() and not output.is_symlink():
            current_stat = output.stat()
            if (current_stat.st_dev, current_stat.st_ino) == created_identity:
                output.unlink()
        raise


def verify_bundle(bundle_path, manifest_path, job_id, *, expected_sha256=None):
    _, _, sources, manifest_hash = contract(manifest_path, job_id)
    bundle_path = Path(bundle_path)
    # ZIP overhead is bounded separately from the uncompressed payload budget.
    raw_zip = read_bounded(bundle_path, MAX_BYTES + MAX_ENTRIES * 1024)
    bundle_hash = digest(raw_zip)
    if expected_sha256 is not None:
        require(bundle_hash == hash_value(expected_sha256, "invalid_expected_bundle_hash"), "bundle_digest_mismatch")
    from io import BytesIO
    with zipfile.ZipFile(BytesIO(raw_zip)) as archive:
        infos = archive.infolist()
        require(0 < len(infos) <= MAX_ENTRIES, "archive_entry_limit")
        names, folded, total = set(), set(), 0
        for info in infos:
            name = member_name(info.filename)
            require(name not in names and name.casefold() not in folded, "duplicate_archive_member")
            names.add(name); folded.add(name.casefold())
            mode = (info.external_attr >> 16) & 0o170000
            require(not info.is_dir() and mode in {0, stat.S_IFREG} and not info.flag_bits & 1, "invalid_archive_member_type")
            require(info.compress_type in {zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED}, "unsupported_zip_compression")
            require(0 <= info.file_size <= MAX_BYTES, "archive_size_limit")
            total += info.file_size
        require(total <= MAX_BYTES and INDEX in names, "archive_size_limit")

        def read(name, limit):
            member_name(name)
            require(name in names and archive.getinfo(name).file_size <= limit, "missing_or_oversized_member")
            with archive.open(name) as stream:
                body = stream.read(limit + 1)
            require(len(body) <= limit, "archive_size_limit")
            return body

        index = decode_json(read(INDEX, MAX_JSON_BYTES))
        require(isinstance(index, dict) and set(index) == {"schema", "purpose", "publicDistributionApproved", "createdAt", "job", "manifestSha256", "collectorSha256", "meaning", "sources", "summary", "files"}, "invalid_bundle_manifest")
        require(index["schema"] == SCHEMA and index["purpose"] == "internal_collaboration" and index["publicDistributionApproved"] is False, "invalid_bundle_purpose")
        require(index["job"] == job_id and index["manifestSha256"] == manifest_hash, "manifest_or_job_mismatch")
        hash_value(index["collectorSha256"], "invalid_collector_hash")
        require(index["collectorSha256"] == digest(Path(collector.__file__).read_bytes()), "collector_version_mismatch")
        collector.aware_time(index["createdAt"])
        require(isinstance(index["sources"], list) and len(index["sources"]) == len(sources), "source_coverage_mismatch")
        require(isinstance(index["files"], list) and len(index["files"]) < MAX_ENTRIES, "invalid_file_inventory")
        inventoried = set()
        for record in index["files"]:
            require(isinstance(record, dict) and set(record) == {"path", "bytes", "sha256"}, "invalid_file_inventory")
            name = member_name(record["path"])
            require(name != INDEX and name not in inventoried and type(record["bytes"]) is int and record["bytes"] >= 0, "invalid_file_inventory")
            body = read(name, MAX_BYTES)
            require(len(body) == record["bytes"] and digest(body) == hash_value(record["sha256"], "invalid_inventory_hash"), "member_digest_or_size_mismatch")
            inventoried.add(name)
        require(inventoried | {INDEX} == names, "unexpected_archive_member")
        referenced = set()
        for source, entry in zip(sources, index["sources"]):
            check_identity(entry, source)
            require(entry.get("policyStatus") == source.get("status") and entry.get("configuredUnit") == source.get("unit"), "source_policy_mismatch")
            allowed = {"id", "sourceId", "datasetId", "kind", "policyStatus", "configuredUnit", "snapshotStatus", "reason", "latest", "body", "blockedOutcome", "recentRun"}
            require(set(entry) <= allowed, "unrecognized_source_entry")
            sid = source["id"]
            if source.get("status") != "ready":
                require(entry.get("snapshotStatus") == "not_requested" and entry.get("reason") == "source_not_ready" and not set(entry) & {"latest", "body", "blockedOutcome", "recentRun"}, "blocked_source_has_files")
                continue
            require(entry.get("snapshotStatus") in {"present", "missing"}, "invalid_snapshot_status")
            if entry["snapshotStatus"] == "present":
                require(entry.get("latest") == f"sources/{sid}/latest.json", "unsafe_latest_reference")
                pointer_body = read(entry["latest"], MAX_JSON_BYTES)
                pointer = decode_json(pointer_body)
                check_identity(pointer, source)
                sha = hash_value(pointer.get("sha256"), "invalid_body_hash")
                require(entry.get("body") == f"sources/{sid}/bodies/{sha}.raw", "unsafe_body_reference")
                check_latest(pointer_body, read(entry["body"], collector.HARD_BYTES), source)
                referenced.update((entry["latest"], entry["body"]))
            else:
                require("latest" not in entry and "body" not in entry, "missing_snapshot_has_files")
            if "blockedOutcome" in entry:
                require(entry["blockedOutcome"] == f"sources/{sid}/blocked-outcome.json", "unsafe_blocked_reference")
                check_block(read(entry["blockedOutcome"], MAX_JSON_BYTES), source)
                referenced.add(entry["blockedOutcome"])
            if "recentRun" in entry:
                name = entry["recentRun"]
                require(isinstance(name, str) and re.fullmatch(rf"sources/{re.escape(sid)}/runs/[0-9]{{8}}T[0-9]{{6}}Z-[a-f0-9]{{12}}\.json", name), "unsafe_run_reference")
                check_run(read(name, MAX_JSON_BYTES), source)
                referenced.add(name)
        require(referenced == inventoried, "unreferenced_or_missing_member")
        require(index["summary"] == summary(index["sources"]), "summary_mismatch")
        return {"schema": SCHEMA, "status": "verified", "purpose": "internal_collaboration", "job": job_id,
                "sha256": bundle_hash, "summary": index["summary"], "publicDistributionApproved": False,
                "verificationMeaning": "Integrity and source contract only; no execution/import/publication authority."}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    export = commands.add_parser("export", help="Create a new ZIP while holding the collector OS lock; never overwrite.")
    export.add_argument("--manifest", required=True, type=Path)
    export.add_argument("--job", required=True)
    export.add_argument("--output", required=True, type=Path)
    verify = commands.add_parser("verify", help="Read-only ZIP integrity and current source-contract verification; no extraction.")
    verify.add_argument("--bundle", required=True, type=Path)
    verify.add_argument("--manifest", required=True, type=Path)
    verify.add_argument("--job", required=True)
    verify.add_argument("--expected-sha256")
    args = parser.parse_args(argv)
    try:
        if args.command == "export":
            result = export_bundle(args.manifest, args.job, args.output)
        else:
            result = verify_bundle(args.bundle, args.manifest, args.job, expected_sha256=args.expected_sha256)
    except (TransferError, collector.CollectionError, OSError, ValueError, TypeError, KeyError, zipfile.BadZipFile, RuntimeError) as exc:
        result = {"status": "failed", "reason": exc.code if isinstance(exc, (TransferError, collector.CollectionError)) else "invalid_bundle_or_storage"}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 1 if result["status"] == "failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())

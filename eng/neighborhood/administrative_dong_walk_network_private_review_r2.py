#!/usr/bin/env python3
"""현재 OA-21208 수집본을 별도 G3c r2 비공개 검토 후보로 만든다.

이 생성기는 과거 G3c r1 동결 범위나 DB 원장을 재구성하지 않는다. 2026-09-26에
다시 받은 한 영수증의 세 자치구 CSV만 결속하고, 결과는 로컬 비공개 검토에만
사용한다. 결과의 선과 점은 현재 통행 가능성, Unity runtime, 공개 지도의 권위가
아니다.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import stat
import struct
import sys
import uuid
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence

import administrative_dong_walk_network_candidate as parser


ROOT_DEFAULT = Path(__file__).resolve().parents[2]
REVISION = "northeast-seoul-admin-dong-walk-network-private-review.g3c.r2"
CANDIDATE_SCHEMA = "administrative-dong-walk-network-private-review-candidate.v2"
MANIFEST_SCHEMA = "administrative-dong-walk-network-private-review-manifest.v2"
AUDIT_SCHEMA = "administrative-dong-walk-network-private-review-audit.v2"
COMPLETE_SCHEMA = "administrative-dong-walk-network-private-review-complete.v2"
OUTPUT_RELATIVE = Path(
    "artifacts/local/validation/admin-dong-walk-network-private-review/r2"
)
RAW_RELATIVE = Path(
    "artifacts/local/public-data/"
    "admin-dong-walk-network-northeast-seoul-20260915-g3c-r1/raw"
)
BOUNDARY_RELATIVE = Path(
    "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/"
    "seoul-administrative-dong-boundary.zip"
)
R2_SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json"
)
R2_MANIFEST_RELATIVE = Path(
    "artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2/"
    "scope-manifest.json"
)
PARSER_RELATIVE = Path("eng/neighborhood/administrative_dong_walk_network_candidate.py")

EXPECTED_INPUTS = {
    "receipt": (RAW_RELATIVE / "receipt.json", 2481,
                "6395471456E77DFCC643404CF08C074014C8921E94C3EA7C9079CA86629AE504"),
    "codebook": (RAW_RELATIVE / "link-node-type-codes.xlsx", 12510,
                 "C718A21352D91B2A9BE50E8C52ED886D92A495ADB14070995B487B0CA5AB4E4F"),
    "gwangjinCsv": (RAW_RELATIVE / "gwangjin.csv", 3647541,
                    "BB485FDF1B31963F1109E4433E660DC6E4B2AF63A15B07D8356EE7F33F365915"),
    "dongdaemunCsv": (RAW_RELATIVE / "dongdaemun.csv", 5066359,
                      "5DFBF2F0C237B680FFD2BF2B7CA8D6B43D35110C87398133F13B3F1C5B3B37C6"),
    "jungnangCsv": (RAW_RELATIVE / "jungnang.csv", 4771869,
                    "55192969525242522C0335C56605B1286EC7D19C7365A65532291BBA5DEE4321"),
    "oa22160BoundaryArchive": (BOUNDARY_RELATIVE, 1676539,
                               "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"),
    "r2ScopeDefinition": (R2_SCOPE_RELATIVE, 11323,
                          "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"),
    "r2ScopeManifest": (R2_MANIFEST_RELATIVE, 18800,
                        "5F651C5460173A5C2EFD68F0E04DB06880B8F8F4CAE74A38AD0BDE11637D5113"),
}

AUTHORITY_FLAGS = {
    "privateReviewOnly": True,
    "historicalBoundaryBootstrapOnly": True,
    "observationCandidateOnly": True,
    "sourceDeclaredCoordinateReference": True,
    "privateReviewVisualizationAllowed": True,
    "exactCoordinatePublicationAllowed": False,
    "currentAdministrativeBoundaryEstablished": False,
    "currentPassabilityEstablished": False,
    "sidewalkWidthEstablished": False,
    "curbEstablished": False,
    "entranceBindingEstablished": False,
    "motorcycleAccessEstablished": False,
    "vehicleLaneEstablished": False,
    "signalBindingEstablished": False,
    "distributionApproved": False,
    "publicDisplayAllowed": False,
    "runtimeAuthorized": False,
    "traversalReady": False,
    "gameplayReady": False,
    "unityApplyAllowed": False,
    "databasePersistenceCompleted": False,
    "currentPointerUsed": False,
    "currentPointerUpdated": False,
    "personalDataIncluded": False,
}


class PrivateReviewError(RuntimeError):
    pass


def require(condition: bool, code: str) -> None:
    if not condition:
        raise PrivateReviewError(code)


def canonical_json(value: Any) -> str:
    return json.dumps(
        value, ensure_ascii=False, sort_keys=True, separators=(",", ":"),
        allow_nan=False,
    )


def pretty_json(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def framed_hash(values: Iterable[str]) -> str:
    digest = hashlib.sha256()
    for value in values:
        raw = value.encode("utf-8")
        require(len(raw) <= 0xFFFFFFFF, "HashFieldTooLarge")
        digest.update(struct.pack(">I", len(raw)))
        digest.update(raw)
    return digest.hexdigest().upper()


def content_hashed(value: dict[str, Any]) -> dict[str, Any]:
    result = dict(value)
    result.pop("contentHashSha256", None)
    result["contentHashSha256"] = sha_bytes(canonical_json(result).encode("utf-8"))
    return result


def safe_path(root: Path, relative: Path | str) -> Path:
    root = root.resolve()
    path = (root / relative).resolve()
    require(path == root or root in path.parents, "PathEscapesRepository")
    return path


def is_reparse(path: Path) -> bool:
    try:
        value = path.lstat()
    except OSError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(getattr(value, "st_file_attributes", 0) & flag)


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise PrivateReviewError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def require_exact_flat_files(folder: Path, names: set[str], code: str) -> None:
    require(folder.is_dir() and not is_reparse(folder), code + ":DirectoryUnsafe")
    entries = list(folder.iterdir())
    require(all(item.is_file() and not is_reparse(item) for item in entries),
            code + ":NonFileOrReparse")
    require({item.name for item in entries} == names, code + ":FileSetChanged")


def validate_file(path: Path, length: int, digest: str, code: str) -> None:
    require(path.is_file() and not is_reparse(path), code + ":Missing")
    require(path.stat().st_size == length, code + ":LengthChanged")
    require(sha_file(path) == digest, code + ":HashChanged")


def load_inputs(root: Path) -> tuple[dict[str, Any], dict[str, Path], dict[str, str]]:
    paths: dict[str, Path] = {}
    hashes: dict[str, str] = {}
    for role, (relative, length, digest) in EXPECTED_INPUTS.items():
        path = safe_path(root, relative)
        validate_file(path, length, digest, f"Input:{role}")
        paths[role] = path
        hashes[role] = digest

    # 기존 파서의 동적 영수증/CSV/코드북 검사를 재사용하되, 과거 r1 scope는
    # 호출하지 않는다. 위의 고정 해시가 2026-09-26 단일 수집본을 결속한다.
    receipt = parser.validate_raw_receipt(root)
    require(receipt.get("revision") ==
            "northeast-seoul-admin-dong-walk-network-candidate.g3c.r1",
            "SourceReceiptRevisionChanged")
    require(receipt.get("acquiredAtUtc") == "2026-09-26T02:47:41.887Z",
            "SourceReceiptAcquisitionChanged")
    require(receipt.get("portalDataUpdateDate") == "2026-09-26",
            "SourcePortalUpdateDateChanged")
    require(receipt.get("sourceReferenceVintage") == "2020" and
            receipt.get("sourceCoordinateReference") == "WGS84",
            "SourceVintageOrCrsChanged")
    require(receipt.get("sourceDistrictFilterBoundaryHaloComplete") is False,
            "SourceHaloAuthorityChanged")
    sources = {str(item.get("storedFileName", "")): item
               for item in receipt.get("sources", [])}
    expected_source_bindings = {
        "link-node-type-codes.xlsx": "codebook",
        "gwangjin.csv": "gwangjinCsv",
        "dongdaemun.csv": "dongdaemunCsv",
        "jungnang.csv": "jungnangCsv",
    }
    require(set(sources) == set(expected_source_bindings), "ReceiptSourceSetChanged")
    for stored_name, role in expected_source_bindings.items():
        require(sources[stored_name].get("sha256") == hashes[role],
                f"ReceiptSourceHashMismatch:{role}")

    r2_scope = load_json(paths["r2ScopeDefinition"])
    r2_manifest = load_json(paths["r2ScopeManifest"])
    scope_ids = sorted(str(item.get("administrativeAreaStableId", ""))
                       for item in r2_scope.get("administrativeAreas", []))
    manifest_ids = sorted(str(item.get("administrativeAreaStableId", ""))
                          for item in r2_manifest.get("modules", []))
    require(len(scope_ids) == 30 and len(set(scope_ids)) == 30 and scope_ids == manifest_ids,
            "HistoricalAdministrativeAreaScopeChanged")
    return receipt, paths, hashes


def build_candidates(root: Path, dep: parser.Dependencies,
                     paths: dict[str, Path], hashes: dict[str, str]
                     ) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    original_revision = parser.REVISION
    original_schema = parser.CANDIDATE_SCHEMA
    original_flags = parser.AUTHORITY_FLAGS
    try:
        parser.REVISION = REVISION
        parser.CANDIDATE_SCHEMA = CANDIDATE_SCHEMA
        parser.AUTHORITY_FLAGS = AUTHORITY_FLAGS
        candidates, audit = parser.build_candidates(root, {}, paths, hashes, dep)
    finally:
        parser.REVISION = original_revision
        parser.CANDIDATE_SCHEMA = original_schema
        parser.AUTHORITY_FLAGS = original_flags

    require(len(candidates) == int(audit["counts"]["candidateRows"]),
            "CandidateCountMismatch")
    require(int(audit["counts"]["sourceRows"]) == 59724, "SourceRowCountChanged")
    require(int(audit["counts"]["administrativeAreasWithCandidates"]) == 30,
            "AdministrativeAreaCoverageChanged")
    require(all(item.get("schemaVersion") == CANDIDATE_SCHEMA and
                item.get("revision") == REVISION and
                item.get("authorityFlags") == AUTHORITY_FLAGS
                for item in candidates), "CandidateRevisionOrAuthorityChanged")
    require(all(item.get("candidateKindCode") in {"Node", "LinkFragment"}
                for item in candidates), "CandidateKindChanged")
    require(all(AUTHORITY_FLAGS[key] is False for key in (
        "exactCoordinatePublicationAllowed", "distributionApproved",
        "publicDisplayAllowed", "runtimeAuthorized", "traversalReady",
        "gameplayReady", "unityApplyAllowed", "databasePersistenceCompleted",
        "currentPointerUsed", "currentPointerUpdated", "personalDataIncluded",
    )), "AuthorityBoundaryInvalid")
    return candidates, audit


@dataclass(frozen=True)
class Prepared:
    candidate_set_hash: str
    files: dict[str, bytes]
    generation_relative: Path
    counts: dict[str, int]
    per_area: dict[str, dict[str, int]]


def prepare(root: Path, dep: parser.Dependencies) -> Prepared:
    receipt, paths, hashes = load_inputs(root)
    candidates, audit_values = build_candidates(root, dep, paths, hashes)
    generator_hash = sha_file(Path(__file__).resolve())
    parser_hash = sha_file(safe_path(root, PARSER_RELATIVE))
    source_hashes = dict(sorted(hashes.items()))
    header = (
        CANDIDATE_SCHEMA,
        REVISION,
        generator_hash,
        parser_hash,
        canonical_json(source_hashes),
        canonical_json(AUTHORITY_FLAGS),
        canonical_json(len(candidates)),
    )
    candidate_set_hash = framed_hash(
        (*header, *(canonical_json(candidate) for candidate in candidates))
    )
    candidate_bytes = b"".join(
        (canonical_json(candidate) + "\n").encode("utf-8")
        for candidate in candidates
    )
    lineage = {
        "oa21208SingleAcquisitionReceiptBound": True,
        "oa21208SourceGenerationsMixed": False,
        "legacyFrozenG3cR1ScopeLoaded": False,
        "legacyFrozenG3cR1GenerationReconstructed": False,
        "sourceReceiptRevision": receipt["revision"],
        "sourceReceiptAcquiredAtUtc": receipt["acquiredAtUtc"],
        "sourceReceiptHashSha256": hashes["receipt"],
        "derivedReviewRevision": REVISION,
    }
    coordinate_policy = {
        "sourceCoordinateReference": "WGS84",
        "assignmentCoordinateReference": "EPSG:5186",
        "exactGeometryStorage": "LocalIgnoredPrivateReviewArtifactOnly",
        "exactCoordinatePublicationAllowed": False,
        "publicSummaryContainsExactCoordinates": False,
    }
    audit = content_hashed({
        "schemaVersion": AUDIT_SCHEMA,
        "revision": REVISION,
        "candidateSetHashSha256": candidate_set_hash,
        **audit_values,
        "sourceReferenceVintage": "2020",
        "boundaryReferenceVintage": "2023-10-31",
        "sourceDistrictFilterBoundaryHaloComplete": False,
        "sourceVintageMismatchCode": "Oa21208Year2020WithOa22160Boundary20231031",
        "lineage": lineage,
        "coordinatePublicationPolicy": coordinate_policy,
        "authorityFlags": AUTHORITY_FLAGS,
    })
    manifest = content_hashed({
        "schemaVersion": MANIFEST_SCHEMA,
        "revision": REVISION,
        "candidateSetHashSha256": candidate_set_hash,
        "candidateRows": len(candidates),
        "candidateNodeRows": audit_values["counts"]["candidateNodes"],
        "candidateLinkFragmentRows": audit_values["counts"]["candidateLinkFragments"],
        "administrativeAreaCount": 30,
        "qualityCode": "PendingHumanReview",
        "sourceHashes": source_hashes,
        "generatorHashSha256": generator_hash,
        "reusedParserRelativePath": PARSER_RELATIVE.as_posix(),
        "reusedParserHashSha256": parser_hash,
        "candidateHashContract": {
            "algorithm": "SHA-256",
            "encoding": "UTF-8",
            "framing": "UnsignedBigEndianUInt32ByteLengthThenUtf8",
            "candidateOrdering": [
                "administrativeAreaStableId", "candidateKindCode", "candidateStableId"
            ],
            "headerOrder": [
                "candidateSchemaVersion", "revision", "generatorHashSha256",
                "parserHashSha256", "sourceHashesCanonicalJson",
                "authorityFlagsCanonicalJson", "candidateCountCanonicalJson",
            ],
            "candidateBinding": "CanonicalFullCandidateJson",
        },
        "lineage": lineage,
        "coordinatePublicationPolicy": coordinate_policy,
        "perAdministrativeArea": audit_values["perAdministrativeArea"],
        "authorityFlags": AUTHORITY_FLAGS,
    })
    files = {
        "manifest.json": pretty_json(manifest),
        "candidates.ndjson": candidate_bytes,
        "audit.json": pretty_json(audit),
        "generator-source.py": Path(__file__).read_bytes(),
    }
    complete = content_hashed({
        "schemaVersion": COMPLETE_SCHEMA,
        "revision": REVISION,
        "completeMarker": True,
        "candidateSetHashSha256": candidate_set_hash,
        "files": [
            {
                "relativePath": name,
                "sha256": sha_bytes(data),
                "byteLength": len(data),
                "recordCount": len(candidates) if name == "candidates.ndjson" else 1,
            }
            for name, data in sorted(files.items())
        ],
        "legacyFrozenG3cR1GenerationReconstructed": False,
        "databasePersistenceCompleted": False,
        "currentPointerCreated": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "unityApplyAllowed": False,
    })
    files["complete.json"] = pretty_json(complete)
    generation = OUTPUT_RELATIVE / "generations" / candidate_set_hash.lower()
    return Prepared(
        candidate_set_hash,
        files,
        generation,
        dict(audit_values["counts"]),
        dict(audit_values["perAdministrativeArea"]),
    )


def verify_generation(root: Path, prepared: Prepared) -> None:
    target = safe_path(root, prepared.generation_relative)
    require_exact_flat_files(target, set(prepared.files), "Generation")
    for name, expected in prepared.files.items():
        require((target / name).read_bytes() == expected, f"GenerationFileChanged:{name}")


def materialize(root: Path, prepared: Prepared) -> dict[str, Any]:
    target = safe_path(root, prepared.generation_relative)
    if target.exists():
        verify_generation(root, prepared)
        return {"status": "PASS", "changedFiles": 0}
    require(not is_reparse(target.parent), "GenerationParentUnsafe")
    target.parent.mkdir(parents=True, exist_ok=True)
    staging = target.parent / (".staging-" + uuid.uuid4().hex)
    staging.mkdir()
    try:
        for name, data in prepared.files.items():
            (staging / name).write_bytes(data)
        require_exact_flat_files(staging, set(prepared.files), "GenerationStaging")
        for name, expected in prepared.files.items():
            require((staging / name).read_bytes() == expected,
                    f"GenerationStagingFileChanged:{name}")
        os.replace(staging, target)
        verify_generation(root, prepared)
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise
    return {"status": "PASS", "changedFiles": len(prepared.files)}


def self_tests(root: Path, dep: parser.Dependencies) -> int:
    passed = 0

    def test(value: bool, code: str) -> None:
        nonlocal passed
        require(value, "SelfTest:" + code)
        passed += 1

    receipt, paths, hashes = load_inputs(root)
    test(sum(int(item.get("rowCount", 0)) for item in receipt["sources"]) == 59724,
         "SingleReceiptSourceRows")
    test(hashes["receipt"] == EXPECTED_INPUTS["receipt"][2], "CurrentReceiptHashBound")
    test(all(hashes[role] == EXPECTED_INPUTS[role][2]
             for role in ("gwangjinCsv", "dongdaemunCsv", "jungnangCsv")),
         "CurrentCsvHashesBound")
    test(paths["r2ScopeDefinition"] !=
         safe_path(root, parser.SCOPE_RELATIVE), "LegacyG3cScopeNotUsedAsR2Scope")
    test(AUTHORITY_FLAGS["privateReviewOnly"] and
         AUTHORITY_FLAGS["privateReviewVisualizationAllowed"], "PrivateReviewEnabled")
    test(all(AUTHORITY_FLAGS[key] is False for key in (
        "exactCoordinatePublicationAllowed", "publicDisplayAllowed", "runtimeAuthorized",
        "traversalReady", "gameplayReady", "unityApplyAllowed",
        "databasePersistenceCompleted", "currentPointerUsed", "currentPointerUpdated",
    )), "PublicationAndAuthorityBlocked")
    boundary_scope = load_json(paths["r2ScopeDefinition"])
    boundaries = parser.read_boundaries(paths["oa22160BoundaryArchive"], boundary_scope, dep)
    test(len(boundaries) == 30 and len({item.stable_id for item in boundaries}) == 30,
         "ThirtyHistoricalBoundaries")
    test(framed_hash(("ab", "c")) != framed_hash(("a", "bc")), "FramedHashBoundary")
    return passed


def result_summary(prepared: Prepared) -> dict[str, Any]:
    per_area_counts = [int(value["candidateRows"]) for value in prepared.per_area.values()]
    return {
        "candidateSetHashSha256": prepared.candidate_set_hash,
        "candidateRows": prepared.counts["candidateRows"],
        "candidateNodes": prepared.counts["candidateNodes"],
        "candidateLinkFragments": prepared.counts["candidateLinkFragments"],
        "sourceRows": prepared.counts["sourceRows"],
        "administrativeAreasWithCandidates":
            prepared.counts["administrativeAreasWithCandidates"],
        "minimumCandidatesPerAdministrativeArea": min(per_area_counts),
        "maximumCandidatesPerAdministrativeArea": max(per_area_counts),
        "generationRelativePath": prepared.generation_relative.as_posix(),
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
    }


def arguments(argv: Sequence[str]) -> argparse.Namespace:
    cli = argparse.ArgumentParser()
    cli.add_argument("mode", choices=("self-test", "build", "verify"))
    cli.add_argument("root", nargs="?", default=str(ROOT_DEFAULT))
    return cli.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = arguments(sys.argv[1:] if argv is None else argv)
    root = Path(args.root).resolve()
    try:
        dep = parser.dependencies(root)
        if args.mode == "self-test":
            result: dict[str, Any] = {
                "status": "PASS",
                "selfTestsPassed": self_tests(root, dep),
            }
        else:
            prepared = prepare(root, dep)
            if args.mode == "build":
                result = materialize(root, prepared)
            else:
                verify_generation(root, prepared)
                result = {"status": "PASS", "verified": True}
            result.update(result_summary(prepared))
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (PrivateReviewError, parser.WalkCandidateError, OSError, ValueError,
            zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)},
                         ensure_ascii=False, sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Bind private OA-15536 point candidates to immutable 30-dong walk review bundles.

DRN is retained as an unresolved source numeric value. The overlay authorizes
only a position glyph for private review; arrow rendering and travel authority
remain closed. Existing v1-v3 bundle fields are copied without alteration.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import uuid
from collections import defaultdict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
WALK = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r3/input/generations/29764f2b861bf00d60951d23f6f03b9d59281d28c0df447e2f0aae35f2972915")
DIRECTION = Path("artifacts/local/validation/admin-dong-road-direction-private-review/r1/input/generations/19fba1601fcedf6aff1a41e497360c89ffc0492da869b992057a3a83410702cf")
OUTPUT = Path("artifacts/local/validation/admin-dong-diorama-observation-review/r4/input")
WALK_HASH = "29764F2B861BF00D60951D23F6F03B9D59281D28C0DF447E2F0AAE35F2972915"
DIRECTION_HASH = "19FBA1601FCEDF6AFF1A41E497360C89FFC0492DA869B992057A3A83410702CF"
WALK_INDEX_SHA = "63B8EEAC823E1CD5F8E4D636331419B4A7292148D1E09CB84AB77EC3FC57C977"
WALK_COMPLETE_SHA = "228307EEC9423AC426D5E119ED9610A284C7D960057C19E6EB82B3369CB24A82"
DIRECTION_MANIFEST_SHA = "F3E4B7EFCA02190068E52D21C85B665BD34706F34E5CA759D1626831EA7D1609"
DIRECTION_COMPLETE_SHA = "D24BFDE936171741BDD353C47C3015113753D57984EE8CD78D6030265F119C6E"
DIRECTION_CANDIDATES_SHA = "B9EF48FD6F54B1876A3B48CF46F5000CD4EBD6CD4BA0F997FDEF0CDEC09C3F12"
EXPECTED_AREAS = 30
EXPECTED_DIRECTION = 8_415
EXPECTED_WALK_NODES = 17_267
EXPECTED_WALK_FRAGMENTS = 23_472
EXPECTED_WALK_PATH_POINTS = 54_574
EXPECTED_WALK_OUTPUT_POINTS = 71_841
AT = "2026-09-26T00:00:00Z"
EXPORTER = "administrative-dong-diorama-unity-review-exporter.r5"
INDEX_SCHEMA = "administrative-dong-diorama-unity-review-index.v4"
BUNDLE_SCHEMA = "administrative-dong-diorama-unity-review-bundle.v4"
COMPLETE_SCHEMA = "administrative-dong-diorama-unity-review-completion.v4"
AUDIT_SCHEMA = "administrative-dong-diorama-unity-review-audit.v4"
OVERLAY_SCHEMA = "administrative-dong-private-road-direction-overlays.v1"


class DirectionUnityReviewError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise DirectionUnityReviewError(code)


def safe(root: Path, relative: Path) -> Path:
    target = (root / relative).resolve()
    require(target == root or root in target.parents, "PathEscapesRepository")
    return target


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")


def pretty(value: Any) -> bytes:
    return json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False).encode("utf-8") + b"\n"


def content_hash(value: dict[str, Any], field: str) -> str:
    body = dict(value)
    body.pop(field, None)
    return sha(canonical(body))


def checked(file: Path, expected: str) -> bytes:
    require(file.is_file() and not file.is_symlink(), "InputMissingOrUnsafe:" + file.name)
    data = file.read_bytes()
    require(sha(data) == expected, "InputHashChanged:" + file.name)
    return data


def bounds_mm(bounds: dict[str, Any]) -> dict[str, int]:
    return {key: round(float(bounds[key]) * 1000) for key in ("minX", "minZ", "maxX", "maxZ")}


def within(point: dict[str, Any], bounds: dict[str, int], code: str) -> None:
    require(set(point) == {"x", "z"} and isinstance(point["x"], int) and isinstance(point["z"], int), code + "CoordinateInvalid")
    require(bounds["minX"] <= point["x"] <= bounds["maxX"] and bounds["minZ"] <= point["z"] <= bounds["maxZ"], code + "OutOfManifestBounds")


def authority() -> dict[str, bool]:
    return {
        "privateReviewVisualizationAllowed": True,
        "reviewVisualizationOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "angleSemanticVerified": False,
        "directionResolved": False,
        "renderedArrowAuthorized": False,
        "sourceLengthUnitVerified": False,
        "publicDisplayAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "sourceCandidateUnityApplyAllowed": False,
    }


def safe_overlay(overlay: dict[str, Any]) -> int:
    forbidden = {"MGRNU", "LENX", "rawId", "managementNumber", "sourceRow",
                 "sourceEpsg5186Coordinates", "pointEpsg5186Millimeters",
                 "fragmentGeometryEpsg5186Millimeters", "longitude", "latitude"}

    def scan(value: Any) -> None:
        if isinstance(value, dict):
            require(not (set(value) & forbidden), "ForbiddenDirectionOverlayField")
            for child in value.values():
                scan(child)
        elif isinstance(value, list):
            for child in value:
                scan(child)

    scan(overlay)
    allowed = {
        "schemaVersion", "revision", "overlaySetStableId", "overlaySetHashSha256",
        "administrativeAreaStableId", "sourceVintage", "candidatePoints", "sourceReceipts",
        "privateReviewVisualizationAllowed", "reviewVisualizationOnly", "historicalBoundaryBootstrapOnly",
        "angleSemanticVerified", "directionResolved", "renderedArrowAuthorized",
        "sourceLengthUnitVerified", "publicDisplayAllowed", "runtimeAuthorized",
        "traversalReady", "gameplayReady", "currentPointerUsed", "currentPointerUpdated",
        "sourceCandidateUnityApplyAllowed",
    }
    require(set(overlay) == allowed, "OverlaySchemaChanged")
    for key, value in authority().items():
        require(overlay[key] is value, "OverlayAuthorityChanged:" + key)
    require(overlay["overlaySetHashSha256"] == content_hash(overlay, "overlaySetHashSha256"), "OverlayHashChanged")
    ids = set()
    for item in overlay["candidatePoints"]:
        require(set(item) == {"candidateStableId", "commonEnuMillimeters", "officialKindCode", "sourceDirectionNumericValue"}, "CandidateFieldForbiddenOrMissing")
        require(item["candidateStableId"].startswith("road-direction-symbol:sha256:") and len(item["candidateStableId"]) == len("road-direction-symbol:sha256:") + 64, "CandidateIdInvalid")
        require(item["candidateStableId"] not in ids, "CandidateIdDuplicated")
        ids.add(item["candidateStableId"])
        require(set(item["commonEnuMillimeters"]) == {"x", "z"}, "SourceCoordinateFieldForbidden")
        require(isinstance(item["sourceDirectionNumericValue"], (int, float)) and not isinstance(item["sourceDirectionNumericValue"], bool), "DirectionNumericInvalid")
    return len(ids)


def load_inputs(root: Path) -> tuple[Path, dict[str, Any], dict[str, Any], dict[str, list[dict[str, Any]]], dict[str, Any]]:
    walk = safe(root, WALK)
    direction = safe(root, DIRECTION)
    index = json.loads(checked(walk / "index.json", WALK_INDEX_SHA))
    complete = json.loads(checked(walk / "complete.json", WALK_COMPLETE_SHA))
    manifest = json.loads(checked(direction / "manifest.json", DIRECTION_MANIFEST_SHA))
    direction_complete = json.loads(checked(direction / "complete.json", DIRECTION_COMPLETE_SHA))
    require(index["schemaVersion"] == "administrative-dong-diorama-unity-review-index.v3" and index["generationHashSha256"] == WALK_HASH, "WalkIndexChanged")
    require(complete["schemaVersion"] == "administrative-dong-diorama-unity-review-completion.v3" and complete["generationHashSha256"] == WALK_HASH, "WalkCompletionChanged")
    require(manifest["schemaVersion"] == "administrative-dong-road-direction-private-manifest.v1" and manifest["generationHashSha256"] == DIRECTION_HASH, "DirectionManifestChanged")
    require(direction_complete["generationHashSha256"] == DIRECTION_HASH and manifest["candidateCount"] == EXPECTED_DIRECTION, "DirectionCompletionChanged")
    require(manifest["authority"]["directionResolved"] is False and manifest["authority"]["renderedArrowAuthorized"] is False, "DirectionSourceAuthorityChanged")
    require(index["administrativeAreaCount"] == EXPECTED_AREAS and len(index["bundles"]) == EXPECTED_AREAS, "WalkAreaCountChanged")
    require((index["walkNetworkNodePointCandidateCount"], index["walkNetworkLinkFragmentCandidateCount"], index["walkNetworkLinkPathVertexCount"], index["walkNetworkOutputPathVertexCount"]) == (EXPECTED_WALK_NODES, EXPECTED_WALK_FRAGMENTS, EXPECTED_WALK_PATH_POINTS, EXPECTED_WALK_OUTPUT_POINTS), "WalkCountsChanged")
    require(index["displayOverlayItemCount"] == 0, "PublicDisplayChanged")
    candidate_file = direction / "candidates.ndjson"
    require(sha_file(candidate_file) == DIRECTION_CANDIDATES_SHA, "DirectionCandidatesChanged")
    candidates: dict[str, list[dict[str, Any]]] = defaultdict(list)
    with candidate_file.open("r", encoding="utf-8") as stream:
        for line in stream:
            item = json.loads(line)
            require(set(item) == {"candidateStableId", "administrativeAreaStableId", "commonEnuMillimeters", "A055_KND_C", "DRN"}, "DirectionCandidateSchemaChanged")
            candidates[item["administrativeAreaStableId"]].append(item)
    require(len(candidates) == EXPECTED_AREAS and sum(map(len, candidates.values())) == EXPECTED_DIRECTION, "DirectionCoverageChanged")
    require(min(map(len, candidates.values())) == 101 and max(map(len, candidates.values())) == 653, "DirectionAreaRangeChanged")
    return walk, index, manifest, candidates, complete


def overlay(area: str, rows: list[dict[str, Any]], manifest: dict[str, Any], bounds: dict[str, int]) -> dict[str, Any]:
    points = []
    for row in sorted(rows, key=lambda item: item["candidateStableId"]):
        point = row["commonEnuMillimeters"]
        within(point, bounds, "DirectionPoint")
        points.append({
            "candidateStableId": row["candidateStableId"],
            "commonEnuMillimeters": point,
            "officialKindCode": row["A055_KND_C"],
            "sourceDirectionNumericValue": row["DRN"],
        })
    value = {
        "schemaVersion": OVERLAY_SCHEMA,
        "revision": "northeast-seoul-private-road-direction-overlay.r1",
        "overlaySetStableId": "private-road-direction-overlay:sha256:" + sha(canonical({"area": area, "directionGeneration": DIRECTION_HASH})).lower(),
        "administrativeAreaStableId": area,
        "sourceVintage": "oa15536-file-20260910+oa22160-file-20231031",
        "sourceReceipts": [{
            "datasetId": "OA-15536",
            "sourceRevision": "2026-09-10",
            "sourceArchiveSha256": manifest["sourceBinding"]["sourceArchiveSha256"],
            "sourceReceiptSha256": manifest["sourceBinding"]["sourceReceiptSha256"],
            "candidateGenerationHashSha256": DIRECTION_HASH,
        }, {
            "datasetId": "OA-22160",
            "sourceRevision": "2023-10-31 historical boundary",
            "sourceArchiveSha256": manifest["sourceBinding"]["historicalBoundarySha256"],
        }],
        "candidatePoints": points,
        **authority(),
    }
    value["overlaySetHashSha256"] = content_hash(value, "overlaySetHashSha256")
    require(safe_overlay(value) == len(rows), "OverlayCandidateCountChanged")
    return value


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    walk, base_index, source_manifest, candidates, base_complete = load_inputs(root)
    exporter_hash = sha_file(Path(__file__).resolve())
    files: dict[str, bytes] = {}
    entries = []
    overlays = []
    walk_node_points = walk_path_points = 0
    area_counts: dict[str, int] = {}
    for entry in sorted(base_index["bundles"], key=lambda item: item["administrativeAreaStableId"]):
        area = entry["administrativeAreaStableId"]
        relative = Path(entry["relativePath"])
        require(len(relative.parts) == 2 and relative.parts[0] == "bundles" and relative.suffix == ".json", "WalkBundlePathInvalid")
        base_file = safe(walk, relative)
        base_data = checked(base_file, entry["sha256"])
        require(len(base_data) == entry["byteLength"], "WalkBundleLengthChanged")
        bundle = json.loads(base_data)
        require(bundle["schemaVersion"] == "administrative-dong-diorama-unity-review-bundle.v3" and bundle["administrativeAreaStableId"] == area, "WalkBundleContractChanged")
        require(bundle["contentHashSha256"] == content_hash(bundle, "contentHashSha256") and bundle["privateWalkNetworkOverlaySetHashSha256"] == entry["privateWalkNetworkOverlaySetHashSha256"], "WalkBundleHashChanged")
        require(bundle["displayOverlays"]["items"] == [], "PublicDisplayChanged")
        bounds = bounds_mm(bundle["manifest"]["bounds"])
        walk_overlay = bundle["privateWalkNetworkOverlays"]
        require(walk_overlay["overlaySetHashSha256"] == entry["privateWalkNetworkOverlaySetHashSha256"], "WalkOverlayHashChanged")
        node_points = walk_overlay["nodePoints"]
        links = walk_overlay["linkFragments"]
        require(len(node_points) == entry["walkNetworkNodePointCandidateCount"] and len(links) == entry["walkNetworkLinkFragmentCandidateCount"], "WalkBundleCountsChanged")
        for item in node_points:
            within(item["commonEnuMillimeters"], bounds, "WalkNode")
            walk_node_points += 1
        path_count = 0
        for item in links:
            for point in item["commonEnuMillimeterPath"]:
                within(point, bounds, "WalkPath")
                walk_path_points += 1
                path_count += 1
        require(path_count == entry["walkNetworkLinkPathVertexCount"] and len(node_points) + path_count == entry["walkNetworkOutputPathVertexCount"], "WalkPathCountChanged")
        new_overlay = overlay(area, candidates[area], source_manifest, bounds)
        overlays.append(new_overlay)
        area_counts[area] = len(new_overlay["candidatePoints"])
        new_bundle = dict(bundle)
        new_bundle.update({
            "schemaVersion": BUNDLE_SCHEMA,
            "sourceVintage": bundle["sourceVintage"] + "+oa15536-file-20260910+road-direction-private-review-r1",
            "generatedAtUtc": AT,
            "exporterSemanticRevision": EXPORTER,
            "exporterSourceHashSha256": exporter_hash,
            "privateRoadDirectionOverlayCaptureReady": True,
            "privateRoadDirectionOverlaySetHashSha256": new_overlay["overlaySetHashSha256"],
            "roadDirectionPointCandidateCount": area_counts[area],
            "privateRoadDirectionOverlays": new_overlay,
        })
        new_bundle["contentHashSha256"] = content_hash(new_bundle, "contentHashSha256")
        payload = pretty(new_bundle)
        files[relative.as_posix()] = payload
        next_entry = dict(entry)
        next_entry.update({
            "sha256": sha(payload), "byteLength": len(payload),
            "contentHashSha256": new_bundle["contentHashSha256"],
            "privateRoadDirectionOverlayCaptureReady": True,
            "privateRoadDirectionOverlaySetHashSha256": new_overlay["overlaySetHashSha256"],
            "roadDirectionPointCandidateCount": area_counts[area],
        })
        entries.append(next_entry)
    require(len(entries) == EXPECTED_AREAS and sum(area_counts.values()) == EXPECTED_DIRECTION, "DirectionBundleCoverageChanged")
    require(walk_node_points == EXPECTED_WALK_NODES and walk_path_points == EXPECTED_WALK_PATH_POINTS, "WalkGeometryCoverageChanged")
    generation_hash = sha(canonical({
        "baseGenerationHashSha256": WALK_HASH,
        "baseIndexSha256": WALK_INDEX_SHA,
        "baseCompletionSha256": WALK_COMPLETE_SHA,
        "directionGenerationHashSha256": DIRECTION_HASH,
        "directionManifestSha256": DIRECTION_MANIFEST_SHA,
        "directionCandidatesSha256": DIRECTION_CANDIDATES_SHA,
        "exporterSourceHashSha256": exporter_hash,
        "bundleHashes": [item["sha256"] for item in entries],
        "overlaySetHashes": [item["overlaySetHashSha256"] for item in overlays],
    }))
    audit = {
        "schemaVersion": AUDIT_SCHEMA, "generationHashSha256": generation_hash,
        "baseGenerationHashSha256": WALK_HASH, "directionGenerationHashSha256": DIRECTION_HASH,
        "exporterSemanticRevision": EXPORTER, "exporterSourceHashSha256": exporter_hash,
        "counts": {"administrativeAreas": EXPECTED_AREAS, "roadDirectionPointCandidates": EXPECTED_DIRECTION,
                   "walkNetworkNodePoints": walk_node_points, "walkNetworkLinkPathVertices": walk_path_points,
                   "walkNetworkLinkFragmentCandidates": EXPECTED_WALK_FRAGMENTS,
                   "walkNetworkOutputPathVertices": EXPECTED_WALK_OUTPUT_POINTS,
                   "publicDisplayItems": 0, "forbiddenDirectionOverlayFields": 0},
        "perAdministrativeArea": dict(sorted(area_counts.items())),
        "minimumDirectionPointsPerArea": min(area_counts.values()),
        "maximumDirectionPointsPerArea": max(area_counts.values()),
        "allWalkAndDirectionPointsWithinManifestBounds": True,
        "walkValuesAndOverlayHashesPreserved": True,
        "sourceDirectionPolicy": {"meaning": "sourceNumericCandidateOnly", "zeroAxisVerified": False,
                                  "rotationDirectionVerified": False, "unitVerified": False},
        **authority(),
    }
    audit["contentHashSha256"] = content_hash(audit, "contentHashSha256")
    files["audit.json"] = pretty(audit)
    index = dict(base_index)
    index.update({
        "schemaVersion": INDEX_SCHEMA,
        "sourceVintage": base_index["sourceVintage"] + "+oa15536-file-20260910+road-direction-private-review-r1",
        "generatedAtUtc": AT, "exporterSemanticRevision": EXPORTER,
        "exporterSourceHashSha256": exporter_hash,
        "generationHashSha256": generation_hash, "bundleSetHashSha256": generation_hash,
        "privateRoadDirectionOverlayCaptureReady": True,
        "privateRoadDirectionOverlaySetHashSha256": sha(canonical([value["overlaySetHashSha256"] for value in overlays])),
        "roadDirectionPointCandidateCount": EXPECTED_DIRECTION,
        "roadDirectionAuditRelativePath": "audit.json", "roadDirectionAuditSha256": sha(files["audit.json"]),
        "directionSourceGenerationHashSha256": DIRECTION_HASH,
        "bundles": entries,
    })
    index["contentHashSha256"] = content_hash(index, "contentHashSha256")
    files["index.json"] = pretty(index)
    complete = dict(base_complete)
    complete.update({
        "schemaVersion": COMPLETE_SCHEMA,
        "generationHashSha256": generation_hash, "bundleSetHashSha256": generation_hash,
        "exporterSemanticRevision": EXPORTER, "exporterSourceHashSha256": exporter_hash,
        "baseWalkReviewGenerationHashSha256": WALK_HASH,
        "directionSourceGenerationHashSha256": DIRECTION_HASH,
        "privateRoadDirectionOverlayCaptureReady": True,
        "roadDirectionPointCandidateCount": EXPECTED_DIRECTION,
        "roadDirectionAuditSha256": sha(files["audit.json"]),
        "angleSemanticVerified": False, "directionResolved": False,
        "renderedArrowAuthorized": False, "sourceLengthUnitVerified": False,
        "files": [{"relativePath": name, "sha256": sha(data), "byteLength": len(data)} for name, data in sorted(files.items())],
    })
    complete["contentHashSha256"] = content_hash(complete, "contentHashSha256")
    files["complete.json"] = pretty(complete)
    summary = {"status": "PASS", "generationHashSha256": generation_hash,
               "indexSha256": sha(files["index.json"]), "baseWalkGenerationHashSha256": WALK_HASH,
               "directionSourceGenerationHashSha256": DIRECTION_HASH,
               "administrativeAreaCount": EXPECTED_AREAS, "roadDirectionPointCandidateCount": EXPECTED_DIRECTION,
               "minimumDirectionPointsPerArea": min(area_counts.values()),
               "maximumDirectionPointsPerArea": max(area_counts.values()),
               "walkNetworkNodePointCandidateCount": walk_node_points,
               "walkNetworkLinkFragmentCandidateCount": EXPECTED_WALK_FRAGMENTS,
               "walkNetworkLinkPathVertexCount": walk_path_points,
               "walkNetworkOutputPathVertexCount": EXPECTED_WALK_OUTPUT_POINTS,
               "allWalkAndDirectionPointsWithinManifestBounds": True,
               "forbiddenDirectionOverlayFields": 0, "publicDisplayItems": 0,
               **authority()}
    return generation_hash, files, summary


def validate(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not target.is_symlink(), "GenerationMissingOrUnsafe")
    actual = {item.relative_to(target).as_posix() for item in target.rglob("*") if item.is_file()}
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        require((target / name).is_file() and (target / name).read_bytes() == payload, "GenerationFileChanged:" + name)


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        b = {"minX": -1, "minZ": -2, "maxX": 1, "maxZ": 2}
        within({"x": 1, "z": -2}, b, "SelfTest")
        require(content_hash({"a": 1, "contentHashSha256": "ignored"}, "contentHashSha256") == sha(b'{"a":1}'), "HashSelfTestFailed")
        require(canonical({"b": 2, "a": 1}) == b'{"a":1,"b":2}', "CanonicalSelfTestFailed")
        require(all(value is False for key, value in authority().items() if key not in {"privateReviewVisualizationAllowed", "reviewVisualizationOnly", "historicalBoundaryBootstrapOnly"}), "AuthoritySelfTestFailed")
        return {"status": "PASS", "selfTestsPassed": 4, "exporterSourceHashSha256": sha_file(Path(__file__).resolve())}
    generation_hash, files, result = prepare(root)
    target = safe(root, OUTPUT / "generations" / generation_hash.lower())
    if mode == "verify" or target.exists():
        validate(target, files)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = target.parent / (".staging-" + uuid.uuid4().hex)
        staging.mkdir()
        try:
            for name, data in files.items():
                output = staging / name
                output.parent.mkdir(parents=True, exist_ok=True)
                output.write_bytes(data)
            validate(staging, files)
            os.replace(staging, target)
        except Exception:
            if staging.exists():
                shutil.rmtree(staging)
            raise
        changed = len(files)
    return {**result, "mode": mode, "changedFiles": changed,
            "generationRelativePath": target.relative_to(root).as_posix()}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        print(json.dumps(run(root, args.mode), ensure_ascii=False, sort_keys=True, indent=2))
        return 0
    except (DirectionUnityReviewError, OSError, ValueError, KeyError) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

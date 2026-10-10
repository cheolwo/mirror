"""확인된 배달 메뉴와 공식 사진 후보를 비공개 로컬 관측 목록으로 옮긴다.

판매가·판매 메뉴·운영 DB를 만들지 않는다. 표시 항목금액과 주문 합계에서
단가를 추론하지 않으며, 공식 사진의 메뉴명 후보를 옵션/권리 승인으로 승격하지 않는다.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
from datetime import date
from decimal import Decimal, InvalidOperation
from pathlib import Path
from urllib.parse import urlsplit


REPO = Path(__file__).resolve().parents[2]
LOCAL = (REPO / "artifacts/local").resolve()
DATASET = LOCAL / "delivery-history-replay-r54/dataset.json"
OUTPUT = LOCAL / "orderer-menu-catalog-r55"
ALLOWED_MATCHES = {"same_menu_name_candidate", "official_same_menu"}
SHA = re.compile(r"[0-9a-f]{64}\Z")
REPLAY_ID = re.compile(r"replay-(?:record|branch|pickup)-[0-9a-f]{64}\Z")
SENSITIVE = re.compile(r"(?:01[016789][- ]?\d{3,4}[- ]?\d{4}|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|고객\s*(?:성명|이름|전화|연락처)|전달지\s*상세)")
IMAGE_TYPES = {"JPEG": (".jpg", "image/jpeg"), "PNG": (".png", "image/png"),
               "WEBP": (".webp", "image/webp"), "GIF": (".gif", "image/gif")}


class ImportFailure(Exception):
    """콘솔에 원문·개인정보·경로 대신 안전한 실패 코드만 보낸다."""


def require(condition, code):
    if not condition:
        raise ImportFailure(code)


def digest(payload):
    return hashlib.sha256(payload).hexdigest()


def text(value, code, optional=False):
    if value is None and optional:
        return None
    require(isinstance(value, str), code)
    require(bool(value.strip()) and len(value) <= 1000 and not SENSITIVE.search(value), code)
    return value  # 관측 원문의 공백/표기도 바꾸지 않는다.


def money(value, code):
    if value is None:
        return None
    require(not isinstance(value, bool), code)
    try:
        parsed = Decimal(str(value))
    except (InvalidOperation, ValueError):
        raise ImportFailure(code) from None
    require(parsed.is_finite() and parsed >= 0, code)
    return int(parsed) if parsed == parsed.to_integral_value() else float(parsed)


def integer(value, code, optional=False):
    if value is None and optional:
        return None
    require(isinstance(value, int) and not isinstance(value, bool) and value > 0, code)
    return value


def stable_id(value, kind):
    require(isinstance(value, str) and REPLAY_ID.fullmatch(value)
            and value.startswith("replay-" + kind + "-"), "SOURCE_ID_INVALID")
    return value


def hash_value(value):
    require(isinstance(value, str) and SHA.fullmatch(value), "SOURCE_SHA_INVALID")
    return value


def date_value(value):
    try:
        parsed = date.fromisoformat(value)
    except (TypeError, ValueError):
        raise ImportFailure("SOURCE_DATE_INVALID") from None
    require(parsed.isoformat() == value, "SOURCE_DATE_INVALID")
    return value


def no_links(path, boundary):
    """resolve 이전 경로에서도 Windows junction/reparse point를 거부한다."""
    path, boundary = Path(path).absolute(), Path(boundary).absolute()
    require(path.is_relative_to(boundary), "PATH_OUTSIDE_BOUNDARY")
    for part in [boundary, *reversed(list(path.parents)[:len(path.parents) - len(boundary.parents)]), path]:
        if not part.exists():
            continue
        attributes = part.lstat()
        require(not stat.S_ISLNK(attributes.st_mode)
                and not (getattr(attributes, "st_file_attributes", 0) & 0x400), "LINK_PATH_FORBIDDEN")
    resolved, base = path.resolve(), boundary.resolve()
    require(resolved.is_relative_to(base), "PATH_OUTSIDE_BOUNDARY")
    return resolved


def local_path(path):
    resolved = no_links(path, LOCAL)
    require(resolved != LOCAL, "OUTPUT_IS_LOCAL_ROOT")
    relative = resolved.relative_to(REPO).as_posix()
    check = subprocess.run(["git", "check-ignore", "-q", "--", relative], cwd=REPO,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    require(check.returncode == 0, "OUTPUT_NOT_GIT_IGNORED")
    return resolved


def public_url(value):
    # 공식 CDN의 숫자 파일명은 전화번호처럼 보일 수 있다. 원장 텍스트 검사와
    # 분리하고, 출처 대조를 마친 공개 URL의 구조/인증 정보만 검사한다.
    require(isinstance(value, str) and 0 < len(value) <= 2048 and value == value.strip()
            and not re.search(r"[\x00-\x1f\x7f]", value), "SOURCE_URL_INVALID")
    parsed = urlsplit(value)
    require(parsed.scheme in {"http", "https"} and bool(parsed.hostname)
            and parsed.username is None and parsed.password is None
            and not re.search(r"\s", parsed.netloc)
            and parsed.hostname not in {"localhost", "127.0.0.1", "::1"}, "SOURCE_URL_INVALID")
    require(not re.search(r"(?:token|secret|password|signature|credential|shareKey)=", parsed.query, re.I),
            "SOURCE_URL_INVALID")
    return value


def index_unique(rows, key, code):
    require(isinstance(rows, list), code)
    result = {}
    for row in rows:
        require(isinstance(row, dict) and isinstance(row.get(key), str) and row[key] not in result, code)
        result[row[key]] = row
    return result


class Sources:
    def __init__(self):
        self.hashes = {}

    def read_bytes(self, path, boundary, expected=None):
        path = no_links(path, boundary)
        require(path.is_file(), "SOURCE_FILE_MISSING")
        payload = path.read_bytes()
        sha = digest(payload)
        require(expected is None or sha == hash_value(expected), "SOURCE_FILE_HASH_MISMATCH")
        require(path not in self.hashes or self.hashes[path] == sha, "SOURCE_CHANGED_DURING_IMPORT")
        self.hashes[path] = sha
        return payload, sha

    def read_json(self, path, boundary):
        payload, sha = self.read_bytes(path, boundary)
        try:
            data = json.loads(payload.decode("utf-8-sig"))
        except (ValueError, UnicodeError):
            raise ImportFailure("SOURCE_JSON_INVALID") from None
        require(isinstance(data, dict), "SOURCE_JSON_INVALID")
        return data, sha

    def verify_unchanged(self):
        require(all(path.is_file() and digest(path.read_bytes()) == sha for path, sha in self.hashes.items()),
                "SOURCE_CHANGED_DURING_IMPORT")


def evidence_hash(review, path, expected):
    matching = [row.get("sha256") for row in review.get("sources", [])
                if isinstance(row, dict) and isinstance(row.get("path"), str)
                and Path(row["path"]).resolve() == path.resolve()]
    require(matching == [expected], "REVIEW_SOURCE_HASH_MISMATCH")


def image_type(payload, declared):
    declared = str(declared).upper()
    require(declared in IMAGE_TYPES, "IMAGE_FORMAT_UNSUPPORTED")
    found = ("PNG" if payload.startswith(b"\x89PNG\r\n\x1a\n") else
             "JPEG" if payload.startswith(b"\xff\xd8\xff") else
             "GIF" if payload.startswith((b"GIF87a", b"GIF89a")) else
             "WEBP" if payload.startswith(b"RIFF") and payload[8:12] == b"WEBP" else None)
    require(found == declared, "IMAGE_SIGNATURE_MISMATCH")
    return IMAGE_TYPES[declared]


def atomic_write(path, payload):
    path = local_path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=".menu-import-", dir=path.parent, delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(payload)
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def import_catalog(dataset_path, image_root, output_dir):
    dataset_path = local_path(dataset_path)
    image_root = Path(image_root).absolute()
    image_root = no_links(image_root, image_root)
    require(image_root.is_dir(), "IMAGE_ROOT_MISSING")
    output_dir = local_path(output_dir)
    require(not dataset_path.is_relative_to(output_dir) and not image_root.is_relative_to(output_dir)
            and not output_dir.is_relative_to(image_root), "SOURCE_OUTPUT_OVERLAP")
    sources = Sources()
    full = image_root / "full-collection"
    dataset, dataset_sha = sources.read_json(dataset_path, LOCAL)
    review, review_sha = sources.read_json(full / "full-review.json", image_root)
    registry, registry_sha = sources.read_json(full / "source-registry.json", image_root)
    manifest, manifest_sha = sources.read_json(full / "image-collection-manifest.json", image_root)
    require(dataset.get("schemaVersion") == 1 and dataset.get("localVerificationOnly") is True,
            "DATASET_NOT_LOCAL_VERIFICATION")
    require(dataset.get("driverId") == "replay-driver", "DATASET_NOT_LOCAL_VERIFICATION")
    require(registry.get("sourceDatasetSha256") == dataset_sha, "REGISTRY_DATASET_HASH_MISMATCH")
    require(manifest.get("sourceRegistrySha256") == registry_sha, "MANIFEST_REGISTRY_HASH_MISMATCH")
    evidence_hash(review, dataset_path, dataset_sha)
    evidence_hash(review, full / "source-registry.json", registry_sha)
    evidence_hash(review, full / "image-collection-manifest.json", manifest_sha)

    branches_in = index_unique(dataset.get("branches"), "branchId", "BRANCHES_INVALID")
    records = index_unique(dataset.get("records"), "recordId", "RECORDS_INVALID")
    branches = [{"branchId": stable_id(b["branchId"], "branch"),
                 "displayName": text(b.get("displayName"), "BRANCH_NAME_INVALID"),
                 "publicPickupAddress": text(b.get("publicPickupAddress"), "PICKUP_ADDRESS_INVALID")}
                for b in branches_in.values()]
    orders, groups = [], {}
    for row in records.values():
        rid = stable_id(row["recordId"], "record")
        bid = stable_id(row.get("branchId"), "branch")
        require(bid in branches_in, "RECORD_BRANCH_MISMATCH")
        orders.append({"recordId": rid, "serviceDate": date_value(row.get("serviceDate")),
                       "sequence": integer(row.get("sequence"), "RECORD_SEQUENCE_INVALID"), "branchId": bid,
                       "pickupVisitId": stable_id(row.get("pickupVisitId"), "pickup"),
                       "foodOrderAmountKrw": money(row.get("foodOrderAmountKrw"), "ORDER_AMOUNT_INVALID")})
        items, summaries = row.get("menuItems"), row.get("menuSummary")
        require(isinstance(items, list) and isinstance(summaries, list), "MENU_COLLECTION_INVALID")
        require(not (items and summaries), "MENU_SUMMARY_DUPLICATES_STRUCTURED")
        for i, item in enumerate(items):
            require(isinstance(item, dict), "MENU_ITEM_INVALID")
            name = text(item.get("name"), "MENU_NAME_INVALID")
            options = item.get("options")
            require(isinstance(options, list), "MENU_OPTIONS_INVALID")
            observation = {"recordId": rid, "menuItemIndex": i, "summaryIndex": None,
                           "quantity": integer(item.get("quantity"), "MENU_QUANTITY_INVALID", optional=True),
                           "unitPriceKrw": money(item.get("unitPriceKrw"), "UNIT_PRICE_INVALID"),
                           "displayedItemAmountKrw": money(item.get("displayedItemAmountKrw"), "ITEM_AMOUNT_INVALID"),
                           "amountBasis": text(item.get("amountBasis"), "AMOUNT_BASIS_INVALID"),
                           "options": [text(value, "MENU_OPTION_INVALID") for value in options]}
            groups.setdefault((bid, "structured", name), []).append(observation)
        for i, name in enumerate(summaries):
            name = text(name, "MENU_SUMMARY_INVALID")
            observation = {"recordId": rid, "menuItemIndex": None, "summaryIndex": i, "quantity": None,
                           "unitPriceKrw": None, "displayedItemAmountKrw": None,
                           "amountBasis": "SummaryNotStructured", "options": []}
            groups.setdefault((bid, "per_order_summary", name), []).append(observation)

    review_menus = index_unique(review.get("menus"), "menuId", "REVIEW_MENUS_INVALID")
    by_key = {}
    for item in review_menus.values():
        key = (item.get("branchId"), item.get("sourceKind"), item.get("rawMenu"))
        require(key in groups and key not in by_key, "REVIEW_MENU_CANONICAL_MISMATCH")
        observed = {row["recordId"] for row in groups[key]}
        declared = item.get("sourceRecordIds")
        require(isinstance(declared, list) and len(declared) == len(set(declared))
                and set(declared) == observed, "REVIEW_RECORD_CANONICAL_MISMATCH")
        require(item.get("branchName") == branches_in[key[0]]["displayName"], "REVIEW_BRANCH_NAME_MISMATCH")
        by_key[key] = item
    candidates = index_unique(registry.get("candidates"), "candidateId", "CANDIDATES_INVALID")
    review_candidates = index_unique(review.get("candidateReview"), "candidateId", "REVIEW_CANDIDATES_INVALID")
    manifest_assets = index_unique(manifest.get("assets"), "assetId", "ASSETS_INVALID")
    review_assets = index_unique(review.get("assets"), "assetId", "REVIEW_ASSETS_INVALID")
    asset_links = {(x.get("candidateId"), x.get("assetId"), x.get("imageUrl"))
                   for x in manifest.get("candidateAssetLinks", []) if x.get("status") == "collected"}
    copied, menus, skipped = {}, [], collections.Counter()
    for key, observations in groups.items():
        reviewed = by_key.get(key)
        menu_id = (reviewed["menuId"] if reviewed else "recorded-menu-" + digest(
            json.dumps(key, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))[:24])
        text(menu_id, "MENU_ID_INVALID")
        image_candidates, seen = [], set()
        for link in (reviewed.get("candidateLinks", []) if reviewed else []):
            require(isinstance(link, dict), "IMAGE_LINK_INVALID")
            if link.get("matchLevel") not in ALLOWED_MATCHES:
                skipped["NotSameMenuName"] += 1
                continue
            cid, aid = link.get("candidateId"), link.get("assetId")
            if not aid:
                skipped["ImageNotCollected"] += 1
                continue
            require(cid in candidates and cid in review_candidates, "IMAGE_CANDIDATE_MISSING")
            source, audited = candidates[cid], review_candidates[cid]
            require(source.get("officialSourceStatus") == "verified"
                    and bool(source.get("associationEvidence")), "OFFICIAL_ASSOCIATION_UNVERIFIED")
            require(all(source.get(field) == audited.get(field) for field in
                        ("officialMenuName", "imageUrl", "sourcePageUrl", "sourcePageSha256")),
                    "REVIEW_CANDIDATE_SOURCE_MISMATCH")
            require(link.get("officialMenuName") == source.get("officialMenuName"), "IMAGE_MENU_NAME_MISMATCH")
            group_id = reviewed.get("inventoryGroupId")
            require(group_id and group_id in source.get("inventoryGroupIds", []), "IMAGE_BRAND_GROUP_MISMATCH")
            require(aid in manifest_assets and aid in review_assets, "IMAGE_ASSET_MISSING")
            require((cid, aid, source.get("imageUrl")) in asset_links, "IMAGE_ASSET_LINK_MISMATCH")
            asset, audited_asset = manifest_assets[aid], review_assets[aid]
            require(all(asset.get(field) == audited_asset.get(field) for field in
                        ("sha256", "assetPath", "bytes", "width", "height", "format")),
                    "REVIEW_ASSET_MISMATCH")
            require(cid in asset.get("candidateIds", []) and source.get("imageUrl") in asset.get("sourceUrls", []),
                    "IMAGE_ASSET_SOURCE_MISMATCH")
            source_page = Path(text(source.get("sourcePageFile"), "OFFICIAL_SOURCE_FILE_MISSING"))
            sources.read_bytes(source_page, image_root, source.get("sourcePageSha256"))
            source_url = public_url(source.get("sourcePageUrl"))
            public_url(source.get("imageUrl"))
            sha = hash_value(asset.get("sha256"))
            require(aid == "asset-" + sha, "IMAGE_ASSET_ID_MISMATCH")
            if aid not in copied:
                original = Path(text(asset.get("assetPath"), "IMAGE_PATH_INVALID"))
                payload, _ = sources.read_bytes(original, image_root, sha)
                require(len(payload) == asset.get("bytes"), "IMAGE_BYTES_MISMATCH")
                extension, mime = image_type(payload, asset.get("format"))
                destination = local_path(output_dir / "assets" / (sha + extension))
                if destination.exists():
                    require(digest(destination.read_bytes()) == sha, "EXISTING_IMAGE_HASH_MISMATCH")
                else:
                    atomic_write(destination, payload)
                copied[aid] = {"assetId": aid, "sha256": sha,
                               "relativePath": destination.relative_to(LOCAL).as_posix(), "mimeType": mime,
                               "bytes": len(payload), "width": integer(asset.get("width"), "IMAGE_WIDTH_INVALID"),
                               "height": integer(asset.get("height"), "IMAGE_HEIGHT_INVALID")}
            if (cid, aid) in seen:
                continue
            seen.add((cid, aid))
            image_candidates.append({"candidateId": cid, "assetId": aid,
                                     "officialMenuName": text(source.get("officialMenuName"), "OFFICIAL_MENU_NAME_INVALID"),
                                     "matchLevel": link["matchLevel"], "officialSourceStatus": "verified",
                                     "sourcePageUrl": source_url, "sourcePageSha256": source["sourcePageSha256"],
                                     "optionsMatch": False, "rightsStatus": "unconfirmed",
                                     "actualBranchPhotoVerified": False, "historicalProductVersionVerified": False,
                                     "publicDisplayApproved": False, "publicUseApproved": False})
        menus.append({"menuId": menu_id, "branchId": key[0], "rawMenu": key[2], "sourceKind": key[1],
                      "observations": observations, "imageCandidates": image_candidates})
    sources.verify_unchanged()
    summary = {"branches": len(branches), "orders": len(orders), "menuEntries": len(menus),
               "observations": sum(len(m["observations"]) for m in menus),
               "knownUnitPrices": sum(o["unitPriceKrw"] is not None for m in menus for o in m["observations"]),
               "knownDisplayedItemAmounts": sum(o["displayedItemAmountKrw"] is not None for m in menus for o in m["observations"]),
               "knownOrderAmounts": sum(o["foodOrderAmountKrw"] is not None for o in orders),
               "menusWithImages": sum(bool(m["imageCandidates"]) for m in menus),
               "imageCandidateLinks": sum(len(m["imageCandidates"]) for m in menus), "imageFiles": len(copied)}
    catalog = {"schemaVersion": 1, "localVerificationOnly": True, "executionModeCode": "Simulation",
               "operationalRegistration": False, "publicDisplayApproved": False, "publicUseApproved": False,
               "sourceDatasetSha256": dataset_sha,
               "sources": [{"kind": kind, "sha256": sha} for kind, sha in
                           (("CanonicalDeliveryDataset", dataset_sha), ("OfficialImageFullReview", review_sha),
                            ("OfficialImageSourceRegistry", registry_sha), ("OfficialImageCollectionManifest", manifest_sha))],
               "branches": branches, "orders": orders, "menus": menus, "assets": list(copied.values()), "summary": summary}
    payload = (json.dumps(catalog, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")
    proof = {"schemaVersion": 1, "catalogSha256": digest(payload), "sources": catalog["sources"], "summary": summary,
             "skippedImageLinks": dict(sorted(skipped.items())), "sourceFilesVerifiedUnchanged": len(sources.hashes),
             "sourceInputsChanged": False, "unitPriceInferred": False, "currentSellingPricesCreated": False,
             "operationalRegistration": False, "publicDisplayApproved": False,
             "excludedData": ["CustomerIdentity", "DeliveryAddress", "PlatformOrderId", "RawScreenshots",
                              "RawTranscript", "UnlinkedBrandReferenceImages", "ComponentOnlyImages", "AmbiguousImages"]}
    atomic_write(output_dir / "catalog.json", payload)
    atomic_write(output_dir / "import-proof.json", (json.dumps(proof, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    return catalog, proof


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path, default=DATASET)
    parser.add_argument("--image-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    arguments = parser.parse_args()
    try:
        catalog, _ = import_catalog(arguments.dataset, arguments.image_root, arguments.output)
        print(json.dumps({"status": "LocalVerificationCatalogCreated", **catalog["summary"]}, ensure_ascii=False))
        return 0
    except ImportFailure as error:
        print("MENU_CATALOG_IMPORT_FAILED: " + str(error), file=sys.stderr)
    except (OSError, ValueError, TypeError, KeyError):
        print("MENU_CATALOG_IMPORT_FAILED: SOURCE_OR_OUTPUT_UNAVAILABLE", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())

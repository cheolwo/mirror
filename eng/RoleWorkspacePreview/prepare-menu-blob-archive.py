"""메뉴 참고 사진의 비공개 Blob 보관 명세만 준비한다. 네트워크와 업로드는 실행하지 않는다."""
import argparse
import hashlib
import io
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
import warnings
from datetime import date
from pathlib import Path, PurePosixPath
from urllib.parse import urlsplit

from PIL import Image, UnidentifiedImageError


REPO = Path(__file__).resolve().parents[2]
LOCAL = REPO / "artifacts/local"
CATALOG = LOCAL / "orderer-menu-catalog-r55/catalog.json"
DEFAULT_CONTAINER = "platform-private"
SHA = re.compile(r"[a-f0-9]{64}\Z")
ID = re.compile(r"[A-Za-z0-9_-]{1,128}\Z")
TYPES = {"image/jpeg": ("JPEG", ".jpg", {".jpg", ".jpeg"}),
         "image/png": ("PNG", ".png", {".png"}),
         "image/webp": ("WEBP", ".webp", {".webp"}),
         "image/gif": ("GIF", ".gif", {".gif"})}
MAX_BYTES = 20 * 1024 * 1024
MAX_CATALOG_BYTES = 32 * 1024 * 1024
FALSE_FLAGS = {"operationalRegistration", "publicDisplayApproved", "publicUseApproved",
               "actualBranchPhotoVerified", "historicalProductVersionVerified", "optionsMatch"}


class PreparationFailure(Exception):
    """경로·인증값·원문을 출력하지 않는 실패 코드."""


def require(condition, code):
    if not condition:
        raise PreparationFailure(code)


def sha256(payload):
    return hashlib.sha256(payload).hexdigest()


def identifier(value):
    require(isinstance(value, str) and ID.fullmatch(value), "CatalogIdentifierInvalid")
    return value


def hash_value(value):
    require(isinstance(value, str) and SHA.fullmatch(value), "CatalogSha256Invalid")
    return value


def integer(value, code, maximum=None):
    require(type(value) is int and value > 0 and (maximum is None or value <= maximum), code)
    return value


def secure_local_path(path):
    path, boundary = Path(path).absolute(), LOCAL.absolute()
    require(path.is_relative_to(boundary) and path != boundary, "PathOutsideLocalBoundary")
    # resolve 전에 모든 기존 조상과 파일의 junction/symlink를 검사한다.
    for part in [*reversed(path.parents), path]:
        if part.exists() or part.is_symlink():
            info = part.lstat()
            require(not stat.S_ISLNK(info.st_mode)
                    and not (getattr(info, "st_file_attributes", 0) & 0x400), "LinkPathForbidden")
    require(path.resolve().is_relative_to(boundary.resolve()), "PathOutsideLocalBoundary")
    return path.resolve()


def asset_path(relative):
    require(isinstance(relative, str) and relative and "\\" not in relative and ":" not in relative
            and not re.search(r"[\x00-\x1f\x7f]", relative), "AssetPathInvalid")
    parts = relative.split("/")
    require(not PurePosixPath(relative).is_absolute() and all(p not in {"", ".", ".."} for p in parts),
            "AssetPathInvalid")
    return secure_local_path(LOCAL.joinpath(*parts))


def reject_duplicate_keys(pairs):
    result = {}
    folded = set()
    for key, value in pairs:
        require(key.casefold() not in folded, "CatalogDuplicateJsonKey")
        folded.add(key.casefold())
        result[key] = value
    return result


def reject_nonfinite(_):
    raise PreparationFailure("CatalogJsonInvalid")


def check_permissions(value):
    if isinstance(value, dict):
        for key, item in value.items():
            folded = key.casefold()
            if folded in {flag.casefold() for flag in FALSE_FLAGS}:
                require(item is False, "PermissionEscalationForbidden")
            if folded in {"localverificationonly", "rightsunconfirmed"}:
                require(item is True, "PermissionEscalationForbidden")
            if folded == "executionmodecode":
                require(item == "Simulation", "PermissionEscalationForbidden")
            if folded == "rightsstatus":
                require(item == "unconfirmed", "PermissionEscalationForbidden")
            check_permissions(item)
    elif isinstance(value, list):
        for item in value:
            check_permissions(item)


def index(rows, key):
    require(isinstance(rows, list), "CatalogListInvalid")
    result = {}
    for row in rows:
        require(isinstance(row, dict), "CatalogRowInvalid")
        value = identifier(row.get(key))
        require(value not in result, "CatalogDuplicateIdentifier")
        result[value] = row
    return result


def observed_service_uri(value):
    if value is None:
        return None
    # 검증되지 않은 후보 입력도 자격 증명·SAS·쿼리·경로를 받지 않는다.
    require(isinstance(value, str) and value == value.strip()
            and not re.search(r"[\s\x00-\x1f\x7f]", value), "ServiceUriInvalid")
    try:
        parsed = urlsplit(value)
    except ValueError:
        raise PreparationFailure("ServiceUriInvalid") from None
    require(parsed.scheme == "https" and parsed.username is None and parsed.password is None
            and not parsed.query and not parsed.fragment and parsed.path in {"", "/"}
            and re.fullmatch(r"[a-z0-9]{3,24}\.blob\.core\.windows\.net", parsed.netloc or ""),
            "ServiceUriInvalid")
    return "https://" + parsed.netloc


def read_source(path, hashes, maximum):
    path = secure_local_path(path)
    require(path.is_file(), "SourceFileMissing")
    require(0 < path.stat().st_size <= maximum, "SourceSizeInvalid")
    payload = path.read_bytes()
    require(0 < len(payload) <= maximum, "SourceSizeInvalid")
    value = sha256(payload)
    require(path not in hashes or hashes[path] == value, "SourceChangedDuringPreparation")
    hashes[path] = value
    return payload, value


def read_json(raw):
    try:
        value = json.loads(raw.decode("utf-8-sig"), object_pairs_hook=reject_duplicate_keys,
                           parse_constant=reject_nonfinite)
    except (UnicodeError, ValueError, RecursionError):
        raise PreparationFailure("CatalogJsonInvalid") from None
    require(isinstance(value, dict), "CatalogJsonInvalid")
    return value


def verify_image(payload, asset, path):
    mime = asset.get("mimeType")
    require(isinstance(mime, str) and mime in TYPES, "ImageMimeInvalid")
    format_name, extension, source_extensions = TYPES[mime]
    require(path.suffix.lower() in source_extensions, "ImageExtensionMismatch")
    width = integer(asset.get("width"), "ImageDimensionsInvalid")
    height = integer(asset.get("height"), "ImageDimensionsInvalid")
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", Image.DecompressionBombWarning)
            with Image.open(io.BytesIO(payload)) as picture:
                require(picture.format == format_name, "ImageMimeMismatch")
                require(picture.size == (width, height), "ImageDimensionsMismatch")
                picture.verify()
            with Image.open(io.BytesIO(payload)) as picture:
                picture.load()
    except (UnidentifiedImageError, OSError, SyntaxError, ValueError,
            Image.DecompressionBombError, Image.DecompressionBombWarning):
        raise PreparationFailure("ImageDecodeFailed") from None
    return extension


def verify_unchanged(hashes):
    for path, expected in hashes.items():
        path = secure_local_path(path)
        require(path.is_file() and sha256(path.read_bytes()) == expected, "SourceChangedDuringPreparation")


def write_manifest(path, payload, catalog_path, input_paths):
    path = secure_local_path(path)
    require(path.parent == catalog_path.parent and path.name not in {"catalog.json", "import-proof.json"}
            and path.suffix == ".json" and path not in input_paths, "OutputOverlapsSource")
    check = subprocess.run(["git", "check-ignore", "-q", "--", path.relative_to(REPO).as_posix()],
                           cwd=REPO, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    require(check.returncode == 0, "OutputNotGitIgnored")
    if path.exists():
        require(path.is_file() and path.read_bytes() == payload, "ExistingManifestConflict")
        return  # 재실행은 같은 명세를 덮어쓰거나 기존 실행 증거를 지우지 않는다.
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=".menu-blob-prepare-", dir=path.parent, delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        secure_local_path(path)
        secure_local_path(temporary)
        verify_unchanged(input_paths)
        try:
            # 같은 볼륨의 완성된 임시 파일을 새 이름으로만 게시한다. 경쟁 파일을 덮어쓰지 않는다.
            os.link(temporary, path)
        except FileExistsError:
            secure_local_path(path)
            require(path.is_file() and path.read_bytes() == payload, "ExistingManifestConflict")
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def prepare(catalog_path=CATALOG, output_path=None, service_uri=None, private_container_name=DEFAULT_CONTAINER):
    require(private_container_name == DEFAULT_CONTAINER, "PrivateContainerMismatch")
    observed_uri = observed_service_uri(service_uri)
    hashes = {}
    catalog_path = secure_local_path(catalog_path)
    raw, catalog_sha = read_source(catalog_path, hashes, MAX_CATALOG_BYTES)
    catalog = read_json(raw)
    check_permissions(catalog)
    require(type(catalog.get("schemaVersion")) is int and catalog["schemaVersion"] == 1
            and catalog.get("localVerificationOnly") is True and catalog.get("executionModeCode") == "Simulation"
            and all(catalog.get(k) is False for k in ("operationalRegistration", "publicDisplayApproved", "publicUseApproved")),
            "CatalogNotPrivateSimulation")
    proof_raw, proof_sha = read_source(catalog_path.with_name("import-proof.json"), hashes, MAX_CATALOG_BYTES)
    proof = read_json(proof_raw)
    check_permissions(proof)
    require(proof.get("schemaVersion") == 1 and proof.get("catalogSha256") == catalog_sha
            and proof.get("sources") == catalog.get("sources")
            and proof.get("sourceInputsChanged") is False and proof.get("unitPriceInferred") is False
            and proof.get("currentSellingPricesCreated") is False,
            "ImportProofCatalogMismatch")
    dataset_sha = hash_value(catalog.get("sourceDatasetSha256"))
    sources = index(catalog.get("sources"), "kind")
    require(sources.get("CanonicalDeliveryDataset", {}).get("sha256") == dataset_sha, "CatalogSourceBindingInvalid")
    for source in sources.values():
        hash_value(source.get("sha256"))
    branches = index(catalog.get("branches"), "branchId")
    orders = index(catalog.get("orders"), "recordId")
    menus = index(catalog.get("menus"), "menuId")
    source_assets = index(catalog.get("assets"), "assetId")
    require(bool(source_assets), "CatalogAssetsEmpty")
    for order in orders.values():
        require(identifier(order.get("branchId")) in branches, "OrderBranchMismatch")
        try:
            require(date.fromisoformat(order.get("serviceDate")).isoformat() == order["serviceDate"], "OrderDateInvalid")
        except (ValueError, TypeError):
            raise PreparationFailure("OrderDateInvalid") from None
    assets = []
    for asset_id, source in source_assets.items():
        expected = hash_value(source.get("sha256"))
        require(asset_id == "asset-" + expected, "AssetIdentityMismatch")
        path = asset_path(source.get("relativePath"))
        payload, actual = read_source(path, hashes, MAX_BYTES)
        require(actual == expected, "AssetHashMismatch")
        require(len(payload) == integer(source.get("bytes"), "AssetByteCountInvalid", MAX_BYTES), "AssetByteCountMismatch")
        extension = verify_image(payload, source, path)
        assets.append({"assetId": asset_id, "sha256": expected, "bytes": len(payload), "mimeType": source["mimeType"],
                       "width": source["width"], "height": source["height"], "sourceRelativePath": source["relativePath"],
                       "sourceCatalogSha256": catalog_sha, "objectName": "menu-reference/" + expected + extension,
                       "access": "Private", "rightsUnconfirmed": True, "publicUseApproved": False,
                       "uploadState": "NotUploaded", "remoteSha256": None, "remoteVerified": False})
    links, referenced_assets = [], set()
    for menu_id, menu in menus.items():
        branch_id = identifier(menu.get("branchId"))
        require(branch_id in branches, "MenuBranchMismatch")
        observations = menu.get("observations")
        require(isinstance(observations, list) and observations, "MenuObservationsMissing")
        for observation in observations:
            require(isinstance(observation, dict), "MenuObservationBranchMismatch")
            require(orders.get(identifier(observation.get("recordId")), {}).get("branchId") == branch_id,
                    "MenuObservationBranchMismatch")
        candidates = menu.get("imageCandidates")
        require(isinstance(candidates, list), "MenuCandidatesInvalid")
        seen = set()
        for position, candidate in enumerate(candidates, 1):
            require(isinstance(candidate, dict), "MenuCandidateInvalid")
            candidate_id, asset_id = identifier(candidate.get("candidateId")), identifier(candidate.get("assetId"))
            require((candidate_id, asset_id) not in seen, "MenuCandidateDuplicate")
            seen.add((candidate_id, asset_id))
            require(asset_id in source_assets, "MenuCandidateAssetMissing")
            require(candidate.get("officialSourceStatus") == "verified"
                    and candidate.get("matchLevel") in ("same_menu_name_candidate", "official_same_menu")
                    and candidate.get("rightsStatus") == "unconfirmed"
                    and all(candidate.get(k) is False for k in FALSE_FLAGS - {"operationalRegistration"}),
                    "MenuCandidateBoundaryInvalid")
            hash_value(candidate.get("sourcePageSha256"))
            referenced_assets.add(asset_id)
            links.append({"branchId": branch_id, "menuId": menu_id, "candidateId": candidate_id, "assetId": asset_id,
                          "sourceCatalogSha256": catalog_sha, "displayOrder": position,
                          "referenceRole": "RepresentativeCandidate" if position == 1 else "DetailCandidate",
                          "orderingBasis": "SourceCandidateOrderOnly", "operationalRepresentativeConfirmed": False,
                          "matchLevel": candidate["matchLevel"], "optionsMatch": False,
                          "rightsUnconfirmed": True, "publicUseApproved": False})
    require(referenced_assets == set(source_assets), "UnlinkedAssetForbidden")
    manifest = {"schemaVersion": 1, "kind": "PrivateMenuBlobArchivePreparation", "localVerificationOnly": True,
                "executionModeCode": "Simulation", "operationalRegistration": False,
                "publicDisplayApproved": False, "publicUseApproved": False, "rightsUnconfirmed": True,
                "preparationStatus": "PreparedBlocked", "uploadPerformed": False, "networkAccessPerformed": False,
                "sourceCatalogRelativePath": catalog_path.relative_to(LOCAL).as_posix(), "sourceCatalogSha256": catalog_sha,
                "sourceCatalogBytes": len(raw), "sourceImportProofSha256": proof_sha, "sourceDatasetSha256": dataset_sha,
                "storage": {"provider": "AzureBlob", "access": "Private", "serviceUri": None, "containerName": None,
                            "observedServiceUri": observed_uri, "observedDefaultContainerName": DEFAULT_CONTAINER,
                            "containerNameBasis": "ObservedDefaultOnly", "liveVerified": False, "privateAclVerified": False,
                            "configurationKeys": {"serviceUri": "AzureBlobStorage:ServiceUri",
                                                  "privateContainerName": "AzureBlobStorage:PrivateContainerName"}},
                "blockedReasons": ["MissingVerifiedServiceUri", "AzureAuthenticationUnavailable",
                                   "ContainerLiveVerificationRequired", "PrivateContainerAclVerificationRequired"],
                "assets": assets, "menuImageLinks": links,
                "summary": {"assets": len(assets), "menuImageLinks": len(links),
                            "menusWithImages": len({x["menuId"] for x in links}),
                            "branchesWithImages": len({x["branchId"] for x in links}),
                            "bytes": sum(x["bytes"] for x in assets), "uploadedAssets": 0, "remoteVerifiedAssets": 0},
                "followUpExecutionContract": {"storageAdapter": "IObjectStorageService", "immutableUploadRequired": True,
                                              "remoteDownloadSha256Required": True, "sameObjectRetryRequired": True,
                                              "privateContainerPublicAccessRequired": "None",
                                              "catalogAndImageBytesMayChange": False, "operationalMenuWritesAllowed": False}}
    verify_unchanged(hashes)
    output_path = output_path or catalog_path.with_name("blob-archive-manifest.json")
    payload = (json.dumps(manifest, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")
    write_manifest(output_path, payload, catalog_path, hashes)
    return manifest, sha256(payload)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=CATALOG)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--service-uri")
    parser.add_argument("--private-container-name", default=DEFAULT_CONTAINER)
    args = parser.parse_args()
    try:
        manifest, manifest_sha = prepare(args.catalog, args.output, args.service_uri, args.private_container_name)
        print(json.dumps({"status": manifest["preparationStatus"], "manifestSha256": manifest_sha,
                          **manifest["summary"], "uploadPerformed": False, "blockedReasons": manifest["blockedReasons"]}, ensure_ascii=False))
        return 0
    except PreparationFailure as error:
        print("MENU_BLOB_PREPARATION_FAILED: " + str(error), file=sys.stderr)
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        print("MENU_BLOB_PREPARATION_FAILED: SourceOrOutputUnavailable", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())

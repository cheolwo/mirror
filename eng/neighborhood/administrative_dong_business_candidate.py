#!/usr/bin/env python3
"""동북서울 30개 행정동의 SEMAS 사업장 비공개 후보 묶음을 결정적으로 생성한다."""

from __future__ import annotations

import argparse
import base64
import copy
import csv
import hashlib
import io
import json
import math
import os
import re
import shutil
import stat
import struct
import subprocess
import sys
import uuid
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Iterator, Sequence
from unittest import mock


DEFAULT_REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-business.g4a.r2.json"
)

DESIGN_DOCUMENT_REF = (
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-business-candidate.implementation.r12.md"
)
DESIGN_HASH_SHA256 = "F4DC06DF8FC300F564B646EDD4295D4EC5B941D143046CAA0C1D1422A2CEEDA4"
WORK_ORDER_REF = (
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-business-candidate.r2.data-implementation.v1.json"
)

SCOPE_SCHEMA = "administrative-dong-business-generation-scope.v1"
CANDIDATE_SCHEMA = "administrative-dong-business-candidate.v1"
REVISION = "northeast-seoul-admin-dong-business-candidate.g4a.r2"
CANDIDATE_SET_DOMAIN = "administrative-dong-business-candidate-set.v1"
SOURCE_ROW_DOMAIN = "semas-business-source-row.v1"
IDENTITY_GROUP_DOMAIN = "semas-business-name-road-address-identity-candidate.v1"

EXPECTED_HEADERS = (
    "상가업소번호",
    "상호명",
    "지점명",
    "상권업종대분류코드",
    "상권업종대분류명",
    "상권업종중분류코드",
    "상권업종중분류명",
    "상권업종소분류코드",
    "상권업종소분류명",
    "표준산업분류코드",
    "표준산업분류명",
    "시도코드",
    "시도명",
    "시군구코드",
    "시군구명",
    "행정동코드",
    "행정동명",
    "법정동코드",
    "법정동명",
    "지번코드",
    "대지구분코드",
    "대지구분명",
    "지번본번지",
    "지번부번지",
    "지번주소",
    "도로명코드",
    "도로명",
    "건물본번지",
    "건물부번지",
    "건물관리번호",
    "건물명",
    "도로명주소",
    "구우편번호",
    "신우편번호",
    "동정보",
    "층정보",
    "호정보",
    "경도",
    "위도",
)

EXPECTED_COUNTS = {
    "nationalCsvEntries": 16,
    "nationalRows": 2_772_484,
    "seoulRows": 554_092,
    "candidateRows": 29_721,
    "foodRows": 8_246,
    "missingProviderShopIdRows": 0,
    "duplicateProviderShopIdRows": 0,
    "missingCoordinateRows": 0,
    "missingRoadAddressRows": 0,
    "missingLandAddressRows": 0,
    "missingBuildingManagementNumberRows": 67,
    "foodMissingBuildingManagementNumberRows": 15,
    "uniqueBuildingManagementNumbers": 11_931,
    "historicalBoundaryMatchedRows": 29_686,
    "historicalBoundaryConflictRows": 29,
    "historicalBoundaryOutsideRows": 6,
    "historicalBoundaryMultipleRows": 0,
    "historicalBoundaryDiagnosticRows": 35,
    "historicalBoundaryDiagnosticGroups": 13,
    "identityCandidateGroups": 236,
    "identityCandidateRows": 490,
    "legacyMyeonmokShopRows": 5_411,
    "legacyFactoryRows": 126,
    "legacyProviderOverlapRows": 5_411,
    "newProviderRows": 24_310,
    "sourceAdministrativeNameDifferenceAreas": 26,
}

EXPECTED_DISTRIBUTION = {
    "1121574000": (1196, 316),
    "1121575000": (1190, 251),
    "1121576000": (785, 225),
    "1121577000": (921, 241),
    "1123056000": (1706, 462),
    "1123057000": (517, 138),
    "1123060000": (929, 207),
    "1123061000": (1012, 211),
    "1123065000": (2153, 626),
    "1123066000": (1688, 517),
    "1123072000": (625, 170),
    "1123073000": (544, 155),
    "1123074000": (995, 349),
    "1123075000": (408, 96),
    "1126052000": (971, 287),
    "1126054000": (526, 123),
    "1126055000": (362, 92),
    "1126056500": (1440, 408),
    "1126057000": (866, 218),
    "1126057500": (1246, 406),
    "1126058000": (648, 176),
    "1126059000": (1578, 586),
    "1126060000": (517, 91),
    "1126061000": (1155, 347),
    "1126062000": (929, 258),
    "1126063000": (923, 253),
    "1126065500": (1755, 538),
    "1126066000": (507, 107),
    "1126068000": (1039, 279),
    "1126069000": (590, 113),
}

EXPECTED_MAPPING = {
    "11215740": ("1121574000", "1121510100"),
    "11215750": ("1121575000", "1121510100"),
    "11215760": ("1121576000", "1121510100"),
    "11215770": ("1121577000", "1121510100"),
    "11230560": ("1123056000", "1123010400"),
    "11230570": ("1123057000", "1123010400"),
    "11230600": ("1123060000", "1123010500"),
    "11230610": ("1123061000", "1123010500"),
    "11230650": ("1123065000", "1123010600"),
    "11230660": ("1123066000", "1123010600"),
    "11230720": ("1123072000", "1123010900"),
    "11230730": ("1123073000", "1123010900"),
    "11230740": ("1123074000", "1123011000"),
    "11230750": ("1123075000", "1123011000"),
    "11260520": ("1126052000", "1126010100"),
    "11260540": ("1126054000", "1126010100"),
    "11260550": ("1126055000", "1126010100"),
    "11260565": ("1126056500", "1126010100"),
    "11260570": ("1126057000", "1126010100"),
    "11260575": ("1126057500", "1126010100"),
    "11260580": ("1126058000", "1126010200"),
    "11260590": ("1126059000", "1126010200"),
    "11260600": ("1126060000", "1126010300"),
    "11260610": ("1126061000", "1126010300"),
    "11260620": ("1126062000", "1126010400"),
    "11260630": ("1126063000", "1126010400"),
    "11260655": ("1126065500", "1126010500"),
    "11260660": ("1126066000", "1126010500"),
    "11260680": ("1126068000", "1126010600"),
    "11260690": ("1126069000", "1126010600"),
}

EXPECTED_CONFLICT_PAIRS = {
    "1123056000>1123060000": 1,
    "1123057000>1123066000": 2,
    "1123060000>1123061000": 1,
    "1123073000>1123057000": 2,
    "1126060000>1126061000": 11,
    "1126066000>1126065500": 7,
    "1126068000>1126065500": 1,
    "1126068000>1126069000": 1,
    "1126069000>1126068000": 3,
}

EXPECTED_OUTSIDE_DISTRIBUTION = {
    "1123056000": 1,
    "1123065000": 4,
    "1123074000": 1,
}

EXPECTED_DIAGNOSTIC_GROUP_DIGEST = (
    "1C1D42F594FAC11370269C11B936B55B8F12A57A7BAC3EE419FCDC03AF1AC983"
)
EXPECTED_LEGACY_FACTORY_SOURCE_ROWS = 384

AUTHORITY_FLAGS = {
    "privateReviewOnly": True,
    "currentAdministrativeBoundaryEstablished": False,
    "sourceCoordinateDatumEstablished": False,
    "exactBuildingBindingEstablished": False,
    "currentOperationVerified": False,
    "merchantClaimVerified": False,
    "distributionApproved": False,
    "publicDisplayAllowed": False,
    "orderScenarioEligible": False,
    "runtimeAuthorized": False,
    "gameplayReady": False,
    "unityApplyAllowed": False,
}

MATCHED_DIAGNOSTIC = "SourceAdministrativeDongMappedHistoricalBoundaryMatched"
CONFLICT_DIAGNOSTIC = "SourceAdministrativeDongMappedHistoricalBoundaryConflict"
OUTSIDE_DIAGNOSTIC = "SourceAdministrativeDongMappedOutsideHistoricalBoundaryScope"
DATUM_DIAGNOSTIC = "SourceCoordinateDatumUnconfirmed"
BUILDING_MISSING_DIAGNOSTIC = "BuildingManagementNumberMissing"
IDENTITY_DIAGNOSTIC = "PotentialSameNameRoadAddressIdentityCandidate"
OPERATION_DIAGNOSTIC = "CurrentOperationUnverified"
PROTECTED_DIAGNOSTIC = "ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly"

PROVIDER_ID_PATTERN = re.compile(r"^MA[0-9A-Z]{18}$")
PROVIDER_ID_TOKEN_PATTERN_BYTES = re.compile(rb"(?<![0-9A-Z])MA[0-9A-Z]{18}(?![0-9A-Z])")
SYNTHETIC_PROVIDER_ID = "MASYNTHETICFIXTURE01"
BUILDING_MANAGEMENT_PATTERN = re.compile(r"^[0-9]{25}$")


class BusinessCandidateError(RuntimeError):
    """안정 오류 코드를 노출하는 생성 오류."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise BusinessCandidateError(code)


def lexical_absolute(path: str | Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(os.fspath(left)) == os.path.normcase(os.fspath(right))


def path_is_within(root: Path, candidate: Path) -> bool:
    try:
        return same_path(Path(os.path.commonpath((root, candidate))), root)
    except ValueError:
        return False


def is_reparse_path(path: Path) -> bool:
    is_junction = getattr(os.path, "isjunction", lambda _: False)
    try:
        attributes = getattr(os.lstat(path), "st_file_attributes", 0)
    except OSError:
        attributes = 0
    return (
        path.is_symlink()
        or bool(is_junction(path))
        or bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
    )


def require_repository_root(root: Path) -> Path:
    lexical = lexical_absolute(root)
    require(lexical.is_dir(), "RepositoryRootMissing")
    for component in reversed((lexical, *lexical.parents)):
        if os.path.lexists(component):
            require(not is_reparse_path(component), "RepositoryRootReparsePathRejected")
    require(same_path(lexical, Path(os.path.realpath(lexical))), "RepositoryRootReparsePathRejected")
    require((lexical / ".git").exists(), "RepositoryMarkerMissing")
    return lexical


def repository_path(root: Path, relative: str | Path) -> Path:
    root = lexical_absolute(root)
    value = Path(relative)
    candidate = lexical_absolute(value if value.is_absolute() else root / value)
    require(path_is_within(root, candidate), "PathOutsideRepository")
    current = root
    require(os.path.lexists(current) and not is_reparse_path(current), "RepositoryRootInvalid")
    for component in candidate.relative_to(root).parts:
        current /= component
        if not os.path.lexists(current):
            break
        require(not is_reparse_path(current), "RepositoryReparsePathRejected")
        require(path_is_within(root, Path(os.path.realpath(current))), "ResolvedPathOutsideRepository")
    return candidate


def ensure_plain_directory(root: Path, path: Path, code: str) -> Path:
    root = require_repository_root(root)
    path = repository_path(root, path)
    path.mkdir(parents=True, exist_ok=True)
    path = repository_path(root, path)
    require(path.is_dir() and not is_reparse_path(path), code)
    require(same_path(Path(os.path.realpath(path)), path), code)
    return path


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def pretty_json_bytes(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n").encode("utf-8")


def document_content_hash(document: dict[str, Any]) -> str:
    candidate = copy.deepcopy(document)
    candidate["contentHashSha256"] = ""
    return sha256_bytes(canonical_json_bytes(candidate))


def apply_content_hash(document: dict[str, Any]) -> None:
    document["contentHashSha256"] = ""
    document["contentHashSha256"] = document_content_hash(document)


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise BusinessCandidateError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def contract_hash(value: Any) -> str:
    return sha256_bytes(canonical_json_bytes(value))


def frame_value(value: Any) -> bytes:
    if value is None:
        return b"\x00" + struct.pack(">I", 0)
    if isinstance(value, bool):
        text = "true" if value else "false"
    elif isinstance(value, int):
        text = str(value)
    elif isinstance(value, str):
        text = value
    else:
        raise BusinessCandidateError("UnsupportedCanonicalFrameValue")
    encoded = text.encode("utf-8")
    require(len(encoded) <= 0xFFFFFFFF, "CanonicalFrameValueTooLarge")
    return b"\x01" + struct.pack(">I", len(encoded)) + encoded


def framed_sha256(values: Iterable[Any]) -> str:
    digest = hashlib.sha256()
    for value in values:
        digest.update(frame_value(value))
    return digest.hexdigest().upper()


def source_row_hash(values: Sequence[str]) -> str:
    require(len(values) == len(EXPECTED_HEADERS), "SourceRowColumnCountMismatch")
    return framed_sha256((SOURCE_ROW_DOMAIN, *values))


@dataclass(frozen=True)
class Dependencies:
    shapefile: Any
    CRS: Any
    Transformer: Any
    translate: Any
    Point: Any
    Polygon: Any
    MultiPolygon: Any
    shape: Any
    unary_union: Any
    pyshp_version: str
    pyproj_version: str
    shapely_version: str
    geos_version: str
    proj_version: str
    epsg_database_version: str


def load_dependencies(root: Path) -> Dependencies:
    runtime = root / "artifacts/local/public-data/gis-runtime-r1"
    if runtime.is_dir() and str(runtime) not in sys.path:
        sys.path.insert(0, str(runtime))
    try:
        import pyproj
        import shapefile
        import shapely
        from pyproj import CRS, Transformer
        from shapely import geos_version_string
        from shapely.affinity import translate
        from shapely.geometry import MultiPolygon, Point, Polygon, shape
        from shapely.ops import unary_union
    except ImportError as exc:
        raise BusinessCandidateError("GisDependenciesMissing") from exc
    return Dependencies(
        shapefile=shapefile,
        CRS=CRS,
        Transformer=Transformer,
        translate=translate,
        Point=Point,
        Polygon=Polygon,
        MultiPolygon=MultiPolygon,
        shape=shape,
        unary_union=unary_union,
        pyshp_version=shapefile.__version__,
        pyproj_version=pyproj.__version__,
        shapely_version=shapely.__version__,
        geos_version=geos_version_string,
        proj_version=pyproj.proj_version_str,
        epsg_database_version=pyproj.database.get_database_metadata("EPSG.VERSION"),
    )


@dataclass(frozen=True)
class CommonEnuFrame:
    origin_latitude: float
    origin_longitude: float
    offset_x: float
    offset_z: float

    @staticmethod
    def _ecef(latitude: float, longitude: float) -> tuple[float, float, float]:
        latitude_radians = math.radians(latitude)
        longitude_radians = math.radians(longitude)
        eccentricity_squared = 0.0066943799901413165
        radius = 6_378_137.0 / math.sqrt(
            1.0 - eccentricity_squared * math.sin(latitude_radians) ** 2
        )
        return (
            radius * math.cos(latitude_radians) * math.cos(longitude_radians),
            radius * math.cos(latitude_radians) * math.sin(longitude_radians),
            radius * (1.0 - eccentricity_squared) * math.sin(latitude_radians),
        )

    def wgs84_to_local(self, longitude: float, latitude: float) -> tuple[int, int]:
        origin_x, origin_y, origin_z = self._ecef(self.origin_latitude, self.origin_longitude)
        point_x, point_y, point_z = self._ecef(latitude, longitude)
        dx, dy, dz = point_x - origin_x, point_y - origin_y, point_z - origin_z
        latitude_radians = math.radians(self.origin_latitude)
        longitude_radians = math.radians(self.origin_longitude)
        east = -math.sin(longitude_radians) * dx + math.cos(longitude_radians) * dy
        north = (
            -math.sin(latitude_radians) * math.cos(longitude_radians) * dx
            - math.sin(latitude_radians) * math.sin(longitude_radians) * dy
            + math.cos(latitude_radians) * dz
        )
        return round((east + self.offset_x) * 1000), round((north + self.offset_z) * 1000)


@dataclass(frozen=True)
class Boundary:
    code: str
    stable_id: str
    geometry: Any


@dataclass
class PreparedBundle:
    scope: dict[str, Any]
    scope_path: Path
    generator_path: Path
    generator_bytes: bytes
    candidates: list[dict[str, Any]]
    candidate_set_hash: str
    payloads: dict[str, bytes]


HEADER_HASH_FIELD_ORDER = (
    "domainSeparator",
    "schemaVersion",
    "revision",
    "designDocumentRef",
    "designHashSha256",
    "scopeRelativePath",
    "scopeDefinitionSha256",
    "generatorRelativePath",
    "generatorSha256",
    "semasArchiveRelativePath",
    "semasArchiveSha256",
    "semasArchiveBytes",
    "seoulEntryLogicalName",
    "seoulEntryLogicalNameUtf8Sha256",
    "seoulEntryRawNameEncoding",
    "seoulEntryRawNameBytes",
    "seoulEntryRawNameBase64",
    "seoulEntryRawNameSha256",
    "seoulEntryBytes",
    "seoulEntryCrc32",
    "seoulEntrySha256",
    "seoulEntryRows",
    "receiptRelativePath",
    "receiptSha256",
    "receiptBytes",
    "downloadMetadataRelativePath",
    "downloadMetadataSha256",
    "downloadMetadataBytes",
    "datasetMetadataRelativePath",
    "datasetMetadataSha256",
    "datasetMetadataBytes",
    "moisArchiveRelativePath",
    "moisArchiveSha256",
    "moisArchiveBytes",
    "moisAdministrativeEntryName",
    "moisAdministrativeEntrySha256",
    "moisAdministrativeEntryBytes",
    "moisAdministrativeEntryCrc32",
    "moisAdministrativeLegalEntryName",
    "moisAdministrativeLegalEntrySha256",
    "moisAdministrativeLegalEntryBytes",
    "moisAdministrativeLegalEntryCrc32",
    "r2ScopeRelativePath",
    "r2ScopeSha256",
    "r2ScopeBytes",
    "r2ScopeManifestRelativePath",
    "r2ScopeManifestFileSha256",
    "r2ScopeManifestBytes",
    "r2ScopeManifestContentHashSha256",
    "historicalBoundaryRelativePath",
    "historicalBoundarySha256",
    "historicalBoundaryBytes",
    "legacySelectedRelativePath",
    "legacySelectedSha256",
    "legacySelectedBytes",
    "legacyFactoryRelativePath",
    "legacyFactorySha256",
    "legacyFactoryBytes",
    "mappingContractSha256",
    "sourceRowContractSha256",
    "historicalBoundaryContractSha256",
    "coordinateContractSha256",
    "identityContractSha256",
    "expectedCountsContractSha256",
)

CANDIDATE_HASH_FIELD_ORDER = (
    "schemaVersion",
    "revision",
    "stableId",
    "sourceFeatureKey",
    "dimensionKey",
    "providerBusinessIdSha256",
    "protectedSource.providerShopIdCanonical",
    "sourceRowHashSha256",
    "sourceProvinceCode",
    "sourceBoroughCode",
    "sourceBoroughName",
    "sourceAdministrativeDongCode",
    "mappedAdministrativeDongCode",
    "administrativeAreaStableId",
    "protectedSource.sourceAdministrativeDongName",
    "sourceLegalDongCode",
    "legalAreaStableId",
    "protectedSource.sourceLegalDongName",
    "largeCategoryCode",
    "largeCategoryName",
    "middleCategoryCode",
    "middleCategoryName",
    "smallCategoryCode",
    "smallCategoryName",
    "standardIndustryCode",
    "standardIndustryName",
    "foodCategory",
    "protectedSource.businessName",
    "protectedSource.branchName",
    "protectedSource.landLotAddress",
    "protectedSource.roadNameAddress",
    "protectedSource.buildingManagementNumber",
    "protectedSource.buildingName",
    "protectedSource.buildingDong",
    "protectedSource.floor",
    "protectedSource.unit",
    "protectedSource.sourceLongitudeText",
    "protectedSource.sourceLatitudeText",
    "sourceCoordinateDatumStatusCode",
    "commonEnuMillimeters.x",
    "commonEnuMillimeters.z",
    "historicalBoundaryHitAdministrativeAreaStableIds[]",
    "historicalBoundaryDiagnosticCode",
    "historicalBoundaryOwnershipDispositionCode",
    "identityCandidateGroupSha256",
    "identityCandidateGroupSize",
    "identityMergeState",
    "qualityCode",
    "qualityDiagnosticCodes[]",
    "authorityFlags.privateReviewOnly",
    "authorityFlags.currentAdministrativeBoundaryEstablished",
    "authorityFlags.sourceCoordinateDatumEstablished",
    "authorityFlags.exactBuildingBindingEstablished",
    "authorityFlags.currentOperationVerified",
    "authorityFlags.merchantClaimVerified",
    "authorityFlags.distributionApproved",
    "authorityFlags.publicDisplayAllowed",
    "authorityFlags.orderScenarioEligible",
    "authorityFlags.runtimeAuthorized",
    "authorityFlags.gameplayReady",
    "authorityFlags.unityApplyAllowed",
)

PROTECTED_SOURCE_PROPERTY_ORDER = (
    "providerShopIdCanonical",
    "sourceAdministrativeDongName",
    "sourceLegalDongName",
    "businessName",
    "branchName",
    "landLotAddress",
    "roadNameAddress",
    "buildingManagementNumber",
    "buildingName",
    "buildingDong",
    "floor",
    "unit",
    "sourceLongitudeText",
    "sourceLatitudeText",
)

CANDIDATE_PROPERTY_ORDER = (
    "schemaVersion",
    "revision",
    "candidateSetHashSha256",
    "stableId",
    "sourceFeatureKey",
    "dimensionKey",
    "providerBusinessIdSha256",
    "sourceRowHashSha256",
    "sourceProvinceCode",
    "sourceBoroughCode",
    "sourceBoroughName",
    "sourceAdministrativeDongCode",
    "mappedAdministrativeDongCode",
    "administrativeAreaStableId",
    "sourceLegalDongCode",
    "legalAreaStableId",
    "largeCategoryCode",
    "largeCategoryName",
    "middleCategoryCode",
    "middleCategoryName",
    "smallCategoryCode",
    "smallCategoryName",
    "standardIndustryCode",
    "standardIndustryName",
    "foodCategory",
    "sourceCoordinateDatumStatusCode",
    "commonEnuMillimeters",
    "historicalBoundaryHitAdministrativeAreaStableIds",
    "historicalBoundaryDiagnosticCode",
    "historicalBoundaryOwnershipDispositionCode",
    "identityCandidateGroupSha256",
    "identityCandidateGroupSize",
    "identityMergeState",
    "qualityCode",
    "qualityDiagnosticCodes",
    "authorityFlags",
    "protectedSource",
)


def nested_value(value: dict[str, Any], path: str) -> Any:
    current: Any = value
    for component in path.split("."):
        current = current[component]
    return current


def candidate_hash_values(candidate: dict[str, Any]) -> Iterator[Any]:
    for path in CANDIDATE_HASH_FIELD_ORDER:
        if path.endswith("[]"):
            items = nested_value(candidate, path[:-2])
            require(isinstance(items, list), "CandidateHashArrayInvalid")
            yield len(items)
            yield from items
        else:
            yield nested_value(candidate, path)


def candidate_set_hash(header: dict[str, Any], candidates: Sequence[dict[str, Any]]) -> str:
    require(tuple(header) == HEADER_HASH_FIELD_ORDER, "CandidateHashHeaderOrderInvalid")
    digest = hashlib.sha256()
    for value in header.values():
        digest.update(frame_value(value))
    prior: str | None = None
    for candidate in candidates:
        provider_id = candidate["protectedSource"]["providerShopIdCanonical"]
        require(prior is None or prior < provider_id, "CandidateProviderOrderInvalid")
        prior = provider_id
        for value in candidate_hash_values(candidate):
            digest.update(frame_value(value))
    return digest.hexdigest().upper()


def source_by_role(scope: dict[str, Any], role: str) -> dict[str, Any]:
    matches = [item for item in scope["sources"] if item.get("role") == role]
    require(len(matches) == 1, f"ScopeSourceMissing:{role}")
    return matches[0]


def verify_frozen_file(root: Path, source: dict[str, Any]) -> Path:
    path = repository_path(root, source["repositoryRelativePath"])
    role = source["role"]
    require(path.is_file() and not is_reparse_path(path), f"SourceMissing:{role}")
    require(path.stat().st_size == source["byteLength"], f"SourceLengthMismatch:{role}")
    require(sha256_file(path) == source["contentHashSha256"], f"SourceHashMismatch:{role}")
    return path


def verify_output_is_ignored(root: Path, output_relative: str) -> None:
    output = repository_path(root, output_relative)
    require(
        output.relative_to(root).as_posix().startswith("artifacts/local/"),
        "PrivateOutputOutsideArtifactsLocal",
    )
    result = subprocess.run(
        ["git", "check-ignore", "-q", "--", output.relative_to(root).as_posix()],
        cwd=root,
        check=False,
        capture_output=True,
    )
    require(result.returncode == 0, "PrivateOutputNotGitIgnored")


def load_scope(root: Path, scope_path: Path) -> tuple[dict[str, Any], dict[str, Path]]:
    root = require_repository_root(root)
    scope_path = repository_path(root, scope_path)
    require(scope_path.is_file() and not is_reparse_path(scope_path), "ScopeMissing")
    scope = load_json(scope_path)
    require(scope.get("schemaVersion") == SCOPE_SCHEMA, "ScopeSchemaMismatch")
    require(scope.get("revision") == REVISION, "ScopeRevisionMismatch")
    require(scope.get("designDocumentRef") == DESIGN_DOCUMENT_REF, "ScopeDesignRefMismatch")
    require(scope.get("designHashSha256") == DESIGN_HASH_SHA256, "ScopeDesignHashMismatch")
    design_path = repository_path(root, DESIGN_DOCUMENT_REF)
    require(sha256_file(design_path) == DESIGN_HASH_SHA256, "DesignDocumentHashMismatch")

    work_order_path = repository_path(root, WORK_ORDER_REF)
    work_order = load_json(work_order_path)
    require(
        work_order.get("schemaVersion") == "public-data-candidate-implementation-work-order.v1",
        "WorkOrderSchemaMismatch",
    )
    require(
        work_order.get("workOrderId")
        == "DATA-WO-NORTHEAST-SEOUL-ADMIN-DONG-BUSINESS-CANDIDATE-R2",
        "WorkOrderIdMismatch",
    )
    gate = work_order.get("planningGate", {})
    require(gate.get("designDocumentRef") == DESIGN_DOCUMENT_REF, "PlanningGateDesignRefMismatch")
    require(gate.get("designHashSha256") == DESIGN_HASH_SHA256, "PlanningGateDesignHashMismatch")
    require(gate.get("statusCode") == "Approved", "PlanningGateNotApproved")
    require(work_order.get("promotionEligible") is False, "WorkOrderPromotionBoundaryInvalid")

    require(scope.get("reviewStatus") == "PendingHumanReview", "ScopeReviewStatusInvalid")
    require(
        scope.get("sourceCoordinateDatumStatusCode") == "SourceCoordinateDatumUnconfirmed",
        "ScopeCoordinateDatumStatusInvalid",
    )
    require(scope.get("authorityFlags") == AUTHORITY_FLAGS, "ScopeAuthorityFlagsMismatch")
    require(scope.get("expectedCounts") == EXPECTED_COUNTS, "ScopeExpectedCountsMismatch")
    expected_distribution = {
        code: {"candidateRows": counts[0], "foodRows": counts[1]}
        for code, counts in EXPECTED_DISTRIBUTION.items()
    }
    require(scope.get("expectedDistribution") == expected_distribution, "ScopeDistributionMismatch")
    require(scope.get("expectedConflictPairs") == EXPECTED_CONFLICT_PAIRS, "ScopeConflictPairsMismatch")
    require(
        scope.get("expectedOutsideDistribution") == EXPECTED_OUTSIDE_DISTRIBUTION,
        "ScopeOutsideDistributionMismatch",
    )
    require(
        scope.get("expectedDiagnosticGroupDigestSha256") == EXPECTED_DIAGNOSTIC_GROUP_DIGEST,
        "ScopeDiagnosticGroupDigestMismatch",
    )
    require(scope.get("sourceRowHeaders") == list(EXPECTED_HEADERS), "ScopeSourceHeadersMismatch")

    mappings = scope.get("administrativeAreas")
    require(isinstance(mappings, list) and len(mappings) == 30, "ScopeAreaCountMismatch")
    require(
        [item.get("sourceAdministrativeDongCode") for item in mappings]
        == sorted(EXPECTED_MAPPING),
        "ScopeAreaOrderMismatch",
    )
    for item in mappings:
        source_code = item["sourceAdministrativeDongCode"]
        hjd, bjd = EXPECTED_MAPPING[source_code]
        require(item.get("mappedAdministrativeDongCode") == hjd, f"ScopeHjdMappingMismatch:{source_code}")
        require(
            item.get("administrativeAreaStableId") == f"region:kr:hjd:{hjd}",
            f"ScopeHjdStableIdMismatch:{source_code}",
        )
        require(
            item.get("legalAreaStableId") == f"region:kr:bjd:{bjd}",
            f"ScopeBjdStableIdMismatch:{source_code}",
        )
        require(
            item.get("expectedCandidateRows") == EXPECTED_DISTRIBUTION[hjd][0]
            and item.get("expectedFoodRows") == EXPECTED_DISTRIBUTION[hjd][1],
            f"ScopeAreaExpectedCountMismatch:{source_code}",
        )

    output = scope.get("output", {})
    require(output.get("candidateSchemaVersion") == CANDIDATE_SCHEMA, "ScopeOutputSchemaMismatch")
    require(output.get("generationDirectoryName") == "generations", "ScopeGenerationNameMismatch")
    require(output.get("fileNames") == [
        "manifest.json", "candidates.ndjson", "audit.json", "generator-source.py", "complete.json"
    ], "ScopeOutputFileNamesMismatch")
    verify_output_is_ignored(root, output["repositoryRelativeDirectory"])

    generator_path = repository_path(root, output["toolchain"]["generatorRelativePath"])
    require(same_path(generator_path, Path(__file__).resolve()), "GeneratorPathMismatch")
    require(
        sha256_file(generator_path) == output["toolchain"]["generatorSha256"],
        "GeneratorHashMismatch",
    )

    paths: dict[str, Path] = {}
    roles = {
        "semasNationalArchive",
        "semasAcquisitionReceipt",
        "semasDownloadMetadata",
        "semasDatasetMetadata",
        "moisAdministrativeCodes",
        "r2ScopeDefinition",
        "r2ScopeManifest",
        "oa22160BoundaryArchive",
        "legacyMyeonmokSelected",
        "legacyFactoryCsv",
    }
    require({item.get("role") for item in scope["sources"]} == roles, "ScopeSourceRoleSetMismatch")
    for role in sorted(roles):
        paths[role] = verify_frozen_file(root, source_by_role(scope, role))

    r2_scope = load_json(paths["r2ScopeDefinition"])
    r2_by_id = {
        item["administrativeAreaStableId"]: item
        for item in r2_scope.get("administrativeAreas", [])
    }
    require(len(r2_by_id) == 30, "R2ScopeAreaCountMismatch")
    for item in mappings:
        actual = r2_by_id.get(item["administrativeAreaStableId"])
        require(actual is not None, "R2ScopeAreaMissing")
        require(actual.get("displayName") == item.get("displayName"), "R2ScopeDisplayNameMismatch")
        require(actual.get("legalAreaStableId") == item.get("legalAreaStableId"), "R2ScopeLegalAreaMismatch")
    r2_manifest = load_json(paths["r2ScopeManifest"])
    require(
        r2_manifest.get("contentHashSha256")
        == source_by_role(scope, "r2ScopeManifest")["documentContentHashSha256"],
        "R2ScopeManifestContentHashMismatch",
    )
    return scope, paths


def verify_zip_entry(
    archive: zipfile.ZipFile, entry: dict[str, Any], role: str
) -> tuple[zipfile.ZipInfo, str]:
    matches = [item for item in archive.infolist() if item.filename == entry["logicalName"]]
    require(len(matches) == 1, f"ZipEntryIdentityMismatch:{role}")
    info = matches[0]
    raw_name = info.filename.encode(entry["rawNameEncoding"])
    require(len(raw_name) == entry["rawNameByteLength"], f"ZipEntryRawNameLengthMismatch:{role}")
    require(base64.b64encode(raw_name).decode("ascii") == entry["rawNameBase64"], f"ZipEntryRawNameMismatch:{role}")
    require(sha256_bytes(raw_name) == entry["rawNameHashSha256"], f"ZipEntryRawNameHashMismatch:{role}")
    require(sha256_bytes(info.filename.encode("utf-8")) == entry["logicalNameUtf8HashSha256"], f"ZipEntryLogicalNameHashMismatch:{role}")
    require(info.file_size == entry["byteLength"], f"ZipEntryLengthMismatch:{role}")
    require(f"{info.CRC:08X}" == entry["crc32"], f"ZipEntryCrcMismatch:{role}")
    digest = hashlib.sha256()
    with archive.open(info) as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    value = digest.hexdigest().upper()
    require(value == entry["contentHashSha256"], f"ZipEntryHashMismatch:{role}")
    return info, value


def verify_named_entry(
    archive: zipfile.ZipFile, name: str, expected: dict[str, Any], role: str
) -> bytes:
    matches = [item for item in archive.infolist() if item.filename == name]
    require(len(matches) == 1, f"ZipEntryIdentityMismatch:{role}")
    info = matches[0]
    require(info.file_size == expected["byteLength"], f"ZipEntryLengthMismatch:{role}")
    require(f"{info.CRC:08X}" == expected["crc32"], f"ZipEntryCrcMismatch:{role}")
    data = archive.read(info)
    require(sha256_bytes(data) == expected["contentHashSha256"], f"ZipEntryHashMismatch:{role}")
    return data


def read_mois_mapping(
    path: Path, scope: dict[str, Any]
) -> dict[str, dict[str, str]]:
    source = source_by_role(scope, "moisAdministrativeCodes")
    entries = source["entries"]
    require(set(entries) == {"administrative", "administrativeLegal"}, "MoisEntryRoleSetMismatch")
    with zipfile.ZipFile(path, "r") as archive:
        require(len(archive.namelist()) == len(set(archive.namelist())), "MoisArchiveDuplicateEntry")
        administrative_bytes = verify_named_entry(
            archive,
            entries["administrative"]["name"],
            entries["administrative"],
            "moisAdministrative",
        )
        mixed_bytes = verify_named_entry(
            archive,
            entries["administrativeLegal"]["name"],
            entries["administrativeLegal"],
            "moisAdministrativeLegal",
        )

    expected_by_hjd = {
        item["mappedAdministrativeDongCode"]: item
        for item in scope["administrativeAreas"]
    }
    administrative_rows: dict[str, list[str]] = {}
    for line in administrative_bytes.decode("cp949").splitlines()[1:]:
        tokens = line.split()
        if not tokens or tokens[0] not in expected_by_hjd:
            continue
        require(len(tokens) == 5, f"MoisAdministrativeCodeNotActive:{tokens[0]}")
        require(tokens[0] not in administrative_rows, f"MoisAdministrativeCodeDuplicate:{tokens[0]}")
        administrative_rows[tokens[0]] = tokens

    mixed_rows: dict[str, list[str]] = {}
    for line in mixed_bytes.decode("cp949").splitlines()[1:]:
        tokens = line.split()
        if not tokens or tokens[0] not in expected_by_hjd:
            continue
        require(len(tokens) == 7, f"MoisAdministrativeLegalCodeNotActive:{tokens[0]}")
        require(tokens[0] not in mixed_rows, f"MoisAdministrativeLegalCodeDuplicate:{tokens[0]}")
        mixed_rows[tokens[0]] = tokens

    require(set(administrative_rows) == set(expected_by_hjd), "MoisAdministrativeCoverageIncomplete")
    require(set(mixed_rows) == set(expected_by_hjd), "MoisAdministrativeLegalCoverageIncomplete")
    result: dict[str, dict[str, str]] = {}
    for hjd, expected in expected_by_hjd.items():
        h = administrative_rows[hjd]
        m = mixed_rows[hjd]
        expected_local_name = expected["displayName"].removeprefix(
            f"서울특별시 {expected['boroughName']} "
        )
        require(h[1] == "서울특별시", f"MoisProvinceMismatch:{hjd}")
        require(h[2] == expected["boroughName"], f"MoisBoroughMismatch:{hjd}")
        require(h[3] == expected_local_name, f"MoisAdministrativeNameMismatch:{hjd}")
        require(m[:4] == h[:4], f"MoisAdministrativeMixedMismatch:{hjd}")
        require(
            m[4] == expected["legalAreaStableId"].removeprefix("region:kr:bjd:"),
            f"MoisLegalCodeMismatch:{hjd}",
        )
        require(m[5] == expected["officialLegalDongName"], f"MoisLegalNameMismatch:{hjd}")
        result[expected["sourceAdministrativeDongCode"]] = {
            "mappedAdministrativeDongCode": hjd,
            "officialAdministrativeDongName": h[3],
            "boroughName": h[2],
            "legalDongCode": m[4],
            "officialLegalDongName": m[5],
        }
    require(set(result) == set(EXPECTED_MAPPING), "MoisExplicitMappingCoverageIncomplete")
    return result


def read_boundaries(
    path: Path, scope: dict[str, Any], dependencies: Dependencies
) -> list[Boundary]:
    expected_by_source = {
        item["sourceAdministrativeDongCode"]: item
        for item in scope["administrativeAreas"]
    }
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "BoundaryArchiveDuplicateEntry")
        entries: dict[str, str] = {}
        for extension in (".cpg", ".dbf", ".prj", ".shp", ".shx"):
            matches = [name for name in names if name.lower().endswith(extension)]
            require(len(matches) == 1, f"BoundaryArchiveEntryInvalid:{extension}")
            entries[extension] = matches[0]
        cpg = archive.read(entries[".cpg"]).decode("ascii").strip().upper()
        require(cpg in {"UTF-8", "UTF8", "65001"}, "BoundaryEncodingMismatch")
        source_crs = dependencies.CRS.from_wkt(archive.read(entries[".prj"]).decode("utf-8-sig"))
        require(source_crs.to_authority() == ("EPSG", "5181"), "BoundaryCrsMismatch")
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])),
            encoding="utf-8",
            encodingErrors="strict",
        )
        selected: dict[str, Boundary] = {}
        try:
            require(len(reader) == 425, "BoundaryRecordCountChanged")
            fields = {field[0] for field in reader.fields[1:]}
            require({"ADSTRD_CD", "ADSTRD_NM"}.issubset(fields), "BoundaryFieldsMissing")
            for shape_record in reader.iterShapeRecords():
                props = shape_record.record.as_dict()
                source_code = str(props["ADSTRD_CD"])
                expected = expected_by_source.get(source_code)
                if expected is None:
                    continue
                hjd = expected["mappedAdministrativeDongCode"]
                require(hjd not in selected, f"BoundaryDuplicate:{hjd}")
                require(shape_record.shape.shapeType == 5, f"BoundaryShapeTypeInvalid:{hjd}")
                geometry = dependencies.shape(shape_record.shape.__geo_interface__)
                require(
                    isinstance(geometry, (dependencies.Polygon, dependencies.MultiPolygon))
                    and not geometry.is_empty
                    and geometry.is_valid,
                    f"BoundaryGeometryInvalid:{hjd}",
                )
                shifted = dependencies.translate(geometry, yoff=100_000.0)
                selected[hjd] = Boundary(hjd, f"region:kr:hjd:{hjd}", shifted)
        finally:
            reader.close()
    expected_hjds = {item["mappedAdministrativeDongCode"] for item in scope["administrativeAreas"]}
    require(set(selected) == expected_hjds, "BoundaryCoverageIncomplete")
    return [selected[code] for code in sorted(selected)]


def _identity_group_hash(name: str, road_address: str) -> str:
    return framed_sha256((IDENTITY_GROUP_DOMAIN, name, road_address))


def _stable_identity(provider_id: str) -> tuple[str, str, str, str]:
    digest = sha256_bytes(provider_id.encode("utf-8"))
    lower = digest.lower()
    return (
        digest,
        f"source-feature:semas:business:sha256:{lower}",
        f"administrative-dong-business-candidate:semas:sha256:{lower}",
        f"business-candidate|semas|sha256|{digest}",
    )


def _candidate_from_row(
    row: list[str], mapping: dict[str, Any]
) -> dict[str, Any]:
    values = dict(zip(EXPECTED_HEADERS, row, strict=True))
    provider_id = values["상가업소번호"].strip()
    require(provider_id != "", "ProviderShopIdMissing")
    require(PROVIDER_ID_PATTERN.fullmatch(provider_id) is not None, "ProviderShopIdFormatInvalid")
    digest, source_key, stable_id, dimension_key = _stable_identity(provider_id)
    building_management_number = values["건물관리번호"]
    require(
        building_management_number == ""
        or BUILDING_MANAGEMENT_PATTERN.fullmatch(building_management_number) is not None,
        "BuildingManagementNumberFormatInvalid",
    )
    require(values["경도"] != "" and values["위도"] != "", "SourceCoordinateMissing")
    require(values["도로명주소"] != "", "RoadAddressMissing")
    require(values["지번주소"] != "", "LandAddressMissing")
    require(values["시도코드"] == "11" and values["시도명"] == "서울특별시", "SourceProvinceMismatch")
    require(values["시군구명"] == mapping["boroughName"], "SourceBoroughMismatch")
    require(values["법정동코드"] == mapping["legalDongCode"], "SourceLegalDongCodeMismatch")
    require(values["법정동명"] == mapping["officialLegalDongName"], "SourceLegalDongNameMismatch")
    try:
        longitude = float(values["경도"])
        latitude = float(values["위도"])
    except ValueError as exc:
        raise BusinessCandidateError("SourceCoordinateInvalid") from exc
    require(math.isfinite(longitude) and math.isfinite(latitude), "SourceCoordinateInvalid")
    protected_source = {
        "providerShopIdCanonical": provider_id,
        "sourceAdministrativeDongName": values["행정동명"],
        "sourceLegalDongName": values["법정동명"],
        "businessName": values["상호명"],
        "branchName": values["지점명"],
        "landLotAddress": values["지번주소"],
        "roadNameAddress": values["도로명주소"],
        "buildingManagementNumber": building_management_number,
        "buildingName": values["건물명"],
        "buildingDong": values["동정보"],
        "floor": values["층정보"],
        "unit": values["호정보"],
        "sourceLongitudeText": values["경도"],
        "sourceLatitudeText": values["위도"],
    }
    require(tuple(protected_source) == PROTECTED_SOURCE_PROPERTY_ORDER, "ProtectedSourcePropertyOrderInvalid")
    return {
        "schemaVersion": CANDIDATE_SCHEMA,
        "revision": REVISION,
        "candidateSetHashSha256": "",
        "stableId": stable_id,
        "sourceFeatureKey": source_key,
        "dimensionKey": dimension_key,
        "providerBusinessIdSha256": digest,
        "sourceRowHashSha256": source_row_hash(row),
        "sourceProvinceCode": values["시도코드"],
        "sourceBoroughCode": values["시군구코드"],
        "sourceBoroughName": values["시군구명"],
        "sourceAdministrativeDongCode": values["행정동코드"],
        "mappedAdministrativeDongCode": mapping["mappedAdministrativeDongCode"],
        "administrativeAreaStableId": f"region:kr:hjd:{mapping['mappedAdministrativeDongCode']}",
        "sourceLegalDongCode": values["법정동코드"],
        "legalAreaStableId": f"region:kr:bjd:{mapping['legalDongCode']}",
        "largeCategoryCode": values["상권업종대분류코드"],
        "largeCategoryName": values["상권업종대분류명"],
        "middleCategoryCode": values["상권업종중분류코드"],
        "middleCategoryName": values["상권업종중분류명"],
        "smallCategoryCode": values["상권업종소분류코드"],
        "smallCategoryName": values["상권업종소분류명"],
        "standardIndustryCode": values["표준산업분류코드"],
        "standardIndustryName": values["표준산업분류명"],
        "foodCategory": values["상권업종대분류코드"] == "I2",
        "sourceCoordinateDatumStatusCode": "SourceCoordinateDatumUnconfirmed",
        "commonEnuMillimeters": {"x": 0, "z": 0},
        "historicalBoundaryHitAdministrativeAreaStableIds": [],
        "historicalBoundaryDiagnosticCode": "",
        "historicalBoundaryOwnershipDispositionCode": "SourceAdministrativeDongOwnershipPreserved",
        "identityCandidateGroupSha256": None,
        "identityCandidateGroupSize": None,
        "identityMergeState": "NotMerged",
        "qualityCode": "PendingHumanReview",
        "qualityDiagnosticCodes": [],
        "authorityFlags": dict(AUTHORITY_FLAGS),
        "protectedSource": protected_source,
        "_longitude": longitude,
        "_latitude": latitude,
    }


def read_seoul_candidates(
    archive_path: Path,
    scope: dict[str, Any],
    mapping: dict[str, dict[str, str]],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    source = source_by_role(scope, "semasNationalArchive")
    entry = source["seoulEntry"]
    candidates: list[dict[str, Any]] = []
    provider_ids: set[str] = set()
    digest_to_provider: dict[str, str] = {}
    source_names_by_hjd: dict[str, set[str]] = defaultdict(set)
    seoul_rows = 0
    with zipfile.ZipFile(archive_path, "r", metadata_encoding="cp949") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "SemasArchiveDuplicateEntry")
        csv_infos = [info for info in archive.infolist() if info.filename.lower().endswith(".csv")]
        require(len(csv_infos) == EXPECTED_COUNTS["nationalCsvEntries"], "NationalCsvEntryCountMismatch")
        info, _ = verify_zip_entry(archive, entry, "seoulCsv")
        with archive.open(info) as raw, io.TextIOWrapper(raw, encoding="utf-8-sig", newline="") as text:
            reader = csv.reader(text)
            try:
                header = next(reader)
            except StopIteration as exc:
                raise BusinessCandidateError("SeoulCsvEmpty") from exc
            require(tuple(header) == EXPECTED_HEADERS, "SeoulCsvHeaderMismatch")
            for row in reader:
                seoul_rows += 1
                require(len(row) == len(EXPECTED_HEADERS), "SeoulCsvColumnCountMismatch")
                source_hjd = row[15]
                mapped = mapping.get(source_hjd)
                if mapped is None:
                    continue
                candidate = _candidate_from_row(row, mapped)
                provider_id = candidate["protectedSource"]["providerShopIdCanonical"]
                require(provider_id not in provider_ids, "SelectedProviderShopIdDuplicate")
                provider_ids.add(provider_id)
                digest = candidate["providerBusinessIdSha256"]
                collision = digest_to_provider.get(digest)
                require(collision is None or collision == provider_id, "ProviderBusinessIdHashCollision")
                digest_to_provider[digest] = provider_id
                source_names_by_hjd[candidate["mappedAdministrativeDongCode"]].add(
                    candidate["protectedSource"]["sourceAdministrativeDongName"]
                )
                candidates.append(candidate)
    require(seoul_rows == EXPECTED_COUNTS["seoulRows"], "SeoulRowCountMismatch")
    require(len(candidates) == EXPECTED_COUNTS["candidateRows"], "CandidateRowCountMismatch")
    require(len(provider_ids) == len(candidates), "SelectedProviderSetCountMismatch")
    official_by_hjd = {
        item["mappedAdministrativeDongCode"]: item["displayName"].removeprefix(
            f"서울특별시 {item['boroughName']} "
        )
        for item in scope["administrativeAreas"]
    }
    require(all(len(names) == 1 for names in source_names_by_hjd.values()), "SourceAdministrativeNameVariantUnexpected")
    name_difference_count = sum(
        next(iter(source_names_by_hjd[code])) != official_name
        for code, official_name in official_by_hjd.items()
    )
    require(
        name_difference_count == EXPECTED_COUNTS["sourceAdministrativeNameDifferenceAreas"],
        "SourceAdministrativeNameDifferenceCountMismatch",
    )
    candidates.sort(key=lambda item: item["protectedSource"]["providerShopIdCanonical"])
    return candidates, {
        "seoulRows": seoul_rows,
        "candidateRows": len(candidates),
        "sourceAdministrativeNameDifferenceAreas": name_difference_count,
        "csvEntryCount": len(csv_infos),
    }


def verify_national_occurrences(
    archive_path: Path,
    selected_provider_ids: set[str],
) -> dict[str, Any]:
    occurrence = Counter()
    national_rows = 0
    csv_entries = 0
    with zipfile.ZipFile(archive_path, "r", metadata_encoding="cp949") as archive:
        infos = sorted(
            (info for info in archive.infolist() if info.filename.lower().endswith(".csv")),
            key=lambda info: info.filename,
        )
        require(len(infos) == EXPECTED_COUNTS["nationalCsvEntries"], "NationalCsvEntryCountMismatch")
        for info in infos:
            csv_entries += 1
            with archive.open(info) as raw, io.TextIOWrapper(raw, encoding="utf-8-sig", newline="") as text:
                reader = csv.reader(text)
                try:
                    header = next(reader)
                except StopIteration as exc:
                    raise BusinessCandidateError("NationalCsvEmpty") from exc
                require(tuple(header) == EXPECTED_HEADERS, "NationalCsvHeaderMismatch")
                for row in reader:
                    national_rows += 1
                    require(len(row) == len(EXPECTED_HEADERS), "NationalCsvColumnCountMismatch")
                    provider_id = row[0].strip()
                    if provider_id in selected_provider_ids:
                        require(PROVIDER_ID_PATTERN.fullmatch(provider_id) is not None, "ProviderShopIdFormatInvalid")
                        occurrence[provider_id] += 1
    require(csv_entries == EXPECTED_COUNTS["nationalCsvEntries"], "NationalCsvEntryCountMismatch")
    require(national_rows == EXPECTED_COUNTS["nationalRows"], "NationalRowCountMismatch")
    require(set(occurrence) == selected_provider_ids, "SelectedProviderMissingNationally")
    require(all(value == 1 for value in occurrence.values()), "SelectedProviderNationalOccurrenceMismatch")
    return {
        "nationalCsvEntries": csv_entries,
        "nationalRows": national_rows,
        "selectedProviderOccurrenceOneRows": len(occurrence),
    }


def verify_legacy_preservation(
    selected_path: Path,
    factory_path: Path,
    selected_provider_ids: set[str],
) -> dict[str, Any]:
    legacy = load_json(selected_path)
    rows = legacy.get("Rows")
    require(isinstance(rows, list), "LegacySelectedRowsInvalid")
    shop_ids: set[str] = set()
    factory_rows = 0
    for item in rows:
        require(isinstance(item, dict), "LegacySelectedRowInvalid")
        kind = item.get("Kind")
        if kind == "shop":
            provider_id = item.get("Fields", {}).get("상가업소번호", "").strip()
            require(provider_id != "" and provider_id not in shop_ids, "LegacyShopProviderInvalid")
            shop_ids.add(provider_id)
        elif kind == "factory":
            factory_rows += 1
        else:
            raise BusinessCandidateError("LegacySelectedKindInvalid")
    require(len(shop_ids) == EXPECTED_COUNTS["legacyMyeonmokShopRows"], "LegacyShopCountMismatch")
    require(factory_rows == EXPECTED_COUNTS["legacyFactoryRows"], "LegacyFactoryCountMismatch")
    with factory_path.open("r", encoding="utf-8-sig", newline="") as stream:
        actual_factory_rows = sum(1 for _ in csv.reader(stream)) - 1
    require(actual_factory_rows == EXPECTED_LEGACY_FACTORY_SOURCE_ROWS, "LegacyFactoryFileCountMismatch")
    overlap = shop_ids & selected_provider_ids
    require(len(overlap) == EXPECTED_COUNTS["legacyProviderOverlapRows"], "LegacyProviderOverlapMismatch")
    require(shop_ids <= selected_provider_ids, "LegacyProviderNotSubset")
    require(
        len(selected_provider_ids - shop_ids) == EXPECTED_COUNTS["newProviderRows"],
        "NewProviderCountMismatch",
    )
    return {
        "legacyMyeonmokShopRows": len(shop_ids),
        "legacyFactoryRows": factory_rows,
        "legacyProviderOverlapRows": len(overlap),
        "newProviderRows": len(selected_provider_ids - shop_ids),
    }


def enrich_candidates(
    candidates: list[dict[str, Any]],
    boundaries: Sequence[Boundary],
    scope: dict[str, Any],
    dependencies: Dependencies,
) -> dict[str, Any]:
    frame_spec = scope["coordinateFrame"]
    frame = CommonEnuFrame(
        origin_latitude=float(frame_spec["originLatitude"]),
        origin_longitude=float(frame_spec["originLongitude"]),
        offset_x=float(frame_spec["worldOffsetX"]),
        offset_z=float(frame_spec["worldOffsetZ"]),
    )
    transformer = dependencies.Transformer.from_crs("EPSG:4326", "EPSG:5186", always_xy=True)
    identity_groups: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for candidate in candidates:
        protected = candidate["protectedSource"]
        identity_groups[(protected["businessName"], protected["roadNameAddress"])].append(candidate)
    duplicate_groups = {
        key: group
        for key, group in identity_groups.items()
        if len({item["protectedSource"]["providerShopIdCanonical"] for item in group}) > 1
    }
    require(len(duplicate_groups) == EXPECTED_COUNTS["identityCandidateGroups"], "IdentityGroupCountMismatch")
    require(
        sum(len(group) for group in duplicate_groups.values()) == EXPECTED_COUNTS["identityCandidateRows"],
        "IdentityGroupRowCountMismatch",
    )
    for (name, road_address), group in duplicate_groups.items():
        group_hash = _identity_group_hash(name, road_address)
        for candidate in group:
            candidate["identityCandidateGroupSha256"] = group_hash
            candidate["identityCandidateGroupSize"] = len(group)

    distribution = Counter()
    food_distribution = Counter()
    missing_building_distribution = Counter()
    boundary_state = Counter()
    boundary_per_hjd: dict[str, Counter[str]] = defaultdict(Counter)
    conflict_pairs = Counter()
    outside_distribution = Counter()
    diagnostic_groups = Counter()
    building_numbers: set[str] = set()
    food_missing_building = 0
    identity_rows = 0
    for candidate in candidates:
        hjd = candidate["mappedAdministrativeDongCode"]
        distribution[hjd] += 1
        if candidate["foodCategory"]:
            food_distribution[hjd] += 1
        protected = candidate["protectedSource"]
        building_number = protected["buildingManagementNumber"]
        if building_number:
            building_numbers.add(building_number)
        else:
            missing_building_distribution[hjd] += 1
            if candidate["foodCategory"]:
                food_missing_building += 1

        longitude = candidate.pop("_longitude")
        latitude = candidate.pop("_latitude")
        local_x, local_z = frame.wgs84_to_local(longitude, latitude)
        candidate["commonEnuMillimeters"] = {"x": local_x, "z": local_z}
        projected_x, projected_y = transformer.transform(longitude, latitude)
        point = dependencies.Point(projected_x, projected_y)
        hits = [boundary.stable_id for boundary in boundaries if boundary.geometry.covers(point)]
        hits.sort()
        candidate["historicalBoundaryHitAdministrativeAreaStableIds"] = hits
        source_stable_id = candidate["administrativeAreaStableId"]
        if hits == [source_stable_id]:
            diagnostic = MATCHED_DIAGNOSTIC
            state = "Matched"
        elif not hits:
            diagnostic = OUTSIDE_DIAGNOSTIC
            state = "Outside"
            outside_distribution[hjd] += 1
        elif len(hits) == 1:
            diagnostic = CONFLICT_DIAGNOSTIC
            state = "Conflict"
            hit_code = hits[0].removeprefix("region:kr:hjd:")
            conflict_pairs[f"{hjd}>{hit_code}"] += 1
        else:
            diagnostic = "SourceAdministrativeDongMappedHistoricalBoundaryMultiple"
            state = "Multiple"
        candidate["historicalBoundaryDiagnosticCode"] = diagnostic
        boundary_state[state] += 1
        boundary_per_hjd[hjd][state] += 1
        if state != "Matched":
            diagnostic_groups[(
                protected["sourceLongitudeText"],
                protected["sourceLatitudeText"],
                source_stable_id,
                tuple(hits),
                state,
            )] += 1

        diagnostics = [diagnostic, DATUM_DIAGNOSTIC]
        if not building_number:
            diagnostics.append(BUILDING_MISSING_DIAGNOSTIC)
        if candidate["identityCandidateGroupSha256"] is not None:
            diagnostics.append(IDENTITY_DIAGNOSTIC)
            identity_rows += 1
        diagnostics.extend((OPERATION_DIAGNOSTIC, PROTECTED_DIAGNOSTIC))
        candidate["qualityDiagnosticCodes"] = diagnostics

    require(boundary_state["Matched"] == EXPECTED_COUNTS["historicalBoundaryMatchedRows"], "BoundaryMatchedCountMismatch")
    require(boundary_state["Conflict"] == EXPECTED_COUNTS["historicalBoundaryConflictRows"], "BoundaryConflictCountMismatch")
    require(boundary_state["Outside"] == EXPECTED_COUNTS["historicalBoundaryOutsideRows"], "BoundaryOutsideCountMismatch")
    require(boundary_state["Multiple"] == EXPECTED_COUNTS["historicalBoundaryMultipleRows"], "BoundaryMultipleCountMismatch")
    require(dict(sorted(conflict_pairs.items())) == EXPECTED_CONFLICT_PAIRS, "BoundaryConflictPairsMismatch")
    require(dict(sorted(outside_distribution.items())) == EXPECTED_OUTSIDE_DISTRIBUTION, "BoundaryOutsideDistributionMismatch")
    require(len(diagnostic_groups) == EXPECTED_COUNTS["historicalBoundaryDiagnosticGroups"], "BoundaryDiagnosticGroupCountMismatch")
    group_lines = [
        json.dumps(
            [key[0], key[1], key[2], list(key[3]), key[4], count],
            ensure_ascii=False,
            separators=(",", ":"),
        )
        for key, count in sorted(diagnostic_groups.items())
    ]
    group_digest = sha256_bytes(("\n".join(group_lines) + "\n").encode("utf-8"))
    require(group_digest == EXPECTED_DIAGNOSTIC_GROUP_DIGEST, "BoundaryDiagnosticGroupDigestMismatch")
    require(identity_rows == EXPECTED_COUNTS["identityCandidateRows"], "IdentityCandidateRowCountMismatch")
    require(len(building_numbers) == EXPECTED_COUNTS["uniqueBuildingManagementNumbers"], "UniqueBuildingCountMismatch")
    require(sum(missing_building_distribution.values()) == EXPECTED_COUNTS["missingBuildingManagementNumberRows"], "MissingBuildingCountMismatch")
    require(food_missing_building == EXPECTED_COUNTS["foodMissingBuildingManagementNumberRows"], "FoodMissingBuildingCountMismatch")
    for code, expected in EXPECTED_DISTRIBUTION.items():
        require(distribution[code] == expected[0], f"DistributionMismatch:{code}")
        require(food_distribution[code] == expected[1], f"FoodDistributionMismatch:{code}")
    return {
        "distribution": dict(sorted(distribution.items())),
        "foodDistribution": dict(sorted(food_distribution.items())),
        "missingBuildingDistribution": dict(sorted(missing_building_distribution.items())),
        "historicalBoundaryState": dict(sorted(boundary_state.items())),
        "historicalBoundaryPerAdministrativeArea": {
            code: {
                "matchedRows": boundary_per_hjd[code]["Matched"],
                "conflictRows": boundary_per_hjd[code]["Conflict"],
                "outsideRows": boundary_per_hjd[code]["Outside"],
                "multipleRows": boundary_per_hjd[code]["Multiple"],
            }
            for code in sorted(EXPECTED_DISTRIBUTION)
        },
        "historicalBoundaryConflictPairs": dict(sorted(conflict_pairs.items())),
        "historicalBoundaryOutsideDistribution": dict(sorted(outside_distribution.items())),
        "historicalBoundaryDiagnosticGroupDigestSha256": group_digest,
        "identityCandidateGroups": len(duplicate_groups),
        "identityCandidateRows": identity_rows,
        "uniqueBuildingManagementNumbers": len(building_numbers),
        "missingBuildingManagementNumberRows": sum(missing_building_distribution.values()),
        "foodMissingBuildingManagementNumberRows": food_missing_building,
    }


def build_hash_header(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    generator_path: Path,
) -> dict[str, Any]:
    source = source_by_role(scope, "semasNationalArchive")
    entry = source["seoulEntry"]
    receipt = source_by_role(scope, "semasAcquisitionReceipt")
    download = source_by_role(scope, "semasDownloadMetadata")
    metadata = source_by_role(scope, "semasDatasetMetadata")
    mois = source_by_role(scope, "moisAdministrativeCodes")
    mois_h = mois["entries"]["administrative"]
    mois_mix = mois["entries"]["administrativeLegal"]
    r2_scope = source_by_role(scope, "r2ScopeDefinition")
    r2_manifest = source_by_role(scope, "r2ScopeManifest")
    boundary = source_by_role(scope, "oa22160BoundaryArchive")
    legacy_selected = source_by_role(scope, "legacyMyeonmokSelected")
    legacy_factory = source_by_role(scope, "legacyFactoryCsv")
    contracts = scope["contracts"]
    header = {
        "domainSeparator": CANDIDATE_SET_DOMAIN,
        "schemaVersion": CANDIDATE_SCHEMA,
        "revision": REVISION,
        "designDocumentRef": DESIGN_DOCUMENT_REF,
        "designHashSha256": DESIGN_HASH_SHA256,
        "scopeRelativePath": scope_path.relative_to(root).as_posix(),
        "scopeDefinitionSha256": sha256_file(scope_path),
        "generatorRelativePath": generator_path.relative_to(root).as_posix(),
        "generatorSha256": sha256_file(generator_path),
        "semasArchiveRelativePath": source["repositoryRelativePath"],
        "semasArchiveSha256": source["contentHashSha256"],
        "semasArchiveBytes": source["byteLength"],
        "seoulEntryLogicalName": entry["logicalName"],
        "seoulEntryLogicalNameUtf8Sha256": entry["logicalNameUtf8HashSha256"],
        "seoulEntryRawNameEncoding": entry["rawNameEncoding"],
        "seoulEntryRawNameBytes": entry["rawNameByteLength"],
        "seoulEntryRawNameBase64": entry["rawNameBase64"],
        "seoulEntryRawNameSha256": entry["rawNameHashSha256"],
        "seoulEntryBytes": entry["byteLength"],
        "seoulEntryCrc32": entry["crc32"],
        "seoulEntrySha256": entry["contentHashSha256"],
        "seoulEntryRows": entry["recordCount"],
        "receiptRelativePath": receipt["repositoryRelativePath"],
        "receiptSha256": receipt["contentHashSha256"],
        "receiptBytes": receipt["byteLength"],
        "downloadMetadataRelativePath": download["repositoryRelativePath"],
        "downloadMetadataSha256": download["contentHashSha256"],
        "downloadMetadataBytes": download["byteLength"],
        "datasetMetadataRelativePath": metadata["repositoryRelativePath"],
        "datasetMetadataSha256": metadata["contentHashSha256"],
        "datasetMetadataBytes": metadata["byteLength"],
        "moisArchiveRelativePath": mois["repositoryRelativePath"],
        "moisArchiveSha256": mois["contentHashSha256"],
        "moisArchiveBytes": mois["byteLength"],
        "moisAdministrativeEntryName": mois_h["name"],
        "moisAdministrativeEntrySha256": mois_h["contentHashSha256"],
        "moisAdministrativeEntryBytes": mois_h["byteLength"],
        "moisAdministrativeEntryCrc32": mois_h["crc32"],
        "moisAdministrativeLegalEntryName": mois_mix["name"],
        "moisAdministrativeLegalEntrySha256": mois_mix["contentHashSha256"],
        "moisAdministrativeLegalEntryBytes": mois_mix["byteLength"],
        "moisAdministrativeLegalEntryCrc32": mois_mix["crc32"],
        "r2ScopeRelativePath": r2_scope["repositoryRelativePath"],
        "r2ScopeSha256": r2_scope["contentHashSha256"],
        "r2ScopeBytes": r2_scope["byteLength"],
        "r2ScopeManifestRelativePath": r2_manifest["repositoryRelativePath"],
        "r2ScopeManifestFileSha256": r2_manifest["contentHashSha256"],
        "r2ScopeManifestBytes": r2_manifest["byteLength"],
        "r2ScopeManifestContentHashSha256": r2_manifest["documentContentHashSha256"],
        "historicalBoundaryRelativePath": boundary["repositoryRelativePath"],
        "historicalBoundarySha256": boundary["contentHashSha256"],
        "historicalBoundaryBytes": boundary["byteLength"],
        "legacySelectedRelativePath": legacy_selected["repositoryRelativePath"],
        "legacySelectedSha256": legacy_selected["contentHashSha256"],
        "legacySelectedBytes": legacy_selected["byteLength"],
        "legacyFactoryRelativePath": legacy_factory["repositoryRelativePath"],
        "legacyFactorySha256": legacy_factory["contentHashSha256"],
        "legacyFactoryBytes": legacy_factory["byteLength"],
        "mappingContractSha256": contract_hash(contracts["mapping"]),
        "sourceRowContractSha256": contract_hash(contracts["sourceRow"]),
        "historicalBoundaryContractSha256": contract_hash(contracts["historicalBoundary"]),
        "coordinateContractSha256": contract_hash(contracts["coordinate"]),
        "identityContractSha256": contract_hash(contracts["identity"]),
        "expectedCountsContractSha256": contract_hash(contracts["expectedCounts"]),
    }
    require(tuple(header) == HEADER_HASH_FIELD_ORDER, "CandidateHashHeaderOrderInvalid")
    return header


def candidate_ndjson_bytes(candidates: Sequence[dict[str, Any]]) -> bytes:
    lines = [
        json.dumps(item, ensure_ascii=False, allow_nan=False, separators=(",", ":"))
        for item in candidates
    ]
    return ("\n".join(lines) + "\n").encode("utf-8")


def aggregate_source_lineage(scope: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for source in scope["sources"]:
        item = {
            "role": source["role"],
            "repositoryRelativePath": source["repositoryRelativePath"],
            "contentHashSha256": source["contentHashSha256"],
            "byteLength": source["byteLength"],
        }
        if "recordCount" in source:
            item["recordCount"] = source["recordCount"]
        result.append(item)
    return result


def assert_aggregate_only(document: Any) -> None:
    banned_keys = {
        "protectedSource",
        "providerShopIdCanonical",
        "businessName",
        "branchName",
        "landLotAddress",
        "roadNameAddress",
        "buildingManagementNumber",
        "buildingName",
        "buildingDong",
        "floor",
        "unit",
        "sourceLongitudeText",
        "sourceLatitudeText",
    }
    if isinstance(document, dict):
        require(not (set(document) & banned_keys), "ProtectedBusinessDetailInAggregateDocument")
        for value in document.values():
            assert_aggregate_only(value)
    elif isinstance(document, list):
        for value in document:
            assert_aggregate_only(value)


def build_documents(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    generator_path: Path,
    generator_bytes: bytes,
    candidates: list[dict[str, Any]],
    counts: dict[str, Any],
    per_area: list[dict[str, Any]],
    boundary_summary: dict[str, Any],
    legacy_summary: dict[str, Any],
) -> tuple[str, dict[str, bytes]]:
    header = build_hash_header(root, scope_path, scope, generator_path)
    set_hash = candidate_set_hash(header, candidates)
    for candidate in candidates:
        candidate["candidateSetHashSha256"] = set_hash
    candidates_bytes = candidate_ndjson_bytes(candidates)
    candidates_hash = sha256_bytes(candidates_bytes)
    generator_hash = sha256_bytes(generator_bytes)
    contracts = scope["contracts"]
    contract_hashes = {
        "mapping": contract_hash(contracts["mapping"]),
        "sourceRow": contract_hash(contracts["sourceRow"]),
        "historicalBoundary": contract_hash(contracts["historicalBoundary"]),
        "coordinate": contract_hash(contracts["coordinate"]),
        "identity": contract_hash(contracts["identity"]),
        "expectedCounts": contract_hash(contracts["expectedCounts"]),
    }
    manifest = {
        "schemaVersion": "administrative-dong-business-candidate-manifest.v1",
        "revision": REVISION,
        "candidateSchemaVersion": CANDIDATE_SCHEMA,
        "candidateSetHashSha256": set_hash,
        "generatedAtUtc": scope["generatedAtUtc"],
        "evidenceAsOfUtc": "2026-06-30T00:00:00Z",
        "derivedAtUtc": "2026-09-15T00:00:00Z",
        "collectedAtUtc": "2026-09-08T10:31:09.6612043Z",
        "designDocumentRef": DESIGN_DOCUMENT_REF,
        "designHashSha256": DESIGN_HASH_SHA256,
        "scopeRelativePath": scope_path.relative_to(root).as_posix(),
        "scopeDefinitionSha256": sha256_file(scope_path),
        "generatorRelativePath": generator_path.relative_to(root).as_posix(),
        "generatorSha256": generator_hash,
        "reviewStatus": "PendingHumanReview",
        "sourceCoordinateDatumStatusCode": "SourceCoordinateDatumUnconfirmed",
        "completionStatusCode": "G4aBusinessCandidateBundleGeneratedAndVerified",
        "privacyBoundaryCode": "ExactBusinessDetailsPrivateLocalAndProtectedRdbOnly",
        "counts": counts,
        "perAdministrativeArea": per_area,
        "historicalBoundarySummary": boundary_summary,
        "legacyPreservationSummary": legacy_summary,
        "sourceLineage": aggregate_source_lineage(scope),
        "contractHashes": contract_hashes,
        "candidateSetHashContract": {
            "algorithm": "SHA-256",
            "framing": "presence-byte-then-uint32-big-endian-utf8-byte-length-then-utf8-bytes",
            "nullPresenceByte": 0,
            "presentPresenceByte": 1,
            "booleanEncoding": "lowercase-true-or-false",
            "integerEncoding": "invariant-decimal",
            "coordinateEncoding": "original-csv-text",
            "arrayEncoding": "framed-count-then-each-item-in-ordinal-order",
            "candidateOrder": "providerShopIdCanonical-ordinal",
            "headerFieldOrder": list(HEADER_HASH_FIELD_ORDER),
            "candidateFieldOrder": list(CANDIDATE_HASH_FIELD_ORDER),
        },
        "authorityFlags": dict(AUTHORITY_FLAGS),
        "contentHashSha256": "",
    }
    apply_content_hash(manifest)
    assert_aggregate_only(manifest)
    manifest_bytes = pretty_json_bytes(manifest)
    manifest_hash = sha256_bytes(manifest_bytes)

    audit = {
        "schemaVersion": "administrative-dong-business-candidate-audit.v1",
        "revision": REVISION,
        "candidateSetHashSha256": set_hash,
        "status": "PASS",
        "scopeDefinitionSha256": sha256_file(scope_path),
        "generatorSha256": generator_hash,
        "counts": counts,
        "perAdministrativeArea": per_area,
        "historicalBoundarySummary": boundary_summary,
        "legacyPreservationSummary": legacy_summary,
        "contractHashes": contract_hashes,
        "candidateFileSha256": candidates_hash,
        "candidateFileByteLength": len(candidates_bytes),
        "manifestFileSha256": manifest_hash,
        "manifestFileByteLength": len(manifest_bytes),
        "generatorSourceFileSha256": generator_hash,
        "generatorSourceFileByteLength": len(generator_bytes),
        "privacyInspectionStatusCode": "AggregateDocumentsContainNoExactBusinessDetailValues",
        "authorityFlags": dict(AUTHORITY_FLAGS),
        "contentHashSha256": "",
    }
    apply_content_hash(audit)
    assert_aggregate_only(audit)
    audit_bytes = pretty_json_bytes(audit)
    audit_hash = sha256_bytes(audit_bytes)

    complete = {
        "schemaVersion": "administrative-dong-business-candidate-complete.v1",
        "revision": REVISION,
        "candidateSetHashSha256": set_hash,
        "status": "Complete",
        "fileCount": 4,
        "files": [
            {"name": "manifest.json", "sha256": manifest_hash, "byteLength": len(manifest_bytes)},
            {"name": "candidates.ndjson", "sha256": candidates_hash, "byteLength": len(candidates_bytes)},
            {"name": "audit.json", "sha256": audit_hash, "byteLength": len(audit_bytes)},
            {"name": "generator-source.py", "sha256": generator_hash, "byteLength": len(generator_bytes)},
        ],
        "authorityFlags": dict(AUTHORITY_FLAGS),
        "contentHashSha256": "",
    }
    apply_content_hash(complete)
    assert_aggregate_only(complete)
    complete_bytes = pretty_json_bytes(complete)
    payloads = {
        "manifest.json": manifest_bytes,
        "candidates.ndjson": candidates_bytes,
        "audit.json": audit_bytes,
        "generator-source.py": generator_bytes,
        "complete.json": complete_bytes,
    }
    return set_hash, payloads


def prepare(root: Path, scope_path: Path) -> PreparedBundle:
    root = require_repository_root(root)
    scope_path = repository_path(root, scope_path)
    scope, paths = load_scope(root, scope_path)
    dependencies = load_dependencies(root)
    mapping = read_mois_mapping(paths["moisAdministrativeCodes"], scope)
    boundaries = read_boundaries(paths["oa22160BoundaryArchive"], scope, dependencies)
    candidates, seoul_summary = read_seoul_candidates(
        paths["semasNationalArchive"], scope, mapping
    )
    selected_provider_ids = {
        item["protectedSource"]["providerShopIdCanonical"] for item in candidates
    }
    require(len(selected_provider_ids) == len(candidates), "SelectedProviderShopIdDuplicate")
    require(
        SYNTHETIC_PROVIDER_ID not in selected_provider_ids,
        "SyntheticFixtureCollidesWithSourceIdentity",
    )
    national_summary = verify_national_occurrences(
        paths["semasNationalArchive"], selected_provider_ids
    )
    legacy_summary = verify_legacy_preservation(
        paths["legacyMyeonmokSelected"],
        paths["legacyFactoryCsv"],
        selected_provider_ids,
    )
    enriched = enrich_candidates(candidates, boundaries, scope, dependencies)

    source_keys = [item["sourceFeatureKey"] for item in candidates]
    stable_ids = [item["stableId"] for item in candidates]
    dimension_keys = [item["dimensionKey"] for item in candidates]
    provider_hashes = [item["providerBusinessIdSha256"] for item in candidates]
    for values, code in (
        (source_keys, "SourceFeatureKeyDuplicate"),
        (stable_ids, "StableIdDuplicate"),
        (dimension_keys, "DimensionKeyDuplicate"),
        (provider_hashes, "ProviderBusinessIdHashCollision"),
    ):
        require(len(values) == len(set(values)), code)
    require(
        all(
            item["protectedSource"]["providerShopIdCanonical"]
            not in item["sourceFeatureKey"]
            and item["protectedSource"]["providerShopIdCanonical"] not in item["stableId"]
            and item["protectedSource"]["providerShopIdCanonical"] not in item["dimensionKey"]
            for item in candidates
        ),
        "RawProviderIdLeakedIntoIdentifier",
    )
    require(
        all(tuple(item) == CANDIDATE_PROPERTY_ORDER for item in candidates),
        "CandidatePropertyOrderInvalid",
    )

    distribution = enriched["distribution"]
    food_distribution = enriched["foodDistribution"]
    missing_building_distribution = enriched["missingBuildingDistribution"]
    boundary_per_hjd = enriched["historicalBoundaryPerAdministrativeArea"]
    per_area = []
    for area in scope["administrativeAreas"]:
        code = area["mappedAdministrativeDongCode"]
        per_area.append(
            {
                "administrativeAreaStableId": area["administrativeAreaStableId"],
                "mappedAdministrativeDongCode": code,
                "candidateRows": distribution.get(code, 0),
                "foodRows": food_distribution.get(code, 0),
                "missingBuildingManagementNumberRows": missing_building_distribution.get(code, 0),
                **boundary_per_hjd[code],
            }
        )

    counts = {
        "nationalCsvEntries": national_summary["nationalCsvEntries"],
        "nationalRows": national_summary["nationalRows"],
        "seoulRows": seoul_summary["seoulRows"],
        "candidateRows": len(candidates),
        "foodRows": sum(1 for item in candidates if item["foodCategory"]),
        "missingProviderShopIdRows": 0,
        "duplicateProviderShopIdRows": 0,
        "missingCoordinateRows": 0,
        "missingRoadAddressRows": 0,
        "missingLandAddressRows": 0,
        "missingBuildingManagementNumberRows": enriched["missingBuildingManagementNumberRows"],
        "foodMissingBuildingManagementNumberRows": enriched["foodMissingBuildingManagementNumberRows"],
        "uniqueBuildingManagementNumbers": enriched["uniqueBuildingManagementNumbers"],
        "historicalBoundaryMatchedRows": enriched["historicalBoundaryState"].get("Matched", 0),
        "historicalBoundaryConflictRows": enriched["historicalBoundaryState"].get("Conflict", 0),
        "historicalBoundaryOutsideRows": enriched["historicalBoundaryState"].get("Outside", 0),
        "historicalBoundaryMultipleRows": enriched["historicalBoundaryState"].get("Multiple", 0),
        "historicalBoundaryDiagnosticRows": (
            enriched["historicalBoundaryState"].get("Conflict", 0)
            + enriched["historicalBoundaryState"].get("Outside", 0)
            + enriched["historicalBoundaryState"].get("Multiple", 0)
        ),
        "historicalBoundaryDiagnosticGroups": EXPECTED_COUNTS["historicalBoundaryDiagnosticGroups"],
        "identityCandidateGroups": enriched["identityCandidateGroups"],
        "identityCandidateRows": enriched["identityCandidateRows"],
        **legacy_summary,
        "sourceAdministrativeNameDifferenceAreas": seoul_summary[
            "sourceAdministrativeNameDifferenceAreas"
        ],
    }
    require(counts == EXPECTED_COUNTS, "ComputedCountsMismatch")
    boundary_summary = {
        "sourceOwnershipPolicyCode": "SourceAdministrativeDongOwnershipPreserved",
        "historicalBoundaryStatusCode": "HistoricalBootstrapDiagnosticOnly",
        "sourceCoordinateDatumStatusCode": "SourceCoordinateDatumUnconfirmed",
        "stateCounts": enriched["historicalBoundaryState"],
        "conflictPairs": enriched["historicalBoundaryConflictPairs"],
        "outsideDistribution": enriched["historicalBoundaryOutsideDistribution"],
        "diagnosticGroupCount": EXPECTED_COUNTS["historicalBoundaryDiagnosticGroups"],
        "diagnosticGroupDigestSha256": enriched[
            "historicalBoundaryDiagnosticGroupDigestSha256"
        ],
    }
    generator_path = repository_path(root, Path(__file__).resolve())
    generator_bytes = generator_path.read_bytes()
    generator_provider_tokens = {
        match.group(0).decode("ascii")
        for match in PROVIDER_ID_TOKEN_PATTERN_BYTES.finditer(generator_bytes)
    }
    require(
        not (generator_provider_tokens & selected_provider_ids),
        "GeneratorContainsSelectedProviderShopId",
    )
    set_hash, payloads = build_documents(
        root,
        scope_path,
        scope,
        generator_path,
        generator_bytes,
        candidates,
        counts,
        per_area,
        boundary_summary,
        legacy_summary,
    )
    return PreparedBundle(
        scope=scope,
        scope_path=scope_path,
        generator_path=generator_path,
        generator_bytes=generator_bytes,
        candidates=candidates,
        candidate_set_hash=set_hash,
        payloads=payloads,
    )


def generation_directory(root: Path, scope: dict[str, Any], set_hash: str) -> Path:
    output = repository_path(root, scope["output"]["repositoryRelativeDirectory"])
    return repository_path(root, output / scope["output"]["generationDirectoryName"] / set_hash.lower())


def verify_payloads(root: Path, prepared: PreparedBundle) -> dict[str, Any]:
    root = require_repository_root(root)
    generation = generation_directory(root, prepared.scope, prepared.candidate_set_hash)
    require(generation.is_dir() and not is_reparse_path(generation), "CandidateGenerationMissing")
    require(
        sorted(item.name for item in generation.iterdir()) == sorted(prepared.payloads),
        "CandidateGenerationFileSetMismatch",
    )
    for name, expected in prepared.payloads.items():
        path = repository_path(root, generation / name)
        require(path.is_file() and not is_reparse_path(path), f"CandidateGenerationFileMissing:{name}")
        require(path.read_bytes() == expected, f"CandidateGenerationFileMismatch:{name}")

    manifest = load_json(generation / "manifest.json")
    audit = load_json(generation / "audit.json")
    complete = load_json(generation / "complete.json")
    for document, schema in (
        (manifest, "administrative-dong-business-candidate-manifest.v1"),
        (audit, "administrative-dong-business-candidate-audit.v1"),
        (complete, "administrative-dong-business-candidate-complete.v1"),
    ):
        require(document.get("schemaVersion") == schema, "GeneratedDocumentSchemaMismatch")
        require(document.get("revision") == REVISION, "GeneratedDocumentRevisionMismatch")
        require(
            document.get("candidateSetHashSha256") == prepared.candidate_set_hash,
            "GeneratedDocumentCandidateSetHashMismatch",
        )
        require(document_content_hash(document) == document["contentHashSha256"], "GeneratedDocumentContentHashMismatch")
        assert_aggregate_only(document)
    require(complete.get("status") == "Complete", "CandidateCompleteMarkerInvalid")
    expected_file_metadata = [
        {
            "name": name,
            "sha256": sha256_file(generation / name),
            "byteLength": (generation / name).stat().st_size,
        }
        for name in ("manifest.json", "candidates.ndjson", "audit.json", "generator-source.py")
    ]
    require(complete.get("fileCount") == 4, "CandidateCompleteFileCountMismatch")
    require(complete.get("files") == expected_file_metadata, "CandidateCompleteFileMetadataMismatch")
    require((generation / "generator-source.py").read_bytes() == prepared.generator_bytes, "GeneratorSourceSnapshotMismatch")

    parsed: list[dict[str, Any]] = []
    with (generation / "candidates.ndjson").open("r", encoding="utf-8", newline="") as stream:
        prior: str | None = None
        for line in stream:
            require(line.endswith("\n") and not line.endswith("\r\n"), "CandidateNdjsonLineEndingInvalid")
            item = json.loads(line)
            require(tuple(item) == CANDIDATE_PROPERTY_ORDER, "CandidateNdjsonPropertyOrderInvalid")
            require(
                tuple(item["protectedSource"]) == PROTECTED_SOURCE_PROPERTY_ORDER,
                "CandidateNdjsonProtectedPropertyOrderInvalid",
            )
            provider_id = item["protectedSource"]["providerShopIdCanonical"]
            require(prior is None or prior < provider_id, "CandidateNdjsonOrderInvalid")
            prior = provider_id
            require(item["candidateSetHashSha256"] == prepared.candidate_set_hash, "CandidateRowSetHashMismatch")
            require(item["authorityFlags"] == AUTHORITY_FLAGS, "CandidateAuthorityFlagsMismatch")
            require(item["qualityCode"] == "PendingHumanReview", "CandidateQualityCodeMismatch")
            parsed.append(item)
    require(len(parsed) == EXPECTED_COUNTS["candidateRows"], "CandidateNdjsonRowCountMismatch")
    header = build_hash_header(root, prepared.scope_path, prepared.scope, prepared.generator_path)
    require(candidate_set_hash(header, parsed) == prepared.candidate_set_hash, "CandidateSetHashRecalculationMismatch")
    return {
        "status": "PASS",
        "revision": REVISION,
        "candidateSetHashSha256": prepared.candidate_set_hash,
        "generationRelativePath": generation.relative_to(root).as_posix(),
        "candidateRows": len(parsed),
        "foodRows": manifest["counts"]["foodRows"],
        "historicalBoundaryDiagnosticRows": manifest["counts"]["historicalBoundaryDiagnosticRows"],
        "identityCandidateGroups": manifest["counts"]["identityCandidateGroups"],
        "identityCandidateRows": manifest["counts"]["identityCandidateRows"],
        "manifestSha256": sha256_file(generation / "manifest.json"),
        "candidatesSha256": sha256_file(generation / "candidates.ndjson"),
        "auditSha256": sha256_file(generation / "audit.json"),
        "generatorSourceSha256": sha256_file(generation / "generator-source.py"),
        "completeSha256": sha256_file(generation / "complete.json"),
        "scopeDefinitionSha256": sha256_file(prepared.scope_path),
        "generatorSha256": sha256_file(prepared.generator_path),
        "authorityFlags": dict(AUTHORITY_FLAGS),
    }


def remove_private_staging(root: Path, staging_root: Path, staging: Path) -> None:
    staging_root = repository_path(root, staging_root)
    staging = repository_path(root, staging)
    require(staging.parent == staging_root, "CandidateStagingDeletePathInvalid")
    require(staging.is_dir() and not is_reparse_path(staging), "CandidateStagingDeletePathInvalid")
    shutil.rmtree(staging)


def build(root: Path, scope_path: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    prepared = prepare(root, repository_path(root, scope_path))
    output_root = ensure_plain_directory(
        root,
        repository_path(root, prepared.scope["output"]["repositoryRelativeDirectory"]),
        "CandidateOutputRootInvalid",
    )
    staging_root = ensure_plain_directory(root, output_root / "staging", "CandidateStagingRootInvalid")
    generations_root = ensure_plain_directory(
        root,
        output_root / prepared.scope["output"]["generationDirectoryName"],
        "CandidateGenerationsRootInvalid",
    )
    staging = repository_path(root, staging_root / ("candidate-" + uuid.uuid4().hex))
    final = repository_path(root, generations_root / prepared.candidate_set_hash.lower())
    staging.mkdir(parents=False, exist_ok=False)
    require(staging.is_dir() and not is_reparse_path(staging), "CandidateStagingPathInvalid")
    try:
        names = ["manifest.json", "candidates.ndjson", "audit.json", "generator-source.py", "complete.json"]
        for name in names:
            payload = prepared.payloads[name]
            path = repository_path(root, staging / name)
            with path.open("xb") as stream:
                stream.write(payload)
            require(path.is_file() and not is_reparse_path(path) and path.read_bytes() == payload, f"CandidateStagingFileMismatch:{name}")
        require(sorted(item.name for item in staging.iterdir()) == sorted(names), "CandidateStagingFileSetMismatch")
        for path in (output_root, staging_root, generations_root, staging):
            require(path.is_dir() and not is_reparse_path(path), "CandidateAtomicMovePathInvalid")
        if os.path.lexists(final):
            require(final.is_dir() and not is_reparse_path(final), "CandidateGenerationPathInvalid")
            require(sorted(item.name for item in final.iterdir()) == sorted(names), "CandidateGenerationCollision")
            for name in names:
                require((final / name).read_bytes() == prepared.payloads[name], "CandidateGenerationCollision")
            changed_files = 0
            remove_private_staging(root, staging_root, staging)
        else:
            os.replace(staging, final)
            final = repository_path(root, final)
            require(final.is_dir() and not is_reparse_path(final), "CandidateAtomicMoveResultInvalid")
            changed_files = len(names)
    finally:
        if os.path.lexists(staging):
            remove_private_staging(root, staging_root, staging)
    result = verify_payloads(root, prepared)
    result["changedFiles"] = changed_files
    return result


def verify(root: Path, scope_path: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    prepared = prepare(root, repository_path(root, scope_path))
    return verify_payloads(root, prepared)


def self_test(root: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    dependencies = load_dependencies(root)
    tests = 0

    def test(condition: bool, code: str) -> None:
        nonlocal tests
        require(condition, f"SelfTestFailed:{code}")
        tests += 1

    test(framed_sha256(("ab", "c")) != framed_sha256(("a", "bc")), "LengthFraming")
    test(frame_value(None) != frame_value(""), "NullPresenceFraming")
    test(frame_value(False) != frame_value(True), "BooleanCanonicalEncoding")
    test(source_row_hash([""] * 39) != source_row_hash(["x"] + [""] * 38), "SourceRowHash")
    digest, source_key, stable_id, dimension_key = _stable_identity(SYNTHETIC_PROVIDER_ID)
    test(len(digest) == 64 and digest == digest.upper(), "ProviderDigest")
    test(SYNTHETIC_PROVIDER_ID not in source_key + stable_id + dimension_key, "RawIdNotInKeys")
    test(source_key.endswith(digest.lower()) and stable_id.endswith(digest.lower()), "HashedStableKeys")
    test(dimension_key.endswith(digest), "HashedDimensionKey")
    test(PROVIDER_ID_PATTERN.fullmatch(SYNTHETIC_PROVIDER_ID) is not None, "ProviderIdFormat")
    test(PROVIDER_ID_PATTERN.fullmatch("MA-short") is None, "ProviderIdBadFormat")
    left = dependencies.Polygon([(0, 0), (1, 0), (1, 1), (0, 1)])
    right = dependencies.Polygon([(1, 0), (2, 0), (2, 1), (1, 1)])
    test(left.covers(dependencies.Point(0.5, 0.5)) and not right.covers(dependencies.Point(0.5, 0.5)), "UniqueCover")
    test(left.covers(dependencies.Point(1, 0.5)) and right.covers(dependencies.Point(1, 0.5)), "SharedBoundaryCover")
    test(not left.covers(dependencies.Point(3, 3)), "OutsideCover")
    test(sum(value is True for value in AUTHORITY_FLAGS.values()) == 1, "PositiveAuthorityFlags")
    test(sum(value is False for value in AUTHORITY_FLAGS.values()) == 11, "NegativeAuthorityFlags")
    marker = {"value": 1, "contentHashSha256": ""}
    apply_content_hash(marker)
    test(marker["contentHashSha256"] == document_content_hash(marker), "DocumentContentHash")
    try:
        assert_aggregate_only({"businessName": "protected"})
        aggregate_rejected = False
    except BusinessCandidateError:
        aggregate_rejected = True
    test(aggregate_rejected, "AggregateProtectedFieldRejected")
    outside_rejected = False
    try:
        repository_path(root, root.parent / "outside-business-scope.json")
    except BusinessCandidateError as exc:
        outside_rejected = str(exc) == "PathOutsideRepository"
    test(outside_rejected, "OutsideScopeRejected")
    target = repository_path(root, Path(__file__).relative_to(root))
    real_reparse = is_reparse_path

    def synthetic_reparse(path: Path) -> bool:
        return same_path(lexical_absolute(path), target) or real_reparse(path)

    reparse_rejected = False
    try:
        with mock.patch(f"{__name__}.is_reparse_path", side_effect=synthetic_reparse):
            repository_path(root, target)
    except BusinessCandidateError as exc:
        reparse_rejected = str(exc) == "RepositoryReparsePathRejected"
    test(reparse_rejected, "ReparseScopeRejected")
    test(len(EXPECTED_MAPPING) == 30 and len(EXPECTED_DISTRIBUTION) == 30, "ExactAreaCount")
    test(sum(value[0] for value in EXPECTED_DISTRIBUTION.values()) == 29_721, "CandidateCount")
    test(sum(value[1] for value in EXPECTED_DISTRIBUTION.values()) == 8_246, "FoodCount")
    test(sum(EXPECTED_CONFLICT_PAIRS.values()) == 29, "ConflictCount")
    test(sum(EXPECTED_OUTSIDE_DISTRIBUTION.values()) == 6, "OutsideCount")
    test(len(EXPECTED_HEADERS) == 39, "SourceHeaderCount")
    return {
        "status": "PASS",
        "selfTestsPassed": tests,
        "generatorSha256": sha256_file(Path(__file__).resolve()),
    }


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify", "self-test"))
    parser.add_argument("--root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--scope", type=Path)
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    try:
        root = require_repository_root(arguments.root)
        scope_path = repository_path(
            root,
            arguments.scope if arguments.scope is not None else DEFAULT_SCOPE_RELATIVE,
        )
        if arguments.mode == "self-test":
            result = self_test(root)
        elif arguments.mode == "build":
            result = build(root, scope_path)
        else:
            result = verify(root, scope_path)
        print(json.dumps(result, ensure_ascii=False, allow_nan=False, indent=2))
        return 0
    except BusinessCandidateError as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

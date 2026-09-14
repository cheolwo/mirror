#!/usr/bin/env python3
"""OA-21208 도보망을 30개 역사 행정동 G3c 비공개 후보로 동결·생성한다.

결과는 현재 보행·차량·오토바이 통행이나 Unity 권위가 아니다.
"""

from __future__ import annotations

import argparse
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
import sys
import tempfile
import urllib.parse
import urllib.request
import uuid
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence
from xml.etree import ElementTree


ROOT_DEFAULT = Path(__file__).resolve().parents[2]
SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-walk-network.g3c.r1.json"
)
DESIGN_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-walk-network-candidate.implementation.r13.md"
)
WORK_ORDER_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-walk-network-candidate.data-implementation.v1.json"
)
OUTPUT_RELATIVE = Path(
    "artifacts/local/public-data/"
    "admin-dong-walk-network-northeast-seoul-20260915-g3c-r1"
)
RAW_RELATIVE = OUTPUT_RELATIVE / "raw"

OFFICIAL_PAGE = "https://data.seoul.go.kr/dataList/OA-21208/A/1/datasetView.do"
SHEET_VIEW = "https://data.seoul.go.kr/dataList/sheetView.do?infId=OA-21208&srvType=S"
SHEET_DATA = "https://data.seoul.go.kr/dataList/dataView.do"
CSV_DOWNLOAD = "https://datafile.seoul.go.kr/bigfile/iot/sheet/csv/download.do"
FILE_DOWNLOAD = "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?&useCache=false"

REVISION = "northeast-seoul-admin-dong-walk-network-candidate.g3c.r1"
DESIGN_REVISION = "administrative-dong-diorama:walk-network-candidate.g3c.r1"
SCOPE_SCHEMA = "administrative-dong-walk-network-generation-scope.v1"
SCOPE_ID = "scope:administrative-dong-walk-network:northeast-seoul-rider:g3c:r1"
CANDIDATE_SCHEMA = "administrative-dong-walk-network-candidate.v1"
MANIFEST_SCHEMA = "administrative-dong-walk-network-candidate-manifest.v1"
AUDIT_SCHEMA = "administrative-dong-walk-network-candidate-audit.v1"
COMPLETE_SCHEMA = "administrative-dong-walk-network-candidate-complete.v1"

BOROUGHS = (
    ("gwangjin", "광진구", "1121500000"),
    ("dongdaemun", "동대문구", "1123000000"),
    ("jungnang", "중랑구", "1126000000"),
)
CSV_HEADER = (
    "노드링크 유형", "노드 WKT", "노드 ID", "노드 유형 코드", "링크 WKT",
    "링크 ID", "링크 유형 코드", "시작노드 ID", "종료노드 ID", "링크 길이",
    "시군구코드", "시군구명", "읍면동코드", "읍면동명", "고가도로",
    "지하철네트워크", "교량", "터널", "육교", "횡단보도", "공원,녹지",
    "건물내", "수집일자",
)
SOURCE_ACTORS = {
    "0000": (), "0001": ("PM",), "0010": ("Bicycle",),
    "0011": ("Bicycle", "PM"), "0100": ("Vehicle",),
    "0101": ("Vehicle", "PM"), "0110": ("Vehicle", "Bicycle"),
    "0111": ("Vehicle", "Bicycle", "PM"), "1000": ("Pedestrian",),
    "1001": ("Pedestrian", "PM"), "1010": ("Pedestrian", "Bicycle"),
    "1011": ("Pedestrian", "Bicycle", "PM"),
    "1100": ("Pedestrian", "Vehicle"),
    "1101": ("Pedestrian", "Vehicle", "PM"),
    "1110": ("Pedestrian", "Vehicle", "Bicycle"),
    "1111": ("Pedestrian", "Vehicle", "Bicycle", "PM"),
}
SOURCE_ACTOR_MEANINGS_KO = {
    "0000": "통행불가", "0001": "PM", "0010": "자전거", "0011": "자전거, PM",
    "0100": "차량", "0101": "차량, PM", "0110": "차량, 자전거",
    "0111": "차량, 자전거, PM", "1000": "보행자", "1001": "보행자, PM",
    "1010": "보행자, 자전거", "1011": "보행자, 자전거, PM",
    "1100": "보행자, 차량", "1101": "보행자, 차량, PM",
    "1110": "보행자, 차량, 자전거", "1111": "보행자, 차량, 자전거, PM",
}
SOURCE_NODE_MEANING_PREFIXES_KO = {
    "0": "일반노드", "1": "지하철 출입구", "2": "버스 정류장", "3": "지하보도 출입구",
}
AUTHORITY_FLAGS = {
    "privateReviewOnly": True,
    "historicalBoundaryBootstrapOnly": True,
    "observationCandidateOnly": True,
    "sourceDeclaredCoordinateReference": True,
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
}
HASH_HEADER_FIELDS = (
    "candidateSchemaVersion", "revision", "scopeDefinitionHashSha256",
    "designHashSha256", "dataImplementationHashSha256", "generatorHashSha256",
    "sourceHashesCanonicalJson", "authorityFlagsCanonicalJson", "candidateCountCanonicalJson",
)
HASH_CANDIDATE_FIELDS = (
    "candidateStableId", "candidateKindCode", "administrativeAreaStableId",
    "assignmentStateCode", "sourceFeatureIdSha256", "sourceSemanticBodySha256",
    "sourceCompleteBodySha256", "sourceFileHashSha256", "sourceOccurrenceKey",
    "sourceBoroughCode", "sourceBoroughName", "sourceLegalDongCode",
    "sourceLegalDongName", "sourceTypeCode", "geometryCanonicalJson",
    "sourceReportedActorCodesCanonicalJson", "sourceBeginNodeIdSha256",
    "sourceEndNodeIdSha256", "sourceReportedLengthMetersCanonicalJson",
    "fragmentOrdinalCanonicalJson", "physicalFragmentHashSha256",
    "sharedAdministrativeAreaIdsCanonicalJson", "qualityDiagnosticCodesCanonicalJson",
    "authorityFlagsCanonicalJson",
)


class WalkCandidateError(RuntimeError):
    pass


def require(condition: bool, code: str) -> None:
    if not condition:
        raise WalkCandidateError(code)


def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)


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
        raise WalkCandidateError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def http_request(url: str, data: dict[str, str] | None = None, timeout: int = 90) -> bytes:
    encoded = None if data is None else urllib.parse.urlencode(data).encode("ascii")
    request = urllib.request.Request(
        url,
        data=encoded,
        headers={"User-Agent": "Ssalddel-Administrative-Dong-G3c/1.0"},
        method="GET" if encoded is None else "POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        host = urllib.parse.urlparse(response.geturl()).hostname
        require(host in {"data.seoul.go.kr", "datafile.seoul.go.kr"}, "OfficialHostChanged")
        data_bytes = response.read(64 * 1024 * 1024 + 1)
        require(0 < len(data_bytes) <= 64 * 1024 * 1024, "DownloadSizeInvalid")
        return data_bytes


def sheet_count(filter_column: str, filter_value: str) -> int:
    query = urllib.parse.urlencode({
        "onepagerow": "100", "srvType": "S", "serviceKind": "0",
        "ssUserId": "SAMPLE_VIEW", "strWhere": "", "strOrderby": "",
        "infId": "OA-21208", "filterCol": filter_column,
        "pageNo": "1", "txtFilter": filter_value,
    })
    text = http_request(SHEET_DATA + "?" + query).decode("utf-8")
    require(re.search(r"\bresult\s*:\s*\"ok\"", text) is not None, "SheetCountUnavailable")
    match = re.search(r"\btotalCount\s*:\s*([0-9]+)", text)
    total = int(match.group(1)) if match else -1
    require(total > 0, "SheetCountInvalid")
    return total


def csv_rows(path: Path) -> tuple[list[str], int]:
    with path.open("r", encoding="cp949", newline="") as stream:
        reader = csv.reader(stream)
        try:
            header = next(reader)
        except StopIteration as exc:
            raise WalkCandidateError("CsvEmpty") from exc
        count = sum(1 for _ in reader)
    return header, count


def require_exact_flat_files(folder: Path, expected_names: set[str], code: str) -> None:
    require(folder.is_dir() and not is_reparse(folder), code + ":DirectoryUnsafe")
    entries = list(folder.iterdir())
    require(all(x.is_file() and not is_reparse(x) for x in entries), code + ":NonFileOrReparse")
    require({x.name for x in entries} == expected_names, code + ":FileSetChanged")


def xlsx_cell_values(path: Path) -> list[list[tuple[str, str]]]:
    namespace = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
    try:
        with zipfile.ZipFile(path, "r") as archive:
            names = set(archive.namelist())
            required = {"xl/sharedStrings.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"}
            require(required <= names, "CodebookWorkbookPartsChanged")
            shared_root = ElementTree.fromstring(archive.read("xl/sharedStrings.xml"))
            shared = [
                "".join(node.text or "" for node in item.iter(
                    "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}t"))
                for item in shared_root.findall("m:si", namespace)
            ]
            result: list[list[tuple[str, str]]] = []
            for sheet_name in ("sheet1.xml", "sheet2.xml"):
                sheet_root = ElementTree.fromstring(archive.read("xl/worksheets/" + sheet_name))
                cells: list[tuple[str, str]] = []
                for cell in sheet_root.findall(".//m:c", namespace):
                    value_node = cell.find("m:v", namespace)
                    if value_node is None:
                        continue
                    value = value_node.text or ""
                    if cell.get("t") == "s":
                        index = int(value)
                        require(0 <= index < len(shared), "CodebookSharedStringIndexInvalid")
                        value = shared[index]
                    cells.append((str(cell.get("r", "")), value.strip()))
                result.append(cells)
            return result
    except ElementTree.ParseError as exc:
        raise WalkCandidateError("CodebookXmlInvalid") from exc


def validate_codebook(path: Path) -> None:
    sheets = xlsx_cell_values(path)
    link_cells = dict(sheets[0])
    node_cells = dict(sheets[1])
    require(link_cells.get("C3") == "LINK_CODE" and link_cells.get("F4") == "PDSR_LINK",
            "CodebookLinkSchemaChanged")
    require(node_cells.get("C3") == "NODE_CODE" and node_cells.get("F4") == "PDSR_NODE",
            "CodebookNodeSchemaChanged")
    actual_link = {link_cells.get(f"B{row}", ""): link_cells.get(f"D{row}", "")
                   for row in range(6, 22)}
    require(actual_link == SOURCE_ACTOR_MEANINGS_KO, "CodebookLinkMeaningsChanged")
    actual_node = {node_cells.get(f"B{row}", ""): node_cells.get(f"D{row}", "")
                   for row in range(7, 11)}
    require(set(actual_node) == set(SOURCE_NODE_MEANING_PREFIXES_KO), "CodebookNodeCodesChanged")
    for code, prefix in SOURCE_NODE_MEANING_PREFIXES_KO.items():
        require(actual_node[code].startswith(prefix + " :"), f"CodebookNodeMeaningChanged:{code}")


def validate_csv_filter(path: Path, expected_name: str, expected_code: str, expected_rows: int) -> None:
    with path.open("r", encoding="cp949", newline="") as stream:
        reader = csv.DictReader(stream)
        require(tuple(reader.fieldnames or ()) == CSV_HEADER, f"FrozenCsvHeaderChanged:{path.name}")
        count = 0
        for row in reader:
            count += 1
            require(row["시군구명"].strip() == expected_name, f"FrozenCsvBoroughNameChanged:{path.name}")
            require(row["시군구코드"].strip() == expected_code, f"FrozenCsvBoroughCodeChanged:{path.name}")
        require(count == expected_rows, f"FrozenCsvRowCountChanged:{path.name}")


def validate_raw_receipt(root: Path) -> dict[str, Any]:
    folder = safe_path(root, RAW_RELATIVE)
    expected_names = {"receipt.json", "link-node-type-codes.xlsx", *(f"{key}.csv" for key, _, _ in BOROUGHS)}
    require_exact_flat_files(folder, expected_names, "FrozenRaw")
    receipt = load_json(folder / "receipt.json")
    require(receipt.get("schemaVersion") == "oa21208-admin-dong-walk-acquisition.v1", "ReceiptSchemaChanged")
    require(receipt.get("officialPageUrl") == OFFICIAL_PAGE, "ReceiptPageChanged")
    require(receipt.get("sourceReferenceVintage") == "2020", "ReceiptVintageChanged")
    require(receipt.get("sourceCoordinateReference") == "WGS84", "ReceiptCrsChanged")
    require(receipt.get("licenseCode") == "KOGL-Type1-Attribution", "ReceiptLicenseChanged")
    require(receipt.get("sourceDistrictFilterBoundaryHaloComplete") is False, "ReceiptHaloAuthorityChanged")
    sources = receipt.get("sources", [])
    require(isinstance(sources, list) and len(sources) == 4, "ReceiptSourceSetChanged")
    expected_source_names = expected_names - {"receipt.json"}
    require({str(x.get("storedFileName", "")) for x in sources} == expected_source_names,
            "ReceiptStoredFileSetChanged")
    boroughs_by_file = {f"{key}.csv": (name, code) for key, name, code in BOROUGHS}
    for source in sources:
        path = folder / str(source.get("storedFileName", ""))
        require(path.is_file() and not is_reparse(path), "FrozenRawMissing")
        require(path.stat().st_size == source.get("byteLength"), "FrozenRawLengthChanged")
        require(sha_file(path) == source.get("sha256"), "FrozenRawHashChanged")
        if source.get("kind") == "BoroughCsv":
            require(path.name in boroughs_by_file, "ReceiptBoroughCsvRoleChanged")
            expected_name, expected_code = boroughs_by_file[path.name]
            require(source.get("boroughName") == expected_name and source.get("boroughCode") == expected_code,
                    f"ReceiptBoroughMetadataChanged:{path.name}")
            validate_csv_filter(path, expected_name, expected_code, int(source.get("rowCount", -1)))
        else:
            require(source.get("kind") == "Codebook" and path.name == "link-node-type-codes.xlsx",
                    "ReceiptCodebookRoleChanged")
            validate_codebook(path)
    return receipt


def acquire(root: Path) -> dict[str, Any]:
    folder = safe_path(root, RAW_RELATIVE)
    if folder.exists():
        receipt = validate_raw_receipt(root)
        return {"status": "PASS", "reusedFrozenAcquisition": True,
                "sourceRows": sum(int(x.get("rowCount", 0)) for x in receipt["sources"]),
                "receiptSha256": sha_file(folder / "receipt.json")}

    require(not is_reparse(folder.parent), "OutputParentUnsafe")
    folder.parent.mkdir(parents=True, exist_ok=True)
    staging = folder.parent / (folder.name + ".acquiring-" + uuid.uuid4().hex)
    staging.mkdir()
    try:
        page = http_request(OFFICIAL_PAGE).decode("utf-8", errors="strict")
        require("서울시 자치구별 도보 네트워크 공간정보" in page, "OfficialTitleChanged")
        require("('20년 기준)" in page and "좌표계: WGS84" in page, "OfficialVintageOrCrsChanged")
        require("공공누리 1유형" in page and "제3저작권자" in page, "OfficialLicenseChanged")
        require("도보네트워크_링크노드유형코드.xlsx" in page, "OfficialCodebookMissing")
        sheet = http_request(SHEET_VIEW).decode("utf-8", errors="strict")
        require('option value="SGG_NM"' in sheet and "bigfile/iot/sheet/" in sheet
                and "download.do" in sheet,
                "OfficialSheetDownloadContractChanged")

        all_rows = sheet_count("", "")
        sources: list[dict[str, Any]] = []
        codebook = http_request(FILE_DOWNLOAD, {
            "infId": "OA-21208", "seqNo": "1", "seq": "1", "infSeq": "3"
        })
        require(codebook[:4] == b"PK\x03\x04", "CodebookNotXlsx")
        codebook_path = staging / "link-node-type-codes.xlsx"
        codebook_path.write_bytes(codebook)
        sources.append({
            "kind": "Codebook", "boroughName": None,
            "sourceFileName": "도보네트워크_링크노드유형코드.xlsx",
            "storedFileName": codebook_path.name, "byteLength": len(codebook),
            "sha256": sha_bytes(codebook), "rowCount": 0,
        })

        for key, name, code in BOROUGHS:
            count = sheet_count("SGG_NM", name)
            body = {
                "srvType": "S", "infId": "OA-21208", "serviceKind": "0",
                "pageNo": "1", "gridTotalCnt": str(count), "ssUserId": "SAMPLE_VIEW",
                "strWhere": "", "strOrderby": "SGG_CD ASC",
                "filterCol": "SGG_NM", "txtFilter": name,
            }
            raw = http_request(CSV_DOWNLOAD, body)
            path = staging / f"{key}.csv"
            path.write_bytes(raw)
            header, rows = csv_rows(path)
            require(tuple(header) == CSV_HEADER, f"CsvHeaderChanged:{key}")
            require(rows == count, f"CsvRowCountChangedDuringDownload:{key}")
            sources.append({
                "kind": "BoroughCsv", "boroughName": name, "boroughCode": code,
                "sourceFileName": "서울시 자치구별 도보 네트워크 공간정보.csv",
                "storedFileName": path.name, "byteLength": len(raw),
                "sha256": sha_bytes(raw), "rowCount": rows,
            })

        update_match = re.search(r"데이터 갱신일[\s\S]{0,300}?([0-9]{4}\.[0-9]{2}\.[0-9]{2})", page)
        receipt = {
            "schemaVersion": "oa21208-admin-dong-walk-acquisition.v1",
            "revision": REVISION,
            "acquiredAtUtc": datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z"),
            "officialPageUrl": OFFICIAL_PAGE,
            "sheetViewUrl": SHEET_VIEW,
            "csvDownloadEndpoint": CSV_DOWNLOAD,
            "sourceReferenceVintage": "2020",
            "sourceReferenceDateExact": None,
            "portalDataUpdateDate": update_match.group(1).replace(".", "-") if update_match else None,
            "sourceCoordinateReference": "WGS84",
            "licenseCode": "KOGL-Type1-Attribution",
            "thirdPartyCopyright": "NoneDeclared",
            "seoulSheetTotalRowsAtAcquisition": all_rows,
            "sourceFilterMethodCode": "OfficialSheetUiSggNmCsvDownload",
            "sourceDistrictFilterBoundaryHaloComplete": False,
            "reviewStatus": "PendingHumanReview",
            "distributionApproved": False,
            "runtimeAuthorized": False,
            "traversalReady": False,
            "gameplayReady": False,
            "sources": sorted(sources, key=lambda x: (x["kind"], x["storedFileName"])),
        }
        (staging / "receipt.json").write_bytes(pretty_json(receipt))
        require_exact_flat_files(staging,
                                 {"receipt.json", "link-node-type-codes.xlsx",
                                  *(f"{key}.csv" for key, _, _ in BOROUGHS)},
                                 "AcquisitionStaging")
        validate_codebook(codebook_path)
        for key, name, code in BOROUGHS:
            source = next(x for x in sources if x.get("storedFileName") == f"{key}.csv")
            validate_csv_filter(staging / f"{key}.csv", name, code, int(source["rowCount"]))
        os.replace(staging, folder)
        validate_raw_receipt(root)
        return {"status": "PASS", "reusedFrozenAcquisition": False,
                "sourceRows": sum(int(x.get("rowCount", 0)) for x in sources),
                "receiptSha256": sha_file(folder / "receipt.json")}
    except Exception:
        shutil.rmtree(staging, ignore_errors=True)
        raise


@dataclass(frozen=True)
class Dependencies:
    shapefile: Any
    CRS: Any
    Transformer: Any
    shape: Any
    wkt: Any
    transform: Any
    Polygon: Any
    MultiPolygon: Any
    Point: Any
    LineString: Any
    MultiLineString: Any
    GeometryCollection: Any
    unary_union: Any
    versions: dict[str, str]


def dependencies(root: Path) -> Dependencies:
    runtime = safe_path(root, "artifacts/local/public-data/gis-runtime-r1")
    require(runtime.is_dir(), "SpatialGenerationDependencyMissing")
    if str(runtime) not in sys.path:
        sys.path.insert(0, str(runtime))
    try:
        import pyproj
        import shapefile
        import shapely
        from pyproj import CRS, Transformer
        from shapely.geometry import GeometryCollection, LineString, MultiLineString, MultiPolygon, Point, Polygon, shape
        from shapely import wkt
        from shapely.ops import transform, unary_union
    except (ImportError, ModuleNotFoundError) as exc:
        raise WalkCandidateError("SpatialGenerationDependencyMissing") from exc
    return Dependencies(
        shapefile, CRS, Transformer, shape, wkt, transform, Polygon, MultiPolygon,
        Point, LineString, MultiLineString, GeometryCollection, unary_union,
        {"python": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
         "pyshp": shapefile.__version__, "pyproj": pyproj.__version__,
         "shapely": shapely.__version__, "geos": shapely.geos_version_string,
         "proj": pyproj.proj_version_str,
         "epsgDatabase": pyproj.database.get_database_metadata("EPSG.VERSION")},
    )


@dataclass(frozen=True)
class Boundary:
    code: str
    stable_id: str
    display_name: str
    borough_name: str
    legal_area_stable_id: str
    geometry: Any
    bounds: tuple[float, float, float, float]


def normalized_dong_name(value: str) -> str:
    compact = value.replace("서울특별시", "").replace("광진구", "")
    compact = compact.replace("동대문구", "").replace("중랑구", "")
    return compact.replace("제", "").replace("·", ".").replace(" ", "")


def read_boundaries(path: Path, r2_scope: dict[str, Any], dep: Dependencies) -> list[Boundary]:
    expected = {
        item["administrativeAreaStableId"].removeprefix("region:kr:hjd:"): item
        for item in r2_scope.get("administrativeAreas", [])
    }
    require(len(expected) == 30, "AdministrativeAreaScopeChanged")
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        entries = {}
        for ext in (".cpg", ".dbf", ".prj", ".shp", ".shx"):
            matches = [x for x in names if x.lower().endswith(ext)]
            require(len(matches) == 1, f"BoundaryArchiveEntryInvalid:{ext}")
            entries[ext] = matches[0]
        require(archive.read(entries[".cpg"]).decode("ascii").strip().upper() in {"UTF-8", "UTF8", "65001"},
                "BoundaryEncodingChanged")
        crs = dep.CRS.from_wkt(archive.read(entries[".prj"]).decode("utf-8-sig"))
        require(crs.to_authority() == ("EPSG", "5181"), "BoundaryCrsChanged")
        reader = dep.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])), encoding="utf-8",
        )
        selected: dict[str, Boundary] = {}
        try:
            require(len(reader) == 425, "BoundaryRecordCountChanged")
            for item in reader.iterShapeRecords():
                props = item.record.as_dict()
                code = str(props.get("ADSTRD_CD", "")) + "00"
                if code not in expected:
                    continue
                geometry = dep.shape(item.shape.__geo_interface__)
                geometry = dep.transform(lambda x, y, z=None: (x, y + 100000.0), geometry)
                require(isinstance(geometry, (dep.Polygon, dep.MultiPolygon)) and geometry.is_valid and not geometry.is_empty,
                        f"BoundaryGeometryInvalid:{code}")
                source = expected[code]
                require(normalized_dong_name(str(props.get("ADSTRD_NM", ""))) ==
                        normalized_dong_name(str(source["displayName"])), f"BoundaryNameMismatch:{code}")
                require(code not in selected, f"BoundaryDuplicate:{code}")
                selected[code] = Boundary(
                    code, source["administrativeAreaStableId"], source["displayName"],
                    source["displayName"].split()[1], source["legalAreaStableId"],
                    geometry, tuple(float(x) for x in geometry.bounds),
                )
        finally:
            reader.close()
    require(set(selected) == set(expected), "BoundaryCoverageIncomplete")
    values = [selected[x] for x in sorted(selected)]
    union = dep.unary_union([x.geometry for x in values])
    require(union.is_valid and not union.is_empty, "BoundaryUnionInvalid")
    require(sum(x.geometry.area for x in values) - union.area < 0.1, "BoundaryOverlapInvalid")
    return values


def verify_file(root: Path, source: dict[str, Any]) -> Path:
    path = safe_path(root, str(source.get("repositoryRelativePath", "")))
    require(path.is_file() and not is_reparse(path), f"SourceMissing:{source.get('role')}")
    require(path.stat().st_size == source.get("byteLength"), f"SourceLengthChanged:{source.get('role')}")
    require(sha_file(path) == source.get("contentHashSha256"), f"SourceHashChanged:{source.get('role')}")
    return path


def load_scope(root: Path, dep: Dependencies) -> tuple[dict[str, Any], dict[str, Path], dict[str, str]]:
    scope_path = safe_path(root, SCOPE_RELATIVE)
    scope = load_json(scope_path)
    require(scope.get("schemaVersion") == SCOPE_SCHEMA and scope.get("scopeStableId") == SCOPE_ID,
            "ScopeIdentityChanged")
    require(scope.get("revision") == REVISION and scope.get("authorityFlags") == AUTHORITY_FLAGS,
            "ScopeRevisionOrAuthorityChanged")
    design_path = safe_path(root, DESIGN_RELATIVE)
    work_path = safe_path(root, WORK_ORDER_RELATIVE)
    generator_path = Path(__file__).resolve()
    gate = scope.get("planningGate", {})
    require(gate.get("designDocumentRef") == DESIGN_RELATIVE.as_posix(), "ScopeDesignRefChanged")
    require(gate.get("designRevision") == DESIGN_REVISION, "ScopeDesignRevisionChanged")
    require(gate.get("designHashSha256") == sha_file(design_path), "ScopeDesignHashChanged")
    require(gate.get("dataImplementationRef") == WORK_ORDER_RELATIVE.as_posix(), "ScopeWorkRefChanged")
    require(gate.get("dataImplementationHashSha256") == sha_file(work_path), "ScopeWorkHashChanged")
    work_order = load_json(work_path)
    require(work_order.get("implementationStableId") ==
            "data-implementation:administrative-dong-walk-network:g3c:r1" and
            work_order.get("revision") == REVISION and
            work_order.get("playableLoopWorkOrder") is False and
            work_order.get("evidenceStageClaimed") is None,
            "DataImplementationIdentityChanged")
    hash_contract = work_order.get("candidateHashContract", {})
    require(hash_contract.get("algorithm") == "SHA-256" and
            hash_contract.get("encoding") == "UTF-8" and
            hash_contract.get("framing") == "UnsignedBigEndianUInt32ByteLengthThenUtf8" and
            tuple(hash_contract.get("headerFields", [])) == HASH_HEADER_FIELDS and
            tuple(hash_contract.get("candidateFields", [])) == HASH_CANDIDATE_FIELDS,
            "DataImplementationHashContractChanged")
    require(work_order.get("sourceOccurrenceContract", {}).get("sourceRonumColumnPresent") is False and
            work_order.get("sourceOccurrenceContract", {}).get("nodeAndLinkIdentityIncludesOccurrence") is True,
            "DataImplementationOccurrenceContractChanged")
    require(work_order.get("snapshotContract", {}).get("exactSnapshotCount") == 17 and
            len(work_order.get("preservationContract", [])) == 4,
            "DataImplementationPersistenceContractChanged")
    require(scope.get("output", {}).get("generatorSha256") == sha_file(generator_path), "ScopeGeneratorHashChanged")
    require(scope.get("output", {}).get("toolchain") == dep.versions, "ScopeToolchainChanged")
    require(scope.get("sourceReferenceVintage") == "2020" and
            scope.get("sourceDistrictFilterBoundaryHaloComplete") is False, "ScopeVintageOrHaloChanged")
    expected_roles = {"receipt", "codebook", "gwangjinCsv", "dongdaemunCsv", "jungnangCsv",
                      "oa22160BoundaryArchive", "r2ScopeDefinition", "r2ScopeManifest"}
    paths: dict[str, Path] = {}
    hashes: dict[str, str] = {}
    for source in scope.get("sources", []):
        role = str(source.get("role", ""))
        require(role in expected_roles and role not in paths, "ScopeSourceRoleChanged")
        paths[role] = verify_file(root, source)
        hashes[role] = str(source["contentHashSha256"])
    require(set(paths) == expected_roles, "ScopeSourceSetChanged")
    receipt = validate_raw_receipt(root)
    require(hashes["receipt"] == sha_file(paths["receipt"]), "ReceiptLineageChanged")
    raw_by_file = {x["storedFileName"]: x for x in receipt["sources"]}
    for role, name in (("codebook", "link-node-type-codes.xlsx"), ("gwangjinCsv", "gwangjin.csv"),
                       ("dongdaemunCsv", "dongdaemun.csv"), ("jungnangCsv", "jungnang.csv")):
        require(raw_by_file[name]["sha256"] == hashes[role], f"ReceiptSourceHashMismatch:{role}")
    r2_scope = load_json(paths["r2ScopeDefinition"])
    r2_manifest = load_json(paths["r2ScopeManifest"])
    ids = sorted(x["administrativeAreaStableId"] for x in r2_scope.get("administrativeAreas", []))
    manifest_ids = sorted(x["administrativeAreaStableId"] for x in r2_manifest.get("modules", []))
    require(len(ids) == 30 and ids == manifest_ids, "R2ScopeManifestMismatch")
    return scope, paths, hashes


def intersects_bounds(a: tuple[float, float, float, float], b: tuple[float, float, float, float]) -> bool:
    return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])


def line_parts(geometry: Any, dep: Dependencies) -> list[Any]:
    if isinstance(geometry, dep.LineString):
        return [geometry]
    if isinstance(geometry, (dep.MultiLineString, dep.GeometryCollection)):
        result: list[Any] = []
        for child in geometry.geoms:
            result.extend(line_parts(child, dep))
        return result
    return []


def coords_mm(line: Any) -> list[list[int]]:
    points = [[int(round(x * 1000.0)), int(round(y * 1000.0))] for x, y, *_ in line.coords]
    reverse = list(reversed(points))
    return min(points, reverse)


def row_semantics(row: dict[str, str]) -> dict[str, Any]:
    return {key: row.get(key, "").strip() for key in CSV_HEADER if key != "수집일자"}


def digest_id(prefix: str, value: str) -> str:
    return sha_bytes((prefix + value).encode("utf-8"))


def candidate_base(row: dict[str, str], source_file_hash: str, source_role: str,
                   source_csv_data_row_number: int) -> tuple[dict[str, Any], str, str]:
    semantic = row_semantics(row)
    semantic_hash = sha_bytes(canonical_json(semantic).encode("utf-8"))
    body_hash = sha_bytes(canonical_json({**semantic, "수집일자": row.get("수집일자", "").strip()}).encode("utf-8"))
    kind = row["노드링크 유형"].strip().upper()
    raw_id = row["노드 ID"].strip() if kind == "NODE" else row["링크 ID"].strip()
    require(raw_id not in {"", "0"}, "SourceObjectIdMissing")
    require(source_role in {"gwangjinCsv", "dongdaemunCsv", "jungnangCsv"}, "SourceOccurrenceRoleInvalid")
    require(source_csv_data_row_number > 0, "SourceOccurrenceOrdinalInvalid")
    return ({
        "sourceKindCode": kind,
        "sourceFeatureIdSha256": digest_id(kind + ":", raw_id),
        "sourceSemanticBodySha256": semantic_hash,
        "sourceCompleteBodySha256": body_hash,
        "sourceFileHashSha256": source_file_hash,
        "sourceOccurrenceKey": f"{source_role}:csv-data-row:{source_csv_data_row_number}",
        "sourceCsvDataRowNumber": source_csv_data_row_number,
        "sourceRonum": None,
        "sourceBoroughCode": row["시군구코드"].strip(),
        "sourceBoroughName": row["시군구명"].strip(),
        "sourceLegalDongCode": row["읍면동코드"].strip(),
        "sourceLegalDongName": row["읍면동명"].strip(),
        "sourceWorkTimestamp": row["수집일자"].strip(),
    }, semantic_hash, body_hash)


def candidate_geometry(candidate: dict[str, Any]) -> Any:
    if candidate["candidateKindCode"] == "Node":
        return candidate["pointEpsg5186Millimeters"]
    return candidate["fragmentGeometryEpsg5186Millimeters"]


def candidate_source_type(candidate: dict[str, Any]) -> str:
    return (candidate.get("sourceNodeTypeCode") if candidate["candidateKindCode"] == "Node"
            else candidate.get("sourceLinkTypeCode")) or ""


def candidate_hash_values(candidate: dict[str, Any]) -> list[str]:
    values = {
        "candidateStableId": candidate["candidateStableId"],
        "candidateKindCode": candidate["candidateKindCode"],
        "administrativeAreaStableId": candidate["administrativeAreaStableId"],
        "assignmentStateCode": candidate["assignmentStateCode"],
        "sourceFeatureIdSha256": candidate["sourceFeatureIdSha256"],
        "sourceSemanticBodySha256": candidate["sourceSemanticBodySha256"],
        "sourceCompleteBodySha256": candidate["sourceCompleteBodySha256"],
        "sourceFileHashSha256": candidate["sourceFileHashSha256"],
        "sourceOccurrenceKey": candidate["sourceOccurrenceKey"],
        "sourceBoroughCode": candidate["sourceBoroughCode"],
        "sourceBoroughName": candidate["sourceBoroughName"],
        "sourceLegalDongCode": candidate["sourceLegalDongCode"],
        "sourceLegalDongName": candidate["sourceLegalDongName"],
        "sourceTypeCode": candidate_source_type(candidate),
        "geometryCanonicalJson": canonical_json(candidate_geometry(candidate)),
        "sourceReportedActorCodesCanonicalJson": canonical_json(candidate.get("sourceReportedActorCodes", [])),
        "sourceBeginNodeIdSha256": candidate.get("sourceBeginNodeIdSha256", ""),
        "sourceEndNodeIdSha256": candidate.get("sourceEndNodeIdSha256", ""),
        "sourceReportedLengthMetersCanonicalJson": canonical_json(candidate.get("sourceReportedLengthMeters")),
        "fragmentOrdinalCanonicalJson": canonical_json(candidate.get("fragmentOrdinal")),
        "physicalFragmentHashSha256": candidate.get("physicalFragmentHashSha256", ""),
        "sharedAdministrativeAreaIdsCanonicalJson": canonical_json(
            candidate.get("sharedAdministrativeAreaIds", [])),
        "qualityDiagnosticCodesCanonicalJson": canonical_json(candidate["qualityDiagnosticCodes"]),
        "authorityFlagsCanonicalJson": canonical_json(candidate["authorityFlags"]),
    }
    require(set(values) == set(HASH_CANDIDATE_FIELDS), "CandidateHashFieldContractInternalMismatch")
    return [str(values[field]) for field in HASH_CANDIDATE_FIELDS]


def build_candidates(root: Path, scope: dict[str, Any], paths: dict[str, Path], hashes: dict[str, str],
                     dep: Dependencies) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    r2_scope = load_json(paths["r2ScopeDefinition"])
    boundaries = read_boundaries(paths["oa22160BoundaryArchive"], r2_scope, dep)
    target_union = dep.unary_union([boundary.geometry for boundary in boundaries])
    transformer = dep.Transformer.from_crs("EPSG:4326", "EPSG:5186", always_xy=True)
    project = lambda geom: dep.transform(transformer.transform, geom)
    candidates: list[dict[str, Any]] = []
    counts: Counter[str] = Counter()
    for key in (
        "missingGeometryRows", "invalidGeometryRows", "invalidCoordinateRows", "multipleBoundaryRows",
        "sourceDistrictConflictCandidates", "boundaryCoincidentSharedPhysicalFragmentGroups",
        "boundaryCoincidentSharedCandidateAssignments", "duplicateNodeIdDifferentBodyGroups",
        "duplicateLinkIdDifferentBodyGroups", "exactDuplicateSourceRowOccurrenceGroups",
        "exactDuplicateSourceRowExtraOccurrences",
    ):
        counts[key] = 0
    per_area: dict[str, Counter[str]] = defaultdict(Counter)
    feature_id_bodies: dict[str, dict[str, set[str]]] = {
        "NODE": defaultdict(set), "LINK": defaultdict(set),
    }
    complete_body_occurrences: Counter[str] = Counter()
    target_length_meters = 0.0
    union_fragment_length_meters = 0.0
    assigned_fragment_length_meters = 0.0
    maximum_source_link_preservation_error_meters = 0.0
    source_files = (("gwangjinCsv", paths["gwangjinCsv"]),
                    ("dongdaemunCsv", paths["dongdaemunCsv"]),
                    ("jungnangCsv", paths["jungnangCsv"]))
    expected_borough = {
        "gwangjinCsv": ("광진구", "1121500000"),
        "dongdaemunCsv": ("동대문구", "1123000000"),
        "jungnangCsv": ("중랑구", "1126000000"),
    }
    for role, path in source_files:
        expected_name, expected_code = expected_borough[role]
        with path.open("r", encoding="cp949", newline="") as stream:
            reader = csv.DictReader(stream)
            require(tuple(reader.fieldnames or ()) == CSV_HEADER, f"CsvHeaderChanged:{role}")
            for row_number, row in enumerate(reader, 2):
                counts["sourceRows"] += 1
                require(row["시군구명"].strip() == expected_name, f"SourceBoroughNameMismatch:{role}:{row_number}")
                require(row["시군구코드"].strip() == expected_code, f"SourceBoroughCodeMismatch:{role}:{row_number}")
                kind = row["노드링크 유형"].strip().upper()
                require(kind in {"NODE", "LINK"}, "SourceKindUnknown")
                counts["source" + kind.title() + "Rows"] += 1
                base, semantic_hash, body_hash = candidate_base(
                    row, hashes[role], role, row_number - 1)
                raw_id = row["노드 ID"].strip() if kind == "NODE" else row["링크 ID"].strip()
                feature_id_bodies[kind][raw_id].add(semantic_hash)
                complete_body_occurrences[body_hash] += 1
                if kind == "NODE":
                    require(row["노드 유형 코드"].strip() in SOURCE_NODE_MEANING_PREFIXES_KO,
                            f"SourceNodeTypeCodeUnknown:{role}:{row_number}")
                else:
                    require(row["링크 유형 코드"].strip().zfill(4) in SOURCE_ACTORS,
                            f"SourceLinkTypeCodeUnknown:{role}:{row_number}")
                wkt_text = row["노드 WKT"].strip() if kind == "NODE" else row["링크 WKT"].strip()
                if not wkt_text:
                    counts["missingGeometryRows"] += 1
                    continue
                try:
                    geometry_wgs84 = dep.wkt.loads(wkt_text)
                except Exception:
                    counts["invalidGeometryRows"] += 1
                    continue
                expected_type = dep.Point if kind == "NODE" else dep.LineString
                if not isinstance(geometry_wgs84, expected_type) or geometry_wgs84.is_empty or not geometry_wgs84.is_valid:
                    counts["invalidGeometryRows"] += 1
                    continue
                minx, miny, maxx, maxy = geometry_wgs84.bounds
                if not (120 <= minx <= maxx <= 140 and 30 <= miny <= maxy <= 45):
                    counts["invalidCoordinateRows"] += 1
                    continue
                geometry = project(geometry_wgs84)
                quality = ["Historical2020Source", "HistoricalBoundary2023VintageMismatch",
                           "SourceDistrictFilterBoundaryHaloUnresolved", "SourceReportedActorCodeOnly"]

                if kind == "NODE":
                    covers = [b for b in boundaries if intersects_bounds(tuple(geometry.bounds), b.bounds)
                              and b.geometry.covers(geometry)]
                    if len(covers) == 0:
                        counts["outsideScopeRows"] += 1
                        continue
                    if len(covers) != 1:
                        counts["multipleBoundaryRows"] += 1
                        continue
                    boundary = covers[0]
                    if boundary.borough_name != base["sourceBoroughName"]:
                        quality.append("SourceDistrictSpatialAssignmentConflict")
                        counts["sourceDistrictConflictCandidates"] += 1
                    point = [int(round(geometry.x * 1000.0)), int(round(geometry.y * 1000.0))]
                    serialized_point = dep.Point(point[0] / 1000.0, point[1] / 1000.0)
                    require(boundary.geometry.buffer(0.002).covers(serialized_point),
                            "SerializedNodeLeavesAssignedBoundary")
                    identity = framed_hash((REVISION, "NODE", base["sourceOccurrenceKey"], semantic_hash,
                                            boundary.stable_id,
                                            canonical_json(point)))
                    candidate = {
                        "schemaVersion": CANDIDATE_SCHEMA, "revision": REVISION,
                        "candidateStableId": "walk-node-candidate:sha256:" + identity.lower(),
                        "candidateKindCode": "Node", "administrativeAreaStableId": boundary.stable_id,
                        "administrativeAreaDisplayName": boundary.display_name,
                        "legalAreaStableId": boundary.legal_area_stable_id,
                        "assignmentStateCode": "UniqueHistoricalBoundaryCover",
                        **base,
                        "sourceNodeTypeCode": row["노드 유형 코드"].strip(),
                        "pointEpsg5186Millimeters": point,
                        "qualityDiagnosticCodes": sorted(quality), "authorityFlags": AUTHORITY_FLAGS,
                    }
                    candidates.append(candidate)
                    per_area[boundary.stable_id]["nodes"] += 1
                    counts["candidateNodes"] += 1
                    continue

                link_type = row["링크 유형 코드"].strip().zfill(4)
                fragments: list[tuple[Boundary, list[list[int]], Any]] = []
                for boundary in boundaries:
                    if not intersects_bounds(tuple(geometry.bounds), boundary.bounds):
                        continue
                    clipped = geometry.intersection(boundary.geometry)
                    for part in line_parts(clipped, dep):
                        if part.length >= 0.001:
                            points = coords_mm(part)
                            require(len(points) >= 2 and len({tuple(x) for x in points}) >= 2,
                                    "SerializedFragmentDegenerate")
                            fragments.append((boundary, points, part))
                if not fragments:
                    counts["outsideScopeRows"] += 1
                    continue

                same_area_fragments = [(x[0].stable_id, canonical_json(x[1])) for x in fragments]
                require(len(same_area_fragments) == len(set(same_area_fragments)),
                        "DuplicateFragmentWithinAdministrativeArea")
                raw_parts = [x[2] for x in fragments]
                target_part = geometry.intersection(target_union)
                target_length = float(target_part.length)
                union_length = float(dep.unary_union(raw_parts).length)
                assigned_length = sum(float(part.length) for part in raw_parts)
                preservation_error = abs(target_length - union_length)
                maximum_source_link_preservation_error_meters = max(
                    maximum_source_link_preservation_error_meters, preservation_error)
                require(preservation_error <= max(0.002, target_length * 1e-9),
                        "SourceLinkTargetLengthNotPreserved")
                target_length_meters += target_length
                union_fragment_length_meters += union_length
                assigned_fragment_length_meters += assigned_length

                fragment_groups: dict[str, list[tuple[Boundary, list[list[int]], Any]]] = defaultdict(list)
                for fragment in fragments:
                    fragment_hash = sha_bytes(canonical_json(fragment[1]).encode("utf-8"))
                    fragment_groups[fragment_hash].append(fragment)
                counts["physicalFragmentGroups"] += len(fragment_groups)
                shared_by_hash: dict[str, list[str]] = {}
                for fragment_hash, group in fragment_groups.items():
                    area_ids = sorted({x[0].stable_id for x in group})
                    require(len(area_ids) == len(group), "PhysicalFragmentRepeatedWithinAdministrativeArea")
                    shared_by_hash[fragment_hash] = area_ids
                    if len(area_ids) > 1:
                        counts["boundaryCoincidentSharedPhysicalFragmentGroups"] += 1
                        counts["boundaryCoincidentSharedCandidateAssignments"] += len(area_ids)
                fragments.sort(key=lambda x: (x[0].stable_id, canonical_json(x[1])))
                ordinal_by_area: Counter[str] = Counter()
                for boundary, points, raw_part in fragments:
                    fragment_hash = sha_bytes(canonical_json(points).encode("utf-8"))
                    ordinal_by_area[boundary.stable_id] += 1
                    ordinal = ordinal_by_area[boundary.stable_id]
                    shared_area_ids = shared_by_hash[fragment_hash]
                    serialized_line = dep.LineString([(x / 1000.0, y / 1000.0) for x, y in points])
                    outside_serialized_length = float(
                        serialized_line.difference(boundary.geometry.buffer(0.002)).length)
                    require(outside_serialized_length <= 0.001,
                            "SerializedMillimeterFragmentLeavesAssignedBoundary")
                    identity = framed_hash((REVISION, "LINK", base["sourceOccurrenceKey"], semantic_hash,
                                            boundary.stable_id, fragment_hash, str(ordinal)))
                    item_quality = list(quality)
                    if len(shared_area_ids) > 1:
                        item_quality.append("BoundaryCoincidentFragmentSharedByAdministrativeAreas")
                    conflict = boundary.borough_name != base["sourceBoroughName"]
                    if conflict:
                        item_quality.append("SourceDistrictSpatialAssignmentConflict")
                        counts["sourceDistrictConflictCandidates"] += 1
                    candidate = {
                        "schemaVersion": CANDIDATE_SCHEMA, "revision": REVISION,
                        "candidateStableId": "walk-link-fragment-candidate:sha256:" + identity.lower(),
                        "candidateKindCode": "LinkFragment",
                        "administrativeAreaStableId": boundary.stable_id,
                        "administrativeAreaDisplayName": boundary.display_name,
                        "legalAreaStableId": boundary.legal_area_stable_id,
                        "assignmentStateCode": "HistoricalBoundaryClippedFragment",
                        **base,
                        "sourceLinkTypeCode": link_type,
                        "sourceReportedActorCodes": list(SOURCE_ACTORS[link_type]),
                        "sourceBeginNodeIdSha256": digest_id("NODE:", row["시작노드 ID"].strip()),
                        "sourceEndNodeIdSha256": digest_id("NODE:", row["종료노드 ID"].strip()),
                        "sourceReportedLengthMeters": float(row["링크 길이"]) if row["링크 길이"].strip() else None,
                        "fragmentOrdinal": ordinal,
                        "fragmentLengthMeters": round(float(serialized_line.length), 3),
                        "fragmentGeometryEpsg5186Millimeters": points,
                        "physicalFragmentHashSha256": fragment_hash,
                        "boundaryCoincidentShared": len(shared_area_ids) > 1,
                        "sharedAdministrativeAreaIds": shared_area_ids if len(shared_area_ids) > 1 else [],
                        "sourceFlags": {
                            "elevatedRoad": row["고가도로"].strip(),
                            "subwayNetwork": row["지하철네트워크"].strip(),
                            "bridge": row["교량"].strip(), "tunnel": row["터널"].strip(),
                            "overpass": row["육교"].strip(), "crosswalk": row["횡단보도"].strip(),
                            "park": row["공원,녹지"].strip(), "buildingInterior": row["건물내"].strip(),
                        },
                        "qualityDiagnosticCodes": sorted(item_quality), "authorityFlags": AUTHORITY_FLAGS,
                    }
                    candidates.append(candidate)
                    per_area[boundary.stable_id]["links"] += 1
                    counts["candidateLinkFragments"] += 1
                counts["sourceLinkRowsWithCandidate"] += 1
                if len({x[0].stable_id for x in fragments}) > 1:
                    counts["sourceLinksSpanningAdministrativeAreas"] += 1

    counts["duplicateNodeIdDifferentBodyGroups"] = sum(
        1 for x in feature_id_bodies["NODE"].values() if len(x) > 1)
    counts["duplicateLinkIdDifferentBodyGroups"] = sum(
        1 for x in feature_id_bodies["LINK"].values() if len(x) > 1)
    counts["exactDuplicateSourceRowOccurrenceGroups"] = sum(
        1 for count in complete_body_occurrences.values() if count > 1)
    counts["exactDuplicateSourceRowExtraOccurrences"] = sum(
        count - 1 for count in complete_body_occurrences.values() if count > 1)
    candidates.sort(key=lambda x: (x["administrativeAreaStableId"], x["candidateKindCode"], x["candidateStableId"]))
    ids = [x["candidateStableId"] for x in candidates]
    require(len(ids) == len(set(ids)), "CandidateIdentityCollision")
    counts["candidateRows"] = len(candidates)
    counts["administrativeAreasWithCandidates"] = sum(1 for b in boundaries if per_area[b.stable_id])
    require(counts["sourceRows"] == sum(x[1].stat().st_size >= 0 and csv_rows(x[1])[1] for x in source_files),
            "SourceCountInternalMismatch")
    distribution = {
        b.code: {"nodeCandidates": per_area[b.stable_id]["nodes"],
                 "linkFragmentCandidates": per_area[b.stable_id]["links"],
                 "candidateRows": per_area[b.stable_id]["nodes"] + per_area[b.stable_id]["links"]}
        for b in boundaries
    }
    return candidates, {
        "counts": dict(sorted(counts.items())),
        "perAdministrativeArea": distribution,
        "boundaryCount": len(boundaries),
        "lengthPreservation": {
            "unit": "meter",
            "comparisonToleranceMetersPerSourceLink": 0.002,
            "targetIntersectionLengthMeters": round(target_length_meters, 6),
            "uniqueUnionFragmentLengthMeters": round(union_fragment_length_meters, 6),
            "assignedFragmentLengthIncludingBoundarySharedDuplicatesMeters": round(
                assigned_fragment_length_meters, 6),
            "maximumSourceLinkPreservationErrorMeters": round(
                maximum_source_link_preservation_error_meters, 9),
            "serializedBoundaryBufferToleranceMeters": 0.002,
            "serializedOutsideLengthToleranceMeters": 0.001,
        },
        "occurrenceIdentity": {
            "sourceRonumColumnPresent": False,
            "methodCode": "FrozenBoroughCsvRolePlusOneBasedCsvDataRowNumber",
            "completeDuplicateRowsRemainDistinctOccurrences": True,
        },
    }


@dataclass(frozen=True)
class Prepared:
    candidate_set_hash: str
    files: dict[str, bytes]
    generation_relative: Path
    counts: dict[str, int]


def prepare(root: Path, dep: Dependencies) -> Prepared:
    scope, paths, hashes = load_scope(root, dep)
    candidates, audit_values = build_candidates(root, scope, paths, hashes, dep)
    work_order = load_json(safe_path(root, WORK_ORDER_RELATIVE))
    expected_counts = work_order.get("expectedCounts", {})
    require(isinstance(expected_counts, dict), "ExpectedCountsContractMissing")
    for key, value in expected_counts.items():
        if key == "registeredRawSnapshots":
            continue
        require(audit_values["counts"].get(key) == value, f"ExpectedCountChanged:{key}")
    header = {
        "candidateSchemaVersion": CANDIDATE_SCHEMA, "revision": REVISION,
        "scopeDefinitionHashSha256": sha_file(safe_path(root, SCOPE_RELATIVE)),
        "designHashSha256": sha_file(safe_path(root, DESIGN_RELATIVE)),
        "dataImplementationHashSha256": sha_file(safe_path(root, WORK_ORDER_RELATIVE)),
        "generatorHashSha256": sha_file(Path(__file__).resolve()),
        "sourceHashesCanonicalJson": canonical_json(dict(sorted(hashes.items()))),
        "authorityFlagsCanonicalJson": canonical_json(AUTHORITY_FLAGS),
        "candidateCountCanonicalJson": canonical_json(len(candidates)),
    }
    require(tuple(header) == HASH_HEADER_FIELDS, "CandidateHashHeaderContractInternalMismatch")
    header_values = [str(header[field]) for field in HASH_HEADER_FIELDS]
    candidate_values = [value for candidate in candidates for value in candidate_hash_values(candidate)]
    candidate_hash = framed_hash(header_values + candidate_values)
    candidate_bytes = b"".join((canonical_json(x) + "\n").encode("utf-8") for x in candidates)
    audit = content_hashed({
        "schemaVersion": AUDIT_SCHEMA, "revision": REVISION,
        "candidateSetHashSha256": candidate_hash, **audit_values,
        "sourceVintageMismatchCode": "Oa21208Year2020WithOa22160Boundary20231031",
        "sourceDistrictFilterBoundaryHaloComplete": False,
        "codebookSemanticValidationCompleted": True,
        "allCsvRowsBoroughFilterValidationCompleted": True,
        "authorityFlags": AUTHORITY_FLAGS,
    })
    manifest = content_hashed({
        "schemaVersion": MANIFEST_SCHEMA, "revision": REVISION,
        "scopeStableId": SCOPE_ID, "candidateSetHashSha256": candidate_hash,
        "candidateRows": len(candidates), "administrativeAreaCount": 30,
        "sourceReferenceVintage": "2020", "sourceCoordinateReference": "WGS84",
        "boundaryReferenceVintage": "2023-10-31", "boundaryCoordinateReference": "EPSG:5181",
        "assignmentCoordinateReference": "EPSG:5186",
        "sourceHashes": dict(sorted(hashes.items())),
        "scopeDefinitionHashSha256": header["scopeDefinitionHashSha256"],
        "designHashSha256": header["designHashSha256"],
        "dataImplementationHashSha256": header["dataImplementationHashSha256"],
        "generatorHashSha256": header["generatorHashSha256"],
        "candidateHashContract": {
            "algorithm": "SHA-256",
            "encoding": "UTF-8",
            "framing": "UnsignedBigEndianUInt32ByteLengthThenUtf8",
            "candidateOrdering": ["administrativeAreaStableId", "candidateKindCode", "candidateStableId"],
            "headerFields": list(HASH_HEADER_FIELDS),
            "candidateFields": list(HASH_CANDIDATE_FIELDS),
        },
        "candidateHashHeaderValues": header_values,
        "qualityCode": "PendingHumanReview", "authorityFlags": AUTHORITY_FLAGS,
        "perAdministrativeArea": audit_values["perAdministrativeArea"],
    })
    files = {
        "manifest.json": pretty_json(manifest), "candidates.ndjson": candidate_bytes,
        "audit.json": pretty_json(audit), "generator-source.py": Path(__file__).read_bytes(),
    }
    complete = content_hashed({
        "schemaVersion": COMPLETE_SCHEMA, "revision": REVISION, "completeMarker": True,
        "candidateSetHashSha256": candidate_hash,
        "files": [{"relativePath": name, "sha256": sha_bytes(data), "byteLength": len(data),
                   "recordCount": len(candidates) if name == "candidates.ndjson" else 1}
                  for name, data in sorted(files.items())],
        "currentPointerCreated": False, "databasePersistenceCompleted": False,
        "runtimeAuthorized": False, "traversalReady": False, "gameplayReady": False,
    })
    files["complete.json"] = pretty_json(complete)
    generation = OUTPUT_RELATIVE / "generations" / candidate_hash.lower()
    return Prepared(candidate_hash, files, generation, audit_values["counts"])


def materialize(root: Path, prepared: Prepared) -> dict[str, Any]:
    target = safe_path(root, prepared.generation_relative)
    if target.exists():
        require_exact_flat_files(target, set(prepared.files), "Generation")
        actual = {x.name: x.read_bytes() for x in target.iterdir()}
        require(actual == prepared.files, "ExistingGenerationChanged")
        return {"status": "PASS", "changedFiles": 0}
    if target.parent.exists():
        require(target.parent.is_dir() and not is_reparse(target.parent), "GenerationParentUnsafe")
    else:
        require(target.parent.parent.is_dir() and not is_reparse(target.parent.parent),
                "GenerationOutputRootUnsafe")
        target.parent.mkdir()
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


def verify_generation(root: Path, prepared: Prepared) -> None:
    target = safe_path(root, prepared.generation_relative)
    require_exact_flat_files(target, set(prepared.files), "Generation")
    for name, expected in prepared.files.items():
        require((target / name).read_bytes() == expected, f"GenerationFileChanged:{name}")


def self_tests(root: Path, dep: Dependencies) -> int:
    tests = 0
    def test(value: bool, code: str) -> None:
        nonlocal tests
        require(value, "SelfTest:" + code)
        tests += 1
    test(SOURCE_ACTORS["1111"] == ("Pedestrian", "Vehicle", "Bicycle", "PM"), "ActorCode")
    test("Motorcycle" not in {x for values in SOURCE_ACTORS.values() for x in values}, "NoMotorcycleInference")
    test(all(value is False for key, value in AUTHORITY_FLAGS.items() if key not in
             {"privateReviewOnly", "historicalBoundaryBootstrapOnly", "observationCandidateOnly", "sourceDeclaredCoordinateReference"}),
         "FalseAuthorityFlags")
    test(all(AUTHORITY_FLAGS[x] for x in
             ("privateReviewOnly", "historicalBoundaryBootstrapOnly", "observationCandidateOnly", "sourceDeclaredCoordinateReference")),
         "TrueReviewFlags")
    row = {key: "" for key in CSV_HEADER}
    row.update({"노드링크 유형": "LINK", "링크 ID": "synthetic-link", "수집일자": "A"})
    base_a, semantic_a, body_a = candidate_base(row, "0" * 64, "gwangjinCsv", 1)
    row["수집일자"] = "B"
    base_b, semantic_b, body_b = candidate_base(row, "0" * 64, "gwangjinCsv", 2)
    test(semantic_a == semantic_b and body_a != body_b, "TimestampExcludedFromSemanticIdentity")
    test(base_a["sourceOccurrenceKey"] != base_b["sourceOccurrenceKey"], "DuplicateOccurrenceIdentity")
    test(framed_hash(("ab", "c")) != framed_hash(("a", "bc")), "FramedHashBoundary")
    line = dep.LineString([(0, 0), (2, 0)])
    polygon = dep.Polygon([(0, -1), (1, -1), (1, 1), (0, 1)])
    parts = line_parts(line.intersection(polygon), dep)
    test(len(parts) == 1 and abs(parts[0].length - 1) < 1e-9, "LineBoundaryClip")
    left = dep.Polygon([(0, -1), (1, -1), (1, 1), (0, 1)])
    right = dep.Polygon([(1, -1), (2, -1), (2, 1), (1, 1)])
    coincident = dep.LineString([(1, -0.5), (1, 0.5)])
    coincident_hashes = [sha_bytes(canonical_json(coords_mm(coincident.intersection(area))).encode("utf-8"))
                         for area in (left, right)]
    test(len(set(coincident_hashes)) == 1 and
         abs(dep.unary_union([coincident.intersection(left), coincident.intersection(right)]).length
             - coincident.length) < 1e-9,
         "BoundaryCoincidentDoubleOwnershipWithoutPhysicalLengthDuplication")
    test(coords_mm(dep.LineString([(1.2345, 2.3455), (3, 4)]))[0] == [1234, 2346], "MillimeterRounding")
    test(coords_mm(dep.LineString([(3, 4), (1.2345, 2.3455)]))[0] == [1234, 2346],
         "DirectionIndependentFragmentGeometry")
    receipt = validate_raw_receipt(root)
    test(sum(int(x.get("rowCount", 0)) for x in receipt["sources"]) > 50_000, "FrozenBoroughCoverage")
    scope, paths, _ = load_scope(root, dep)
    test(scope["sourceDistrictFilterBoundaryHaloComplete"] is False, "HaloGapExplicit")
    boundaries = read_boundaries(paths["oa22160BoundaryArchive"], load_json(paths["r2ScopeDefinition"]), dep)
    test(len(boundaries) == 30, "ThirtyHistoricalBoundaries")
    test(len({x.stable_id for x in boundaries}) == 30, "BoundaryIdsUnique")
    test(tuple(HASH_HEADER_FIELDS)[-1] == "candidateCountCanonicalJson", "HashHeaderCountBound")
    test("sourceOccurrenceKey" in HASH_CANDIDATE_FIELDS, "HashCandidateOccurrenceBound")
    return tests


def arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("acquire", "self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT_DEFAULT))
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = arguments(sys.argv[1:] if argv is None else argv)
    root = Path(args.root).resolve()
    try:
        if args.mode == "acquire":
            result = acquire(root)
        else:
            dep = dependencies(root)
            if args.mode == "self-test":
                result = {"status": "PASS", "selfTestsPassed": self_tests(root, dep)}
            else:
                prepared = prepare(root, dep)
                if args.mode == "build":
                    result = materialize(root, prepared)
                else:
                    verify_generation(root, prepared)
                    result = {"status": "PASS", "verified": True}
                result.update({"candidateSetHashSha256": prepared.candidate_set_hash,
                               "candidateRows": prepared.counts["candidateRows"],
                               "sourceRows": prepared.counts["sourceRows"],
                               "administrativeAreasWithCandidates": prepared.counts["administrativeAreasWithCandidates"],
                               "generationRelativePath": prepared.generation_relative.as_posix()})
        print(json.dumps(result, ensure_ascii=False, sort_keys=True))
        return 0
    except (WalkCandidateError, OSError, ValueError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "error": str(exc)}, ensure_ascii=False, sort_keys=True))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

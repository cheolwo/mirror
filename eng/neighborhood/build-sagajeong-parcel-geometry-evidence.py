#!/usr/bin/env python3
"""공식 연속지적 AL_D002에서 사가정 화면 건물의 필지 도형만 추출·감사한다."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path
from typing import Any


STATION_STABLE_ID = "station:kr:kric:s1107:0722"
REGION_STABLE_ID = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer"
ADDRESS_LEDGER_RELATIVE = Path(
    "artifacts/local/public-data/sagajeong-presentation-building-evidence-20260914-r1/"
    "presentation-building-addresses.json"
)
ADDRESS_LEDGER_FILE_SHA256 = "E751344083E98C28E3F4D6B153FA71E7679A7C47534D71B9721DEBE467596984"
ADDRESS_LEDGER_CONTENT_SHA256 = "9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33"
EXPECTED_PRESENTATION_BUILDINGS = 4_062
EXPECTED_UNIQUE_PNU = 3_774
SOURCE_DATASET_ID = "vworld-na-dataset-23-al-d002"
SOURCE_CRS = "EPSG:5186"
SOURCE_PNU_FIELD = "A1"
OUTPUT_GEOMETRY = "sagajeong-parcel-geometries.epsg5186.geojson"
OUTPUT_MANIFEST = "sagajeong-parcel-geometry-manifest.json"


def require(condition: bool, code: str) -> None:
    if not condition:
        raise ValueError(f"SagajeongParcelGeometry:{code}")


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def install_local_geospatial_path(repository_root: Path) -> None:
    candidate = repository_root / "artifacts/local/python-packages/geospatial"
    if candidate.is_dir() and str(candidate) not in sys.path:
        sys.path.insert(0, str(candidate))


def load_targets(repository_root: Path) -> tuple[list[str], int]:
    path = repository_root / ADDRESS_LEDGER_RELATIVE
    require(path.is_file(), "AddressLedgerMissing")
    require(sha256_file(path) == ADDRESS_LEDGER_FILE_SHA256, "AddressLedgerFileHashChanged")
    ledger = json.loads(path.read_text(encoding="utf-8"))
    require(ledger.get("schemaVersion") == "presentation-building-address-assignment-ledger.v1", "AddressLedgerSchema")
    require(ledger.get("contentSha256") == ADDRESS_LEDGER_CONTENT_SHA256, "AddressLedgerContentHash")
    assignments = ledger.get("assignments")
    require(isinstance(assignments, list) and len(assignments) == EXPECTED_PRESENTATION_BUILDINGS, "AddressLedgerPopulation")
    pnus = [item.get("parcelIdentifierPnu", "") for item in assignments]
    require(all(re.fullmatch(r"\d{19}", value) for value in pnus), "ParcelIdentifierShape")
    unique = sorted(set(pnus))
    require(len(unique) == EXPECTED_UNIQUE_PNU, "ParcelIdentifierDistinctCount")
    return unique, len(assignments)


def find_source_layer(archive: zipfile.ZipFile) -> tuple[str, list[str]]:
    names = archive.namelist()
    shape_files = [
        name for name in names
        if name.lower().endswith(".shp") and re.search(r"(^|/)AL_D002_11(?:_|\.)", name, re.IGNORECASE)
    ]
    require(len(shape_files) == 1, "SourceLayerCount")
    stem = shape_files[0][:-4]
    members = [name for name in names if name.lower() in {f"{stem}.{ext}".lower() for ext in ("shp", "shx", "dbf", "prj", "cpg")}]
    extensions = {Path(name).suffix.lower() for name in members}
    require({".shp", ".shx", ".dbf", ".prj"}.issubset(extensions), "SourceLayerComponents")
    return shape_files[0], members


def extract_layer(archive_path: Path, temporary_root: Path) -> Path:
    with zipfile.ZipFile(archive_path, "r") as archive:
        bad = archive.testzip()
        require(bad is None, f"SourceArchiveCrc:{bad or ''}")
        shape_name, members = find_source_layer(archive)
        for member in members:
            safe_name = Path(member).name
            require(safe_name not in ("", ".", ".."), "SourceEntryName")
            with archive.open(member, "r") as source, (temporary_root / safe_name).open("wb") as destination:
                shutil.copyfileobj(source, destination)
        return temporary_root / Path(shape_name).name


def geometry_mapping(geometry: Any) -> dict[str, Any]:
    from shapely.geometry import mapping

    return mapping(geometry)


def build_features(frame: Any, target_pnus: list[str]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    from shapely import normalize, to_wkb
    from shapely.ops import unary_union

    require(SOURCE_PNU_FIELD in frame.columns, "SourcePnuField")
    require(frame.crs is not None and frame.crs.to_epsg() == 5186, "SourceCrs")
    frame = frame[[SOURCE_PNU_FIELD, "geometry"]].copy()
    frame[SOURCE_PNU_FIELD] = frame[SOURCE_PNU_FIELD].astype(str).str.strip()
    frame = frame[frame[SOURCE_PNU_FIELD].isin(set(target_pnus))]

    features: list[dict[str, Any]] = []
    invalid: list[str] = []
    source_feature_total = 0
    multipart_count = 0
    for pnu, group in frame.groupby(SOURCE_PNU_FIELD, sort=True):
        source_feature_total += len(group)
        geometries = [value for value in group.geometry if value is not None and not value.is_empty]
        if not geometries:
            invalid.append(pnu)
            continue
        merged = normalize(unary_union(geometries))
        if merged.geom_type not in ("Polygon", "MultiPolygon") or merged.is_empty or not merged.is_valid:
            invalid.append(pnu)
            continue
        if merged.geom_type == "MultiPolygon":
            multipart_count += 1
        min_x, min_y, max_x, max_y = [round(float(value), 6) for value in merged.bounds]
        geometry_hash = sha256_bytes(to_wkb(merged, hex=False, byte_order=1, include_srid=False))
        features.append({
            "type": "Feature",
            "id": f"parcel:kr:pnu:{pnu}",
            "properties": {
                "pnu": pnu,
                "sourceFeatureCount": len(group),
                "geometrySha256": geometry_hash,
                "areaSquareMeters": round(float(merged.area), 6),
                "bboxEpsg5186": [min_x, min_y, max_x, max_y],
                "usageState": "PrivateReviewOnly",
                "applicationAuthorized": False,
            },
            "geometry": geometry_mapping(merged),
        })

    found = {feature["properties"]["pnu"] for feature in features}
    missing = sorted(set(target_pnus) - found - set(invalid))
    features.sort(key=lambda feature: feature["properties"]["pnu"])
    summary = {
        "targetParcelCount": len(target_pnus),
        "sourceFeatureCount": source_feature_total,
        "geometryParcelCount": len(features),
        "missingParcelCount": len(missing),
        "invalidParcelCount": len(invalid),
        "multipartParcelCount": multipart_count,
        "duplicateSourceFeatureParcelCount": sum(1 for feature in features if feature["properties"]["sourceFeatureCount"] > 1),
        "coverageState": "Complete" if len(features) == len(target_pnus) else "Missing",
        "missingParcelIdentifiers": missing,
        "invalidParcelIdentifiers": sorted(invalid),
    }
    return features, summary


def read_official_frame(shape_path: Path, target_pnus: list[str], repository_root: Path) -> Any:
    install_local_geospatial_path(repository_root)
    import pyogrio

    prefix = target_pnus[0][:10]
    require(all(value.startswith(prefix) for value in target_pnus), "TargetLegalDongScope")
    frame = pyogrio.read_dataframe(
        shape_path,
        columns=[SOURCE_PNU_FIELD],
        where=f"{SOURCE_PNU_FIELD} LIKE '{prefix}%'",
    )
    require(len(frame) > 0, "SourceLegalDongRowsMissing")
    return frame


def output_boundary() -> dict[str, bool]:
    return {
        "observationPresentationOnly": True,
        "distributionApproved": False,
        "deliveryEligible": False,
        "priceObservationEligible": False,
        "businessLocationEligible": False,
        "unityApplyAllowed": False,
        "traversalReady": False,
        "gameplayReady": False,
    }


def build(
    repository_root: Path,
    archive_path: Path,
    expected_source_hash: str,
    source_revision: str,
    output_dir: Path,
) -> dict[str, Any]:
    require(archive_path.is_file(), "SourceArchiveMissing")
    require(re.fullmatch(r"[0-9A-Fa-f]{64}", expected_source_hash) is not None, "ExpectedSourceHash")
    actual_hash = sha256_file(archive_path)
    require(actual_hash == expected_source_hash.upper(), "SourceArchiveHashChanged")
    require(bool(source_revision.strip()), "SourceRevision")
    target_pnus, presentation_count = load_targets(repository_root)
    with tempfile.TemporaryDirectory(prefix="sagajeong-parcel-") as temporary:
        shape_path = extract_layer(archive_path, Path(temporary))
        frame = read_official_frame(shape_path, target_pnus, repository_root)
        features, summary = build_features(frame, target_pnus)

    collection = {
        "type": "FeatureCollection",
        "name": "sagajeong-parcel-geometries",
        "crs": {"type": "name", "properties": {"name": SOURCE_CRS}},
        "features": features,
    }
    geometry_bytes = canonical_bytes(collection) + b"\n"
    geometry_hash = sha256_bytes(geometry_bytes)
    manifest = {
        "schemaVersion": "station-parcel-geometry-evidence.v1",
        "revision": "sagajeong-parcel-geometry-evidence.r1",
        "stationStableId": STATION_STABLE_ID,
        "regionStableId": REGION_STABLE_ID,
        "source": {
            "provider": "국토교통부·브이월드",
            "datasetId": SOURCE_DATASET_ID,
            "sourceRevision": source_revision,
            "archiveFileName": archive_path.name,
            "archiveSha256": actual_hash,
            "sourceCrs": SOURCE_CRS,
            "sourcePnuField": SOURCE_PNU_FIELD,
            "legalEffect": "ReferenceGeometryOnly",
        },
        "input": {
            "addressLedgerRelativePath": ADDRESS_LEDGER_RELATIVE.as_posix(),
            "addressLedgerFileSha256": ADDRESS_LEDGER_FILE_SHA256,
            "addressLedgerContentSha256": ADDRESS_LEDGER_CONTENT_SHA256,
            "presentationBuildingCount": presentation_count,
            "uniqueParcelIdentifierCount": len(target_pnus),
        },
        "output": {
            "geometryFileName": OUTPUT_GEOMETRY,
            "geometryFileSha256": geometry_hash,
        },
        "summary": summary,
        "boundary": output_boundary(),
    }
    manifest["contentSha256"] = sha256_bytes(canonical_bytes(manifest))

    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / OUTPUT_GEOMETRY).write_bytes(geometry_bytes)
    (output_dir / OUTPUT_MANIFEST).write_bytes(canonical_bytes(manifest) + b"\n")
    return manifest


def validate(repository_root: Path, output_dir: Path) -> dict[str, Any]:
    manifest_path = output_dir / OUTPUT_MANIFEST
    geometry_path = output_dir / OUTPUT_GEOMETRY
    require(manifest_path.is_file() and geometry_path.is_file(), "OutputMissing")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    expected_content_hash = manifest.pop("contentSha256", None)
    require(expected_content_hash == sha256_bytes(canonical_bytes(manifest)), "ManifestContentHash")
    manifest["contentSha256"] = expected_content_hash
    require(manifest.get("schemaVersion") == "station-parcel-geometry-evidence.v1", "ManifestSchema")
    require(manifest.get("stationStableId") == STATION_STABLE_ID, "ManifestStation")
    require(manifest["source"].get("sourceCrs") == SOURCE_CRS, "ManifestCrs")
    require(manifest["source"].get("sourcePnuField") == SOURCE_PNU_FIELD, "ManifestPnuField")
    require(manifest["output"].get("geometryFileSha256") == sha256_file(geometry_path), "GeometryFileHash")
    targets, presentation_count = load_targets(repository_root)
    require(manifest["input"].get("presentationBuildingCount") == presentation_count, "ManifestPresentationPopulation")
    require(manifest["summary"].get("targetParcelCount") == len(targets), "ManifestTargetPopulation")
    require(manifest["summary"].get("geometryParcelCount") + manifest["summary"].get("missingParcelCount") + manifest["summary"].get("invalidParcelCount") == len(targets), "ManifestCoveragePartition")
    require(all(value is False for key, value in manifest["boundary"].items() if key != "observationPresentationOnly"), "ManifestAuthorityBoundary")
    return manifest


def self_test(repository_root: Path) -> dict[str, Any]:
    install_local_geospatial_path(repository_root)
    import geopandas as gpd
    import pyogrio
    from shapely.geometry import Polygon

    frame = gpd.GeoDataFrame(
        {SOURCE_PNU_FIELD: ["1126010100100010001", "1126010100100010001", "1126010100100020001"]},
        geometry=[
            Polygon([(200000, 550000), (200010, 550000), (200010, 550010), (200000, 550010)]),
            Polygon([(200010, 550000), (200020, 550000), (200020, 550010), (200010, 550010)]),
            Polygon([(200030, 550000), (200040, 550000), (200040, 550010), (200030, 550010)]),
        ],
        crs=SOURCE_CRS,
    )
    targets = ["1126010100100010001", "1126010100100020001", "1126010100100030001"]
    with tempfile.TemporaryDirectory(prefix="sagajeong-parcel-self-test-") as temporary:
        temporary_root = Path(temporary)
        shape_path = temporary_root / "AL_D002_11_20990101.shp"
        pyogrio.write_dataframe(frame, shape_path, encoding="UTF-8")
        archive_path = temporary_root / "official-fixture.zip"
        with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for component in temporary_root.glob("AL_D002_11_20990101.*"):
                archive.write(component, component.name)
        extraction_root = temporary_root / "extracted"
        extraction_root.mkdir()
        extracted_shape = extract_layer(archive_path, extraction_root)
        reread = read_official_frame(extracted_shape, targets, repository_root)
        features, summary = build_features(reread, targets)
    require(len(features) == 2, "SelfTestGeometryCount")
    require(summary["missingParcelCount"] == 1 and summary["duplicateSourceFeatureParcelCount"] == 1, "SelfTestCoverage")
    require(summary["coverageState"] == "Missing", "SelfTestCoverageState")
    first_hash = sha256_bytes(canonical_bytes(features))
    second_hash = sha256_bytes(canonical_bytes(build_features(reread.iloc[::-1], targets)[0]))
    require(first_hash == second_hash, "SelfTestDeterminism")
    return {"status": "SelfTestPassed", "tests": 10, "networkRequested": False}


def readiness(repository_root: Path, source_archive: str) -> dict[str, Any]:
    targets, presentation_count = load_targets(repository_root)
    available = bool(source_archive) and Path(source_archive).is_file()
    return {
        "status": "ReadyForOfficialArchive" if available else "BlockedExternalAccess",
        "blockerCode": None if available else "VWorldLoginRequiredAndNoOfficialArchiveAvailable",
        "earliestResumePoint": "Build" if available else "ProvideOfficialSeoulAlD002Archive",
        "stationStableId": STATION_STABLE_ID,
        "sourceDatasetId": SOURCE_DATASET_ID,
        "sourceCrs": SOURCE_CRS,
        "sourcePnuField": SOURCE_PNU_FIELD,
        "presentationBuildingCount": presentation_count,
        "targetParcelCount": len(targets),
        "geometryParcelCount": 0,
        "collectionState": "NotCollected",
        "coverageState": "Missing",
        "applicationAuthorized": False,
        "networkRequested": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("readiness", "build", "validate", "self-test"), required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--source-archive", default="")
    parser.add_argument("--expected-source-sha256", default="")
    parser.add_argument("--source-revision", default="")
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    root = args.repository_root.resolve()
    output = args.output_dir.resolve()
    if args.mode == "readiness":
        result = readiness(root, args.source_archive)
    elif args.mode == "self-test":
        result = self_test(root)
    elif args.mode == "build":
        result = build(root, Path(args.source_archive).resolve(), args.expected_source_sha256, args.source_revision, output)
    else:
        result = validate(root, output)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, zipfile.BadZipFile) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)

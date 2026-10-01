#!/usr/bin/env python3
"""OA-21145 2025 biotope polygons clipped to 30 historical OA-22160 dong.

The output is an ignored, local, private observation candidate. It grants no
legal, species, safety, passage, runtime, Unity, or gameplay authority.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import io
import json
import math
import re
import sys
import zipfile
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path("artifacts/local/public-data/admin-dong-biotope-20260926-r1/raw/비오톱유형_평가도(2025년기준).zip")
RECEIPT = SOURCE.parent / "receipt.json"
BOUNDARY = Path("artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip")
SCOPE = Path("eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json")
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
OUTPUT = Path("artifacts/local/validation/admin-dong-biotope-private-review/r1")
SOURCE_SHA = "7FF3802116EC35BECABAAD8F5E8401C6AF8FC8C56FE1DFB526F39BF154657663"
RECEIPT_SHA = "CDF352945CF221B2850BC540D129396E32875250D8E1E8330DA78FED8E72AE0A"
BOUNDARY_SHA = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
EXPECTED_RECORDS = 42544
SCHEMA = "administrative-dong-biotope-private-candidate.v1"
REVISION = "northeast-seoul-biotope-private-review.r1"
TIMESTAMP = "2026-09-26T00:00:00Z"
MAX_AREA_DELTA_M2 = 0.01
MAX_OUTSIDE_AREA_M2 = 0.0001
FORBIDDEN_CANDIDATE_KEY = re.compile(r"고유번호|관리번호|주소|전화|원본ID|rawId|sourceId|species|safety|traversal|gameplay", re.I)


class ReviewError(RuntimeError):
    pass


def require(ok: bool, code: str) -> None:
    if not ok:
        raise ReviewError(code)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def sha_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def checked(root: Path, relative: Path, expected: str) -> Path:
    path = (root / relative).resolve()
    require(root in path.parents and path.is_file() and not path.is_symlink(), "InputMissingOrUnsafe:" + relative.name)
    require(sha_file(path) == expected, "InputHashChanged:" + relative.name)
    return path


def canonical(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")


def load_batch(root: Path):
    runtime = root / "artifacts/local/public-data/gis-runtime-r1"
    for path in (runtime, root / "eng/neighborhood"):
        if str(path) not in sys.path:
            sys.path.insert(0, str(path))
    spec = importlib.util.spec_from_file_location("biotope_historical_boundary_batch", root / BATCH)
    require(spec is not None and spec.loader is not None, "BoundaryModuleUnavailable")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def context(root: Path):
    checked(root, SOURCE, SOURCE_SHA)
    checked(root, RECEIPT, RECEIPT_SHA)
    checked(root, BOUNDARY, BOUNDARY_SHA)
    receipt = json.loads((root / RECEIPT).read_text(encoding="utf-8"))
    require(receipt["datasetId"] == "OA-21145" and receipt["vintage"]["selected"] == "2025", "SourceVintageMismatch")
    batch = load_batch(root)
    scope = batch.load_scope(root, root / SCOPE)
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, boundary_audit = batch.read_boundaries(root, scope, frame, dependencies)
    require(len(boundaries) == 30 and boundary_audit["historicalBootstrapOnly"], "HistoricalScopeMismatch")
    return batch, scope, dependencies, frame, boundaries, boundary_audit


def to_local(geometry, to_wgs, frame, dependencies):
    return dependencies.transform(lambda x, y, z=None: batch_xy(to_wgs, frame, x, y), geometry)


def batch_xy(to_wgs, frame, x, y):
    longitude, latitude = to_wgs.transform(x, y)
    if isinstance(longitude, (float, int)):
        return frame.wgs84_to_local(float(longitude), float(latitude))
    points = [frame.wgs84_to_local(float(lon), float(lat)) for lon, lat in zip(longitude, latitude)]
    return [p[0] for p in points], [p[1] for p in points]


def polygon_parts(geometry, dependencies):
    if isinstance(geometry, dependencies.Polygon):
        return [geometry]
    if isinstance(geometry, dependencies.MultiPolygon):
        return list(geometry.geoms)
    if isinstance(geometry, dependencies.GeometryCollection):
        return [p for g in geometry.geoms for p in polygon_parts(g, dependencies)]
    return []


def ring_mm(coordinates):
    # Six decimal places in millimetres preserve the clipped planar geometry to 1 nm.
    ring = [[round(float(x) * 1000, 6), round(float(y) * 1000, 6)] for x, y in coordinates]
    require(len(ring) >= 4 and ring[0] == ring[-1], "RingInvalid")
    return ring


def encode_geometry(geometry, dependencies):
    return [
        {"exteriorRingMm": ring_mm(p.exterior.coords),
         "interiorRingsMm": [ring_mm(h.coords) for h in p.interiors]}
        for p in polygon_parts(geometry, dependencies) if p.area > 0
    ]


def decode_geometry(polygons, dependencies):
    parts = [dependencies.Polygon(
        [(x / 1000, y / 1000) for x, y in p["exteriorRingMm"]],
        [[(x / 1000, y / 1000) for x, y in ring] for ring in p["interiorRingsMm"]],
    ) for p in polygons]
    require(parts and all(p.is_valid and p.area > 0 for p in parts), "EncodedPolygonInvalid")
    return parts[0] if len(parts) == 1 else dependencies.MultiPolygon(parts)


def safe_classification(properties):
    fields = {"typeCode": "비오톱유형", "evaluationGrade": "평가등급", "legendLabel": "유형범례"}
    result = {}
    for output, source in fields.items():
        value = str(properties.get(source, "")).strip()
        require(len(value) <= 50 and not any(c in value for c in "\r\n\t"), "ClassificationInvalid")
        result[output] = value
    return result


def scan_candidate_fields(value):
    if isinstance(value, dict):
        for key, child in value.items():
            require(not FORBIDDEN_CANDIDATE_KEY.search(key), "ForbiddenCandidateField:" + key)
            scan_candidate_fields(child)
    elif isinstance(value, list):
        for child in value:
            scan_candidate_fields(child)


def candidate_id(raw_id: object, index: int, dong_code: str) -> str:
    material = f"OA-21145|2025|{SOURCE_SHA}|{index}|{raw_id}|{dong_code}".encode("utf-8")
    return "biotope:" + digest(material)[:32].lower()


def input_entries(archive):
    names = archive.namelist()
    require(len(names) == len(set(names)), "SourceZipDuplicateEntry")
    require(all("UPIS_BIOTOP_TYP_2025" in n for n in names if n.lower().endswith((".shp", ".shx", ".dbf", ".prj"))), "Legacy2022ArchiveMixed")
    return {extension: next_only([n for n in names if n.lower().endswith(extension)], "SourceZipEntry:" + extension)
            for extension in (".shp", ".shx", ".dbf", ".prj")}


def next_only(values, code):
    require(len(values) == 1, code)
    return values[0]


def source_window(boundaries, frame, dependencies, to_source):
    points = []
    for boundary in boundaries:
        for x, y in boundary.geometry.exterior.coords:
            lon, lat = frame.local_to_wgs84(x, y, dependencies)
            points.append(to_source.transform(lon, lat))
    return (min(p[0] for p in points) - 100, min(p[1] for p in points) - 100,
            max(p[0] for p in points) + 100, max(p[1] for p in points) + 100)


def intersects_bounds(a, b):
    return a[0] <= b[2] and a[2] >= b[0] and a[1] <= b[3] and a[3] >= b[1]


def generate(root: Path):
    batch, scope, dependencies, frame, boundaries, boundary_audit = context(root)
    from shapely.validation import explain_validity
    out = defaultdict(list)
    quarantine = []
    counts = Counter()
    classes = Counter()
    per_dong_area = Counter()
    max_delta = 0.0
    max_outside = 0.0
    source_path = root / SOURCE
    with zipfile.ZipFile(source_path) as archive:
        members = input_entries(archive)
        source_crs = dependencies.CRS.from_wkt(archive.read(members[".prj"]).decode("utf-8-sig"))
        require(source_crs.to_authority() == ("EPSG", "5174"), "SourceCrsMismatch")
        to_wgs = dependencies.Transformer.from_crs(source_crs, 4326, always_xy=True)
        to_source = dependencies.Transformer.from_crs(4326, source_crs, always_xy=True)
        window = source_window(boundaries, frame, dependencies, to_source)
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(members[".shp"])),
            shx=io.BytesIO(archive.read(members[".shx"])),
            dbf=io.BytesIO(archive.read(members[".dbf"])),
            encoding="cp949", encodingErrors="strict")
        try:
            require(len(reader) == EXPECTED_RECORDS, "SourceRecordCountChanged")
            names = {field[0] for field in reader.fields[1:]}
            require({"비오톱유형", "평가등급", "유형범례", "고유번호"}.issubset(names), "SourceFieldsChanged")
            for index, record in enumerate(reader.iterShapeRecords()):
                counts["sourceRecords"] += 1
                shape = record.shape
                properties = record.record.as_dict()
                if shape.shapeType == 0 or not shape.points:
                    counts["emptyGeometry"] += 1
                    quarantine.append({"recordOrdinal": index, "reason": "EmptyGeometry"})
                    continue
                counts["nonEmptyGeometry"] += 1
                if shape.shapeType not in (5, 15, 25):
                    counts["invalidGeometry"] += 1
                    quarantine.append({"recordOrdinal": index, "reason": "UnexpectedShapeType"})
                    continue
                if not intersects_bounds(shape.bbox, window):
                    counts["outsideScopeBbox"] += 1
                    continue
                native = dependencies.shape(shape.__geo_interface__)
                if native.is_empty or not native.is_valid:
                    counts["invalidGeometry"] += 1
                    invalid_local_bounds = to_local(native, to_wgs, frame, dependencies).bounds
                    affected = [b.code for b in boundaries if intersects_bounds(invalid_local_bounds, b.geometry.bounds)]
                    quarantine.append({"recordOrdinal": index, "reason": "InvalidNativeGeometry", "detail": explain_validity(native)[:120], "possibleDongCodesByBbox": affected})
                    continue
                local = to_local(native, to_wgs, frame, dependencies)
                if not local.is_valid or local.is_empty:
                    counts["invalidGeometry"] += 1
                    quarantine.append({"recordOrdinal": index, "reason": "InvalidTransformedGeometry", "detail": explain_validity(local)[:120]})
                    continue
                counts["validScopeBboxGeometry"] += 1
                for boundary in boundaries:
                    if not intersects_bounds(local.bounds, boundary.geometry.bounds):
                        continue
                    try:
                        clipped = local.intersection(boundary.geometry)
                    except Exception as exc:
                        counts["quarantinedIntersection"] += 1
                        quarantine.append({"recordOrdinal": index, "dongCode": boundary.code, "reason": "IntersectionError", "detail": type(exc).__name__})
                        continue
                    parts = polygon_parts(clipped, dependencies)
                    if not parts or clipped.area <= 0:
                        continue
                    if not clipped.is_valid:
                        counts["quarantinedIntersection"] += 1
                        quarantine.append({"recordOrdinal": index, "dongCode": boundary.code, "reason": "InvalidIntersection"})
                        continue
                    encoded = encode_geometry(clipped, dependencies)
                    if not encoded:
                        continue
                    try:
                        decoded = decode_geometry(encoded, dependencies)
                    except ReviewError:
                        counts["quarantinedIntersection"] += 1
                        quarantine.append({"recordOrdinal": index, "dongCode": boundary.code, "reason": "InvalidEncodedGeometry"})
                        continue
                    difference = abs(decoded.area - clipped.area)
                    outside = decoded.difference(boundary.geometry).area
                    require(difference <= MAX_AREA_DELTA_M2, "AreaPreservationFailed")
                    require(outside <= MAX_OUTSIDE_AREA_M2, "BoundaryOutsideFailed")
                    classification = safe_classification(properties)
                    candidate = {
                        "stableId": candidate_id(properties["고유번호"], index, boundary.code),
                        "classification": classification,
                        "polygons": encoded,
                    }
                    scan_candidate_fields(candidate)
                    out[boundary.code].append(candidate)
                    counts["intersections"] += 1
                    counts["polygonParts"] += len(encoded)
                    counts["interiorRings"] += sum(len(p["interiorRingsMm"]) for p in encoded)
                    counts["clippedAreaMicrosquareMeters"] += round(clipped.area * 1_000_000)
                    counts["encodedAreaMicrosquareMeters"] += round(decoded.area * 1_000_000)
                    counts["outsideAreaNanosquareMeters"] += round(outside * 1_000_000_000)
                    classes[classification["typeCode"]] += 1
                    per_dong_area[boundary.code] += round(decoded.area * 1_000_000)
                    max_delta = max(max_delta, difference)
                    max_outside = max(max_outside, outside)
        finally:
            reader.close()
    require(counts["sourceRecords"] == EXPECTED_RECORDS and counts["emptyGeometry"] == 1
            and counts["nonEmptyGeometry"] == EXPECTED_RECORDS - 1, "SourceGeometryCountChanged")
    require(set(out) == {b.code for b in boundaries}, "DongCoverageIncomplete")
    files = {}
    for boundary in boundaries:
        items = sorted(out[boundary.code], key=lambda item: item["stableId"])
        files[f"dong/{boundary.code}.json"] = canonical({
            "schemaVersion": SCHEMA, "revision": REVISION,
            "administrativeAreaStableId": boundary.stable_id,
            "sourceDatasetId": "OA-21145", "sourceVintage": "2025",
            "historicalBoundaryDatasetId": "OA-22160", "historicalBoundaryVintage": "2023",
            "coordinateFrame": scope["coordinateFrame"], "coordinateUnit": "millimeter",
            "publicAllowed": False, "databaseAllowed": False, "mongoAllowed": False,
            "currentAllowed": False, "unityAllowed": False, "runtimeAllowed": False,
            "traversalAllowed": False, "gameplayAllowed": False,
            "candidates": items,
        })
    quarantine.sort(key=lambda x: (x["recordOrdinal"], x.get("dongCode", ""), x["reason"]))
    files["audit.json"] = canonical({
        "schemaVersion": "administrative-dong-biotope-private-audit.v1",
        "revision": REVISION, "counts": dict(sorted(counts.items())),
        "historicalBoundary": boundary_audit,
        "quarantine": quarantine,
        "coverage": {b.code: len(out[b.code]) for b in boundaries},
        "perDongAreaMicrosquareMeters": dict(sorted(per_dong_area.items())),
        "officialTypeCodeCounts": dict(sorted(classes.items())),
        "maximumCandidateAreaDifferenceSquareMeters": max_delta,
        "maximumCandidateOutsideAreaSquareMeters": max_outside,
        "areaPreservationToleranceSquareMetersPerCandidate": MAX_AREA_DELTA_M2,
        "boundaryOutsideToleranceSquareMetersPerCandidate": MAX_OUTSIDE_AREA_M2,
    })
    entries = [{"path": name, "bytes": len(data), "sha256": digest(data)} for name, data in sorted(files.items())]
    manifest = {
        "schemaVersion": "administrative-dong-biotope-private-manifest.v1",
        "revision": REVISION, "generatedAtUtc": TIMESTAMP,
        "scopeStableId": scope["scopeStableId"], "scopeSha256": sha_file(root / SCOPE),
        "sourceDatasetId": "OA-21145", "sourceVintage": "2025", "sourceSha256": SOURCE_SHA,
        "sourceReceiptSha256": RECEIPT_SHA, "historicalBoundaryDatasetId": "OA-22160",
        "historicalBoundaryVintage": "2023", "historicalBoundarySha256": BOUNDARY_SHA,
        "generatorSha256": sha_file(Path(__file__)),
        "administrativeAreaCount": len(boundaries), "candidateCount": counts["intersections"],
        "authority": {key: False for key in ("public", "database", "mongo", "current", "unity", "runtime", "traversal", "gameplay", "legalEffect", "speciesObservation", "safety", "passage")},
        "files": entries,
    }
    files["manifest.json"] = canonical(manifest)
    files["complete.json"] = canonical({
        "schemaVersion": "administrative-dong-biotope-private-complete.v1",
        "revision": REVISION, "manifestSha256": digest(files["manifest.json"]),
        "fileCount": len(files) + 1, "administrativeAreaCount": len(boundaries),
        "candidateCount": counts["intersections"],
    })
    return files, counts


def write_changed(root: Path, files: dict[str, bytes]):
    output = (root / OUTPUT).resolve()
    require(root in output.parents and not output.is_symlink(), "OutputPathUnsafe")
    output.mkdir(parents=True, exist_ok=True)
    expected = set(files)
    actual = {p.relative_to(output).as_posix() for p in output.rglob("*") if p.is_file()}
    require(not (actual - expected), "UnexpectedOutputFile")
    changed = 0
    for name, data in sorted(files.items()):
        path = output / name
        require(output in path.resolve().parents and not path.is_symlink(), "OutputFileUnsafe")
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != data:
            path.write_bytes(data)
            changed += 1
    return changed


def verify(root: Path, expected: dict[str, bytes] | None = None):
    if expected is None:
        expected, counts = generate(root)
    else:
        counts = json.loads(expected["audit.json"])["counts"]
    output = root / OUTPUT
    require(output.is_dir(), "OutputMissing")
    actual = {p.relative_to(output).as_posix() for p in output.rglob("*") if p.is_file()}
    require(actual == set(expected), "OutputFileSetChanged")
    for name, data in expected.items():
        require((output / name).read_bytes() == data, "OutputContentChanged:" + name)
    for name in sorted(expected):
        if name.startswith("dong/"):
            payload = json.loads(expected[name])
            require(payload["publicAllowed"] is False and payload["databaseAllowed"] is False
                    and payload["mongoAllowed"] is False and payload["currentAllowed"] is False
                    and payload["unityAllowed"] is False and payload["runtimeAllowed"] is False
                    and payload["traversalAllowed"] is False and payload["gameplayAllowed"] is False,
                    "PrivateAuthorityChanged")
            scan_candidate_fields(payload["candidates"])
    return {"status": "PASS", "files": len(expected), "administrativeAreas": 30,
            "candidates": counts["intersections"], "sourceRecords": counts["sourceRecords"],
            "nonEmptyGeometry": counts["nonEmptyGeometry"],
            "emptyGeometry": counts["emptyGeometry"], "invalidGeometry": counts["invalidGeometry"],
            "quarantinedIntersection": counts.get("quarantinedIntersection", 0),
            "polygonParts": counts["polygonParts"], "interiorRings": counts["interiorRings"],
            "sourceAreaSquareMeters": counts["clippedAreaMicrosquareMeters"] / 1_000_000,
            "encodedAreaSquareMeters": counts["encodedAreaMicrosquareMeters"] / 1_000_000,
            "outsideAreaSquareMeters": counts["outsideAreaNanosquareMeters"] / 1_000_000_000,
            "manifestSha256": digest(expected["manifest.json"])}


def self_test(root: Path):
    _, _, dep, _, boundaries, _ = context(root)
    outer = dep.Polygon([(0, 0), (10, 0), (10, 10), (0, 10)], holes=[[(2, 2), (4, 2), (4, 4), (2, 4)]])
    mask = dep.Polygon([(1, 1), (9, 1), (9, 9), (1, 9)])
    clip = outer.intersection(mask)
    restored = decode_geometry(encode_geometry(clip, dep), dep)
    require(len(restored.interiors) == 1 and abs(restored.area - clip.area) < 1e-9, "SelfTestHolePreservation")
    require(restored.difference(mask).area < 1e-9, "SelfTestBoundaryOutside")
    require(not dep.Polygon([(0, 0), (2, 2), (0, 2), (2, 0)]).is_valid, "SelfTestInvalidGeometry")
    require(candidate_id(123, 9, boundaries[0].code) == candidate_id(123, 9, boundaries[0].code), "SelfTestStableId")
    try:
        scan_candidate_fields({"rawId": 123})
    except ReviewError:
        pass
    else:
        raise ReviewError("SelfTestForbiddenFieldScan")
    require(len(boundaries) == 30, "SelfTestScopeCoverage")
    return {"status": "PASS", "tests": ["holePreservation", "areaPreservation", "boundaryOutside", "invalidGeometryQuarantine", "stableId", "forbiddenFieldScan", "30DongScope"]}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT))
    args = parser.parse_args(argv)
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        if args.mode == "self-test":
            result = self_test(root)
        elif args.mode == "build":
            files, _ = generate(root)
            changed = write_changed(root, files)
            result = verify(root, files)
            result["changedFiles"] = changed
        else:
            result = verify(root)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (ReviewError, OSError, ValueError, KeyError, zipfile.BadZipFile) as exc:
        print(json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

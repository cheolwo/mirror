"""행정동 디오라마의 건물 지번과 음식점 인허가 주소를 같은 필지 단위로 대조한다."""
import argparse
import csv
import gzip
import hashlib
import json
import re
import sys
import zipfile
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / "artifacts/local"
BASE = LOCAL / "admin-dong-precision-modules-20261115"
SUPPLEMENT = LOCAL / "admin-dong-diorama-supplement-20261115-r2"
R3 = LOCAL / "admin-dong-diorama-precision-r3"
sys.path.insert(0, str(LOCAL / "public-data/gis-runtime-r1"))
from sagajeong_spatial_presentation import _dbf_text, _read_dbf_header, _read_exact

# 뒷부분에 별도 지번·필지나 건물명이 붙으면 이 좁은 판에서는 보류한다.
ADDRESS = re.compile(r"서울특별시\s+(\S+구)\s+(\S+)\s+(산\s*)?([0-9]{1,4})(?:-([0-9]{1,4}))?(?:번지)?(?:\s+(?:지하\s*)?[0-9]{1,3}층(?:\s*일부)?)?")
LOT = re.compile(r"([0-9]{1,4})(?:-([0-9]{1,4}))?")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify(path, sha):
    if digest(path) != sha.lower():
        raise ValueError(f"입력 파일 지문 불일치 {path}")
    return path


def read(path):
    return json.loads(path.read_bytes())


def save(directory, name, value):
    path = directory / name
    data = (json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n").encode()
    if path.suffix == ".gz":
        data = gzip.compress(data, compresslevel=6, mtime=0)
    path.parent.mkdir(exist_ok=True, parents=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_bytes(data)
    temporary.replace(path)
    return {"path": name, "sha256": digest(path), "bytes": len(data)}


def 지번주소키(row, 법정동):
    if row["legalDongBindingMethod"] != "SourceDeclaredCode":
        return None, "legalCodeMissingOrInConflict"
    if len(row["sourceRecordIds"]) != 1:
        return None, "multipleLicenseObservations"
    match = ADDRESS.fullmatch((row["parcelAddressSource"] or "").strip())
    if not match:
        return None, "missingOrCompoundAddress"
    district, name, mountain, main, sub = match.groups()
    code = row["legalDongCodeSource"]
    if 법정동.get(code) != (district, name) or district != row["sourceDistrict"] or int(main) == 0:
        return None, "addressCodeMismatch"
    return f"{code}{'2' if mountain else '1'}{int(main):04}{int(sub or 0):04}", None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--edition", choices=("historical-2023", "candidate-2026"), default="historical-2023")
    edition = parser.parse_args().edition
    out = R3 / "parcels"
    date = "2023-10-31"
    base_path = verify(BASE / "package-manifest.json", "a0c1da9f340a94ea3772fa40cead4d0e998ff4c65dd9bc6b06843dd957cb11ee")
    crosswalk = verify(BASE / "context/administrative-legal-crosswalk.csv", "5d83d21bdbbd9544109e2cc1fe3dd40a2d9e53c7878b0602d26719f9d22f69b9")
    licenses = verify(SUPPLEMENT / "restaurants/nonspatial-context.jsonl.gz", "8f71c3838a96a67d498290d067c2413ae01054fe4303760b792083c323455dca")
    building_zip = verify(LOCAL / "neighborhood-source-acquisition/AL_D010_11_20260809.zip", "674c5a9583996dd6b8946525edad8197be79a634f1db00d39e2b3133d0d2a755")
    modules = read(base_path)["historicalModules"]
    source_paths = [base_path, crosswalk, licenses, building_zip]
    if edition == "candidate-2026":
        date = "2026-10-01"
        out = out / "candidate-20261001"
        detail = R3 / "candidate-detail/manifest.json"
        source_paths.append(detail)
        current = {row["code"]: row for row in read(base_path)["currentModules"]}
        modules = [{**row, "name": current[row["code"]]["name"], "district": current[row["code"]]["district"]}
                   for row in read(detail)["modules"]]
    with crosswalk.open(encoding="utf-8-sig", newline="") as stream:
        법정동 = {row["legalCode"]: (row["district"], row["legalName"]) for row in csv.DictReader(stream)}

    # 하나의 건물 원본 행이 여러 경계에서 잘릴 수 있다. 원본 행과 동 도형 키를 따로 보존한다.
    pieces = defaultdict(list)
    identity = {}
    building_hashes = {}
    for module in modules:
        if edition == "historical-2023":
            meta = read(verify(BASE / module["path"], module["sha256"]))
            layer_root = BASE
        else:
            meta = module
            layer_root = detail.parent
        layer = next(x for x in meta["layers"] if x["layer"] == "buildings")
        building_hashes[module["code"]] = layer["sha256"].lower()
        with gzip.open(verify(layer_root / layer["path"], layer["sha256"])) as stream:
            features = json.load(stream)["features"]
        for feature in features:
            p = feature["properties"]
            n = p["sourceRowOneBased"]
            pair = p["sourceId"], p["sourceLegalDongCode"]
            if pair != identity.setdefault(n, pair):
                raise ValueError(f"동 경계 조각의 원본 식별자 충돌 {n}")
            pieces[n].append((module["code"], feature["id"]))

    building_info, pnu_rows = {}, defaultdict(list)
    with zipfile.ZipFile(building_zip) as archive, archive.open("AL_D010_11_20260809.dbf") as dbf:
        count, _, width, fields = _read_dbf_header(dbf)
        for n in range(1, count + 1):
            record = _read_exact(dbf, width, "TruncatedALD010")
            if record[:1] == b"*":
                continue
            legal = _dbf_text(record, fields["A3"], "ascii")
            if legal not in 법정동 and n not in pieces:
                continue
            def field(key):
                return _dbf_text(record, fields[key], "cp949")
            pnu, lot, flag = field("A2"), field("A5"), field("A6")
            number = LOT.fullmatch(lot)
            valid = (number is not None and flag in ("1", "2") and int(number[1]) > 0
                     and pnu == f"{legal}{flag}{int(number[1]):04}{int(number[2] or 0):04}"
                     and field("A4") == " ".join(("서울특별시", *법정동.get(legal, ("?", "?")))))
            if legal in 법정동 and re.fullmatch(r"[0-9]{19}", pnu):
                # 보류·범위 밖 도형도 동등한 원본 후보 수에 포함한다.
                pnu_rows[pnu].append((n, valid))
            if n in pieces:
                if (field("A1") or None, legal or None) != identity[n]:
                    raise ValueError(f"원본 건물 행과 식별자가 다름 {n}")
                building_info[n] = {"sourceRow": n, "pnuSource": pnu or None, "legalNameSource": field("A4"),
                                    "lotSource": lot, "landTypeSource": field("A7"),
                                    "useNameSource": field("A9") or None, "parcelFieldsConsistent": valid}
    if set(building_info) != set(pieces):
        raise ValueError("건물 원본 행 결측")

    outputs = {m["code"]: {"code": m["code"], "name": m["name"], "district": m["district"],
                            "boundaryDate": date, "edition": edition, "buildingGeometrySha256": building_hashes[m["code"]],
                            "buildingAddresses": [], "restaurantParcelCandidates": [],
                            "storefrontConfirmed": False, "placementAuthorized": False} for m in modules}
    for n, info in building_info.items():
        for code, feature_id in pieces[n]:
            outputs[code]["buildingAddresses"].append({"featureId": feature_id, **info})

    ledger, counts = {}, Counter()
    with gzip.open(licenses, "rt", encoding="utf-8") as stream:
        for line in stream:
            row = json.loads(line)
            key = row["managementKey"]
            if key in ledger or row["sourceStatusGroup"] != "OperatingReported":
                raise ValueError(f"인허가 키 중복 또는 대상 외 상태 {key}")
            pnu, reason = 지번주소키(row, 법정동)
            matches = pnu_rows.get(pnu, [])
            candidates = defaultdict(list)
            for n, valid in matches:
                if valid:
                    for code, feature_id in pieces.get(n, []):
                        candidates[code].append(feature_id)
            if not reason:
                if not matches:
                    reason = "parcelAbsentFromBuildingSource"
                elif not candidates:
                    reason = "noValidatedFootprintInReviewArea"
                elif len(matches) > 1:
                    reason = "multipleSourceRowsOnParcel"
                else:
                    reason = "oneSourceRowOnParcel"
            ledger[key] = {"state": reason, "pnu": pnu, "buildingSourceRowsOnParcel": len(matches),
                           "moduleCandidates": sorted(candidates)}
            counts[reason] += 1
            for code, feature_ids in candidates.items():
                outputs[code]["restaurantParcelCandidates"].append({
                    "managementKey": key, "name": row["businessNameSource"], "category": row["businessSubtypeSource"],
                    "permitSourceRecord": row["sourceRecordIds"][0], "pnu": pnu,
                    "buildingSourceRowsOnParcel": len(matches), "candidateGeometryIds": sorted(feature_ids)})

    entries = []
    for code, module in sorted(outputs.items()):
        ref = save(out, f"modules/{code}.json.gz", module)
        entries.append({"code": code, **ref, "buildingPieces": len(module["buildingAddresses"]),
                        "licenseKeys": len(module["restaurantParcelCandidates"])})
    ledger_ref = save(out, "license-parcel-dispositions.json.gz", ledger)
    sources = [{"path": p.relative_to(ROOT).as_posix(), "sha256": digest(p)}
               for p in source_paths]
    report = {"moduleUnit": "AdministrativeDongReview", "edition": edition, "boundaryDate": date,
              "buildingSourceDate": "2026-08-09", "acquiredNewRaw": False, "sources": sources,
              "fieldDefinition": "https://www.ktdb.go.kr/DATA/pblcte/20250805093928585.pdf",
              "sourceCatalog": "https://www.data.go.kr/data/15083092/fileData.do",
              "buildingPieces": sum(e["buildingPieces"] for e in entries), "buildingRows": len(building_info),
              "buildingRowsWithConsistentParcelFields": sum(int(x["parcelFieldsConsistent"]) for x in building_info.values()),
              "licenseKeys": len(ledger), "states": dict(counts), "matchedLicenseKeys": sum(bool(x["moduleCandidates"]) for x in ledger.values()),
              "licenseModuleOccurrences": sum(e["licenseKeys"] for e in entries), "ledger": ledger_ref, "modules": entries,
              "restaurantCoordinatesUsed": False, "storefrontConfirmed": False, "distributionApproved": False,
              "interpretation": "License snapshot and parcel-address correspondence only. Even one matching source footprint does not locate a storefront, floor or entrance. Boundary membership belongs to the building geometry at the stated edition."}
    save(out, "manifest.json", report)
    print(json.dumps({k: v for k, v in report.items() if k in ("buildingPieces","buildingRows","matchedLicenseKeys","states","licenseKeys")}, ensure_ascii=False))


if __name__ == "__main__":
    main()

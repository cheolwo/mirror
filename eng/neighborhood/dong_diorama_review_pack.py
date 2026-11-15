"""행정동과 경계 판본을 골라 Claude가 읽을 검토 묶음을 만들거나 검사한다."""
import argparse
import gzip
import hashlib
import json
import os
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / "artifacts/local"
R2 = LOCAL / "admin-dong-diorama-supplement-20261115-r2"
R3 = LOCAL / "admin-dong-diorama-precision-r3"
EDITIONS = ("historical-2023", "candidate-2026")
DATES = dict(zip(EDITIONS, ("2023-10-31", "2026-10-01")))
CATALOG = R3 / "catalog.json"
SEOUL = "https://data.seoul.go.kr/dataList/"
SOURCES = {
    "buildings": ["https://www.data.go.kr/data/15083092/fileData.do"],
    "roads": ["https://www.data.go.kr/data/15025526/fileData.do"],
    "walk": [SEOUL + "OA-21208/A/1/datasetView.do"],
    "walk-codebook": [SEOUL + "OA-21208/A/1/datasetView.do"],
    "streets": [SEOUL + "OA-21208/A/1/datasetView.do", SEOUL + "OA-15537/S/1/datasetView.do"],
    "green": [SEOUL + "OA-15529/F/1/datasetView.do", SEOUL + "OA-394/F/1/datasetView.do"],
    "biotope": [SEOUL + "OA-21145/A/1/datasetView.do"],
    "contours": [SEOUL + "OA-22241/F/1/datasetView.do"],
    "spot-elevations": [SEOUL + "OA-22241/F/1/datasetView.do"],
    "intersections": [SEOUL + "OA-15534/S/1/datasetView.do"],
    "population": [SEOUL + "OA-22178/S/1/datasetView.do"],
    "parcels": ["https://www.data.go.kr/data/15083092/fileData.do", SEOUL + "OA-23056/S/1/datasetView.do", SEOUL + "OA-23059/S/1/datasetView.do"],
}


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")) + "\n").encode()


def load(path):
    return json.loads(path.read_bytes())


def write(path, value):
    write_payload(path, json_bytes(value))


def write_payload(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        stream.write(payload)
        temp = Path(stream.name)
    try:
        temp.replace(path)
    finally:
        temp.unlink(missing_ok=True)


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def checked_file(folder, entry):
    p = (folder / entry["path"]).resolve()
    if not p.is_relative_to(folder.resolve()) or sha(p) != entry["sha256"].lower():
        raise ValueError(f"경로 또는 SHA-256 불일치 {p}")
    return p


def layer(folder, entry, key, meaning, crs=None):
    path = checked_file(folder, entry)
    return {"key": key, "role": meaning, "path": path.relative_to(ROOT).as_posix(),
            "bytes": path.stat().st_size, "sha256": entry["sha256"].lower(),
            "crs": crs or entry.get("crs"), "sourceVintage": entry.get("sourceVintage"), "sources": SOURCES.get(key, []),
            "archivePath": f"layers/{key}{''.join(path.suffixes[-2:])}" if path.suffix == ".gz" else f"layers/{key}{path.suffix}"}


def indexed(rows):
    table = {r["code"]: r for r in rows}
    if len(table) != len(rows):
        raise ValueError("행정동 코드 중복")
    return table


def check_binding(ref, expectations):
    with gzip.open(ROOT / ref["path"]) as stream:
        data = json.load(stream)
    for key, expected in expectations.items():
        if data.get(key) != expected:
            raise ValueError(f"동·판본 결속 오류 {key} {ref['path']}")


def build():
    r2path = R2 / "bake-input-manifest.json"
    if sha(r2path) != "78db555331e3a2047f6236dd5ac5d5827276f1b0e86ff09e4e84bb601a43d618":
        raise ValueError("기준 r2 변경")
    r2 = load(r2path)
    boundaries = load(R3 / "boundary/manifest.json")
    result = {"schema": "hongdal.dong-diorama-review.r3", "moduleUnit": "행정동", "editions": {},
              "grid500mDefault": False, "officialCurrentBoundaryVerified": False,
              "distributionApproved": False, "unityApplied": False, "sourceManifests": {}}
    index = ["# 행정동별 디오라마 검토 자료", "", "2026년 10월 1일 경계 후보로 나눈 동별 목록이다. 건물·도로는 경계에서 잘린 조각 수이고 음식점은 해당 동의 건물과 같은 필지로 연결된 인허가 키 수다. 한 필지가 여러 동에 걸치면 각 동에 나타난다.", "",
             "| 구 | 행정동 | 코드 | 건물 조각 | 도로 조각 | 도보선 조각 | 공원 계획면 조각 | 음식점 필지 후보 |",
             "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |"]
    for edition in EDITIONS:
        historical = edition == EDITIONS[0]
        identities = indexed(r2["historicalModules"] if historical else r2["currentModules"])
        groups = {}
        suffix = "" if historical else "/candidate-20261001"
        for k in ("parcels", "green", "streets", *(("candidate-detail", "boundary") if not historical else ())):
            directory = R3 / (k + suffix if k in ("parcels","green","streets") else k)
            manifest = directory / "manifest.json"
            meta = load(manifest)
            records = indexed(meta["modules"])
            if records.keys() != identities.keys():
                raise ValueError(f"행정동 코드 집합 불일치 {manifest}")
            groups[k] = directory, records
            result["sourceManifests"][directory.relative_to(R3).as_posix()] = {"path": manifest.relative_to(ROOT).as_posix(), "sha256": sha(manifest)}
        entries = []
        for code, ident in sorted(identities.items()):
            layers, options = [], []
            if historical:
                record = load(checked_file(ROOT, ident))
                for geo in record["geometryLayers"]:
                    layers.append(layer(ROOT, geo, geo["layer"], geo["shapePolicy"]))
                layers.append(layer(ROOT, record["populationContext"], "population", "행정동 단위 시간대별 유동인구 추정", "nonspatial"))
                layers.append(layer(ROOT, record["facilityReferenceLayers"]["intersections"], "intersections", "교차로 지점", "EPSG:5186"))
                options.append(layer(ROOT, record["optionalIndexes"]["grid500m"]["reference"], "grid500m", "선택 조회 색인", "r1 common ENU"))
            else:
                folder, boundaries_by_code = groups["boundary"]
                boundary = load(checked_file(folder, boundaries_by_code[code]))
                layers.append(layer(folder, boundary["candidate20261001"], "boundary", "SGIS 2차 가공 경계", "OGC:CRS84"))
                boundary_sha = layers[-1]["sha256"]
                folder, details = groups["candidate-detail"]
                for geo in details[code]["layers"]:
                    if geo["layer"] in ("buildings", "roads"):
                        if geo["boundaryCandidateSha256"] != boundary_sha:
                            raise ValueError(f"절단 경계 불일치 {code}")
                        layers.append(layer(folder, geo, geo["layer"], "2026 후보 경계와 원본을 직접 교차"))
            for k, meaning, crs in (("streets", "거리 확대용 보행선·교통 지점·차선", "EPSG:5186; see encoded origin and scale"),
                                    ("green", "공원 계획면과 공원 대표점", "EPSG:5186; observedLonLat is WGS84"),
                                    ("parcels", "건물 지번과 음식점 인허가의 동일 필지 후보", "featureId; nonspatial")):
                directory, by_code = groups[k]
                layers.append(layer(directory, by_code[code], k, meaning, crs))
                if k == "parcels":
                    building_sha = next(x["sha256"] for x in layers if x["key"] == "buildings")
                    check_binding(layers[-1], {"code": code, "edition": edition, "buildingGeometrySha256": building_sha})
                elif not historical:
                    checks = ({"candidateDongCode":code, "candidateBoundarySha256":boundary_sha} if k == "streets"
                              else {"candidateCode":code, "candidateSha256":boundary_sha})
                    check_binding(layers[-1], checks)
            book = R3 / "streets/walk-codebook.json"
            layers.append(layer(book.parent, {"path": book.name, "sha256": sha(book)}, "walk-codebook", "서울시 도보망 유형표", "nonspatial"))
            required = {"boundary","buildings","roads","green","streets","parcels","walk-codebook"}
            if not required.issubset({x["key"] for x in layers}):
                raise ValueError(f"필수 자료 누락 {code}")
            vintage = {"boundary": DATES[edition], "buildings": "2026-08-09", "roads": "2026-08-12",
                       "green": "2018 park planning; 2026 H2 park registry", "population": "2021Q1–2026Q2",
                       "streets": "2020 walk source; 2021 lane attachment", "intersections": "2025-08-14 attachment",
                       "parcels": "building 2026-08-09; licenses collected 2026-11-13"}
            for ref in layers:
                key = ref["key"]
                ref["sourceVintage"] = ref["sourceVintage"] or vintage.get(key)
                if key == "boundary":
                    ref["sources"] = [SEOUL + "OA-22160/F/1/datasetView.do"] if historical else [f"https://github.com/vuski/admdongkor/tree/{boundaries['source2026Commit']}/ver20261001"]
            module = {"schema": result["schema"], "edition": edition, "code": code, "name": ident["name"], "district": ident["district"],
                      "clippingBoundaryDate": DATES[edition], "officialCurrentBoundaryVerified": False,
                      "boundaryBasis": "서울 OA-22160 역사 경계" if historical else boundaries["source2026Attribution"],
                      "layers": layers, "options": options, "distributionApproved": False, "unityApplied": False,
                      "note": "경계와 각 관측 자료의 기준시점은 다릅니다. 동일 필지 후보는 점포 위치를 확정하지 않습니다. 이 파일은 자료 검토용입니다."}
            path = R3 / "modules" / edition / f"{code}.json"
            write(path, module)
            entries.append({"code": code, "name": ident["name"], "district": ident["district"],
                            "path": path.relative_to(R3).as_posix(), "sha256": sha(path)})
            if not historical:
                detail_counts = {x["layer"]:x["featureCount"] for x in groups["candidate-detail"][1][code]["layers"]}
                walk = groups["streets"][1][code]["counts"]["walkLinks"]
                parks = groups["green"][1][code]["parkPieces"]
                permits = groups["parcels"][1][code]["licenseKeys"]
                index.append(f"| {ident['district']} | [{ident['name']}]({path.relative_to(R3).as_posix()}) | {code} | {detail_counts['buildings']:,} | {detail_counts['roads']:,} | {walk:,} | {parks:,} | {permits:,} |")
        result["editions"][edition] = entries
    result["sourceManifests"]["r2"] = {"path": r2path.relative_to(ROOT).as_posix(), "sha256": sha(r2path)}
    write(CATALOG, result)
    write_payload(R3 / "MODULES.md", ("\n".join(index) + "\n").encode())
    print({k: len(v) for k,v in result["editions"].items()})


def review(args):
    entries = load(CATALOG)["editions"][args.edition]
    if args.action == "list":
        for item in entries:
            print(item["code"], item["district"], item["name"])
        return
    entry = indexed(entries).get(args.code)
    if entry is None:
        raise ValueError(f"선택 판본에 없는 행정동 {args.edition} {args.code}")
    module = load(checked_file(R3, entry))
    refs = list(module.pop("layers"))
    options = module.pop("options")
    if args.with_500m_index:
        if not options:
            raise ValueError("이 경계 판본에서 산출한 500m 색인이 없습니다")
        refs.extend(options)
    files = [(ref, checked_file(ROOT, ref)) for ref in refs]
    # 옛 녹지 색인의 비오톱 파일 등 중첩 참조도 ZIP 안에서 해석할 수 있다.
    module["includedSourcePaths"] = {ref["path"]: ref["archivePath"] for ref in refs}
    module["layers"] = [{**{k: v for k,v in ref.items() if k not in ("path","archivePath")}, "path": ref["archivePath"]} for ref in refs]
    if args.action == "inspect":
        print(json.dumps(module, ensure_ascii=False, indent=2))
        return
    dest = args.output.resolve()
    if dest.suffix.lower() != ".zip" or dest.exists():
        raise ValueError("새 ZIP 파일 경로를 지정하세요")
    dest.parent.mkdir(parents=True, exist_ok=True)
    # 모든 입력을 미리 검증하고 ZIP을 완성한 뒤 최종 경로에 게시한다.
    with tempfile.NamedTemporaryFile(dir=dest.parent, delete=False) as stream:
        temp = Path(stream.name)
    try:
        with zipfile.ZipFile(temp, "w", compression=zipfile.ZIP_STORED) as archive:
            # ZipInfo의 고정 시각은 같은 입력을 다시 내보낼 때 바이트까지 같게 한다.
            archive.writestr(zipfile.ZipInfo("module.json"), json_bytes(module))
            for ref, p in files:
                data = p.read_bytes()
                if hashlib.sha256(data).hexdigest() != ref["sha256"]:
                    raise ValueError(f"내보내기 중 입력 변경 {p}")
                archive.writestr(zipfile.ZipInfo(ref["archivePath"]), data)
        with zipfile.ZipFile(temp) as archive:
            if archive.testzip() is not None:
                raise ValueError("ZIP 검증 실패")
        # 같은 시각에 다른 사용자가 만든 결과를 덮지 않는다.
        os.link(temp, dest)
    finally:
        temp.unlink(missing_ok=True)
    print(dest, sha(dest))


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    commands = cli.add_subparsers(dest="action", required=True)
    commands.add_parser("build")
    for action in ("list", "inspect", "pack"):
        cmd = commands.add_parser(action)
        cmd.add_argument("--edition", choices=EDITIONS, required=True)
        if action != "list":
            cmd.add_argument("code")
            cmd.add_argument("--with-500m-index", action="store_true")
        if action == "pack":
            cmd.add_argument("--output", type=Path, required=True)
    args = cli.parse_args()
    try:
        build() if args.action == "build" else review(args)
    except (OSError, KeyError, ValueError) as exc:
        cli.exit(1, str(exc) + "\n")

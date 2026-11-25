"""서울 네 구의 디오라마 건물과 월간 건축물대장을 필지 단위에서만 대조한다."""
import argparse
import csv
import gzip
import hashlib
import io
import json
import re
import sqlite3
import sys
import zipfile
from collections import Counter, defaultdict
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / "artifacts/local"
sys.path.insert(0, str(LOCAL / "public-data/gis-runtime-r1"))
from sagajeong_spatial_presentation import _dbf_text, _read_dbf_header, _read_exact


R3 = LOCAL / "admin-dong-diorama-precision-r3"
REGISTER = LOCAL / "public-data/map-precision-buildings-20261116-r1"
OUTPUT = LOCAL / "public-data/map-precision-enrichment-20261116-r1"
RESOURCE_DIR = Path.home() / "ssalddel/Assets/Ssalddel/WindowsGame/Resources"
FOUR_GU = {"11215", "11230", "11260", "11350"}
ADDRESS = re.compile(r"(.+?)\s+(산\s*)?([0-9]{1,4})(?:-([0-9]{1,4}))?\s*(?:번지)?")


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def check(path, expected):
    actual = sha(path)
    if actual != expected.lower():
        raise ValueError(f"입력 지문 불일치: {path}")
    return actual


def json_out(path, obj):
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(path)


def csv_out(path, rows):
    tmp = path.with_suffix(path.suffix + ".tmp")
    with tmp.open("wb") as raw:
        stream = gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) if path.suffix == ".gz" else raw
        with io.TextIOWrapper(stream, encoding="utf-8-sig", newline="") as text:
            writer = csv.DictWriter(text, fieldnames=rows[0].keys(), lineterminator="\n")
            writer.writeheader()
            writer.writerows(rows)
    tmp.replace(path)


def read_modules(r3):
    buildings, dongs, district_of, revisions = {}, defaultdict(set), {}, {}
    manifest = read(r3 / "candidate-detail/manifest.json")
    raw = manifest["sources"]["data-go-kr-15083092"]
    for meta_path in sorted((r3 / "modules/candidate-2026").glob("*.json")):
        module = read(meta_path)
        code = module["code"]
        if code in district_of:
            raise ValueError("중복 동 코드 " + code)
        district_of[code] = module["district"]
        layers = {row["key"]: row for row in module["layers"]}

        def load(key):
            path = ROOT / layers[key]["path"]
            revisions[str(path.relative_to(ROOT).as_posix())] = check(path, layers[key]["sha256"])
            with gzip.open(path, "rt", encoding="utf-8") as file:
                return json.load(file)

        parcels = load("parcels")
        if (module["edition"] != "candidate-2026" or parcels["edition"] != module["edition"]
                or parcels["boundaryDate"] != manifest["boundaryDate"] or parcels["boundaryDate"] != "2026-10-01"):
            raise ValueError("필지 경계 판본 불일치 " + code)
        if parcels["buildingGeometrySha256"] != layers["buildings"]["sha256"]:
            raise ValueError("필지·건물 참조 불일치 " + code)
        addresses = {p["featureId"]: p for p in parcels["buildingAddresses"]}
        features = load("buildings")["features"]
        if len(addresses) != len(parcels["buildingAddresses"]) or len(addresses) != len(features):
            raise ValueError("필지와 건물 조각 수 불일치 " + code)
        for feature in features:
            p = feature["properties"]
            addr = addresses.pop(feature["id"])
            row = p["sourceRowOneBased"]
            if row != addr["sourceRow"] or p["sourceSha256"].lower() != raw["contentHashSha256"].lower():
                raise ValueError(f"원본 행 불일치 {code}/{row}")
            info = dict(sourceId=p["sourceId"], legal=p["sourceLegalDongCode"], category=p["sourceCategoryCode"],
                        height=p["observedHeightMeters"], floors=p["aboveGroundFloorCount"], pnu=addr["pnuSource"],
                        parcelOK=addr["parcelFieldsConsistent"], legalName=addr["legalNameSource"], useName=addr["useNameSource"])
            if info != buildings.setdefault(row, info):
                raise ValueError(f"같은 원본 행의 속성이 동별로 다름 {row}")
            dongs[code].add(row)
    if len(district_of) != 65 or {code[:5] for code in district_of} != FOUR_GU:
        raise ValueError("예상한 4구 65개 동 범위와 다름")
    return buildings, dongs, district_of, raw, revisions


def source_counts(raw, buildings, runtime_ids):
    archive = ROOT / raw["repositoryRelativePath"]
    check(archive, raw["contentHashSha256"])
    needed = {b["pnu"] for b in buildings.values() if b["pnu"]}
    counts, matches = Counter(), defaultdict(list)
    found = set()
    with zipfile.ZipFile(archive) as z:
        member, = [n for n in z.namelist() if n.lower().endswith(".dbf")]
        with z.open(member) as dbf:
            size, _, width, fields = _read_dbf_header(dbf)
            for row in range(1, size + 1):
                record = _read_exact(dbf, width, "TruncatedBuildingDbf")
                if record[:1] == b"*":
                    continue
                pnu = _dbf_text(record, fields["A2"], "ascii")
                if pnu in needed:
                    counts[pnu] += 1
                source_id = _dbf_text(record, fields["A1"], "ascii")
                if source_id in runtime_ids:
                    matches[source_id].append(row)
                if row in buildings:
                    b = buildings[row]
                    if (source_id or None) != b["sourceId"] or (pnu or None) != b["pnu"]:
                        raise ValueError(f"원본 DBF와 모듈의 속성 차이 {row}")
                    found.add(row)
    if found != set(buildings):
        raise ValueError("DBF에 없는 모듈 건물")
    return counts, matches


def title_pnu(title):
    legal = title["sigunguCd"] + title["bjdongCd"]
    land = {"0": "1", "1": "2"}.get(title["platGbCd"])
    lot, sub = title["bun"], title["ji"]
    if not (re.fullmatch(r"[0-9]{10}", legal) and land and re.fullmatch(r"[0-9]{1,4}", lot or "") and int(lot)>0):
        return None
    if not re.fullmatch(r"[0-9]{1,4}", sub or ""):
        return None
    return legal + land + lot.zfill(4) + sub.zfill(4)


def register_records(folder, parcels):
    report = read(folder / "receipt/regional-verification.json")
    path = folder / "normalized/building-title-2026-09-four-gu.sqlite"
    entry = next(x for x in report["outputs"] if x["path"] == "normalized/" + path.name)
    checksum = check(path, entry["sha256"])
    counts, single, outliers = Counter(), {}, []
    with sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True) as db:
        db.row_factory = sqlite3.Row
        for record in db.execute("SELECT * FROM building_title"):
            pnu = record["pnuCandidate"]
            if pnu and pnu != title_pnu(record):
                raise ValueError("대장 필지 구성 필드와 준비된 후보 불일치: " + str(record["sourceRowNumber"]))
            if record["heightM"] is not None and record["heightM"] > 1000:
                outliers.append({field: record[field] for field in
                                 ("sourceRowNumber", "mgmBldrgstPk", "pnuCandidate", "heit", "groundFloors")})
            if pnu not in parcels or not pnu:
                continue
            counts[pnu] += 1
            if counts[pnu] == 1:
                single[pnu] = dict(record)
            else:
                single.pop(pnu, None)
    quality = read(folder / "receipt/height-value-review.json")
    reviewed = {item["mgmBldrgstPk"] for item in quality["rowsRequiringSourceValueReview"]}
    if quality["sourceSqlite"]["sha256"] != checksum or reviewed != {item["mgmBldrgstPk"] for item in outliers}:
        raise ValueError("건축물대장 높이 이상값 검토 기록과 원본 불일치")
    return counts, single, checksum, outliers


def address_status(building, title):
    text = title["platPlc"] or ""
    m = ADDRESS.fullmatch(text.strip())
    if not m:
        return "RegisterAddressUnconfirmed"
    place, mountain, lot, sub = m.groups()
    pnu = building["pnu"]
    if ("".join(place.split()) != "".join((building["legalName"] or "").split()) or
        ("2" if mountain else "1") != pnu[10] or lot.zfill(4) != pnu[11:15] or (sub or "0").zfill(4) != pnu[15:]):
        return "RegisterAddressConflict"
    return None


def classify(b, source_count, title_count, record):
    pnu = b["pnu"] or ""
    if not re.fullmatch(r"[0-9]{19}", pnu) or not b["parcelOK"] or pnu[:10] != b["legal"]:
        return "SourceParcelUnconfirmed"
    if pnu[:5] not in FOUR_GU:
        return "OutsideFourDistrictRegister"
    if source_count > 1 and title_count > 1:
        return "MultipleBuildingAndRegisterRows"
    if source_count != 1:
        return "MultipleSourceBuildings" if source_count else "SourceParcelUnconfirmed"
    if title_count != 1:
        return "MultipleRegisterRows" if title_count else "NoSameParcelRegister"
    issue = address_status(b, record)
    if issue:
        return issue
    first, second = b["category"], record["mainPurpsCd"]
    use_a = "".join((b["useName"] or "").split())
    use_b = "".join((record["mainPurpsCdNm"] or "").split())
    if not first or not second or not use_a or not use_b or first == "00000" or second == "00000":
        return "UseUnconfirmed"
    if first != second or use_a != use_b:
        return "UseConflict"
    if not record["mgmBldrgstPk"]:
        return "RegisterIdUnconfirmed"
    return "UniqueParcelCandidate"


def old_kind(height, floors):
    if height is not None and height > 0:
        return "height"
    return "floors" if floors is not None and floors > 0 else "symbolic"


def accumulate(counts, kind, record, result):
    counts["buildingCount"] += 1
    counts["original_" + kind] += 1
    if result != "UniqueParcelCandidate":
        counts["held"] += 1
        return
    counts["uniqueParcelCandidates"] += 1
    # 수집 영수증이 표시한 1000 m 초과 관측은 사람이 확인할 때까지 추가 높이 통계에서 제외한다.
    height = record["heightM"]
    if height is not None and height > 1000:
        counts["registerHeightOutlier"] += 1
        height = None
    if height is not None:
        counts["withRegisterHeight"] += 1
        if kind != "height":
            counts["additionalHeightCandidate"] += 1
            counts["symbolicToHeight" if kind == "symbolic" else "floorsToHeight"] += 1
    elif record["groundFloors"] is not None:
        counts["registerFloorsOnly"] += 1
        if kind == "symbolic":
            counts["symbolicToFloors"] += 1
    else:
        counts["registerHeightAndFloorsUnknown"] += 1


def height_review(reviewed, outliers):
    by_parcel = defaultdict(list)
    changes = []
    matched = equal = above_ten = 0
    suspect_parcels = {r["pnuCandidate"] for r in outliers}
    for item in reviewed:
        if item["pnu"] in suspect_parcels:
            by_parcel[item["pnu"]].append(dict(row=item["sourceRowOneBased"], status=item["status"]))
        if item["originalEvidence"] != "height" or item["usableRegisterHeightCandidateM"] is None:
            continue
        matched += 1
        diff = abs(Decimal(str(item["originalHeightM"])) - Decimal(str(item["usableRegisterHeightCandidateM"])))
        if not diff:
            equal += 1
            continue
        if diff >= 10:
            above_ten += 1
        changes.append(dict(sourceRow=item["sourceRowOneBased"], sourceId=item["sourceId"], pnu=item["pnu"],
                            registerPk=item["registerPk"], dongCodes=item["dongCodes"],
                            originalHeightM=item["originalHeightM"], registerHeightM=item["registerHeightM"],
                            absoluteDifferenceM=float(diff)))
    for item in outliers:
        item["matchingR3SourceRows"] = by_parcel[item["pnuCandidate"]]
        item["heightDecision"] = "Above1000mWithheld"
    changes.sort(key=lambda r: (-r["absoluteDifferenceM"], r["sourceRow"]))
    return dict(positiveExistingAndRegisterHeight=matched, sameReportedHeight=equal, differentReportedHeight=len(changes),
                differenceAtLeast10m=above_ten, largestDifferences=changes[:20], registerOutliers=outliers,
                note="The 10 m band is descriptive. Two dated source observations can differ; all matches still require review.")


def execute(args):
    buildings, dongs, districts, raw, layer_hashes = read_modules(args.r3)
    resource = args.resources / "hongdal-dong-buildings.json"
    runtime = read(resource)
    runtime_rows = [b for b in runtime["buildings"] if not b["fill"]]
    roster = read(args.resources / "hongdal-dong-modules.json")
    six = {r["code"] for r in roster["modules"]}
    if len(six) != 6 or {r["region"].split(":")[-1] for r in runtime_rows} != six:
        raise ValueError("현재 6동 실행판 색인 범위가 달라졌음")
    prefix = "vworld:al-d010:"
    source_counts_by_pnu, runtime_matches = source_counts(raw, buildings,
        {b["id"][len(prefix):] for b in runtime_rows if b["id"].startswith(prefix)})
    title_counts, titles, title_hash, outliers = register_records(args.register, {b["pnu"] for b in buildings.values()})
    belonging = defaultdict(list)
    for dong, ids in dongs.items():
        for row in ids:
            belonging[row].append(dong)
    stats, statuses, examples = defaultdict(Counter), Counter(), defaultdict(list)
    reviewed, source_decisions = [], {}
    for row, b in sorted(buildings.items()):
        pnu = b["pnu"]
        n, m = source_counts_by_pnu[pnu], title_counts[pnu]
        record = titles.get(pnu)
        status = classify(b, n, m, record)
        source_decisions[row] = status
        statuses[status] += 1
        kind = old_kind(b["height"], b["floors"])
        selected = record if status == "UniqueParcelCandidate" else None
        register_height = selected["heightM"] if selected else None
        above_limit = register_height is not None and register_height > 1000
        dong_codes = sorted(belonging[row])
        item = dict(sourceRowOneBased=row, sourceId=b["sourceId"], dongCodes="|".join(dong_codes),
                    pnu=pnu, allSeoulSourceRowsAtParcel=n, fourGuRegisterRowsAtParcel=m, status=status,
                    originalEvidence=kind, originalHeightM=b["height"], originalFloors=b["floors"],
                    registerRow=selected["sourceRowNumber"] if selected else None,
                    registerPk=selected["mgmBldrgstPk"] if selected else None,
                    registerHeightRaw=selected["heit"] if selected else None,
                    registerHeightM=register_height,
                    usableRegisterHeightCandidateM=None if above_limit else register_height,
                    registerGroundFloors=selected["groundFloors"] if selected else None,
                    registerUndergroundFloors=selected["undergroundFloors"] if selected else None,
                    registerHeightReview="Above1000mWithheld" if above_limit else None)
        reviewed.append(item)
        if len(examples[status]) < 3:
            examples[status].append(item)
        groups = [("fourGu", "all")] + [("district", k) for k in sorted({districts[c] for c in dong_codes})]
        groups += [("dong", c) for c in dong_codes]
        for key in groups:
            accumulate(stats[key], kind, record, status)

    runtime_links, active_rows = [], {}
    for item in runtime_rows:
        identity = item["id"]
        candidates = runtime_matches.get(identity[len(prefix):], []) if identity.startswith(prefix) else []
        row = candidates[0] if len(candidates)==1 else None
        status = source_decisions.get(row, "NoUniqueR3SourceRow")
        code = item["region"].split(":")[-1]
        brief = dict(runtimeId=identity,dong=code,r3SourceRow=row,originalEvidence=item["heightSource"],status=status)
        runtime_links.append(brief)
        # 같은 지도나 이웃 지도에 반복된 표현 조각은 원본 건물 총계에 다시 더하지 않는다.
        key = ("source", row) if row is not None else ("unresolved-runtime-id", identity)
        if key not in active_rows:
            active_rows[key] = (set(), brief, row)
        dong_ids, baseline, _ = active_rows[key]
        if baseline["originalEvidence"] != brief["originalEvidence"] or baseline["status"] != status:
            raise ValueError("실행판 조각끼리 기존 높이 근거가 다름 " + identity)
        dong_ids.add(code)
    for dong_ids, item, row in active_rows.values():
        b = buildings.get(row)
        record = titles.get(b["pnu"]) if b else None
        for group in [("activeSix","all")] + [("activeDong", code) for code in sorted(dong_ids)]:
            accumulate(stats[group], item["originalEvidence"], record, item["status"])

    names = {code: read(args.r3 / f"modules/candidate-2026/{code}.json")["name"] for code in dongs}
    keys = ("buildingCount", "original_height", "original_floors", "original_symbolic", "held", "uniqueParcelCandidates",
            "withRegisterHeight", "additionalHeightCandidate", "floorsToHeight", "symbolicToHeight", "registerFloorsOnly",
            "symbolicToFloors", "registerHeightAndFloorsUnknown", "registerHeightOutlier")
    summaries = [dict(scope=group, label=key, dongName=names.get(key), **{k: c[k] for k in keys})
                 for (group,key), c in sorted(stats.items())]
    for r in summaries:
        if r["buildingCount"]!=r["held"]+r["uniqueParcelCandidates"] or r["buildingCount"]!=sum(r[k] for k in keys[1:4]):
            raise ValueError("검토 집계 불일치 " + str(r))
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    csv_out(out/"building-candidates.csv.gz", reviewed)
    csv_out(out/"scope-summary.csv", summaries)
    csv_out(out/"active-six-source-index.csv.gz", runtime_links)
    json_out(out/"disposition-examples.json", examples)
    height_audit = height_review(reviewed, outliers)
    json_out(out/"height-value-crosscheck.json", height_audit)
    files = [out/name for name in ("building-candidates.csv.gz", "scope-summary.csv", "active-six-source-index.csv.gz",
                                   "disposition-examples.json", "height-value-crosscheck.json")]
    receipt = dict(clippingBoundaryDate="2026-10-01", geometrySourceDate="2026-08-09", registerMonth="2026-09",
                   geometrySha256=raw["contentHashSha256"].lower(), registerSqliteSha256=title_hash,
                   runtimeIndexSha256=sha(resource),runtimeManifestSha256=sha(args.resources/"hongdal-dong-modules.json"),
                   collectorSha256=sha(Path(__file__)), layerSha256=layer_hashes,
                   statusCounts=statuses, allFourGu=next(r for r in summaries if r["scope"]=="fourGu"),
                   activeSix=next(r for r in summaries if r["scope"]=="activeSix"),
                   geometryRows=len(buildings), geometryDongMemberships=sum(map(len,dongs.values())),
                   activeSixIndexEntries=len(runtime_links),activeSixDistinct=len(active_rows),
                   activeSixSourceUnresolved=sum(r["status"]=="NoUniqueR3SourceRow" for r in runtime_links),
                   distributionApproved=False, unityApplied=False, geometryRightsAtR3=raw["licenseCode"],
                   rule="Count all live Seoul AL_D010 source rows at the PNU, including unrendered rows; count all four-district register rows. Both must equal one, with a consistent legal lot and matching use code and name. Source row numbers refer only to the frozen archive.",
                   limits=["Parcel agreement is a candidate, not a confirmed building ID or present-day height. Missing register evidence and uses are withheld.",
                           "R3 uses the dated secondary 2026 boundary. A building in two dongs counts once overall but in each dong individually. Active-six statistics instead use the actual read-only Unity renderer index and its original height basis.",
                           "Source and register observation dates differ. Active source IDs are linked only when unique across the Seoul DBF. No estimated height is generated.",
                           "Parcel review compares the legal address and lot. A change of street address, building identity, rebuild or survey requires separate evidence."],
                   files={f.name:sha(f) for f in files})
    json_out(out/"receipt.json",receipt)
    print(json.dumps({"fourGu":receipt["allFourGu"],"activeSix":receipt["activeSix"],"statusCounts":statuses},ensure_ascii=False))


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--r3",type=Path,default=R3)
    parser.add_argument("--register",type=Path,default=REGISTER)
    parser.add_argument("--resources",type=Path,default=RESOURCE_DIR)
    parser.add_argument("--output",type=Path,default=OUTPUT)
    execute(parser.parse_args())

"""동결된 전국 표제부를 원문 지역 코드별로 분할하고 재조회용 색인을 만든다.

새 다운로드·설치·DB 반입·게임 적용은 하지 않는다. 원문은 한 번만
스트리밍해 공식 설명에서 위치를 확인한 27열을 보존한다. 코드 판본이
다르면 별도로 표시하며 옛 지역을 현행 지역으로 자동 변환하지 않는다.
"""
from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import io
import json
import math
import re
import sqlite3
import sys
import time
import zipfile
from collections import Counter, defaultdict
from contextlib import ExitStack
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INPUT = ROOT / "artifacts/local/public-data/map-precision-buildings-20261116-r1"
OUTPUT = ROOT / "artifacts/local/public-data/nationwide-diorama-register-20261116-r1"
CODE_LATEST = ROOT / "artifacts/local/data-collection-runs/geography/mois-legal-dong/latest.json"
ZIP_SHA = "bed087c4fd471f073482fa7cd8b30fc5c3f83092a625c05469587a870cb25381"
MEMBER_SHA = "0ecd0ad32f63a4f760eb5fa4ef9885f73f41230816fce1a53234bd50dbd25118"
EXPECTED_ROWS = 8_064_354
ORDINALS = {
    1: "mgmBldrgstPk", 6: "platPlc", 7: "newPlatPlc", 8: "bldNm",
    9: "sigunguCd", 10: "bjdongCd", 11: "platGbCd", 12: "bun", 13: "ji",
    18: "naRoadCd", 19: "naBjdongCd", 20: "naUgrndCd", 21: "naMainBun",
    22: "naSubBun", 23: "dongNm", 24: "mainAtchGbCd", 26: "platArea",
    27: "archArea", 29: "totArea", 35: "mainPurpsCd", 36: "mainPurpsCdNm",
    43: "heit", 44: "grndFlrCnt", 45: "ugrndFlrCnt", 75: "crtnDay",
    76: "rserthqkDsgnApplyYn", 77: "rserthqkAblty",
}
EXTRA = [
    "sourceSidoCode", "sourceLegalDongCode", "legalCodeSnapshotStatus",
    "legalCodeSnapshotName", "pnuCandidate", "pnuCandidateStatus",
    "heightValueStatus", "groundFloorsStatus", "undergroundFloorsStatus",
    "sourceRowNumber", "sourceObservationMonth",
]
FIELDS = list(ORDINALS.values()) + EXTRA
METRICS = [
    "rows", "heightPositiveNumeric", "heightPositiveAtMost1000m",
    "heightZero", "heightNegative", "heightAbove1000m", "heightMissingOrInvalid",
    "groundFloorsPositiveInteger", "undergroundFloorsNonnegativeInteger",
    "heightOrGroundFloorsPresent", "pnuDerived", "legalCodeActive",
    "legalCodeAbolished", "legalCodeUnknown", "duplicatePkOccurrences",
]


def sha(path: Path) -> str:
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def relative(path: Path) -> str:
    return path.resolve().relative_to(ROOT).as_posix()


def json_out(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def numeric(s: str):
    try:
        value = float(s)
        return value if math.isfinite(value) else None
    except ValueError:
        return None


def height_status(s: str) -> str:
    n = numeric(s)
    if n is None:
        return "MissingOrNonFiniteOrNonnumeric"
    if n < 0:
        return "NegativeSourceValueReview"
    if n == 0:
        return "ZeroNotObservedHeight"
    if n > 1000:
        return "Above1000mSourceValueReview"
    return "PositiveNumericNotAccuracyCertified"


def floors(s: str, *, underground=False) -> bool:
    n = numeric(s)
    return n is not None and n.is_integer() and (n >= 0 if underground else n > 0)


def pnu(a):
    sg, bj, gb, bun, ji = a[8:13]
    if not re.fullmatch(r"\d{5}", sg) or not re.fullmatch(r"\d{5}", bj):
        return "", "InvalidOrMissingLegalCode"
    if gb not in {"0", "1"}:
        return "", "NonParcelLandCategoryOrMissing"
    if not re.fullmatch(r"\d{1,4}", bun) or not re.fullmatch(r"\d{1,4}", ji):
        return "", "InvalidOrMissingLotNumbers"
    if int(bun) == 0:
        return "", "ZeroMainLotNumber"
    return sg + bj + {"0": "1", "1": "2"}[gb] + bun.zfill(4) + ji.zfill(4), "DerivedCandidateNotBuildingMatch"


class CsvGzip:
    """파일 gzip과 압축 전 바이트 지문을 동시에 만든다."""
    def __init__(self, path):
        self.path = path
        path.parent.mkdir(parents=True, exist_ok=True)
        self.raw = path.open("xb")
        self.zipped = gzip.GzipFile(filename="", fileobj=self.raw, mode="wb", compresslevel=1, mtime=0)
        self.digest = hashlib.sha256()
        self.writer = csv.writer(self, lineterminator="\n")
        self.writer.writerow(FIELDS)
        self.rows = 0
        self.first = None
        self.last = None

    def write(self, text):
        data = text.encode("utf-8")
        self.digest.update(data)
        self.zipped.write(data)
        return len(text)

    def row(self, values):
        self.writer.writerow(values)
        self.rows += 1
        if self.first is None:
            self.first = list(map(str, values))
        self.last = list(map(str, values))

    def close(self):
        self.zipped.close()
        self.raw.close()
        return {"path": self.path.relative_to(OUTPUT).as_posix(), "rows": self.rows,
                "bytes": self.path.stat().st_size, "sha256": sha(self.path),
                "uncompressedSha256": self.digest.hexdigest(), "first": self.first, "last": self.last}


def code_snapshot():
    latest = json.loads(CODE_LATEST.read_text("utf-8"))
    body = CODE_LATEST.parent / latest["body"]
    assert sha(body) == latest["sha256"]
    with zipfile.ZipFile(body) as archive:
        names = archive.namelist()
        assert len(names) == 1
        text = archive.read(names[0]).decode("cp949")
    rows = list(csv.DictReader(io.StringIO(text), delimiter="\t"))
    assert len(rows) == latest["recordCount"]
    codes = {r["법정동코드"]: r for r in rows}
    assert len(codes) == len(rows)
    active_sido = {}
    for code, r in codes.items():
        if r["폐지여부"] == "존재":
            active_sido.setdefault(code[:2], r["법정동명"].split()[0])
    return codes, active_sido, {
        "latestMetadata": relative(CODE_LATEST), "body": relative(body),
        "sha256": latest["sha256"], "kind": "dataset", "recordCount": len(rows),
        "collectedAtUtc": latest["collectedAt"], "effectiveDate": latest.get("evidenceAsOf"),
        "dateLimit": "수집시점의 보유 코드와 대조했다. 파일은 별도 시행일을 제공하지 않으며 오늘의 경계/코드 권위로 승격하지 않는다.",
        "activeSidoCodes": active_sido, "redistributionApproved": False,
    }


def status_of(code, codes):
    r = codes.get(code)
    if not r:
        return "UnknownInCodeSnapshot", ""
    return ("ActiveInCodeSnapshot" if r["폐지여부"] == "존재" else "AbolishedInCodeSnapshot"), r["법정동명"]


def sido_prefix(sigungu):
    return sigungu[:2] if re.fullmatch(r"\d{5}", sigungu) else "unknown"


def source_description():
    source_zip = INPUT / "raw/building-title-2026-09-national.zip"
    schema_path = INPUT / "raw/official-schema-dom-selected.json"
    return {"sourceId": "source:building-hub", "datasetId": "building-register-title-0303", "kind": "dataset", "observationMonth": "2026-09", "zipPath": relative(source_zip), "zipBytes": source_zip.stat().st_size, "zipSha256": ZIP_SHA, "member": "mart_djy_03.txt", "memberSha256": MEMBER_SHA, "officialSchemaEvidence": {"path": relative(schema_path), "sha256": sha(schema_path), "confirmedColumns": 27}, "license": "이용허락범위 제한 없음", "licenseEvidence": relative(INPUT / "raw/data-go-kr-15044720.html"), "licenseCatalogUrl": "https://www.data.go.kr/data/15044720/fileData.do", "newDownloadPerformed": False, "crs": None, "coordinatesPresent": False, "distributionApproved": False}


def self_check():
    a = [""] * 77
    a[8:13] = ["11260", "10100", "0", "59", "45"]
    assert pnu(a) == ("1126010100100590045", "DerivedCandidateNotBuildingMatch")
    a[10] = "1"
    assert pnu(a)[0] == "1126010100200590045"
    a[10] = "2"
    assert pnu(a)[0] == ""
    assert height_status("-4.8") == "NegativeSourceValueReview"
    assert height_status("0") == "ZeroNotObservedHeight"
    assert height_status("7100") == "Above1000mSourceValueReview"
    assert not floors("0") and floors("0", underground=True)
    assert not floors("1.5")
    assert sido_prefix("11260") == "11"
    assert sido_prefix("0") == "unknown"


def file_verify(entry):
    p = OUTPUT / entry["path"]
    assert sha(p) == entry["sha256"], entry["path"]
    digest = hashlib.sha256()
    newline_count = 0
    tail = b""
    with gzip.open(p, "rb") as f:
        while chunk := f.read(1024 * 1024):
            digest.update(chunk)
            newline_count += chunk.count(b"\n")
            tail = (tail + chunk)[-32768:]
    assert digest.hexdigest() == entry["uncompressedSha256"], entry["path"]
    assert newline_count - 1 == entry["rows"], (entry["path"], newline_count, entry["rows"])
    if entry["rows"]:
        with gzip.open(p, "rt", encoding="utf-8", newline="") as f:
            reader = csv.reader(f)
            assert next(reader) == FIELDS
            assert next(reader) == entry["first"]
        last_line = tail.rstrip(b"\n").split(b"\n")[-1].decode("utf-8")
        assert next(csv.reader([last_line])) == entry["last"]
    return {"path": entry["path"], "rows": entry["rows"], "compressedHash": "passed", "uncompressedHash": "passed", "rowCount": "passed", "firstAndLastRecord": "passed"}


def make_index(counters, sido_counters, partitions, codes, code_evidence, source):
    path = OUTPUT / "index.sqlite"
    assert not path.exists()
    db = sqlite3.connect(path)
    db.executescript("""
CREATE TABLE source_metadata (key TEXT PRIMARY KEY,value_json TEXT NOT NULL);
CREATE TABLE partition_file (kind TEXT,source_code TEXT,path TEXT,rows INTEGER,bytes INTEGER,sha256 TEXT,uncompressed_sha256 TEXT,PRIMARY KEY(kind,source_code));
CREATE TABLE district_summary (source_sigungu_code TEXT PRIMARY KEY,source_sido_code TEXT,name_from_code_snapshot TEXT,code_snapshot_status TEXT,rows INTEGER,height_positive_numeric INTEGER,height_positive_at_most_1000m INTEGER,ground_floors_positive_integer INTEGER,height_or_ground_floors_present INTEGER,pnu_derived INTEGER,legal_code_active INTEGER,legal_code_abolished INTEGER,legal_code_unknown INTEGER,height_negative INTEGER,height_above_1000m INTEGER,stats_json TEXT,sido_csv_path TEXT,seoul_district_csv_path TEXT);
CREATE TABLE sido_summary (source_sido_code TEXT PRIMARY KEY,name_from_code_snapshot TEXT,code_snapshot_status TEXT,rows INTEGER,stats_json TEXT,csv_path TEXT);
""")
    for key, value in {"source": source, "codeSnapshot": code_evidence, "columns": FIELDS, "usage": {"distributionApproved": False, "noAutomaticCodeRemapping": True, "crs": None, "geometryPresent": False}}.items():
        db.execute("INSERT INTO source_metadata VALUES (?,?)", (key, json.dumps(value, ensure_ascii=False)))
    for (kind, code), info in partitions.items():
        db.execute("INSERT INTO partition_file VALUES (?,?,?,?,?,?,?)", (kind, code, info["path"], info["rows"], info["bytes"], info["sha256"], info["uncompressedSha256"]))
    for sg, c in sorted(counters.items()):
        state, name = status_of(sg + "00000", codes)
        si = sido_prefix(sg)
        sidopath = partitions[("sido", si)]["path"]
        seoulpath = partitions.get(("seoul-district", sg), {}).get("path")
        values = [sg, si, name, state] + [c[k] for k in ["rows", "heightPositiveNumeric", "heightPositiveAtMost1000m", "groundFloorsPositiveInteger", "heightOrGroundFloorsPresent", "pnuDerived", "legalCodeActive", "legalCodeAbolished", "legalCodeUnknown", "heightNegative", "heightAbove1000m"]] + [json.dumps(dict(c), ensure_ascii=False), sidopath, seoulpath]
        db.execute("INSERT INTO district_summary VALUES (" + ",".join(["?"] * len(values)) + ")", values)
    for si, c in sorted(sido_counters.items()):
        state, name = status_of(si + "00000000", codes)
        if not name:
            name = code_evidence["activeSidoCodes"].get(si, "")
            state = "ActivePrefixInCodeSnapshot" if name else state
        db.execute("INSERT INTO sido_summary VALUES (?,?,?,?,?,?)", (si, name, state, c["rows"], json.dumps(dict(c), ensure_ascii=False), partitions[("sido", si)]["path"]))
    db.execute("CREATE INDEX ix_district_sido ON district_summary(source_sido_code)")
    db.commit()
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert db.execute("SELECT SUM(rows) FROM district_summary").fetchone()[0] == EXPECTED_ROWS
    db.close()


def query_district(code):
    db = sqlite3.connect((OUTPUT / "index.sqlite").as_uri() + "?mode=ro", uri=True)
    db.row_factory = sqlite3.Row
    row = db.execute("SELECT * FROM district_summary WHERE source_sigungu_code=?", (code,)).fetchone()
    db.close()
    if row is None:
        raise ValueError("원문에 없는 시군구 코드: " + code)
    result = dict(row)
    result["stats_json"] = json.loads(result["stats_json"])
    return result


def extract_district(code, target=None):
    info = query_district(code)
    target = target or OUTPUT / "extracts" / f"sigungu-{code}.csv.gz"
    assert target.resolve().is_relative_to(OUTPUT.resolve()), "추출 대상은 전용 폴더 안이어야 한다"
    assert not target.exists(), "기존 추출 파일을 덮어쓰지 않는다"
    writer = CsvGzip(target)
    source = OUTPUT / (info["seoul_district_csv_path"] or info["sido_csv_path"])
    with gzip.open(source, "rt", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            if row["sigunguCd"] == code:
                writer.row([row[k] for k in FIELDS])
    entry = writer.close()
    assert entry["rows"] == info["rows"]
    return entry


def build():
    started = time.monotonic()
    self_check()
    if OUTPUT.exists() and any(OUTPUT.iterdir()):
        raise ValueError("전용 출력 폴더가 비어 있지 않다. 기존 산출물을 덮어쓰지 않는다")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source_zip = INPUT / "raw/building-title-2026-09-national.zip"
    schema_path = INPUT / "raw/official-schema-dom-selected.json"
    schema = json.loads(schema_path.read_text("utf-8-sig"))
    assert {x["ordinal"] for x in schema["selectedColumns"]} == set(ORDINALS)
    assert sha(source_zip) == ZIP_SHA
    codes, active_sido, code_evidence = code_snapshot()
    source = source_description()
    writers = {}
    summaries = defaultdict(Counter)
    sido_summaries = defaultdict(Counter)
    global_stats = Counter()
    seen_pk = {}
    pk_duplicate_count = 0
    first_pk_duplicates = []
    samples = []
    sample_count = Counter()
    member_digest = hashlib.sha256()
    total_bytes = 0
    special = {}
    with ExitStack() as stack:
        special["code-review"] = CsvGzip(OUTPUT / "review/code-status-not-active.csv.gz")
        special["height-review"] = CsvGzip(OUTPUT / "review/height-negative-or-above1000m.csv.gz")
        duplicates = stack.enter_context((OUTPUT / "review/duplicate-registry-pk.csv").open("x", encoding="utf-8-sig", newline=""))
        dw = csv.writer(duplicates, lineterminator="\n")
        dw.writerow(["mgmBldrgstPk", "firstSourceRowNumber", "repeatedSourceRowNumber", "sigunguCd"])
        quarantine_raw = stack.enter_context((OUTPUT / "review/unexpected-source-rows.jsonl").open("x", encoding="utf-8"))
        archive = stack.enter_context(zipfile.ZipFile(source_zip))
        assert archive.namelist() == ["mart_djy_03.txt"]
        zip_info = archive.getinfo("mart_djy_03.txt")
        stream = stack.enter_context(archive.open(zip_info))
        for rownum, line in enumerate(stream, 1):
            member_digest.update(line)
            total_bytes += len(line)
            global_stats["sourceRows"] += 1
            a = line.decode("utf-8", errors="strict").rstrip("\r\n").split("|")
            if len(a) != 77:
                global_stats["unexpectedSourceFieldCountRows"] += 1
                quarantine_raw.write(json.dumps({"sourceRowNumber": rownum, "fieldCount": len(a), "rawLine": line.decode("utf-8"), "rawLineSha256": hashlib.sha256(line).hexdigest()}, ensure_ascii=False) + "\n")
                continue
            sg = a[8]
            si = sido_prefix(sg)
            legal = sg + a[9]
            legal_state, legal_name = status_of(legal, codes)
            pn, pn_state = pnu(a)
            hstate = height_status(a[42])
            hp = hstate in {"PositiveNumericNotAccuracyCertified", "Above1000mSourceValueReview"}
            gp, up = floors(a[43]), floors(a[44], underground=True)
            duplicate = a[0] in seen_pk
            if duplicate:
                pk_duplicate_count += 1
                dw.writerow([a[0], seen_pk[a[0]], rownum, sg])
                if len(first_pk_duplicates) < 20:
                    first_pk_duplicates.append({"mgmBldrgstPk": a[0], "firstSourceRowNumber": seen_pk[a[0]], "repeatedSourceRowNumber": rownum})
            else:
                seen_pk[a[0]] = rownum
            values = [a[i - 1] for i in ORDINALS] + [si, legal, legal_state, legal_name, pn, pn_state, hstate, "PositiveInteger" if gp else "MissingOrNonpositiveOrNoninteger", "NonnegativeInteger" if up else "MissingOrNegativeOrNoninteger", rownum, "2026-09"]
            if si not in writers:
                writers[("sido", si)] = CsvGzip(OUTPUT / "sido" / f"sido-{si}.csv.gz")
                # 같은 시도 스트림의 존재 여부를 위한 짧은 별칭이다.
                writers[si] = writers[("sido", si)]
            writers[si].row(values)
            if si == "11":
                key = ("seoul-district", sg)
                if key not in writers:
                    writers[key] = CsvGzip(OUTPUT / "seoul-district" / f"sigungu-{sg}.csv.gz")
                writers[key].row(values)
            if legal_state != "ActiveInCodeSnapshot":
                special["code-review"].row(values)
            if hstate in {"NegativeSourceValueReview", "Above1000mSourceValueReview"}:
                special["height-review"].row(values)
                if sample_count[(si, hstate)] < 2:
                    samples.append(values)
                    sample_count[(si, hstate)] += 1
            metrics = {
                "rows": 1, "heightPositiveNumeric": hp,
                "heightPositiveAtMost1000m": hstate == "PositiveNumericNotAccuracyCertified",
                "heightZero": hstate == "ZeroNotObservedHeight",
                "heightNegative": hstate == "NegativeSourceValueReview",
                "heightAbove1000m": hstate == "Above1000mSourceValueReview",
                "heightMissingOrInvalid": hstate == "MissingOrNonFiniteOrNonnumeric",
                "groundFloorsPositiveInteger": gp, "undergroundFloorsNonnegativeInteger": up,
                "heightOrGroundFloorsPresent": hp or gp, "pnuDerived": bool(pn),
                "legalCodeActive": legal_state == "ActiveInCodeSnapshot",
                "legalCodeAbolished": legal_state == "AbolishedInCodeSnapshot",
                "legalCodeUnknown": legal_state == "UnknownInCodeSnapshot",
                "duplicatePkOccurrences": duplicate,
            }
            metrics = {key: int(value) for key, value in metrics.items()}
            summaries[sg].update(metrics)
            sido_summaries[si].update(metrics)
            if rownum % 250000 == 0:
                print(json.dumps({"scannedRows": rownum, "sourceSidoPartitions": len(sido_summaries), "elapsedSeconds": round(time.monotonic() - started, 1)}), flush=True)
    assert global_stats["sourceRows"] == EXPECTED_ROWS
    assert member_digest.hexdigest() == MEMBER_SHA
    assert total_bytes == zip_info.file_size
    assert not global_stats["unexpectedSourceFieldCountRows"]
    partitions = {key: writer.close() for key, writer in writers.items() if isinstance(key, tuple)}
    reviews = {key: writer.close() for key, writer in special.items()}
    del seen_pk
    total_rows = sum(x["rows"] for (kind, _), x in partitions.items() if kind == "sido")
    assert total_rows == EXPECTED_ROWS
    assert len([1 for kind, _ in partitions if kind == "seoul-district"]) == 25
    assert sum(x["rows"] for (kind, _), x in partitions.items() if kind == "seoul-district") == 584691
    summary_path = OUTPUT / "district-summary.csv"
    with summary_path.open("x", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["sourceSigunguCode", "sourceSidoCode", "nameFromCodeSnapshot", "codeSnapshotStatus"] + METRICS)
        for sg, c in sorted(summaries.items()):
            state, name = status_of(sg + "00000", codes)
            writer.writerow([sg, sido_prefix(sg), name, state] + [c[k] for k in METRICS])
    for entry in partitions.values():
        if entry["first"]:
            samples.append(entry["first"])
            samples.append(entry["last"])
    with (OUTPUT / "verification-samples.csv").open("x", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(FIELDS)
        w.writerows(samples)
    finalize_result(source, code_evidence, codes, active_sido, summaries, sido_summaries,
                    partitions, reviews, pk_duplicate_count, first_pk_duplicates,
                    total_bytes, started)


def finalize_result(source, code_evidence, codes, active_sido, summaries, sido_summaries,
                    partitions, reviews, pk_duplicate_count, first_pk_duplicates,
                    total_bytes, started, *, resumed=False):
    source_zip = INPUT / "raw/building-title-2026-09-national.zip"
    with zipfile.ZipFile(source_zip) as archive:
        zip_info = archive.getinfo("mart_djy_03.txt")
    total_rows = sum(x["rows"] for (kind, _), x in partitions.items() if kind == "sido")
    assert total_rows == EXPECTED_ROWS
    if resumed and (OUTPUT / "index.sqlite").exists():
        failed_index = OUTPUT / "verification/index-before-unknown-code-fix.sqlite"
        assert not failed_index.exists()
        (OUTPUT / "index.sqlite").rename(failed_index)
    make_index(summaries, sido_summaries, partitions, codes, code_evidence, source)
    print(json.dumps({"phase": "outputVerification", "sourceRows": total_rows, "partitionFiles": len(partitions), "elapsedSeconds": round(time.monotonic() - started, 1)}), flush=True)
    verification = [file_verify(entry) for entry in [*partitions.values(), *reviews.values()]]
    extracted = extract_district("11260", OUTPUT / "verification/sigungu-11260.csv.gz")
    verification.append(file_verify(extracted))
    assert extracted["rows"] == 27282
    assert sha(source_zip) == ZIP_SHA
    total_stats = Counter()
    for c in sido_summaries.values():
        total_stats.update(c)
    receipt = {
        "schemaVersion": "national-building-register-partitions.v1", "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "status": "PreparedAndVerifiedNoGameApplication", "source": source, "codeSnapshot": code_evidence,
        "script": {"path": relative(Path(__file__)), "sha256": sha(Path(__file__))},
        "sourceOnePassNormalization": True, "sourceRows": EXPECTED_ROWS,
        "finalizationResumedFromClosedPartitionFiles": resumed,
        "normalizationScriptBeforeIndexFix": ({"path": "verification/normalization-script-before-index-fix.py", "sha256": sha(OUTPUT / "verification/normalization-script-before-index-fix.py")} if resumed else None),
        "sourceMemberBytes": total_bytes, "sourceMemberCRC32": f"{zip_info.CRC:08x}", "sourceMemberFullReadCRCVerified": True,
        "sourceRowsPreserved": total_rows == EXPECTED_ROWS, "sourceSidoCodes": sorted(sido_summaries),
        "sourceSidoPartitionCount": len(sido_summaries), "sourceSigunguCount": len(summaries),
        "seoulDistrictFileCount": 25, "seoulRows": 584691,
        "activeSnapshotSidoWithoutSourceRows": sorted(set(active_sido) - set(sido_summaries)),
        "sourceSidoNotActiveInSnapshot": sorted(set(sido_summaries) - set(active_sido)),
        "globalMetrics": dict(total_stats), "uniqueRegistryPk": EXPECTED_ROWS - pk_duplicate_count,
        "duplicatePkOccurrences": pk_duplicate_count, "firstDuplicatePkSamples": first_pk_duplicates,
        "sidoMetrics": {si: dict(c) for si, c in sorted(sido_summaries.items())},
        "partitions": [{"kind": kind, "sourceCode": code, **{k: v for k, v in x.items() if k not in {"first", "last"}}} for (kind, code), x in sorted(partitions.items())],
        "reviewFiles": {key: {k: v for k, v in entry.items() if k not in {"first", "last"}} for key, entry in reviews.items()},
        "verification": {"selfChecks": "passed", "globalDuplicatePkCheckedUsingExactStrings": True, "inputZipAndMemberHashes": "passed", "outputFiles": verification, "indexSqliteIntegrityCheck": "ok", "jungnangQueryAndExtractionRows": extracted["rows"]},
        "normalization": {"officiallyConfirmedSourceColumns": 27, "fieldNames": FIELDS, "allSelectedSourceValuesPreservedAsText": True, "sourceMemberAndRowNumberRetained": True, "pnuFormula": "sigunguCd+bjdongCd+(platGbCd 0->1,1->2)+bun4+ji4; invalid/zero main lot are blank", "pnuIsParcelCandidateNotBuildingMatch": True, "heightQualityPolicy": "0/음수/결측을 실제 높이로 채우지 않는다. 1000m 초과는 검토 목록에 남기며 그 이하는 정확성 인증이 아니다.", "automaticCodeMappingPerformed": False, "administrativeDongOrGeometryAssignmentPerformed": False, "syntheticHeightsGenerated": False},
        "limits": ["원문 시도·시군구 코드는 보존하며 현행16시도에 자동 재매핑하지 않는다.", "법정동 코드와 행정동 경계는 다른 자료다. 현재 행정동/건물 도형 매칭은 하지 않았다.", "대장 PK는 GIS polygon sourceId나 25자리 도로명주소 건물관리번호와 같지 않다.", "같은 PNU에 여러 대장/건물이 있을 수 있다.", "제품·공용DB·기존예약·기존4구자료·공용문서를 바꾸지 않았다."],
        "elapsedSeconds": round(time.monotonic() - started, 2),
    }
    json_out(OUTPUT / "receipt.json", receipt)
    files = [{"path": p.relative_to(OUTPUT).as_posix(), "bytes": p.stat().st_size, "sha256": sha(p)} for p in sorted(OUTPUT.rglob("*")) if p.is_file()]
    byte_total = sum(x["bytes"] for x in files)
    assert byte_total < 2 * 1024**3, byte_total
    json_out(OUTPUT / "inventory.json", {"createdAtUtc": datetime.now(timezone.utc).isoformat(), "files": files, "fileCount": len(files), "bytes": byte_total, "under2GiB": True})
    print(json.dumps({"status": receipt["status"], "rows": EXPECTED_ROWS, "sourceSidoCodes": receipt["sourceSidoCodes"], "sourceSidoNotActiveInSnapshot": receipt["sourceSidoNotActiveInSnapshot"], "activeSnapshotSidoWithoutSourceRows": receipt["activeSnapshotSidoWithoutSourceRows"], "districts": len(summaries), "globalMetrics": dict(total_stats), "duplicatePkOccurrences": pk_duplicate_count, "outputBytes": byte_total, "elapsedSeconds": receipt["elapsedSeconds"]}, ensure_ascii=False), flush=True)


def inspect_closed_partition(path):
    digest = hashlib.sha256()
    newlines = 0
    tail = b""
    with gzip.open(path, "rb") as f:
        while chunk := f.read(1024 * 1024):
            digest.update(chunk)
            newlines += chunk.count(b"\n")
            tail = (tail + chunk)[-32768:]
    with gzip.open(path, "rt", encoding="utf-8", newline="") as f:
        reader = csv.reader(f)
        assert next(reader) == FIELDS
        first = next(reader, None)
    last = next(csv.reader([tail.rstrip(b"\n").split(b"\n")[-1].decode("utf-8")])) if newlines > 1 else None
    return {"path": path.relative_to(OUTPUT).as_posix(), "rows": newlines - 1,
            "bytes": path.stat().st_size, "sha256": sha(path),
            "uncompressedSha256": digest.hexdigest(), "first": first, "last": last}


def resume_finalization():
    """원문 재분할 없이 닫힌 분할 파일과 통계를 재검사해 색인 단계를 재개한다."""
    started = time.monotonic()
    self_check()
    assert not (OUTPUT / "receipt.json").exists()
    codes, active_sido, code_evidence = code_snapshot()
    summaries, sido_summaries = {}, defaultdict(Counter)
    def integer(value):
        return 1 if value == "True" else 0 if value == "False" else int(value)
    with (OUTPUT / "district-summary.csv").open("r", encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            sg = row["sourceSigunguCode"]
            assert sg not in summaries
            summaries[sg] = Counter({key: integer(row[key]) for key in METRICS})
            sido_summaries[sido_prefix(sg)].update(summaries[sg])
    assert sum(c["rows"] for c in summaries.values()) == EXPECTED_ROWS
    assert summaries["0"]["rows"] == 1
    with (OUTPUT / "district-summary.csv").open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["sourceSigunguCode", "sourceSidoCode", "nameFromCodeSnapshot", "codeSnapshotStatus"] + METRICS)
        for sg, c in sorted(summaries.items()):
            state, name = status_of(sg + "00000", codes)
            w.writerow([sg, sido_prefix(sg), name, state] + [int(c[k]) for k in METRICS])
    partitions = {}
    for folder, kind, prefix in [("sido", "sido", "sido-"), ("seoul-district", "seoul-district", "sigungu-")]:
        for path in sorted((OUTPUT / folder).glob("*.csv.gz")):
            code = path.name.removeprefix(prefix).removesuffix(".csv.gz")
            partitions[(kind, code)] = inspect_closed_partition(path)
    reviews = {key: inspect_closed_partition(OUTPUT / file) for key, file in {
        "code-review": "review/code-status-not-active.csv.gz",
        "height-review": "review/height-negative-or-above1000m.csv.gz",
    }.items()}
    duplicates, examples = 0, []
    with (OUTPUT / "review/duplicate-registry-pk.csv").open("r", encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            duplicates += 1
            if len(examples) < 20:
                examples.append(row)
    assert duplicates == sum(c["duplicatePkOccurrences"] for c in summaries.values())
    json_out(OUTPUT / "verification/initial-index-failure.json", {
        "stage": "make_index after full source partition and full exact-PK scan",
        "exception": "KeyError: ('sido', '0')", "unexpectedSourceSigunguCd": "0", "rows": 1,
        "resolution": "시군구 코드 형식이 아닌 값은 시도 분할과 색인에서 모두 unknown으로 보존. 원문을 재분할하거나 코드를 추정하지 않음.",
        "sourceRenormalizationPerformed": False,
    })
    with zipfile.ZipFile(INPUT / "raw/building-title-2026-09-national.zip") as archive:
        total_bytes = archive.getinfo("mart_djy_03.txt").file_size
    finalize_result(source_description(), code_evidence, codes, active_sido, summaries,
                    sido_summaries, partitions, reviews, duplicates, examples,
                    total_bytes, started, resumed=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=["build", "resume-finalization", "query", "extract", "verify"], default="build")
    parser.add_argument("--sigungu", help="원문5자리 시군구 코드. 현행코드로 자동 변환하지 않는다.")
    args = parser.parse_args()
    if args.mode == "build":
        build()
    elif args.mode == "resume-finalization":
        resume_finalization()
    elif args.mode == "query":
        print(json.dumps(query_district(args.sigungu), ensure_ascii=False, indent=2))
    elif args.mode == "extract":
        print(json.dumps(extract_district(args.sigungu), ensure_ascii=False, indent=2))
    else:
        inventory = json.loads((OUTPUT / "inventory.json").read_text("utf-8"))
        for entry in inventory["files"]:
            assert sha(OUTPUT / entry["path"]) == entry["sha256"], entry["path"]
        print(json.dumps({"status": "InventoryHashesVerified", "files": inventory["fileCount"], "bytes": inventory["bytes"]}))


if __name__ == "__main__":
    main()

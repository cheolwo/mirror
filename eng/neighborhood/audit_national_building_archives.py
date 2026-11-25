"""수령된 전국 건물 SHP ZIP을 읽기 전용으로 검사하고 원본 준비표를 만든다.

CRC/해시/형식 검사는 건물 정확도, 현재 경계 부합, 고유 건물 수 또는
전국 디오라마 완성을 뜻하지 않는다. 다운로드 중 파일은 동결하지 않는다.
"""
from __future__ import annotations

import argparse
import codecs
import hashlib
import json
import math
import re
import struct
import time
import zipfile
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "artifacts/local/public-data/nationwide-diorama-buildings-20261116-r1"
RAW = BASE / "raw"
RECEIPT = BASE / "receipt"
CACHE = RECEIPT / "archive-audit-cache.json"
ALGORITHM = "building-shp-archive-audit.v1"


def sha(path):
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def json_out(path, value):
    assert path.parent == RECEIPT and path.name.startswith("archive-audit")
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    tmp.replace(path)


def identity(path):
    s = path.stat()
    return {"bytes": s.st_size, "mtimeNs": s.st_mtime_ns, "ctimeNs": s.st_ctime_ns}


class HashReader:
    def __init__(self, stream):
        self.stream = stream
        self.digest = hashlib.sha256()
        self.bytes = 0

    def read(self, n=-1):
        data = self.stream.read(n)
        self.digest.update(data)
        self.bytes += len(data)
        return data

    def exact(self, n):
        data = self.read(n)
        if len(data) != n:
            raise ValueError(f"구성파일이 예상 길이보다 짧음: {n} -> {len(data)}")
        return data


def finite_box(values):
    return [v if math.isfinite(v) else None for v in values]


def shape_header(data, member_size):
    if len(data) != 100:
        raise ValueError("SHP/SHX 헤더100바이트 누락")
    file_code = struct.unpack_from(">I", data, 0)[0]
    length_words = struct.unpack_from(">I", data, 24)[0]
    version, stype = struct.unpack_from("<II", data, 28)
    xy = struct.unpack_from("<4d", data, 36)
    z_m = struct.unpack_from("<4d", data, 68)
    valid = (file_code == 9994 and version == 1000 and length_words * 2 == member_size
             and all(math.isfinite(v) for v in xy) and xy[0] <= xy[2] and xy[1] <= xy[3])
    return {"fileCode": file_code, "version": version, "shapeType": stype,
            "declaredBytes": length_words * 2, "memberBytes": member_size,
            "xyBounds": finite_box(xy), "zAndMBounds": finite_box(z_m),
            "headerStructureValid": valid}


def parse_shp(reader, member_size):
    header = shape_header(reader.exact(100), member_size)
    offsets = hashlib.sha256()
    count = 0
    types = Counter()
    invalid_box = 0
    outside = 0
    invalid_polygon_lengths = 0
    unexpected_number = 0
    observed_box = None
    first = None
    last = None
    while True:
        start = reader.bytes
        rec_header = reader.read(8)
        if not rec_header:
            break
        if len(rec_header) != 8:
            raise ValueError("SHP 레코드 헤더 길이 불일치")
        number, words = struct.unpack(">II", rec_header)
        if words * 2 > member_size or words < 2:
            raise ValueError("SHP 레코드 길이 범위 오류")
        content = reader.exact(words * 2)
        count += 1
        unexpected_number += number != count
        offsets.update(struct.pack(">II", start // 2, words))
        stype = struct.unpack_from("<I", content, 0)[0]
        types[stype] += 1
        sample = {"rowOneBased": count, "recordNumber": number, "shapeType": stype,
                  "contentBytes": len(content), "recordContentSha256": hashlib.sha256(content).hexdigest()}
        if stype in {3, 5, 13, 15, 23, 25}:
            if len(content) < 44:
                raise ValueError("선/면 레코드 기본 구조 누락")
            box = struct.unpack_from("<4d", content, 4)
            parts, points = struct.unpack_from("<II", content, 36)
            sample.update(xyBounds=finite_box(box), parts=parts, points=points)
            invalid_polygon_lengths += len(content) < 44 + parts * 4 + points * 16
            if not all(math.isfinite(v) for v in box) or box[0] > box[2] or box[1] > box[3]:
                invalid_box += 1
            else:
                if observed_box is None:
                    observed_box = list(box)
                else:
                    observed_box = [min(observed_box[0], box[0]), min(observed_box[1], box[1]),
                                    max(observed_box[2], box[2]), max(observed_box[3], box[3])]
                hb = header["xyBounds"]
                if all(v is not None for v in hb):
                    outside += box[0] < hb[0] - 1e-6 or box[1] < hb[1] - 1e-6 or box[2] > hb[2] + 1e-6 or box[3] > hb[3] + 1e-6
        if first is None:
            first = sample
        last = sample
    return {"header": header, "streamedGeometryRecordCount": count,
            "shapeTypeCounts": dict(types), "recordOffsetsAndLengthsSha256": offsets.hexdigest(),
            "nonSequentialRecordNumbers": unexpected_number,
            "invalidRecordEnvelopeCount": invalid_box, "recordEnvelopeOutsideFileHeaderCount": outside,
            "polygonCoordinateArrayTooShortCount": invalid_polygon_lengths,
            "observedRecordEnvelopeUnion": observed_box,
            "representativeGeometryRecords": [first, last] if first else []}


def parse_shx(reader, member_size):
    header = shape_header(reader.exact(100), member_size)
    digest = hashlib.sha256()
    total = 0
    first = None
    last = None
    while chunk := reader.read(1024 * 1024):
        digest.update(chunk)
        total += len(chunk)
        if first is None and len(chunk) >= 8:
            first = list(struct.unpack(">II", chunk[:8]))
        if len(chunk) >= 8:
            last = list(struct.unpack(">II", chunk[-8:]))
    return {"header": header, "entryBytesMultipleOfEight": total % 8 == 0,
            "entryCount": total // 8, "entryBytesSha256": digest.hexdigest(),
            "firstOffsetWordsAndContentWords": first, "lastOffsetWordsAndContentWords": last}


def decode_record(record, fields, encoding):
    result = {}
    for field in fields:
        data = record[field["byteOffset"]:field["byteOffset"] + field["length"]].rstrip(b" \x00")
        try:
            result[field["name"]] = data.decode(encoding or "ascii", errors="strict")
        except UnicodeDecodeError:
            result[field["name"]] = {"undecodedRawHex": data.hex(), "encodingAttempt": encoding or "ascii"}
    return result


def parse_dbf(reader, member_size, encoding):
    head = reader.exact(32)
    count, header_length, record_length = struct.unpack_from("<IHH", head, 4)
    if header_length < 33 or record_length < 1:
        raise ValueError("DBF 헤더 길이/레코드 길이 오류")
    descriptors = reader.exact(header_length - 32)
    fields = []
    pos, offset = 0, 1
    while pos < len(descriptors) and descriptors[pos] != 13:
        descriptor = descriptors[pos:pos + 32]
        if len(descriptor) != 32:
            raise ValueError("DBF 필드 설명 길이 오류")
        name = descriptor[:11].split(b"\x00", 1)[0].decode("ascii", errors="strict")
        length, decimals = descriptor[16:18]
        fields.append({"name": name, "type": chr(descriptor[11]), "length": length,
                       "decimals": decimals, "byteOffset": offset})
        offset += length
        pos += 32
    if pos >= len(descriptors) or descriptors[pos] != 13:
        raise ValueError("DBF 필드 설명 종료자 누락")
    a3 = next((f for f in fields if f["name"] == "A3"), None)
    flags = Counter()
    prefixes = Counter()
    first = None
    last = None
    streamed = 0
    remaining = count
    while remaining:
        take = min(1024, remaining)
        batch = reader.exact(take * record_length)
        for n in range(take):
            row = memoryview(batch)[n * record_length:(n + 1) * record_length]
            flags[chr(row[0])] += 1
            if a3:
                value = bytes(row[a3["byteOffset"]:a3["byteOffset"] + a3["length"]]).strip(b" \x00")
                try:
                    text = value.decode("ascii")
                    prefixes[text[:2] if re.fullmatch(r"\d{10}", text) else "_invalidOrBlank"] += 1
                except UnicodeDecodeError:
                    prefixes["_nonAscii"] += 1
        if first is None:
            first = {"rowOneBased": 1, "deletionFlag": chr(batch[0]),
                     "rawRecordSha256": hashlib.sha256(batch[:record_length]).hexdigest(),
                     "fields": decode_record(batch[:record_length], fields, encoding)}
        last_bytes = batch[-record_length:]
        streamed += take
        last = {"rowOneBased": streamed, "deletionFlag": chr(last_bytes[0]),
                "rawRecordSha256": hashlib.sha256(last_bytes).hexdigest(),
                "fields": decode_record(last_bytes, fields, encoding)}
        remaining -= take
    tail = reader.read()
    return {"versionByte": head[0], "declaredRecordCount": count, "streamedRecordCount": streamed,
            "headerLength": header_length, "recordLength": record_length, "fields": fields,
            "fieldWidthsMatchRecordLength": offset == record_length,
            "expectedBytesWithoutEofMarker": header_length + count * record_length,
            "memberBytes": member_size, "trailingBytesHex": tail[:64].hex(),
            "trailingByteCount": len(tail), "deletionFlagCounts": dict(flags),
            "declaredEncoding": encoding,
            "fieldA3TenDigitPrefixProbe": {"field": "A3", "allRawRows": dict(prefixes),
                                          "interpretation": "10자리 코드 형태의 원문 접두값 관측. 현행 법정동/지역에 대한 의미·시점은 별도 대조 필요."},
            "representativeRawRecords": [first, last] if first else []}


def component(reader, info, extension, encoding):
    if extension == ".shp":
        return parse_shp(reader, info.file_size)
    if extension == ".shx":
        return parse_shx(reader, info.file_size)
    if extension == ".dbf":
        return parse_dbf(reader, info.file_size, encoding)
    if extension in {".prj", ".cpg"}:
        data = reader.read()
        return {"text": data.decode("utf-8-sig", errors="strict")}
    while reader.read(1024 * 1024):
        pass
    return {}


def audit(path, resource):
    before = identity(path)
    result = {"sidoCodeFromOfficialResource": resource["sido"], "officialResourceName": resource["name"],
              "fileName": path.name, "sourceDateFromFileName": "2026-11-09",
              "fileIdentity": before, "status": "Pending", "rowCountSemantics": "RawLayerRowsOnly",
              "buildingCount": None, "uniqueBuildingsVerified": False, "distributionApproved": False}
    try:
        result["sha256"] = sha(path)
        members, layers = [], defaultdict(dict)
        with zipfile.ZipFile(path) as archive:
            infos = archive.infolist()
            names = [i.filename for i in infos]
            result["duplicateZipMemberNames"] = sorted(n for n, count in Counter(names).items() if count > 1)
            encoding_by_stem = {}
            for info in infos:
                if info.filename.lower().endswith(".cpg"):
                    label = archive.read(info).decode("utf-8-sig", errors="strict").strip()
                    try:
                        encoding_by_stem[str(Path(info.filename).with_suffix(""))] = codecs.lookup(label).name
                    except LookupError:
                        encoding_by_stem[str(Path(info.filename).with_suffix(""))] = None
            for info in infos:
                if info.is_dir():
                    continue
                extension = Path(info.filename).suffix.lower()
                stem = str(Path(info.filename).with_suffix(""))
                with archive.open(info) as f:
                    reader = HashReader(f)
                    parsed = component(reader, info, extension, encoding_by_stem.get(stem))
                    assert reader.bytes == info.file_size, (info.filename, reader.bytes, info.file_size)
                    # ZipExtFile는 마지막 바이트까지 읽을 때 ZIP의 CRC를 함께 검사한다.
                    entry = {"name": info.filename, "bytes": info.file_size, "compressedBytes": info.compress_size,
                             "crc32": f"{info.CRC:08x}", "crcFullRead": "passed",
                             "sha256": reader.digest.hexdigest(), "parsed": parsed}
                members.append(entry)
                if extension in {".dbf", ".shp", ".shx", ".prj", ".cpg"}:
                    layers[stem][extension] = entry
        result["fileIdentityAfterRead"] = identity(path)
        if before != result["fileIdentityAfterRead"]:
            result.update(status="PendingFileChangedDuringRead", crcStatus="NotFrozen")
            return result
        result.update(members=members, crcStatus="AllMembersPassed", zipMemberCount=len(members),
                      expandedBytes=sum(x["bytes"] for x in members))
        layer_results = []
        issues = []
        for stem, parts in sorted(layers.items()):
            missing = sorted({".dbf", ".shp", ".shx", ".prj", ".cpg"} - set(parts))
            lr = {"layerName": stem, "missingComponents": missing, "rowCountSemantics": "RawLayerRowsOnly"}
            if missing:
                issues.append(stem + ": missing " + ",".join(missing))
            else:
                dbf, shp, shx = [parts[e]["parsed"] for e in [".dbf", ".shp", ".shx"]]
                rows_match = dbf["declaredRecordCount"] == dbf["streamedRecordCount"] == shp["streamedGeometryRecordCount"] == shx["entryCount"]
                offsets_match = shp["recordOffsetsAndLengthsSha256"] == shx["entryBytesSha256"]
                lr.update(rawLayerRows=dbf["declaredRecordCount"], geometryRecords=shp["streamedGeometryRecordCount"],
                          indexEntries=shx["entryCount"], rowCountsMatch=rows_match,
                          shpAndShxAllOffsetsAndLengthsMatch=offsets_match,
                          shpAndShxHeaderBoundsMatch=shp["header"]["xyBounds"] == shx["header"]["xyBounds"],
                          dbfSha256=parts[".dbf"]["sha256"], shpSha256=parts[".shp"]["sha256"],
                          prjText=parts[".prj"]["parsed"]["text"], cpgText=parts[".cpg"]["parsed"]["text"],
                          fileHeaderXYBounds=shp["header"]["xyBounds"],
                          fieldA3PrefixProbe=dbf["fieldA3TenDigitPrefixProbe"])
                good = (rows_match and offsets_match and dbf["fieldWidthsMatchRecordLength"]
                        and shp["header"]["headerStructureValid"] and shx["header"]["headerStructureValid"]
                        and shx["entryBytesMultipleOfEight"] and not shp["nonSequentialRecordNumbers"]
                        and not shp["invalidRecordEnvelopeCount"] and not shp["polygonCoordinateArrayTooShortCount"])
                lr["structureChecksPassed"] = bool(good)
                if not good:
                    issues.append(stem + ": structure or row alignment needs review")
            layer_results.append(lr)
        dbfs = [m for m in members if m["name"].lower().endswith(".dbf")]
        hashes, sizes = defaultdict(list), defaultdict(list)
        for member in dbfs:
            hashes[member["sha256"]].append(member["name"])
            sizes[member["bytes"]].append(member)
        result["duplicateDbfContentGroups"] = [{"sha256": digest, "members": names} for digest, names in hashes.items() if len(names) > 1]
        result["sameSizedDbfDiagnostics"] = [{"bytes": size, "members": [{"name": m["name"], "sha256": m["sha256"], "rows": m["parsed"]["declaredRecordCount"]} for m in group], "allContentHashesEqual": len({m["sha256"] for m in group}) == 1} for size, group in sizes.items() if len(group) > 1]
        result.update(layers=layer_results, layerCount=len(layer_results), structureIssues=issues,
                      rawLayerRowCounts=[x.get("rawLayerRows") for x in layer_results],
                      geometryAttributeAssociationStatus="CountsAndOffsetsOnlyNotFeatureIdentityProof",
                      status="FrozenValidatedRaw" if layer_results and not issues and not result["duplicateZipMemberNames"] else "RawStructureReviewRequired")
        if result["duplicateDbfContentGroups"]:
            result["geometryAttributeAssociationStatus"] = "DuplicateDbfContentAcrossLayersRequiresReview"
        return result
    except (OSError, ValueError, AssertionError, zipfile.BadZipFile, RuntimeError, EOFError, struct.error) as exc:
        result.update(status="PendingIncompleteOrUnverifiable", crcStatus="NotFullyVerified",
                      error={"type": type(exc).__name__, "message": str(exc)})
        try:
            result["fileIdentityAfterRead"] = identity(path)
        except OSError:
            pass
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sido", help="선택 지역만 새로 검사. 나머지 지역은 기존 검증 캐시 또는 Pending으로 표시한다.")
    args = parser.parse_args()
    started = time.monotonic()
    official_path = RECEIPT / "official-resource-list.json"
    official = json.loads(official_path.read_text("utf-8-sig"))
    prior = json.loads(CACHE.read_text("utf-8")) if CACHE.exists() else {}
    reusable = prior.get("archives", {}) if prior.get("algorithmVersion") == ALGORITHM else {}
    records = []
    next_cache = dict(reusable)
    reused, fresh = 0, 0
    for resource in official["resources"]:
        code = resource["sido"]
        path = RAW / f"AL_D010_{code}_20261109.zip"
        if not path.exists():
            record = {"sidoCodeFromOfficialResource": code, "officialResourceName": resource["name"], "fileName": path.name, "status": "PendingNotReceived"}
        elif path.name in reusable and reusable[path.name].get("status") == "FrozenValidatedRaw" and reusable[path.name].get("fileIdentityAfterRead") == identity(path):
            record = dict(reusable[path.name])
            record["cacheReusedThisRun"] = True
            reused += 1
        elif args.sido and args.sido != code:
            record = {"sidoCodeFromOfficialResource": code, "officialResourceName": resource["name"], "fileName": path.name, "status": "PendingAuditNotSelected"}
        else:
            record = audit(path, resource)
            record["cacheReusedThisRun"] = False
            fresh += 1
            print(json.dumps({"sido": code, "status": record["status"], "rawLayerRows": record.get("rawLayerRowCounts"), "duplicateDbfGroups": len(record.get("duplicateDbfContentGroups", [])), "elapsedSeconds": round(time.monotonic() - started, 1)}, ensure_ascii=False), flush=True)
        records.append(record)
        if record.get("status") == "FrozenValidatedRaw":
            next_cache[path.name] = record
        else:
            next_cache.pop(path.name, None)
        # 긴 전체 검사 도중 중단되어도 이미 읽은 ZIP의 CRC/해시를 다시 읽지 않게 한다.
        json_out(CACHE, {"algorithmVersion": ALGORITHM,
                         "createdAtUtc": datetime.now(timezone.utc).isoformat(), "archives": next_cache})
    now = datetime.now(timezone.utc)
    global_dbfs = []
    for record in records:
        for member in record.get("members", []):
            if member["name"].lower().endswith(".dbf"):
                global_dbfs.append({"sido": record["sidoCodeFromOfficialResource"],
                                    "member": member["name"], "bytes": member["bytes"],
                                    "rows": member["parsed"]["declaredRecordCount"],
                                    "sha256": member["sha256"]})
    by_size, by_hash = defaultdict(list), defaultdict(list)
    for dbf in global_dbfs:
        by_size[dbf["bytes"]].append(dbf)
        by_hash[dbf["sha256"]].append(dbf)
    result = {"schemaVersion": "national-building-archive-readiness.v1", "algorithmVersion": ALGORITHM,
              "createdAtUtc": now.isoformat(), "officialResourceList": {"path": str(official_path.relative_to(ROOT)).replace("\\", "/"), "sha256": sha(official_path), "sourceDate": official["sourceDate"], "sourceUrl": official["sourceUrl"], "rightsDisplayed": official["rightsDisplayed"]},
              "script": {"path": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"), "sha256": sha(Path(__file__))},
              "expectedRegions": len(official["resources"]), "statusCounts": dict(Counter(r["status"] for r in records)),
              "freshArchiveReads": fresh, "reusedValidatedArchives": reused, "archives": records,
              "globalSameSizedDbfDiagnostics": [{"bytes": size, "members": group,
                                                 "allContentHashesEqual": len({x["sha256"] for x in group}) == 1}
                                                for size, group in by_size.items() if len(group) > 1],
              "globalDuplicateDbfContentGroups": [{"sha256": digest, "members": group}
                                                 for digest, group in by_hash.items() if len(group) > 1],
              "cachePolicy": "파일 크기/mtime/ctime과 검사 판본이 같은 FrozenValidatedRaw만 재사용. 취득 중·실패·변경 파일은 생략하지 않는다.",
              "limits": ["RawLayerRowsOnly. 여러 레이어의 DBF행을 고유 건물 수로 합산하지 않는다.", "CRC·SHA·행수·offset 검사는 현재 건물 정확도, 행정경계 부합, 피처 동일성, 전국 표현 완성을 증명하지 않는다.", "원본 읽기 전용. 새 다운로드·공개·제품/공용DB/예약/공용문서 변경은 하지 않는다."],
              "distributionApproved": False, "nationwidePresentationVerified": False,
              "elapsedSeconds": round(time.monotonic() - started, 2)}
    cache_result = {"algorithmVersion": ALGORITHM, "createdAtUtc": now.isoformat(), "archives": next_cache}
    json_out(CACHE, cache_result)
    timestamped = RECEIPT / ("archive-audit-" + now.strftime("%Y%m%dT%H%M%S%fZ") + ".json")
    json_out(timestamped, result)
    json_out(RECEIPT / "archive-audit-latest.json", result)
    print(json.dumps({"statusCounts": result["statusCounts"], "freshArchiveReads": fresh,
                      "reusedValidatedArchives": reused, "receipt": str(timestamped.relative_to(ROOT)).replace("\\", "/"),
                      "receiptSha256": sha(timestamped), "elapsedSeconds": result["elapsedSeconds"]}, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()

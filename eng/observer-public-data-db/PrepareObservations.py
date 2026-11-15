"""기존 공공 원문을 내부 DB 검토용 JSON으로 준비한다. 네트워크·DB·예약·제품 변경 없음."""
from __future__ import annotations
import argparse
import calendar
import csv
import hashlib
import io
import json
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from pathlib import Path

VERSION = "observer-db-preparation.r2"
MAX_RECORDS = 100_000
METRIC_TOTAL = "demography.estimated-floating-population.total"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(body):
    return hashlib.sha256(body).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def utc_o(value):
    """UTC .NET DateTimeOffset O: 7자리 소수초를 보존한다."""
    match = re.fullmatch(r"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(Z|\+00:00)", value)
    require(match is not None, "UtcTimestampRequired:" + str(value))
    datetime.fromisoformat(value.replace("Z", "+00:00"))
    return match[1] + "." + (match[2] or "").ljust(7, "0") + "+00:00"


def database_collected_at(value):
    """DB datetime(6)에 맞춰 100ns 자리를 버린다. 반올림하거나 관측일을 만들지 않는다."""
    exact = utc_o(value)
    whole, fraction = exact.split(".", 1)
    return whole + "." + fraction[:6] + "+00:00"


def anchor(year, month, day=1):
    return utc_o(f"{year:04}-{month:02}-{day:02}T00:00:00Z")


def number(value):
    try:
        n = Decimal(str(value).replace(",", "").strip())
    except InvalidOperation as error:
        raise ValueError("InvalidNumeric") from error
    require(n.is_finite() and n >= 0, "NonnegativeFiniteNumericRequired")
    if n == n.to_integral_value():
        return int(n)
    result = float(n)
    require(Decimal(str(result)) == n, "NumericJsonRoundtripLoss")
    return result


def record_key(source, dataset, region, metric, evidence, dimension):
    canonical = "|".join([source.strip().lower(), dataset.strip().lower(), region.strip().lower(),
                          metric.strip().lower(), utc_o(evidence), dimension.strip()])
    return digest(canonical.encode("utf-8"))


def csv_rows(body):
    for encoding in ("utf-8-sig", "cp949"):
        try:
            text = body.decode(encoding, errors="strict")
            break
        except UnicodeError:
            pass
    else:
        raise ValueError("CsvEncodingUnknown")
    reader = csv.DictReader(io.StringIO(text, newline=""))
    require(reader.fieldnames is not None, "CsvHeaderMissing")
    require(len(set(reader.fieldnames)) == len(reader.fieldnames), "CsvHeaderDuplicate")
    rows = list(reader)
    require(rows and all(None not in row and all(v is not None for v in row.values()) for row in rows),
            "CsvShapeInvalid")
    return rows, encoding


def run(root, output):
    root, output = root.resolve(), output.resolve()
    require(output.is_relative_to(root / "artifacts/local/public-data-db-20261113"), "OutputOutsideOwnedDirectory")
    audit_input = root / "artifacts/local/observer-public-data-research-20261113/repo-audit.json"
    catalog_input = root / "docs/AI/DataCollection/Research/2026-11-13-observer-source-catalog.json"
    inventory = json.loads(audit_input.read_text(encoding="utf-8"))
    jobs_document = json.loads((root / "eng/data-collection/jobs.json").read_text(encoding="utf-8"))
    configured = {}
    for job in jobs_document["jobs"]:
        for source in job["sources"]:
            configured[source["id"]] = source
    entries, pending_outputs, evidence = [], [], []
    total_records = 0

    def relative(path):
        resolved = path.resolve()
        require(resolved.is_relative_to(root), "InputOutsideRepository")
        return resolved.relative_to(root).as_posix()

    def entry(identifier, source, dataset, kind, source_version, collected, raw_path, expected_hash,
              content_type, official_url, basis_note):
        body = raw_path.read_bytes()
        actual_hash = digest(body)
        require(actual_hash == expected_hash.lower(), "RawHashMismatch:" + identifier)
        revision = VERSION + ":sha256:" + actual_hash
        item = dict(id=identifier, kind=kind, sourceId=source, datasetId=dataset,
                    sourceVersion=source_version, dataRevision=revision,
                    collectedAtUtc=database_collected_at(collected), evidenceAsOfUtc=None,
                    extra=dict(originalCollectedAtUtc=collected,
                               collectedAtPrecisionPolicy="TruncatedToDatabaseMicroseconds"),
                    rawPath=relative(raw_path), rawSha256=actual_hash, contentType=content_type,
                    officialUrl=official_url, recordsPath=None, recordsSha256=None,
                    recordCount=0, evidenceNote=basis_note,
                    observationDateWasInferredFromCollection=False)
        entries.append(item)
        return item, body

    def row(item, stable, region, metric, value, unit, evidence_at, spatial, temporal, dimension, text, limitations):
        require(region.startswith(("country:", "region:", "point:", "grid:", "area:", "raster:")), "RegionPrefixInvalid")
        dimension += ";rawRevision=" + item["dataRevision"]
        text = dict(text)
        text["originalCollectedAtUtc"] = item["extra"]["originalCollectedAtUtc"]
        return dict(recordKey=record_key(item["sourceId"], item["datasetId"], region, metric, evidence_at, dimension),
                    stableId=stable, sourceId=item["sourceId"], datasetId=item["datasetId"],
                    regionStableId=region.lower(), metricCode=metric, numericValue=value,
                    textValue=json.dumps(text, ensure_ascii=False, separators=(",", ":")),
                    unitCode=unit, evidenceAsOfUtc=utc_o(evidence_at), collectedAtUtc=item["collectedAtUtc"],
                    spatialPrecisionCode=spatial, temporalPrecisionCode=temporal,
                    qualityCode="PendingHumanReview", limitationCode="PrivateReviewOnly;NoRuntime;" + limitations,
                    dimensionKey=dimension, sourceVersion=item["sourceVersion"], dataRevision=item["dataRevision"])

    def finish(item, rows):
        nonlocal total_records
        require(rows and len({r["recordKey"] for r in rows}) == len(rows), "EmptyOrDuplicateRecords:" + item["id"])
        require(all(r["sourceVersion"] == item["sourceVersion"] and r["dataRevision"] == item["dataRevision"] for r in rows),
                "ManifestVersionMismatch")
        total_records += len(rows)
        require(total_records <= MAX_RECORDS, "PreparationRecordLimitExceeded")
        target = output / ("observations." + item["id"] + ".json")
        body = json_bytes(rows)
        pending_outputs.append((target, body))
        item.update(recordsPath=relative(target), recordsSha256=digest(body), recordCount=len(rows),
                    evidenceAsOfUtc=max(r["evidenceAsOfUtc"] for r in rows),
                    temporalPrecisionCode=rows[0]["temporalPrecisionCode"])

    expected_numeric = {"worldbank-kor-arable", "bls-rice", "seoul-trading-population", "seoul-administrative-population"}
    require(len(inventory["currentRawSnapshots"]) == 15, "Expected15CurrentRawPointers")
    for old in inventory["currentRawSnapshots"]:
        pointer_path = root / old["pointer"]
        pointer = json.loads(pointer_path.read_text(encoding="utf-8"))
        require(pointer["sourceId"] == old["sourceId"] and pointer["datasetId"] == old["datasetId"], "PointerIdentityChanged")
        require(pointer["sha256"] == old["sha256"], "PointerRevisionChangedSinceAudit:" + old["id"])
        source = configured[old["id"]]
        identifier = old["id"]
        kind = "metadata" if pointer["kind"] == "metadata" else "observations" if identifier in expected_numeric else "reference"
        content_type = ("application/zip" if identifier == "mois-legal-dong" else
                        "text/csv; charset=cp949" if identifier.startswith("seoul-") and kind != "metadata" else
                        "text/html" if identifier.startswith("seoul-") else "application/json")
        item, body = entry(identifier, pointer["sourceId"], pointer["datasetId"], kind,
                           source.get("validationPolicyVersion", "source-metadata.r1"), pointer["collectedAt"],
                           pointer_path.parent / pointer["body"], pointer["sha256"], content_type,
                           source.get("metadataUrl", source["url"]),
                           "Metadata publication date is not an observation date." if kind == "metadata" else
                           "Unknown effective date; reference attributes only, no invented observation timestamp." if kind == "reference" else
                           "Observation period comes from each provider row, not fetchedAt.")
        if kind != "observations":
            continue
        rows = []
        if identifier == "worldbank-kor-arable":
            payload = json.loads(body)
            source_version = "wdi:" + str(payload[0]["sourceid"]) + ":lastupdated:" + payload[0]["lastupdated"]
            item["sourceVersion"] = source_version
            for raw in payload[1]:
                require(raw["indicator"]["id"] == "AG.LND.ARBL.HA" and raw["countryiso3code"] == "KOR", "WorldBankIdentityInvalid")
                require(re.fullmatch(r"\d{4}", raw["date"]) is not None and raw["value"] is not None, "WorldBankPeriodValueInvalid")
                year = int(raw["date"])
                rows.append(row(item, f"agricultural-land:country:kr:arable-area:{year}", "country:kr",
                                "agricultural-land.arable-area", number(raw["value"]), "ha", anchor(year, 12, 31),
                                "country", "annual", "indicator:AG.LND.ARBL.HA;period:annual",
                                dict(indicator="AG.LND.ARBL.HA", year=year, rawUnit=raw.get("unit"),
                                     unitEvidence="WorldBank경지면적Dataset.UnitCode=ha; provider indicator name hectares"),
                                "national-aggregate-fao-derived;AnnualPeriodAnchorNotDailyObservation"))
        elif identifier == "bls-rice":
            payload = json.loads(body)
            require(payload["status"] == "REQUEST_SUCCEEDED", "BlsStatusInvalid")
            for series in payload["Results"]["series"]:
                require(series["seriesID"] == "APU0000701312", "BlsSeriesInvalid")
                for raw in series["data"]:
                    require(re.fullmatch(r"M(?:0[1-9]|1[0-2])", raw["period"]) is not None, "BlsPeriodInvalid")
                    year, month = int(raw["year"]), int(raw["period"][1:])
                    missing = raw["value"] == "-"
                    rows.append(row(item, f"bls-ap:{series['seriesID']}:{year}:{raw['period']}", "country:us",
                                    "price.average-retail", None if missing else number(raw["value"]), "USD/lb",
                                    anchor(year, month), "us-city-average", "calendar-month",
                                    f"series={series['seriesID']};item=701312;period={raw['period']};unit=USD/lb",
                                    dict(series=series["seriesID"], year=year, period=raw["period"], rawValue=raw["value"],
                                         missing=missing, footnotes=raw.get("footnotes", []), product="rice-white-long-grain"),
                                    "USCityAverage;NotKoreaPrice;NotFXConverted;MonthStartPeriodAnchor" +
                                    (";ProviderMissingNotZero" if missing else "")))
        else:
            raw_rows, encoding = csv_rows(body)
            administrative = identifier == "seoul-administrative-population"
            code_column = "행정동_코드" if administrative else "상권_코드"
            name_column = "행정동_코드_명" if administrative else "상권_코드_명"
            require("명" in source["unit"] and source["numericColumns"] == ["총_유동인구_수"], "SeoulMeaningUnitUnverified")
            for raw in raw_rows:
                period, code, value = raw["기준_년분기_코드"].strip(), raw[code_column].strip(), raw["총_유동인구_수"].strip()
                require(re.fullmatch(r"\d{4}[1-4]", period) is not None and code.isdigit(), "SeoulPeriodIdentityInvalid")
                require(len(code) == (8 if administrative else 7), "SeoulProviderCodeLengthInvalid")
                require(value != "", "SeoulTotalMissing")
                year, quarter = int(period[:4]), int(period[4])
                month = 3 * quarter - 2
                next_start = anchor(year + (quarter == 4), 1 if quarter == 4 else month + 3)
                kind_name = "administrative-dong-stat" if administrative else "trading-area"
                region = f"area:seoul-open-data:{kind_name}:{code}"
                rows.append(row(item, f"seoul-floating:{item['datasetId']}:{period}:{code}:total", region,
                                METRIC_TOTAL, number(value), "person", anchor(year, month),
                                "provider-area", "calendar-quarter", f"period={period};providerArea={code};measure=total",
                                dict(providerPeriod=period, periodStartUtc=anchor(year, month), periodEndExclusiveUtc=next_start,
                                     providerAreaCode=code, providerAreaName=raw[name_column],
                                     sourceColumn="총_유동인구_수", sourceUnit=source["unit"], encoding=encoding),
                                "CityAggregate;ProviderEstimated;NotUniquePersons;NotRoadCounts;NotLegalDong;" +
                                "NoAdministrativeCodeCrosswalk;QuarterStartPeriodAnchor;NoDailyAverageInference"))
        finish(item, rows)
        evidence.append(dict(id=identifier, rawRows=pointer["recordCount"], preparedRecords=len(rows),
                             numericRecords=sum(r["numericValue"] is not None for r in rows),
                             missingRecords=sum(r["numericValue"] is None for r in rows),
                             originalUnit=pointer.get("unit"), originalPeriod=pointer.get("evidenceAsOf")))

    marine_folder = root / "artifacts/local/public-data/domestic-marine/20260922T080805-61216b99"
    receipt = json.loads((marine_folder / "receipt.json").read_text(encoding="utf-8"))
    for original in receipt["Entries"]:
        if original["Status"] != "Success":
            continue
        identifier = original["Id"]
        require(original["SourceId"] == "kcs" and original["DatasetId"] == "marine-monthly-trade-history", "MarineSourceIdentityInvalid")
        meta_path = marine_folder / (identifier + ".metadata.json")
        meta_body = meta_path.read_bytes()
        require(digest(meta_body) == original["MetadataHash"], "MarineMetadataHashMismatch")
        metadata = json.loads(meta_body)
        description = metadata["description"]
        require(all(term in description for term in ("kg", "CIF", "FOB", "금액")), "MarineUnitBasisUnverified")
        item, body = entry(identifier, original["SourceId"], original["DatasetId"], "observations",
                           "marine-monthly-trade.r1", original["CheckedAt"], marine_folder / (identifier + ".raw"),
                           original["PayloadHash"], "application/xml", metadata["url"],
                           "Source metadata confirms net kg, import CIF/USD and export FOB/USD; calendar-month scope.")
        require(b"<!DOCTYPE" not in body.upper() and len(body) <= 2 * 1024 * 1024, "MarineXmlUnsafe")
        document = ET.fromstring(body)
        require(document.tag == "response" and document.findtext("./header/resultCode") == "00", "MarineStatusInvalid")
        rows, ignored = [], 0
        month, query_hs, partner = original["Month"], original["HsCode"], original["Partner"]
        require(month == "202606" and query_hs in {"030354", "030214", "030363"} and partner in {"NO", "RU"}, "MarineScopeChanged")
        for raw in document.findall("./body/items/item"):
            period = raw.findtext("year", "").strip()
            if period in ("총계", "합계", "총 합계"):
                ignored += 1
                continue
            hs = raw.findtext("hsCd", "").strip()
            require(period.replace(".", "").replace("-", "") == month and hs.startswith(query_hs)
                    and re.fullmatch(r"\d{6}(?:\d{4})?", hs) is not None and raw.findtext("statCd") == partner,
                    "MarineResponseIdentityInvalid")
            for metric in ("impWgt", "impDlr", "expWgt", "expDlr"):
                unit, basis = ("kg" if metric.endswith("Wgt") else "USD"), ("CIF" if metric.startswith("imp") else "FOB")
                value = number(raw.findtext(metric, ""))
                rows.append(row(item, f"kcs:marine:{month}:{hs}:{partner}:{metric}", "country:kr", metric, value, unit,
                                anchor(int(month[:4]), int(month[4:])), "country-trade-not-habitat", "calendar-month",
                                f"hs={hs};partner={partner};metric={metric};basis={basis}",
                                dict(hsCode=hs, queryHs6=query_hs, partner=partner, month=month, metric=metric,
                                     unit=unit, basis=basis, providerValue=raw.findtext(metric), metadataSha256=original["MetadataHash"]),
                                "NotFishPopulation;NotCatchVolume;NoDeletionInference;MonthStartPeriodAnchor"))
        finish(item, rows)
        evidence.append(dict(id=identifier, preparedRecords=len(rows), skippedTotalRows=ignored,
                             metadataPath=relative(meta_path), metadataSha256=original["MetadataHash"],
                             unitEvidence="metadata description: net kg; import CIF; export FOB; USD amounts"))

    require(len(entries) == 18 and sum(e["kind"] == "observations" for e in entries) == 7, "UnexpectedManifestEntryCount")
    all_rows = [json.loads(body) for _, body in pending_outputs]
    require(sum(len(x) for x in all_rows) == total_records, "OutputCountMismatch")
    keys = [x["recordKey"] for rows in all_rows for x in rows]
    require(len(keys) == len(set(keys)), "CrossFileRecordKeyDuplicate")
    require(total_records == 45666, "FrozenRawExpectedCountChanged")
    catalog = json.loads(catalog_input.read_text(encoding="utf-8"))
    audit = dict(schemaVersion="public-data-normalization-audit.v1", mode="LocalPreparationOnly",
                 preparationVersion=VERSION, rawFilesVerified=len(entries), preparedRecords=total_records,
                 collectedAtPrecisionPolicy=dict(
                     code="TruncatedToDatabaseMicroseconds",
                     targetPrecision="UTC datetime(6), six fractional digits",
                     rounding=False,
                     maximumDeltaMicrosecondsExclusive=1,
                     actualMaximumDiscardedNanoseconds=max(
                         int(utc_o(e["extra"]["originalCollectedAtUtc"]).split(".")[1][6]) * 100
                         for e in entries),
                     originalTimestampPaths=["manifest.entries[].extra.originalCollectedAtUtc",
                                             "observations[].textValue.originalCollectedAtUtc",
                                             "immutable source latest pointer / domestic-marine receipt CheckedAt"],
                     sourceFilesChanged=False,
                     evidenceAnchorsChanged=False,
                     observationDateWasInferredFromCollection=False),
                 numericRecords=total_records - 1, explicitMissingRecords=1,
                 classification=dict(observationPayloads=7, referencePayloads=3, metadataPayloads=8),
                 maxRecords=MAX_RECORDS, evidence=evidence,
                 codeReuse=[
                     "Ssalddel/Services/External/PublicData/WorldBank/WorldBank경지면적Normalizer.cs",
                     "Ssalddel/Services/External/PublicData/WorldBank/WorldBank경지면적Source.cs",
                     "Ssalddel/Services/AgriculturalFisheries/Information/Bls평균소매가격ArchiveService.cs:356-405",
                     "Ssalddel/Services/AgriculturalFisheries/Information/Bls평균소매가격SeriesCatalog.cs:248",
                     "eng/data-collection/collect_public_snapshots.py:316-453",
                     "eng/data-collection/seoul_activity.py:10-126",
                     "eng/data-collection/jobs.json:134-238",
                     "Ssalddel/Services/External/PublicData/Korea/국내해양통계RevisionNormalizer.cs",
                     "Ssalddel.Domain/PublicData/ExternalDataIngestionModels.cs:154-175"],
                 criticalBoundaries=[
                     "Codego normalizer currently falls back to collectedAt when effective date is null. It was not executed/reused for these undated reference rows.",
                     "Codego legal-dong name/status 53403 rows are references, not numeric observations or polygons.",
                     "Seoul OA-15560/OA-22160 have representative x/y and area attributes, no polygons; effective date and CRS/area unit remain unknown. recordsPath/evidenceAsOfUtc stay null.",
                     "Seoul provider 8-digit statistical dong codes were not mapped to 10-digit MOIS administrative/legal codes.",
                     "Only total floating population is prepared. Expanding all 22 population measure columns would exceed one million facts and requires a separate scope.",
                     "Quarter/month/year timestamps are explicit period anchors with precision, never purported exact observation instants.",
                     "RecordKey canonicalization matches .NET UTC O with seven fractional digits and lower source/dataset/region/metric. rawRevision dimension intentionally preserves correction versions.",
                     "StableId keeps logical identity; sourceVersion/dataRevision exactly match each manifest entry. Existing historical keys are not overwritten by this preparation.",
                     "No collectedAt/publication date was used to invent an unknown observation date.",
                     "BLS 2025-M10 '-' remains numericValue null, not zero. US city average is not a Korean price.",
                     "KCS monthly trade is not catch volume, fish stock, habitat or current port movement.",
                     "Current 26 research candidates are metadata-only in this research; no new source mapping/acquisition claimed by this preparation."],
                 researchCandidateCount=catalog["candidateEntryCount"],
                 researchCandidatesChanged=False, networkRequestsMade=False, collectionRunsExecuted=False,
                 databaseConnectionsMade=False, databaseWritesMade=False, productFilesChanged=False,
                 scheduleChangesMade=False, privateUserDataIncluded=False,
                 sourceVersionPolicy="WorldBank uses provider WDI sourceid/lastupdated. Others use existing source validation/parser version, or metadata descriptor r1. These are protocol/interpretation versions, not invented source observation dates.",
                 dataRevisionPolicy=VERSION + ":sha256:<full raw content SHA256>; fixed per entry and every child record",
                 normalizationMetricPolicy="New BLS price.average-retail and Seoul estimated-floating-population.total metric names are internal preparation identifiers; source semantics/units remain in textValue.")
    manifest = dict(schemaVersion="public-data-db-preparation.v1", pathBase="repository-root",
                    purpose="PrivateReviewOnly;DB import review;NotAUserDataPack;NoRuntime",
                    preparationVersion=VERSION, maxRecords=MAX_RECORDS, recordCount=total_records,
                    collectedAtPrecisionPolicy=audit["collectedAtPrecisionPolicy"],
                    sourceVersionPolicy=audit["sourceVersionPolicy"], dataRevisionPolicy=audit["dataRevisionPolicy"],
                    entries=entries)
    # 검증 실패 시 산출물을 쓰지 않는다. 원본·제품·DB·예약에는 접근하지 않는다.
    output.mkdir(parents=True, exist_ok=True)
    for target, body in pending_outputs:
        target.write_bytes(body)
        require(digest(target.read_bytes()) == digest(body), "OutputWriteHashMismatch")
    (output / "manifest.partial.json").write_bytes(json_bytes(manifest))
    (output / "normalization-audit.json").write_bytes(json_bytes(audit))
    print(json.dumps(dict(preparedRecords=total_records, numericRecords=total_records - 1, missingRecords=1,
                          entries=len(entries), output=relative(output), databaseWritesMade=False)))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    run(args.root, args.output or args.root / "artifacts/local/public-data-db-20261113")


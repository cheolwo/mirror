"""Check the authorized MOLIT public API on a bounded sample without changing housing snapshots."""

import argparse
from contextlib import closing
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen
from xml.etree import ElementTree

from collect_public_snapshots import atomic_json
from map_public_api_transport import IntakeError, credentials
from seoul_area_geography import check_database, read_only
from seoul_tile_population import PUBLIC, digest


OUTPUT = PUBLIC / "role-tile-intake-20261126/housing-api"
BASE = PUBLIC / "hongdal-housing-transactions-20261125/housing-transactions.sqlite"
ENDPOINT = "https://apis.data.go.kr/1613000"
PORTAL = "https://www.data.go.kr/data"
SAMPLE_DISTRICT, SAMPLE_MONTH = "11260", "202610"
SPECS = (
    ("15126468", "RTMSDataSvcAptTradeDev", "apartment", "sale"),
    ("15126474", "RTMSDataSvcAptRent", "apartment", "rent"),
    ("15126467", "RTMSDataSvcRHTrade2", "villa", "sale"),
    ("15126473", "RTMSDataSvcRHRent", "villa", "rent"),
    ("15126463", "RTMSDataSvcNrgTrade", "commercial_business", "sale"),
)


def parse_sample(raw):
    if len(raw) > 2_000_000 or b"<!DOCTYPE" in raw.upper():
        return {"state": "response_size_or_markup_rejected"}
    try:
        root = ElementTree.fromstring(raw)
    except ElementTree.ParseError:
        return {"state": "response_not_xml"}
    code = root.findtext("./header/resultCode") or root.findtext(".//returnReasonCode")
    if not code or not code.isascii() or not code.isdecimal() or len(code) > 3:
        return {"state": "response_code_unrecognized"}
    if code not in {"0", "00", "000"}:
        return {"state": "service_authorization_required" if code in {"20", "30", "31", "32"}
                else "provider_error", "providerCode": code}
    total = root.findtext("./body/totalCount")
    page = root.findall("./body/items/item")
    if total is None or not total.isdecimal() or len(page) != min(int(total), 3):
        return {"state": "sample_count_unverified", "providerCode": code}
    return {"state": "sample_response_ok" if page else "sample_returned_zero",
            "providerCode": code, "providerCount": int(total), "sampleRows": len(page),
            "returnedColumns": sorted({node.tag for row in page for node in row})}


def read_sample(endpoint, secret):
    params = {"LAWD_CD": SAMPLE_DISTRICT, "DEAL_YMD": SAMPLE_MONTH,
              "numOfRows": "3", "pageNo": "1", **secret}
    request = Request(endpoint + "?" + urlencode(params), headers={"User-Agent": "Mozilla/5.0"})
    try:
        with urlopen(request, timeout=25) as response:
            return {"httpStatus": response.status, **parse_sample(response.read(2_000_001))}
    except HTTPError as error:
        with error:
            detail = parse_sample(error.read(2_000_001))
        return {"httpStatus": error.code, **detail}
    except (URLError, TimeoutError, OSError):
        return {"state": "transport_unavailable"}


def probe(output):
    output.mkdir(parents=True, exist_ok=True)
    with closing(read_only(BASE)) as db:
        check_database(db)
        counts = dict(((kind, group), n) for kind, group, n in db.execute("""SELECT housing_type,
            contract_group,COUNT(*) FROM housing_transaction WHERE district_code=? AND
            substr(contract_date,1,7)='2026-10' GROUP BY 1,2""", (SAMPLE_DISTRICT,)))
        snapshots = db.execute("SELECT count(*) FROM housing_snapshot").fetchone()[0]
    try:
        secret = credentials({"shape": "standard", "keyParam": "serviceKey"})
    except IntakeError:
        secret = None
    results = []
    for portal_id, name, kind, group in SPECS:
        endpoint = f"{ENDPOINT}/{name}/get{name}"
        sample = read_sample(endpoint, secret) if secret else {"state": "credential_unavailable"}
        results.append({"datasetId": portal_id, "officialUrl": f"{PORTAL}/{portal_id}/openapi.do",
                        "endpoint": endpoint, "housingType": kind, "contractGroup": group,
                        "retainedOctoberComparableFileRows": counts.get((kind, group)), **sample})
    receipt = {"probedAtUtc": datetime.now(timezone.utc).isoformat(), "districtCode": SAMPLE_DISTRICT,
               "contractMonth": SAMPLE_MONTH, "requestedSampleRowsPerService": 3,
               "baselineSnapshotCount": snapshots, "baselineSha256": digest(BASE), "services": results,
               "switchState": "pending_comparable_scope_validation"}
    atomic_json(output / "verification.json", receipt)
    return results


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    for service in probe(parser.parse_args().output):
        print(service["datasetId"], service.get("httpStatus"), service.get("providerCode"), service["state"])

"""국토교통부 실거래가 공개시스템의 조건별 CSV 내보내기."""

from datetime import date, datetime, timezone
from http.client import HTTPException
from http.cookiejar import CookieJar
import json
import re
from urllib.request import HTTPCookieProcessor

from collect_public_snapshots import CollectionError, fetch, opener


ORIGIN = "https://rt.molit.go.kr"
SOURCE_URL = ORIGIN + "/pt/xls/xls.do"
DOWNLOAD_URL = ORIGIN + "/pt/xls/ptXlsCSVDown.do"
COUNT_URL = ORIGIN + "/pt/xls/ptXlsDownDataCheck.do"
HOUSING_TYPES = {"apartment": "A", "villa": "B"}
CONTRACT_GROUPS = {"sale": "1", "rent": "2"}
MAX_FORM_BYTES = 256 * 1024
MAX_COUNT_BYTES = 16 * 1024
MAX_CSV_BYTES = 20 * 1024 * 1024
TIMEOUT_SECONDS = 25


class HousingDownloadError(CollectionError):
    """외부 응답이나 네트워크 주소 대신 정해진 오류 코드만 전달한다."""


class HousingDownload:
    def __init__(self):
        self._client = opener()
        self._client.add_handler(HTTPCookieProcessor(CookieJar()))
        self._client.addheaders = [("Origin", ORIGIN), ("Referer", SOURCE_URL)]
        body, _ = self._fetch(SOURCE_URL, MAX_FORM_BYTES)
        if b'frm_xls' not in body:
            raise HousingDownloadError("public_housing_form_missing")

    def _fetch(self, url, limit, form=None):
        source = {"url": url}
        if form is not None:
            source.update(method="POST", form=form)
        try:
            status, body, headers = fetch(
                source, None, TIMEOUT_SECONDS, limit, datetime.now(timezone.utc),
                open_client=self._client,
            )
        except CollectionError as exc:
            raise HousingDownloadError(exc.code, blocked=exc.blocked) from None
        except (OSError, HTTPException):
            raise HousingDownloadError("housing_network_failed") from None
        if status != 200 or not body:
            raise HousingDownloadError("public_housing_response_empty")
        return body, headers

    def download(
        self, district_code: str, province_name: str, district_name: str,
        housing_type: str, contract_group: str, start: date, end: date,
    ) -> tuple[int, bytes | None]:
        if (not isinstance(district_code, str) or re.fullmatch(r"[0-9]{5}", district_code) is None
                or housing_type not in HOUSING_TYPES or contract_group not in CONTRACT_GROUPS
                or type(start) is not date or type(end) is not date
                or not 0 <= (end - start).days < 366):
            raise HousingDownloadError("public_housing_scope_invalid")
        if not all(isinstance(name, str) and 0 < len(name.strip()) <= 50 and name.isprintable()
                   for name in (province_name, district_name)):
            raise HousingDownloadError("public_housing_region_name_invalid")

        form = {
            "srhThingNo": HOUSING_TYPES[housing_type],
            "srhDelngSecd": CONTRACT_GROUPS[contract_group],
            "srhAddrGbn": "1",
            "srhLfstsSecd": "1",
            "sidoNm": province_name,
            "sggNm": district_name,
            "emdNm": "전체",
            "loadNm": "전체",
            "areaNm": "전체",
            "hsmpNm": "전체",
            "mobileAt": "",
            "srhFromDt": start.isoformat(),
            "srhToDt": end.isoformat(),
            "srhNewRonSecd": "",
            "srhSidoCd": district_code[:2],
            "srhSggCd": district_code,
            "srhEmdCd": "",
            "srhRoadNm": "",
            "srhLoadCd": "",
            "srhHsmpCd": "",
            "srhArea": "",
            "srhFromAmount": "",
            "srhToAmount": "",
            "srhLrArea": "",
        }
        response, _ = self._fetch(COUNT_URL, MAX_COUNT_BYTES, form)
        try:
            result = json.loads(response)
        except (ValueError, UnicodeError):
            raise HousingDownloadError("public_housing_count_invalid") from None
        if isinstance(result, dict) and result.get("error"):
            raise HousingDownloadError("public_housing_count_rejected", blocked=True)
        count = result.get("cnt") if isinstance(result, dict) else None
        if type(count) is not int or count < 0:
            raise HousingDownloadError("public_housing_count_invalid")
        if count == 0:
            return 0, None

        body, headers = self._fetch(DOWNLOAD_URL, MAX_CSV_BYTES, form)
        disposition = headers.get("Content-Disposition", "").lower()
        if not disposition.startswith("attachment;") or ".csv" not in disposition:
            raise HousingDownloadError("public_housing_csv_attachment_missing")
        return count, body

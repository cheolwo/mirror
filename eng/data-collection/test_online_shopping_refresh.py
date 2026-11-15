"""온라인쇼핑 공식 월 발표 발견/원본 보존의 오프라인 경계 검증."""
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from collect_public_snapshots import CollectionError
import online_shopping_refresh as refresh

NOW = datetime(2026, 12, 5, 5, 0, tzinfo=timezone.utc)
PDF = b"%PDF-1.7\nnew monthly example\n%%EOF\n"
OLD_PDF = b"%PDF-1.7\nfrozen September example\n%%EOF\n"


def listing(month="10", list_no="448222", published="2026-12-02", bid="241", href=None):
    href = href or (f"javascript:addSearchParam('/board.es?mid=a10301010000&amp;bid={bid}"
                    f"&amp;act=view&amp;list_no={list_no}&amp;tag=&amp;nPage=1&amp;ref_bid=');")
    return (f'<html><ul><li><a class="board_link" href="{href}">2026년 {month}월 온라인쇼핑동향</a>'
            f'<div class="board_class"><ul><li><strong>게시일</strong><span>{published}</span></li></ul></div></li></ul></html>').encode()


def post(month="10", list_no="448222", published="2026-12-02", attachment=None):
    attachment = attachment or f"/boardDownload.es?bid=241&amp;list_no={list_no}&amp;seq=1"
    return (f'<html><meta property="og:url" content="http://10.0.0.1/private">'
            f'<h4 class="bv_title">2026년 {month}월 온라인쇼핑동향</h4>'
            f'<li class="date"><strong>게시일</strong><span>{published}</span></li>'
            f'<a class="bvf_name" href="{attachment}">2026년 {month}월 온라인쇼핑동향 보도자료.pdf</a>'
            f'<div class="open-nuri-type">국가데이터처 "2026년 {month}월 온라인쇼핑동향" '
            '공공누리 2유형(출처표시+상업적 이용금지)</div></html>').encode()


class Fetcher:
    def __init__(self, *responses):
        self.responses = list(responses)
        self.urls = []

    def __call__(self, source, previous, timeout, limit, now):
        self.urls.append(source["url"])
        if not self.responses:
            raise AssertionError("Unexpected HTTP request")
        response = self.responses.pop(0)
        if isinstance(response, Exception):
            raise response
        if isinstance(response, tuple):
            return response
        content_type = "application/pdf" if response.startswith(b"%PDF-") else "text/html; charset=utf-8"
        return 200, response, {"Content-Type": content_type}


class Parsers(unittest.TestCase):
    def assert_code(self, code, action):
        with self.assertRaises(CollectionError) as caught:
            action()
        self.assertEqual(caught.exception.code, code)

    def test_next_month_listing_and_document_are_bound(self):
        candidate = refresh.parse_listing(listing(), NOW)[0]
        self.assertEqual((candidate["observationMonth"], candidate["listNo"], candidate["publicationDate"]),
                         ("2026-10", "448222", "2026-12-02"))
        result = refresh.parse_post(post(), candidate, NOW)
        self.assertEqual(result["officialUrl"], "https://www.mods.go.kr/boardDownload.es?bid=241&list_no=448222&seq=1")
        self.assertEqual(result["rights"]["status"], "page_statement_preserved")
        self.assertFalse(result["rights"]["gameUseApproved"])

    def test_unrelated_board_and_non_monthly_posts_are_excluded(self):
        self.assertEqual(refresh.parse_listing(listing(bid="999"), NOW), [])
        self.assertEqual(refresh.parse_listing(listing().replace("온라인쇼핑동향".encode(), "소비자물가동향".encode()), NOW), [])

    def test_latest_observation_month_not_input_order(self):
        html = listing(month="9", list_no="447781", published="2026-11-02") + listing()
        self.assertEqual(refresh.parse_listing(html, NOW)[0]["observationMonth"], "2026-10")

    def test_unsafe_download_host_and_other_release_are_rejected(self):
        candidate = refresh.parse_listing(listing(), NOW)[0]
        self.assert_code("unapproved_official_url", lambda: refresh.parse_post(
            post(attachment="https://mods.go.kr.evil.example/boardDownload.es?bid=241&amp;list_no=448222&amp;seq=1"), candidate, NOW))
        self.assert_code("unsafe_attachment", lambda: refresh.parse_post(
            post(attachment="/boardDownload.es?bid=241&amp;list_no=1&amp;seq=1"), candidate, NOW))

    def test_wrong_attachment_period_and_publication_date_are_rejected(self):
        candidate = refresh.parse_listing(listing(), NOW)[0]
        body = post().replace("2026년 10월 온라인쇼핑동향 보도자료.pdf".encode(), "2026년 9월 온라인쇼핑동향 보도자료.pdf".encode())
        self.assert_code("attachment_period_mismatch", lambda: refresh.parse_post(body, candidate, NOW))
        self.assert_code("listing_post_date_mismatch", lambda: refresh.parse_post(post(published="2026-12-01"), candidate, NOW))
        self.assert_code("publication_period_invalid", lambda: refresh.parse_listing(listing(published="2026-12-06"), NOW))

    def test_credentials_and_unapproved_javascript_are_rejected(self):
        self.assert_code("credential_query_forbidden", lambda: refresh.attachment_url(
            "https://www.mods.go.kr/boardDownload.es?bid=241&list_no=1&seq=1&token=hidden", refresh.LIST_URL, "1"))
        self.assert_code("unsupported_board_link", lambda: refresh.post_url("javascript:evil('/board.es?bid=241')"))

    def test_malformed_optional_html_attributes_do_not_supply_a_target(self):
        self.assertEqual(refresh.parse_listing(b'<html><a class href>empty</a></html>', NOW), [])
        self.assert_code("unsupported_board_link", lambda: refresh.post_url(None))


class Collector(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)
        raw = self.root / "artifacts/local/management-research-data-20261114/ecommerce/raw/korea-online-shopping-202609.pdf"
        raw.parent.mkdir(parents=True)
        raw.write_bytes(OLD_PDF)
        self.index = self.root / refresh.BASELINE
        data = {"checkedAtUtc": "2026-11-14T00:00:00Z", "sources": [{
            "sourceId": refresh.SOURCE_ID, "datasetId": "online-shopping-trends-202609", "kind": "dataset",
            "title": "2026년 9월 온라인쇼핑동향", "officialUrl": "https://www.mods.go.kr/boardDownload.es?bid=241&list_no=447781&seq=1",
            "rawPath": raw.relative_to(self.root).as_posix(), "rawSha256": hashlib.sha256(OLD_PDF).hexdigest(),
            "bytes": len(OLD_PDF), "collectedAtUtc": "2026-11-13T23:45:37Z", "publicationDate": "2026-11-02", "rights": {"status": "specific-license-unconfirmed"}}]}
        self.index.write_text(json.dumps(data), encoding="utf-8")
        self.frozen_bytes = (self.index.read_bytes(), raw.read_bytes())
        self.raw = raw

    def collect(self, fetcher, now=NOW, **kwargs):
        result = refresh.collect(self.root, now=now, fetcher=fetcher, **kwargs)
        self.assertEqual(self.frozen_bytes, (self.index.read_bytes(), self.raw.read_bytes()))
        return result

    def test_dry_run_and_not_due_make_no_http_or_files(self):
        fake = Fetcher()
        now = datetime(2026, 11, 14, 1, tzinfo=timezone.utc)
        self.assertEqual(self.collect(fake, now=now, dry_run=True)["status"], "not_due")
        self.assertEqual(self.collect(fake, dry_run=True)["status"], "would_check")
        self.assertEqual(fake.urls, [])
        self.assertFalse((self.root / refresh.OUTPUT).exists())

    def test_unchanged_month_never_requests_frozen_pdf(self):
        fake = Fetcher(listing(month="9", list_no="447781", published="2026-11-02"))
        result = self.collect(fake)
        self.assertEqual((result["status"], len(fake.urls), result["pdfRequested"]), ("unchanged", 1, False))
        self.assertFalse((self.root / refresh.OUTPUT / "online-shopping/bodies").exists())
        second = self.collect(Fetcher(), now=NOW + timedelta(hours=1))
        self.assertEqual(second["status"], "not_due")

    def test_new_month_saves_one_pdf_index_and_receipt(self):
        fake = Fetcher(listing(), post(), PDF)
        result = self.collect(fake)
        self.assertEqual((result["status"], len(fake.urls)), ("new", 3))
        output = self.root / refresh.OUTPUT / "online-shopping"
        latest = json.loads((output / "latest.json").read_text(encoding="utf-8"))
        self.assertEqual(latest["observationMonth"], "2026-10")
        self.assertEqual((self.root / latest["rawPath"]).read_bytes(), PDF)
        self.assertIsNone(latest["rows"])
        self.assertEqual(len(json.loads((output / "source-index.json").read_text(encoding="utf-8"))["sources"]), 2)
        self.assertTrue((output / "runs" / result["runId"] / "receipt.json").is_file())
        self.assertFalse(any("10.0.0.1" in url for url in fake.urls))

    def test_same_month_republication_same_sha_no_new_body_or_repeat_download(self):
        fake = Fetcher(listing(month="9", list_no="447782", published="2026-11-03"),
                       post(month="9", list_no="447782", published="2026-11-03"), OLD_PDF)
        result = self.collect(fake)
        self.assertEqual((result["status"], result["reason"]), ("unchanged", "duplicate_pdf_sha256_no_new_body"))
        self.assertFalse((self.root / refresh.OUTPUT / "online-shopping/bodies").exists())
        future = Fetcher(listing(month="9", list_no="447782", published="2026-11-03"))
        self.assertEqual(self.collect(future, now=NOW + timedelta(days=8))["status"], "unchanged")
        self.assertEqual(len(future.urls), 1)

    def test_old_pdf_under_new_month_title_is_not_accepted(self):
        result = self.collect(Fetcher(listing(), post(), OLD_PDF))
        self.assertEqual((result["status"], result["reason"]), ("failed", "new_month_reuses_previous_pdf"))
        self.assertFalse((self.root / refresh.OUTPUT / "online-shopping/latest.json").exists())

    def test_failure_is_recorded_and_throttled(self):
        fake = Fetcher(CollectionError("network_failed"))
        result = self.collect(fake)
        self.assertEqual((result["status"], result["reason"]), ("failed", "network_failed"))
        self.assertEqual(self.collect(Fetcher(), now=NOW + timedelta(hours=167))["status"], "not_due")

    def test_no_monthly_release_retains_metadata_and_blocks_pdf(self):
        fake = Fetcher(listing(bid="999"))
        result = self.collect(fake)
        self.assertEqual((result["status"], result["reason"], result["blocked"]), ("failed", "monthly_release_not_found", True))
        self.assertFalse(result["pdfRequested"])
        folder = self.root / refresh.OUTPUT / "online-shopping/runs" / result["runId"]
        self.assertTrue((folder / "listing.html").is_file())

    def test_unsafe_attachment_is_never_requested(self):
        fake = Fetcher(listing(), post(attachment="https://evil.example/download.pdf"))
        result = self.collect(fake)
        self.assertEqual(result["status"], "failed")
        self.assertEqual(len(fake.urls), 2)
        self.assertFalse(result["pdfRequested"])

    def test_redirect_and_bad_pdf_failure_keep_latest_unchanged(self):
        for third in (CollectionError("redirect_forbidden", blocked=True), (200,b"<html>not a pdf</html>",{"Content-Type":"application/pdf"})):
            with self.subTest(third=third):
                result = self.collect(Fetcher(listing(), post(), third), now=NOW + timedelta(days=8 if isinstance(third,tuple) else 0))
                self.assertEqual(result["status"], "failed")
                self.assertFalse((self.root / refresh.OUTPUT / "online-shopping/latest.json").exists())


if __name__ == "__main__":
    unittest.main()

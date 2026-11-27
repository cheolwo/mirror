"""가격 저장, 기사 시기 검증, 부분 실패 뒤 재실행을 확인한다."""

from contextlib import redirect_stdout
from datetime import datetime, timedelta
from io import StringIO
import json
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch

from collect_public_snapshots import CollectionError, KST
import cabbage_news_followup as followup

NOW = datetime(2026, 11, 25, 13, tzinfo=KST)
ARTICLE = "https://www.mafra.go.kr/bbs/home/792/582233/artclView.do"
HEADER = "<tr><th>품목</th><th>품종</th><th>단위</th><th>등급</th><th>당월평균(A)</th><th>전월평균(B)</th></tr>"
PRICE = "<tr><th>배추</th><th>가을</th><th>1포기</th><th>상품</th><td>2,741</td><td>4,236</td></tr>"


def market_page(header=HEADER, row=PRICE, month=11, county=""):
    return (f'<span class="txt_date">2026년 {month}월 기준</span>'
            f'<input id="countycode" value="{county}">'
            f'<table id="itemTable_1">{header}{row}</table>').encode()


def official_rss():
    items = "".join(
        f"<item><link>{url}</link><title>{title}</title><pubDate>{when}</pubDate></item>"
        for url, title, when in [
            (ARTICLE, "배추 수급대책", "2026-11-11 17:20:00.1"),
            (ARTICLE + "?old=1", "오래된 배추 수급대책", "2026-11-10 13:00:00"),
            (ARTICLE + "?future=1", "미래 배추 수급대책", "2026-11-25 13:30:00"),
            (ARTICLE + "?nonrelated=1", "도로 교통", "2026-11-12 13:00:00"),
        ])
    return f"<rss><channel>{items}</channel></rss>".encode()


class Fetcher:
    def __init__(self):
        self.urls = []
        self.fail_gdelt = False
        self.market = market_page()

    def __call__(self, source, previous, timeout, limit, now):
        url = source["url"]
        self.urls.append(url)
        if url.startswith(followup.KAMIS + "?"):
            return 200, self.market, {}
        if url == followup.MAFRA:
            return 200, official_rss(), {}
        if self.fail_gdelt:
            raise CollectionError("http_429", blocked=True)
        return 200, json.dumps({"articles": [
            {"url": "https://example.com/news/1?utm_source=partner", "title": "배춧값 현장",
             "seendate": "20261110T150000Z"},
            {"url": "https://example.com/news/2", "title": "배추 가격", "seendate": "20261125T043000Z"},
        ]}).encode(), {}


class NewsFollowup(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.output = Path(temporary.name)
        self.db = sqlite3.connect(":memory:")
        self.db.row_factory = sqlite3.Row
        self.db.executescript(followup.SCHEMA)
        self.addCleanup(self.db.close)
        self.fetcher = Fetcher()

    def collect(self, elapsed=timedelta(0)):
        return followup.collect(self.db, self.output, NOW + elapsed, self.fetcher)

    def test_observation_then_two_sources_and_same_day_deduplication(self):
        result = self.collect()
        self.assertEqual((2741, 4236, 2, 2), tuple(result[key] for key in
                         ("priceKrw", "previousMonthKrw", "newsRequests", "candidates")))
        candidates = json.loads((self.output / "article-candidates.json").read_text(encoding="utf-8"))
        self.assertEqual(["2026-11-11", "2026-11-25"], candidates["publishedWindow"])
        indexed, published = candidates["candidates"]
        self.assertEqual("https://example.com/news/1", indexed["article_url"])
        self.assertEqual((None, "2026-11-10T15:00:00Z"), (indexed["feed_published_on"], indexed["indexed_at"]))
        self.assertEqual("2026-11-11", published["feed_published_on"])
        self.fetcher.market += b"<!-- HTML-only change -->"
        retry = self.collect(timedelta(minutes=1))
        self.assertEqual(result["observationId"], retry["observationId"])
        self.assertEqual((False, 0, 4), (retry["created"], retry["newsRequests"], len(self.fetcher.urls)))
        refresh = self.collect(timedelta(hours=6))
        self.assertEqual((False, 2, 4), (refresh["created"], refresh["newsRequests"], refresh["candidates"]))

    def test_failed_provider_retries_without_losing_success(self):
        self.fetcher.fail_gdelt = True
        first = self.collect()
        self.assertEqual({"mafra": "completed", "gdelt": "failed"}, first["newsStatus"])
        self.assertEqual(1, first["candidates"])
        pending = self.collect(timedelta(minutes=1))
        self.assertEqual({"mafra": "reused", "gdelt": "retry_pending"}, pending["newsStatus"])
        self.fetcher.fail_gdelt = False
        recovered = self.collect(timedelta(minutes=5))
        self.assertEqual({"mafra": "reused", "gdelt": "completed"}, recovered["newsStatus"])
        self.assertEqual((1, 2), (recovered["newsRequests"], recovered["candidates"]))

    def test_recent_publication_and_subject_period_required_for_review(self):
        result = self.collect()
        record = {"observationId": result["observationId"], "articleUrl": ARTICLE,
                  "title": "배추 수급대책", "publisher": "농림축산식품부",
                  "publishedOn": "2026-11-11", "subjectFrom": "2026-11-01", "subjectTo": "2026-11-10",
                  "evidenceNote": "11월 상순 설명을 비교했다. 월중 평균의 배추 가격과 집계 기간이 다르다."}
        with self.assertRaisesRegex(CollectionError, "publication_outside_window"):
            followup.review(self.db, {**record, "publishedOn": "2026-11-10"}, NOW)
        with self.assertRaisesRegex(CollectionError, "article_subject_outside_observation"):
            followup.review(self.db, {**record, "subjectFrom": "2026-10-01"}, NOW)
        with self.assertRaisesRegex(CollectionError, "article_subject_outside_observation"):
            followup.review(self.db, {**record, "subjectTo": "2026-11-12"}, NOW)
        self.assertEqual(1, followup.review(self.db, record, NOW)["reviewedRelationsAdded"])
        self.assertEqual(0, followup.review(self.db, record, NOW)["reviewedRelationsAdded"])
        with self.assertRaisesRegex(CollectionError, "review_conflicts_with_saved_relation"):
            followup.review(self.db, {**record, "evidenceNote": "다른 근거"}, NOW)
        followup.export_cards(self.db, self.output)
        export = json.loads((self.output / "reviewed-news-cards.json").read_text(encoding="utf-8"))
        self.assertEqual(1, len(export["relations"]))
        self.assertEqual("same_period_context", export["relations"][0]["relation_kind"])
        self.assertEqual("2026-11-25", export["relations"][0]["available_from"])

    def test_unverified_price_does_not_search_or_insert(self):
        self.fetcher.market = market_page(row=PRICE.replace("2,741", "-"))
        with self.assertRaisesRegex(CollectionError, "kamis_price_missing"):
            self.collect()
        self.assertEqual(1, len(self.fetcher.urls))
        self.assertEqual(0, self.db.execute("SELECT count(*) FROM observation").fetchone()[0])

    def test_wrong_month_or_regional_page_does_not_search(self):
        for body in (market_page(month=10), market_page(county="1101")):
            self.fetcher.market = body
            with self.assertRaisesRegex(CollectionError, "kamis_wrong_month_or_scope"):
                self.collect()
        self.assertEqual(2, len(self.fetcher.urls))
        self.assertEqual(0, self.db.execute("SELECT count(*) FROM observation").fetchone()[0])

    def test_cli_returns_partial_failure_then_successful_review(self):
        self.fetcher.fail_gdelt = True
        collect = followup.collect
        arguments = ["--output", str(self.output)]
        with patch.object(followup, "LOCAL", self.output), redirect_stdout(StringIO()):
            with patch.object(followup, "collect", side_effect=lambda db, output, now:
                              collect(db, output, NOW, self.fetcher)):
                self.assertEqual(2, followup.main(["collect", *arguments]))
            candidates = json.loads((self.output / "article-candidates.json").read_text(encoding="utf-8"))
            record = {"observationId": candidates["observationId"], "articleUrl": ARTICLE,
                      "title": "배추 수급대책", "publisher": "농림축산식품부",
                      "publishedOn": "2026-11-11", "subjectFrom": "2026-11-01", "subjectTo": "2026-11-10",
                      "evidenceNote": "확인된 11월 상순의 시기 설명"}
            review_file = self.output / "review.json"
            review_file.write_text(json.dumps(record), encoding="utf-8")
            self.assertEqual(0, followup.main(["review", *arguments, "--review", str(review_file)]))


if __name__ == "__main__":
    unittest.main()

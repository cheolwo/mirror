"""Collect the public KAMIS cabbage snapshot and index contemporary news locally."""

import argparse
from calendar import monthrange
from contextlib import closing
from datetime import date, datetime, time, timedelta
from hashlib import sha256
import json
from pathlib import Path
import re
import sqlite3
from tempfile import NamedTemporaryFile
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit
from xml.etree import ElementTree

from collect_public_snapshots import (
    CollectionError, KST, UTC, atomic_json, aware_time, fetch, global_lock, safe_header, stamp,
    validate_url,
)
from online_shopping_refresh import Tree, compact

LOCAL = Path(__file__).resolve().parents[2] / "artifacts/local"
KAMIS = "https://www.kamis.or.kr/customer/price/agricultureRetail/catalogue.do"
MAFRA = "https://www.mafra.go.kr/bbs/home/792/rssList.do?row=50"
GDELT = "https://api.gdeltproject.org/api/v2/doc/doc"
ATTRIBUTION = {"mafra": "https://www.mafra.go.kr/home/5327/subview.do",
               "gdelt": "https://www.gdeltproject.org/"}
SERIES = "kamis:national:retail:autumn-cabbage:premium:head:monthly"
LOOKBACK = timedelta(days=14)
RETRY_DELAY = timedelta(minutes=5)
NEWS_REFRESH = timedelta(hours=6)
TOPIC = re.compile(r"배추|배춧값|김장|\bcabbages?\b|\bkimchi\b", re.IGNORECASE)
SCHEMA = """
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS observation (
    id TEXT PRIMARY KEY, series TEXT NOT NULL, period_start TEXT NOT NULL, period_end TEXT NOT NULL,
    retrieved_at TEXT NOT NULL, retrieved_on TEXT NOT NULL, month_to_date INTEGER NOT NULL,
    price_krw INTEGER NOT NULL, previous_month_krw INTEGER NOT NULL,
    source_url TEXT NOT NULL, raw_sha256 TEXT NOT NULL, source_http_date TEXT
);
CREATE TABLE IF NOT EXISTS search (
    observation_id TEXT NOT NULL REFERENCES observation(id), provider TEXT NOT NULL,
    status TEXT NOT NULL, request_url TEXT NOT NULL, retrieved_at TEXT NOT NULL, detail TEXT,
    PRIMARY KEY (observation_id, provider)
);
CREATE TABLE IF NOT EXISTS article_candidate (
    observation_id TEXT NOT NULL, provider TEXT NOT NULL, article_url TEXT NOT NULL,
    title TEXT NOT NULL, feed_published_on TEXT, indexed_at TEXT,
    PRIMARY KEY (observation_id, provider, article_url),
    FOREIGN KEY (observation_id, provider) REFERENCES search(observation_id, provider)
);
CREATE TABLE IF NOT EXISTS article_relation (
    observation_id TEXT NOT NULL REFERENCES observation(id), article_url TEXT NOT NULL,
    title TEXT NOT NULL, publisher TEXT NOT NULL, published_on TEXT NOT NULL,
    subject_from TEXT NOT NULL, subject_to TEXT NOT NULL, evidence_note TEXT NOT NULL,
    reviewed_at TEXT NOT NULL, PRIMARY KEY (observation_id, article_url)
);
CREATE VIEW IF NOT EXISTS v_news_context AS
SELECT r.*, max(r.published_on, o.retrieved_on) AS available_from,
       'same_period_context' AS relation_kind
FROM article_relation AS r JOIN observation AS o ON o.id = r.observation_id;
"""


def get_public(url, now, fetcher):
    validate_url(url, [url])
    status, body, headers = fetcher({"url": url}, None, 18, 2 * 1024 * 1024, now)
    if status != 200 or not body:
        raise CollectionError("empty_public_response")
    return body, headers


def cabbage_prices(body, month):
    nodes = list(Tree(body).root.descendants())
    month_labels = [compact(node.text()) for node in nodes if node.has_class("txt_date")]
    county_codes = [node.attrs.get("value") for node in nodes if node.attrs.get("id") == "countycode"]
    valid_labels = {f"{month.year}년 {value}월 기준" for value in (month.month, f"{month.month:02d}")}
    if len(month_labels) != 1 or month_labels[0] not in valid_labels or county_codes != [""]:
        raise CollectionError("kamis_wrong_month_or_scope")
    tables = [node for node in nodes
              if node.tag == "table" and node.attrs.get("id") == "itemTable_1"]
    if len(tables) != 1:
        raise CollectionError("kamis_monthly_table_missing")
    rows = [[compact(cell.text()) for cell in node.children
             if getattr(cell, "tag", None) in {"td", "th"}]
            for node in tables[0].descendants() if node.tag == "tr"]
    header = ["품목", "품종", "단위", "등급", "당월평균(A)", "전월평균(B)"]
    matches = [row for row in rows if row[:4] == ["배추", "가을", "1포기", "상품"]]
    if not rows or rows[0][:6] != header or len(matches) != 1:
        raise CollectionError("kamis_cabbage_row_changed")
    fields = matches[0][4:6]
    if len(fields) != 2 or any(not re.fullmatch(r"[1-9]\d{0,2}(?:,\d{3})*|[1-9]\d*", val) for val in fields):
        raise CollectionError("kamis_price_missing")
    return tuple(int(val.replace(",", "")) for val in fields)


def candidate_url(url):
    parsed = validate_url(url, [url])
    if not parsed.path or len(url) > 2048:
        raise CollectionError("article_address_invalid")
    query = [(key, value) for key, value in parse_qsl(parsed.query, keep_blank_values=True)
             if not key.lower().startswith("utm_") and key.lower() not in {"gclid", "fbclid"}]
    return urlunsplit(("https", parsed.hostname, parsed.path, urlencode(sorted(query)), ""))


def time_window(observation):
    retrieved = date.fromisoformat(observation["retrieved_on"])
    return max(date.fromisoformat(observation["period_start"]), retrieved - LOOKBACK), retrieved


def save_raw(output, body, digest):
    raw = output / "kamis-raw" / f"{digest}.html"
    raw.parent.mkdir(exist_ok=True)
    if not raw.exists():
        temporary = None
        try:
            with NamedTemporaryFile(dir=raw.parent, suffix=".tmp", delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(body)
            temporary.replace(raw)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    if sha256(raw.read_bytes()).hexdigest() != digest:
        raise CollectionError("kamis_snapshot_hash_mismatch")


def request_urls(start, end, now):
    def utc_bound(day):
        return datetime.combine(day, time.min, KST).astimezone(UTC)

    upper = min(now.astimezone(UTC), utc_bound(end + timedelta(days=1)) - timedelta(seconds=1))
    return {"mafra": MAFRA, "gdelt": f"{GDELT}?" + urlencode({
        "query": "cabbage price sourcecountry:southkorea", "mode": "ArtList", "format": "json",
        "maxrecords": 20, "sort": "DateDesc",
        "startdatetime": utc_bound(start).strftime("%Y%m%d%H%M%S"),
        "enddatetime": upper.strftime("%Y%m%d%H%M%S"),
    })}


def article_candidates(provider, body, start, end, now):
    if provider == "gdelt":
        payload = json.loads(body)
        rows = payload.get("articles") if isinstance(payload, dict) else None
        if not isinstance(rows, list):
            raise ValueError("invalid_gdelt_article_list")
    else:
        xml = body.decode("utf-8-sig").strip()
        if re.search(r"<!\s*(DOCTYPE|ENTITY)\b", xml, re.IGNORECASE):
            raise ValueError("rss_document_type_forbidden")
        root = ElementTree.fromstring(xml)
        channel = root.find("channel")
        if root.tag != "rss" or channel is None:
            raise ValueError("invalid_official_rss")
        rows = [{"url": item.findtext("link", "").replace("http://www.mafra.go.kr/", "https://www.mafra.go.kr/", 1),
                 "title": item.findtext("title", ""), "pubDate": item.findtext("pubDate", "")}
                for item in channel.findall("item")]
    result = {}
    for item in rows:
        try:
            url = candidate_url(item["url"])
            title = compact(item["title"])[:300]
            if not TOPIC.search(title):
                continue
            if provider == "mafra":
                if urlsplit(url).hostname != "www.mafra.go.kr":
                    continue
                proof_time = datetime.fromisoformat(item["pubDate"])
                if proof_time.tzinfo is None:
                    proof_time = proof_time.replace(tzinfo=KST)
            else:
                proof_time = datetime.strptime(item["seendate"], "%Y%m%dT%H%M%SZ").replace(tzinfo=UTC)
            local_day = proof_time.astimezone(KST).date()
        except (CollectionError, KeyError, AttributeError, ValueError, TypeError):
            continue
        if start <= local_day <= end and proof_time <= now:
            result[url] = (url, title, local_day.isoformat() if provider == "mafra" else None,
                           stamp(proof_time) if provider == "gdelt" else None)
    return list(result.values())


def export_cards(db, output):
    cards = [dict(row) for row in db.execute(
        "SELECT * FROM v_news_context ORDER BY available_from, observation_id, article_url")]
    observations = [dict(row) for row in db.execute("SELECT * FROM observation ORDER BY retrieved_at, id")]
    for observation in observations:
        observation["actualLastObservationDate"] = None
    atomic_json(output / "reviewed-news-cards.json", {
        "schema": "hongdal-public-news-cards.v1", "distribution": "local-review",
        "priceSeries": {"id": SERIES, "product": "가을배추", "grade": "상품", "marketStage": "소매",
                        "region": "전국 평균", "unit": "원/포기"},
        "searchSources": ATTRIBUTION, "observations": observations, "relations": cards,
    })


def collect(db, output, now, fetcher=fetch):
    today = now.astimezone(KST).date()
    first = today.replace(day=1)
    last = today.replace(day=monthrange(today.year, today.month)[1])
    source_url = KAMIS + "?" + urlencode({"action": "monthly", "yyyy": today.year, "mm": f"{today.month:02d}",
                                          "countycode": "", "convert_kg_yn": "N"})
    body, headers = get_public(source_url, now, fetcher)
    price, previous = cabbage_prices(body, first)
    digest = sha256(body).hexdigest()
    # Different HTML with the same price pair is one observation per Korean calendar day.
    identity = sha256(f"{SERIES}|{today}|{price}|{previous}".encode()).hexdigest()
    observation = db.execute("SELECT * FROM observation WHERE id = ?", (identity,)).fetchone()
    created = observation is None
    if created:
        save_raw(output, body, digest)
        # The last actual survey date is not published on this monthly table.
        with db:
            db.execute("INSERT INTO observation VALUES (?,?,?,?,?,?,?,?,?,?,?,?)", (
                identity, SERIES, first.isoformat(), last.isoformat(), stamp(now), today.isoformat(),
                1, price, previous, source_url, digest, safe_header(headers, "Date"),
            ))
        observation = db.execute("SELECT * FROM observation WHERE id = ?", (identity,)).fetchone()
    else:
        stored_raw = output / "kamis-raw" / f"{observation['raw_sha256']}.html"
        if sha256(stored_raw.read_bytes()).hexdigest() != observation["raw_sha256"]:
            raise CollectionError("kamis_snapshot_hash_mismatch")
    start, end = time_window(observation)
    statuses = {}
    requests = 0
    for provider, url in request_urls(start, end, now).items():
        prior = db.execute("SELECT status, retrieved_at FROM search WHERE observation_id = ? AND provider = ?",
                           (identity, provider)).fetchone()
        if (prior and prior["status"] == "completed"
                and now < aware_time(prior["retrieved_at"]) + NEWS_REFRESH):
            statuses[provider] = "reused"
            continue
        last_search = db.execute("""SELECT status, retrieved_at FROM search WHERE provider = ?
                                    ORDER BY retrieved_at DESC LIMIT 1""", (provider,)).fetchone()
        if (last_search and last_search["status"] == "failed"
                and now < aware_time(last_search["retrieved_at"]) + RETRY_DELAY):
            statuses[provider] = "retry_pending"
            continue
        requests += 1
        try:
            news_body, _ = get_public(url, now, fetcher)
            candidates = article_candidates(provider, news_body, start, end, now)
        except (CollectionError, ValueError, OSError, ElementTree.ParseError) as exc:
            status, detail = ("failed", exc.code if isinstance(exc, CollectionError) else "invalid_news_response")
            candidates = []
        else:
            status, detail = "completed", None
        with db:
            db.execute("""INSERT INTO search VALUES (?,?,?,?,?,?)
                          ON CONFLICT (observation_id, provider) DO UPDATE SET
                          status=excluded.status, request_url=excluded.request_url,
                          retrieved_at=excluded.retrieved_at, detail=excluded.detail""",
                       (identity, provider, status, url, stamp(now), detail))
            db.executemany("""INSERT INTO article_candidate VALUES (?,?,?,?,?,?)
                              ON CONFLICT (observation_id, provider, article_url) DO NOTHING""",
                           [(identity, provider, *candidate) for candidate in candidates])
        statuses[provider] = status
    candidates = [dict(row) for row in db.execute(
        "SELECT * FROM article_candidate WHERE observation_id = ? ORDER BY provider, article_url", (identity,))]
    atomic_json(output / "article-candidates.json", {
        "observationId": identity, "publishedWindow": [start.isoformat(), end.isoformat()],
        "sources": ATTRIBUTION, "candidates": candidates})
    return {"observationId": identity, "created": created, "priceKrw": price, "previousMonthKrw": previous,
            "newsRequests": requests, "newsStatus": statuses, "candidates": len(candidates)}


def review(db, record, now):
    identity = record["observationId"]
    observation = db.execute("SELECT * FROM observation WHERE id = ?", (identity,)).fetchone()
    if observation is None:
        raise CollectionError("observation_missing")
    url = candidate_url(record["articleUrl"])
    title, publisher, note = (compact(record[key]) for key in ("title", "publisher", "evidenceNote"))
    if not title or not publisher or not note or not TOPIC.search(title):
        raise CollectionError("review_evidence_missing")
    published, start, end = (date.fromisoformat(record[key])
                             for key in ("publishedOn", "subjectFrom", "subjectTo"))
    earliest, latest = time_window(observation)
    if not earliest <= published <= min(latest, now.astimezone(KST).date()):
        raise CollectionError("publication_outside_window")
    if not (date.fromisoformat(observation["period_start"]) <= start <= end
            <= min(published, date.fromisoformat(observation["retrieved_on"]), date.fromisoformat(observation["period_end"]))):
        raise CollectionError("article_subject_outside_observation")
    relation = (title, publisher, published.isoformat(), start.isoformat(), end.isoformat(), note)
    current = db.execute("""SELECT title, publisher, published_on, subject_from, subject_to, evidence_note
                            FROM article_relation WHERE observation_id = ? AND article_url = ?""",
                         (identity, url)).fetchone()
    if current is not None and tuple(current) != relation:
        raise CollectionError("review_conflicts_with_saved_relation")
    with db:
        changed = db.execute("""INSERT INTO article_relation VALUES (?,?,?,?,?,?,?,?,?)
                              ON CONFLICT (observation_id, article_url) DO NOTHING""", (
            identity, url, *relation, stamp(now),
        )).rowcount
    return {"observationId": identity, "reviewedRelationsAdded": changed}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("collect", "review"))
    parser.add_argument("--output", type=Path, default=LOCAL / "public-data/hongdal-cabbage-news-followup")
    parser.add_argument("--review", type=Path, help="Verified article metadata and source-period evidence")
    args = parser.parse_args(argv)
    output = args.output.resolve()
    if not output.is_relative_to(LOCAL.resolve()) or (args.operation == "review") != bool(args.review):
        parser.error("use an output under artifacts/local and provide --review only for review")
    now = datetime.now(UTC)
    try:
        with global_lock(output / "followup.lock") as locked:
            if not locked:
                raise CollectionError("news_followup_already_running")
            with closing(sqlite3.connect(output / "news-followup.sqlite")) as db:
                db.row_factory = sqlite3.Row
                db.executescript(SCHEMA)
                if args.operation == "collect":
                    result = collect(db, output, now)
                else:
                    result = review(db, json.loads(args.review.read_text(encoding="utf-8-sig")), now)
                export_cards(db, output)
    except (CollectionError, OSError, ValueError, KeyError, TypeError, sqlite3.Error) as exc:
        print(json.dumps({"status": "failed", "reason": exc.code if isinstance(exc, CollectionError) else "invalid_input_or_storage"}))
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if {"failed", "retry_pending"}.isdisjoint(result.get("newsStatus", {}).values()) else 2


if __name__ == "__main__":
    raise SystemExit(main())

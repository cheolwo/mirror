"""국가데이터처 온라인쇼핑 월 발표 발견과 제한된 PDF 원본 수집.

동결 기초 자료는 참조만 하며 수치 파싱·DB/게임 반입은 하지 않는다.
같은 글/관측월/공표일의 무고지 PDF 교체는 재다운로드 없이 검증하지 않는다.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta, timezone
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
from urllib import parse
import uuid

from collect_public_snapshots import (
    CollectionError, atomic_json, aware_time, beneath, fetch, global_lock,
    safe_header, stamp, validate_url,
)

UTC = timezone.utc
KST = timezone(timedelta(hours=9))
SOURCE_ID = "mods-online-shopping"
LIST_URL = "https://www.mods.go.kr/board.es?mid=a10301010000&bid=241&act=list"
BASELINE = "artifacts/local/management-research-data-20261114/ecommerce/source-index.json"
OUTPUT = "artifacts/local/data-collection-runs"
INTERVAL_HOURS = 168
MAX_META_BYTES = 2 * 1024 * 1024
MAX_PDF_BYTES = 16 * 1024 * 1024
MAX_TOTAL_BYTES = 20 * 1024 * 1024
MAX_REQUESTS = 3  # 목록1 + 본문1 + 첨부1; 상위 예약의 요청 예산4 이내.
PERIOD = re.compile(r"^\s*(20\d{2})년\s*(0?[1-9]|1[0-2])월\s*온라인\s*쇼핑\s*동향")
VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"}


@dataclass
class Node:
    tag: str
    attrs: dict = field(default_factory=dict)
    children: list = field(default_factory=list)
    parent: "Node | None" = field(default=None, repr=False)

    def has_class(self, name):
        value = self.attrs.get("class")
        return isinstance(value, str) and name in value.split()

    def text(self):
        if self.tag in {"script", "style"}:
            return ""
        return " ".join(c.text() if isinstance(c, Node) else c for c in self.children)

    def descendants(self):
        for child in self.children:
            if isinstance(child, Node):
                yield child
                yield from child.descendants()


class Tree(HTMLParser):
    def __init__(self, body):
        super().__init__(convert_charrefs=True)
        self.root = Node("root")
        self.stack = [self.root]
        try:
            self.feed(body.decode("utf-8-sig"))
            self.close()
        except (UnicodeError, RecursionError):
            raise CollectionError("official_html_invalid") from None

    def handle_starttag(self, tag, attrs):
        node = Node(tag, dict(attrs), parent=self.stack[-1])
        self.stack[-1].children.append(node)
        if tag not in VOID:
            self.stack.append(node)

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        if tag not in VOID:
            self.handle_endtag(tag)

    def handle_endtag(self, tag):
        for index in range(len(self.stack) - 1, 0, -1):
            if self.stack[index].tag == tag:
                del self.stack[index:]
                break

    def handle_data(self, data):
        self.stack[-1].children.append(data)


def compact(value):
    return re.sub(r"\s+", " ", value).strip()


def month_from_title(title):
    match = PERIOD.match(title)
    if not match:
        raise CollectionError("monthly_title_missing")
    return f"{int(match[1]):04d}-{int(match[2]):02d}"


def release_date(value, month, now):
    try:
        parsed = date.fromisoformat(value)
        first = date.fromisoformat(month + "-01")
    except (TypeError, ValueError):
        raise CollectionError("publication_date_invalid") from None
    if parsed < first or parsed > now.astimezone(KST).date():
        raise CollectionError("publication_period_invalid")
    return parsed.isoformat()


def official_url(url, *, path):
    parsed = validate_url(url, [url])
    if parsed.hostname not in {"mods.go.kr", "www.mods.go.kr"} or parsed.path != path:
        raise CollectionError("unapproved_official_url", blocked=True)
    pairs = parse.parse_qsl(parsed.query, keep_blank_values=True)
    if len({key for key, _ in pairs}) != len(pairs):
        raise CollectionError("duplicate_url_parameters", blocked=True)
    return parsed, dict(pairs)


def post_url(href, base=LIST_URL):
    if not isinstance(href, str):
        raise CollectionError("unsupported_board_link", blocked=True)
    if href.startswith("javascript:"):
        match = re.fullmatch(r"javascript:addSearchParam\((['\"])(/board\.es\?[^'\"]+)\1\);?", href)
        if not match:
            raise CollectionError("unsupported_board_link", blocked=True)
        href = match[2]
    joined = parse.urljoin(base, href)
    parsed, values = official_url(joined, path="/board.es")
    if (values.get("bid") != "241" or values.get("act") != "view"
            or values.get("mid") != "a10301010000"
            or not re.fullmatch(r"[1-9]\d{0,11}", values.get("list_no", ""))):
        raise CollectionError("unrelated_board_link", blocked=True)
    return parse.urlunsplit(("https", parsed.netloc, "/board.es", parse.urlencode({
        "mid": "a10301010000", "bid": "241", "act": "view", "list_no": values["list_no"]}), ""))


def attachment_url(href, base, list_no):
    url = parse.urljoin(base, href)
    _, values = official_url(url, path="/boardDownload.es")
    if (set(values) != {"bid", "list_no", "seq"} or values.get("bid") != "241"
            or values.get("list_no") != str(list_no)
            or not re.fullmatch(r"[1-9]\d?", values.get("seq", ""))):
        raise CollectionError("unsafe_attachment", blocked=True)
    return url


def parse_listing(body, now):
    result = {}
    for node in Tree(body).root.descendants():
        if node.tag != "a" or not node.has_class("board_link"):
            continue
        title = compact(node.text())
        try:
            month = month_from_title(title)
        except CollectionError:
            continue
        try:
            url = post_url(node.attrs.get("href", ""))
        except CollectionError as exc:
            if exc.code == "unrelated_board_link":
                continue
            raise
        row = node.parent
        while row is not None and row.tag != "li":
            row = row.parent
        match = re.search(r"게시일\s*(20\d{2}-\d{2}-\d{2})", compact(row.text()) if row else "")
        if not match:
            raise CollectionError("listing_publication_date_missing")
        published = release_date(match[1], month, now)
        list_no = parse.parse_qs(parse.urlsplit(url).query)["list_no"][0]
        entry = dict(title=title, observationMonth=month, publicationDate=published,
                     listNo=list_no, postUrl=url)
        key = (month, published, list_no)
        if key in result and result[key] != entry:
            raise CollectionError("ambiguous_listing_entry")
        result[key] = entry
    return sorted(result.values(), key=lambda item: (
        item["observationMonth"], item["publicationDate"], int(item["listNo"])), reverse=True)


def parse_post(body, candidate, now):
    tree = Tree(body).root
    titles = [compact(n.text()) for n in tree.descendants() if n.has_class("bv_title")]
    if len(titles) != 1 or month_from_title(titles[0]) != candidate["observationMonth"]:
        raise CollectionError("post_title_period_mismatch")
    if titles[0] != candidate["title"]:
        raise CollectionError("listing_post_title_mismatch")
    dates = [compact(n.text()) for n in tree.descendants() if n.tag == "li" and n.has_class("date")]
    matches = [re.search(r"게시일\s*(20\d{2}-\d{2}-\d{2})", item) for item in dates]
    dates = [m[1] for m in matches if m]
    if len(dates) != 1 or release_date(dates[0], candidate["observationMonth"], now) != candidate["publicationDate"]:
        raise CollectionError("listing_post_date_mismatch")
    attachments = set()
    for node in tree.descendants():
        if node.tag != "a" or not node.has_class("bvf_name"):
            continue
        label = compact(node.text())
        if not label.lower().endswith(".pdf"):
            continue
        if month_from_title(label) != candidate["observationMonth"]:
            raise CollectionError("attachment_period_mismatch")
        attachments.add(attachment_url(node.attrs.get("href", ""), candidate["postUrl"], candidate["listNo"]))
    if len(attachments) != 1:
        raise CollectionError("monthly_pdf_attachment_missing_or_ambiguous")
    rights = []
    for node in tree.descendants():
        if node.has_class("open-nuri-type"):
            text = compact(node.text())
            if titles[0] in text:
                rights.append(text)
    return dict(candidate, officialUrl=attachments.pop(), rights={
        "status": "page_statement_preserved" if rights else "specific_license_unconfirmed",
        "scope": "release-title-named-page-block" if rights else None,
        "statements": rights, "redistributionApproved": False, "gameUseApproved": False,
        "aiClauseInterpretation": "별도 문구 보존만 하며 AI특례/게임이용허락을 판정하지 않음"})


def read_json(path):
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        raise CollectionError("local_json_missing_or_invalid") from None
    if not isinstance(value, dict):
        raise CollectionError("local_json_shape_invalid")
    return value


def raw_reference(workspace, snapshot):
    value = snapshot.get("rawPath")
    if not isinstance(value, str) or Path(value).is_absolute() or "\\" in value or ":" in value:
        raise CollectionError("unsafe_raw_reference", blocked=True)
    path = beneath(workspace, value)
    allowed = [beneath(workspace, "artifacts/local/management-research-data-20261114/ecommerce/raw"),
               beneath(workspace, OUTPUT, "online-shopping/bodies")]
    if not any(path.is_relative_to(root) for root in allowed):
        raise CollectionError("unsafe_raw_reference", blocked=True)
    digest = snapshot.get("rawSha256")
    if not isinstance(digest, str) or not re.fullmatch(r"[a-fA-F0-9]{64}", digest):
        raise CollectionError("raw_digest_invalid")
    if not path.is_file() or path.stat().st_size > MAX_PDF_BYTES:
        raise CollectionError("raw_reference_missing_or_oversized")
    body = path.read_bytes()
    if not body.startswith(b"%PDF-") or hashlib.sha256(body).hexdigest() != digest.lower():
        raise CollectionError("raw_reference_integrity_failed")
    if snapshot.get("sourceId") != SOURCE_ID or not re.fullmatch(r"20\d{2}-(0[1-9]|1[0-2])", snapshot.get("observationMonth", "")):
        raise CollectionError("snapshot_identity_invalid")
    if not re.fullmatch(r"[1-9]\d{0,11}", str(snapshot.get("listNo", ""))):
        raise CollectionError("snapshot_list_number_invalid")
    aware_time(snapshot.get("collectedAtUtc"))
    try:
        published = date.fromisoformat(snapshot.get("publicationDate", ""))
        if published < date.fromisoformat(snapshot["observationMonth"] + "-01"):
            raise ValueError
    except (TypeError, ValueError):
        raise CollectionError("snapshot_publication_date_invalid") from None
    return path


def baseline_snapshot(workspace, baseline_index):
    index_path = beneath(workspace, baseline_index)
    index = read_json(index_path)
    items = index.get("sources")
    if not isinstance(items, list) or any(not isinstance(item, dict) for item in items):
        raise CollectionError("baseline_source_index_invalid")
    choices = []
    for item in items:
        match = re.fullmatch(r"online-shopping-trends-(20\d{2})(0[1-9]|1[0-2])", item.get("datasetId", ""))
        if item.get("sourceId") != SOURCE_ID or item.get("kind") != "dataset" or not match:
            continue
        _, parameters = official_url(item["officialUrl"], path="/boardDownload.es")
        list_no = parameters.get("list_no", "")
        attachment_url(item["officialUrl"], LIST_URL, list_no)
        snapshot = {key: item.get(key) for key in (
            "sourceId", "datasetId", "title", "publicationDate", "rawPath", "rawSha256", "bytes", "collectedAtUtc", "rights")}
        snapshot.update(observationMonth=match[1] + "-" + match[2], listNo=list_no,
                        officialUrl=item["officialUrl"], kind="dataset", rows=None,
                        acquisition="reused", status="reused", baselineIndexPath=index_path.relative_to(workspace).as_posix(),
                        baselineIndexSha256=hashlib.sha256(index_path.read_bytes()).hexdigest())
        raw_reference(workspace, snapshot)
        choices.append(snapshot)
    if not choices:
        raise CollectionError("baseline_online_shopping_pdf_missing")
    current = max(choices, key=lambda x: (x["observationMonth"], x["publicationDate"], int(x["listNo"])))
    checked = index.get("checkedAtUtc", current["collectedAtUtc"])
    aware_time(checked)
    return current, checked


def load_state(workspace, output, baseline_index):
    current, baseline_checked = baseline_snapshot(workspace, baseline_index)
    latest_path = beneath(output, "latest.json")
    if latest_path.exists():
        current = read_json(latest_path)
        raw_reference(workspace, current)
    state_path = beneath(output, "state.json")
    state = read_json(state_path) if state_path.exists() else {"lastAttemptAtUtc": baseline_checked, "lastOutcome": "frozen_baseline"}
    aware_time(state.get("lastAttemptAtUtc"))
    return current, state


def same_release(candidate, current):
    return all(candidate.get(key) is not None and candidate.get(key) == current.get(key)
               for key in ("observationMonth", "publicationDate", "listNo"))


class Budget:
    def __init__(self, now, *, fetcher=fetch):
        self.now, self.fetcher = now, fetcher
        self.requests, self.bytes = [], 0

    def get(self, url, limit, previous=None):
        # 동적으로 발견한 URL도 호출 직전에 공식 호스트/경로를 제한한다.
        path = parse.urlsplit(url).path
        if url == LIST_URL:
            official_url(url, path="/board.es")
        elif path == "/board.es":
            if post_url(url) != url:
                raise CollectionError("post_url_not_canonical", blocked=True)
        elif path == "/boardDownload.es":
            _, q = official_url(url, path=path)
            attachment_url(url, LIST_URL, q.get("list_no"))
        else:
            raise CollectionError("unapproved_official_url", blocked=True)
        if len(self.requests) >= MAX_REQUESTS or self.bytes >= MAX_TOTAL_BYTES:
            raise CollectionError("run_budget_exceeded")
        receipt = {"url": url, "requestedAtUtc": stamp(self.now)}
        self.requests.append(receipt)
        try:
            status, body, headers = self.fetcher({"url": url}, previous, 30,
                min(limit, MAX_TOTAL_BYTES - self.bytes), self.now)
        except CollectionError as exc:
            self.bytes += exc.received
            receipt.update(status="failed", reason=exc.code, receivedBytes=exc.received)
            raise
        self.bytes += len(body)
        if len(body) > limit or self.bytes > MAX_TOTAL_BYTES:
            raise CollectionError("run_budget_exceeded")
        receipt.update(httpStatus=status, bytes=len(body), sha256=hashlib.sha256(body).hexdigest(),
                       contentType=safe_header(headers, "Content-Type"),
                       etag=safe_header(headers, "ETag"), lastModified=safe_header(headers, "Last-Modified"))
        return status, body, headers


def write_blob(path, body):
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with path.open("xb") as stream:
            stream.write(body)
    except FileExistsError:
        if path.read_bytes() != body:
            raise CollectionError("existing_body_integrity_conflict") from None


def collect(workspace, *, baseline_index=BASELINE, now=None, dry_run=False, fetcher=fetch):
    workspace = Path(workspace).resolve()
    now = now or datetime.now(UTC)
    if now.tzinfo is None or now.utcoffset() is None:
        raise CollectionError("timestamp_timezone_required")
    output_root = beneath(workspace, OUTPUT)
    output = beneath(output_root, "online-shopping")
    current, state = load_state(workspace, output, baseline_index)
    next_check = aware_time(state["lastAttemptAtUtc"]) + timedelta(hours=INTERVAL_HOURS)
    result = {"schemaVersion": "hongdal-online-shopping-refresh.v1", "sourceId": SOURCE_ID,
        "checkedAtUtc": stamp(now), "dryRun": dry_run, "previousObservationMonth": current["observationMonth"],
        "nextEligibleCheckAtUtc": stamp(next_check), "requests": [], "pdfRequested": False,
        "databaseImported": False, "gamePackCreated": False, "redistributionApproved": False,
        "evidenceLimit": "RawMonthlyReleaseDiscoveryOnly;NoNumericPdfParsing;NoDBOrGameConsumption"}
    if dry_run:
        result["status"] = "not_due" if now < next_check else "would_check"
        result["plannedRequestLimit"] = 0 if now < next_check else MAX_REQUESTS
        return result
    with global_lock(beneath(output_root, "collector.lock")) as acquired:
        if not acquired:
            return dict(result, status="failed", reason="collector_locked")
        current, state = load_state(workspace, output, baseline_index)
        next_check = aware_time(state["lastAttemptAtUtc"]) + timedelta(hours=INTERVAL_HOURS)
        result["nextEligibleCheckAtUtc"] = stamp(next_check)
        run_id = now.astimezone(UTC).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex[:8]
        run_dir = beneath(output, "runs", run_id)
        run_dir.mkdir(parents=True, exist_ok=False)
        result["runId"] = run_id
        if now < next_check:
            result["status"] = "not_due"
            atomic_json(beneath(run_dir, "receipt.json"), result)
            return result
        budget = Budget(now, fetcher=fetcher)
        try:
            _, body, headers = budget.get(LIST_URL, MAX_META_BYTES)
            if not (safe_header(headers, "Content-Type") or "").lower().startswith("text/html"):
                raise CollectionError("listing_content_type_invalid")
            write_blob(beneath(run_dir, "listing.html"), body)
            releases = parse_listing(body, now)
            if not releases:
                raise CollectionError("monthly_release_not_found", blocked=True)
            candidate = releases[0]
            result["discovered"] = candidate
            if candidate["observationMonth"] < current["observationMonth"]:
                raise CollectionError("listing_older_than_saved_month")
            verified = state.get("lastVerifiedRelease", {})
            previously_verified = (isinstance(verified, dict) and same_release(candidate, verified)
                and state.get("lastVerifiedRawSha256", "").lower() == current["rawSha256"].lower())
            if same_release(candidate, current) or previously_verified:
                result.update(status="unchanged", reason="same_release_no_pdf_request")
            else:
                _, post_body, post_headers = budget.get(candidate["postUrl"], MAX_META_BYTES)
                if not (safe_header(post_headers, "Content-Type") or "").lower().startswith("text/html"):
                    raise CollectionError("post_content_type_invalid")
                write_blob(beneath(run_dir, "post.html"), post_body)
                release = parse_post(post_body, candidate, now)
                result["discovered"] = release
                result["pdfRequested"] = True
                status, pdf, pdf_headers = budget.get(release["officialUrl"], MAX_PDF_BYTES,
                    current if release["officialUrl"] == current.get("officialUrl") else None)
                if status == 304:
                    if release["observationMonth"] != current["observationMonth"]:
                        raise CollectionError("new_month_pdf_not_modified")
                    result.update(status="unchanged", reason="pdf_not_modified")
                else:
                    content_type = (safe_header(pdf_headers, "Content-Type") or "").split(";", 1)[0].strip().lower()
                    if content_type not in {"application/pdf", "application/octet-stream"} or not pdf.startswith(b"%PDF-") or b"%%EOF" not in pdf[-2048:]:
                        raise CollectionError("pdf_structure_or_content_type_invalid")
                    digest = hashlib.sha256(pdf).hexdigest()
                    if digest == current["rawSha256"].lower():
                        if release["observationMonth"] != current["observationMonth"]:
                            raise CollectionError("new_month_reuses_previous_pdf")
                        result.update(status="unchanged", reason="duplicate_pdf_sha256_no_new_body")
                    else:
                        raw = beneath(output, "bodies", digest + ".pdf")
                        write_blob(raw, pdf)
                        current = dict(release, sourceId=SOURCE_ID, datasetId="online-shopping-trends-" + release["observationMonth"].replace("-", ""),
                            kind="dataset", acquisition="new", status="saved", rawPath=raw.relative_to(workspace).as_posix(),
                            rawSha256=digest, bytes=len(pdf), collectedAtUtc=stamp(now), evidenceAsOfUtc=None,
                            evidencePeriod=release["observationMonth"], rows=None, numericFacts=None,
                            etag=safe_header(pdf_headers, "ETag"), lastModified=safe_header(pdf_headers, "Last-Modified"),
                            unit="원문표 단위 유지; 새 수치 파싱/일별·동별 수요 환산 안 함", region="대한민국 조사범위; 개별판매자/동네 주문으로 미배분",
                            updateFrequency="monthly discovery; minimum 168 hours between attempts",
                            limitations=["PDF 수치/기간별 표의 새 파싱·DB/게임 반입 미실행", "같은 글/공표일의 무고지 첨부 교체는 확인하지 않음", "이번 회차는 최신 한 발표만 수집하며 빠진 과거 월은 자동 backfill하지 않음"],
                            distributionApproved=False, gameRulesAdopted=False)
                        catalog_path = beneath(output, "source-index.json")
                        catalog = read_json(catalog_path) if catalog_path.exists() else {"schemaVersion": "hongdal-online-shopping-source-index.v1", "sources": [baseline_snapshot(workspace, baseline_index)[0]]}
                        if not any(s.get("rawSha256", "").lower() == digest and s.get("listNo") == current["listNo"] for s in catalog["sources"]):
                            catalog["sources"].append(current)
                        catalog["checkedAtUtc"] = stamp(now)
                        atomic_json(catalog_path, catalog)
                        atomic_json(beneath(output, "latest.json"), current)
                        result.update(status="new", snapshot=current)
        except CollectionError as exc:
            result.update(status="failed", reason=exc.code, blocked=exc.blocked)
        except (OSError, ValueError, KeyError, TypeError, RecursionError):
            result.update(status="failed", reason="refresh_state_or_storage_invalid")
        result["requests"], result["receivedBytes"] = budget.requests, budget.bytes
        result["nextEligibleCheckAtUtc"] = stamp(now + timedelta(hours=INTERVAL_HOURS))
        atomic_json(beneath(run_dir, "receipt.json"), result)
        next_state = dict(state, schemaVersion="hongdal-online-shopping-check-state.v1",
            lastAttemptAtUtc=stamp(now), lastOutcome=result["status"], lastRunId=run_id,
            lastSuccessfulCheckAtUtc=stamp(now) if result["status"] in {"new", "unchanged"} else state.get("lastSuccessfulCheckAtUtc"))
        if result["status"] in {"new", "unchanged"}:
            next_state["lastVerifiedRelease"] = {key: result["discovered"][key]
                for key in ("observationMonth", "publicationDate", "listNo")}
            next_state["lastVerifiedRawSha256"] = current["rawSha256"]
        atomic_json(beneath(output, "state.json"), next_state)
        return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--baseline-index", default=BASELINE)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)
    try:
        result = collect(args.workspace, baseline_index=args.baseline_index, dry_run=args.dry_run)
    except CollectionError as exc:
        result = {"status": "failed", "reason": exc.code, "blocked": exc.blocked, "dryRun": args.dry_run}
    except OSError:
        result = {"status": "failed", "reason": "refresh_storage_failed", "dryRun": args.dry_run}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 1 if result["status"] == "failed" else 0


if __name__ == "__main__":
    raise SystemExit(main())

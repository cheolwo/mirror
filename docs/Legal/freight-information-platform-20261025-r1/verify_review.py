"""Validate the finished artifacts; visual completion is supplied explicitly."""
from __future__ import annotations

import argparse
import hashlib
import html
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

from pypdf import PdfReader

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ARTIFACTS = ROOT / "artifacts/local/cargo-info-publication-r2/legal"
PDF = ROOT / "output/pdf/화물정보플랫폼-법적검토-20261025.pdf"
HTML = ROOT / "output/html/화물정보플랫폼-법적검토-20261025.html"


def fingerprint(path: Path) -> dict:
    return {"path": str(path), "bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


parser = argparse.ArgumentParser()
parser.add_argument("--all-pages-visually-inspected", action="store_true")
parser.add_argument("--html-screenshots-visually-inspected", action="store_true")
args = parser.parse_args()
data = json.loads((HERE / "review-content.json").read_text(encoding="utf-8"))
reader = PdfReader(PDF)
assert len(reader.pages) == 6
page_text = [page.extract_text() or "" for page in reader.pages]
assert all(page["title"] in text for page, text in zip(data["pages"], page_text))
assert not any("\ufffd" in text or "\u25a0" in text for text in page_text)
links = [annot.get_object() for page in reader.pages for annot in page.get("/Annots", [])
         if annot.get_object().get("/Subtype") == "/Link"]
assert len(links) == 8
raw_html = HTML.read_text(encoding="utf-8")
assert raw_html.count('<section id="page-') == 6
assert "<script" not in raw_html.lower()
for page in data["pages"]:
    assert html.escape(page["intro"]) in raw_html
    for block in page["blocks"]:
        for value in ([block["text"]] if "text" in block else []):
            assert html.escape(value) in raw_html
html_qa = json.loads((ARTIFACTS / "html-validation.json").read_text(encoding="utf-8"))
assert {r["width"] for r in html_qa} == {390, 1280}
assert all(r["bodyWidth"] <= r["width"] and r["sectionCount"] == 6 and
           r["sourceLinks"] == 8 and not r["errors"] and r["scriptCount"] == 0 for r in html_qa)
sources = next(b["items"] for b in data["pages"][-1]["blocks"] if b["type"] == "sources")
for source in sources:
    assert html.escape(source["url"]) in raw_html
    assert any(str(link.get("/A", {}).get("/URI", "")) == source["url"] for link in links)
pngs = [ARTIFACTS / f"page-{n}.png" for n in range(1, 7)]
screenshots = [ARTIFACTS / f"html-{n}.png" for n in (390, 1280)]
for path in pngs + screenshots:
    assert path.read_bytes().startswith(b"\x89PNG\r\n\x1a\n")
for path in HERE.glob("*"):
    if path.is_file() and path.suffix in (".md", ".json", ".py"):
        text = path.read_text(encoding="utf-8")
        assert not any(line.rstrip() != line for line in text.splitlines()), path
        assert not any(line.startswith(("<<<<<<< ", ">>>>>>> ")) for line in text.splitlines()), path

record = {
    "recordedAt": datetime.now(timezone(timedelta(hours=9))).isoformat(),
    "scope": "Written legal/design reading material only; not agency permission or product execution evidence",
    "pdf": fingerprint(PDF), "html": fingerprint(HTML),
    "contentSource": fingerprint(HERE / "review-content.json"),
    "generator": fingerprint(HERE / "build_review.py"),
    "verifier": fingerprint(HERE / "verify_review.py"),
    "pdfPageCount": 6, "pdfOfficialLinks": 8,
    "pdfPageTextLengths": [len(t) for t in page_text],
    "pdfHeadingAndGlyphValidation": "Passed",
    "sameSourceHtmlContentChecks": "Passed",
    "htmlChecks": html_qa,
    "pdfVisualInspection": {
        "status": "All6PagesPassed" if args.all_pages_visually_inspected else "Pending",
        "method": "Poppler 110dpi page PNGs inspected with view_image for clipping, overlap, glyphs and readable spacing",
        "pages": [fingerprint(path) for path in pngs],
    },
    "htmlVisualInspection": {
        "status": "BothWidthsPassed" if args.html_screenshots_visually_inspected else "Pending",
        "screenshots": [fingerprint(path) for path in screenshots],
    },
    "legalDateChecks": {
        "asOf": "2026-10-25",
        "selectedCurrentVersionHeader": "2026-07-01",
        "law21711Promulgation": "2026-05-29",
        "warning": "Do not apply a selected consolidated-law header date to every amended provision",
        "law21970Promulgation": "2026-09-22",
        "platformProvisionsScheduledEffective": "2027-09-23",
        "certainPenaltyProvisionsSeparateEffective": "2028-01-01",
        "serviceSpecificPermitOrRegistrationApplicability": "Not determined; prepare actual activity, contract and money-flow details for agency review",
    },
    "officialSources": sources,
    "notPerformed": ["Agency inquiry or application", "Production/app/DB/Unity changes", "Service launch eligibility confirmation"],
}
(ARTIFACTS / "final-validation.json").write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"pdfSha256": record["pdf"]["sha256"], "htmlSha256": record["html"]["sha256"],
                  "pdfPages": 6, "pdfVisual": record["pdfVisualInspection"]["status"],
                  "htmlVisual": record["htmlVisualInspection"]["status"]}, indent=2))

from __future__ import annotations

import html
import json
import re
from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, KeepTogether, PageBreak, PageTemplate,
    Paragraph, Spacer, Table, TableStyle,
)


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DATA = json.loads((HERE / "review-content.json").read_text(encoding="utf-8"))
PDF = ROOT / "output/pdf/화물정보플랫폼-법적검토-20261025.pdf"
HTML = ROOT / "output/html/화물정보플랫폼-법적검토-20261025.html"
ARTIFACTS = ROOT / "artifacts/local/cargo-info-publication-r2/legal"
for folder in (PDF.parent, HTML.parent, ARTIFACTS):
    folder.mkdir(parents=True, exist_ok=True)

pdfmetrics.registerFont(TTFont("Malgun", "C:/Windows/Fonts/malgun.ttf"))
pdfmetrics.registerFont(TTFont("Malgun-Bold", "C:/Windows/Fonts/malgunbd.ttf"))
pdfmetrics.registerFontFamily("Malgun", normal="Malgun", bold="Malgun-Bold", italic="Malgun", boldItalic="Malgun-Bold")

NAVY = colors.HexColor("#13344B")
TEAL = colors.HexColor("#176C70")
INK = colors.HexColor("#243947")
MUTED = colors.HexColor("#536773")
PALE = colors.HexColor("#EDF5F5")
LINE = colors.HexColor("#D7E3E8")
PAGE_W, PAGE_H = A4
LEFT, RIGHT, TOP, BOTTOM = 48, 48, 62, 47
WIDTH = PAGE_W - LEFT - RIGHT


def style(name, size=10.2, leading=16.3, **kwargs):
    return ParagraphStyle(name, fontName="Malgun", fontSize=size, leading=leading,
                          textColor=INK, wordWrap="CJK", alignment=TA_LEFT,
                          spaceAfter=8, allowWidows=0, allowOrphans=0, **kwargs)


BODY = style("Body")
INTRO = style("Intro", 10.5, 17.1)
SMALL = style("Small", 8.8, 13.8)
TINY = style("Tiny", 8, 11.7)
H2 = ParagraphStyle(
    "H2", fontName="Malgun-Bold", fontSize=12.4, leading=17.6, textColor=NAVY,
    wordWrap="CJK", spaceBefore=11, spaceAfter=7, keepWithNext=True,
)
TITLE = ParagraphStyle("Title", fontName="Malgun-Bold", fontSize=23.5,
                       leading=32, textColor=NAVY, wordWrap="CJK", spaceAfter=14)
CELL = style("Cell", 9.3, 14.7)
CELL_HEADER = ParagraphStyle("CellHeader", parent=CELL, fontName="Malgun-Bold", textColor=colors.white)


def p(text, st=BODY):
    value = escape(text).replace("\n", "<br/>")
    value = re.sub(r"(\S+) ((?:\[\d+\])+)$", r"<nobr>\1 \2</nobr>", value)
    return Paragraph(value, st)


def box(label, text):
    label_style = ParagraphStyle("BoxLabel", parent=SMALL, fontName="Malgun-Bold", textColor=TEAL, spaceAfter=5)
    table = Table([[p(label, label_style)], [p(text)]], colWidths=[WIDTH - 26])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), PALE),
        ("BOX", (0, 0), (-1, -1), .6, LINE),
        ("LEFTPADDING", (0, 0), (-1, -1), 13),
        ("RIGHTPADDING", (0, 0), (-1, -1), 13),
        ("TOPPADDING", (0, 0), (-1, 0), 10),
        ("BOTTOMPADDING", (0, 0), (-1, 0), 0),
        ("TOPPADDING", (0, 1), (-1, 1), 0),
        ("BOTTOMPADDING", (0, 1), (-1, 1), 7),
    ]))
    return [table, Spacer(1, 10)]


def make_table(block):
    rows = [[p(cell, CELL_HEADER) for cell in block["headers"]]]
    rows += [[p(cell, CELL) for cell in row] for row in block["rows"]]
    table = Table(rows, colWidths=[WIDTH * ratio for ratio in block["widths"]], repeatRows=1)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), NAVY),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ("LINEBELOW", (0, 0), (-1, 0), .5, NAVY),
        ("LINEBELOW", (0, 1), (-1, -1), .45, LINE),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F7FAFB")]),
    ]))
    return [table, Spacer(1, 8)]


def page_decoration(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(TEAL)
    canvas.rect(LEFT, PAGE_H - 35, 26, 3, fill=1, stroke=0)
    canvas.setFont("Malgun", 8.2)
    canvas.setFillColor(MUTED)
    canvas.drawString(LEFT + 34, PAGE_H - 35, "화물 정보 플랫폼 | 법적 검토·운영 준비")
    canvas.setStrokeColor(LINE)
    canvas.line(LEFT, 35, PAGE_W - RIGHT, 35)
    canvas.setFont("Malgun", 7.6)
    canvas.drawString(LEFT, 22, "확인일 2026-10-25 | 읽기 자료 r1 | 기관 확인·운영 허용은 별도")
    canvas.drawRightString(PAGE_W - RIGHT, 22, f"{doc.page} / {len(DATA['pages'])}")
    canvas.restoreState()


def pdf_blocks(block):
    kind = block["type"]
    if kind == "heading":
        return [p(block["text"], H2)]
    if kind == "paragraph":
        return [p(block["text"])]
    if kind == "note":
        return [Spacer(1, 3), p(block["text"], SMALL)]
    if kind == "callout":
        return box(block["label"], block["text"])
    if kind == "table":
        return make_table(block)
    if kind in ("bullets", "checklist"):
        out = []
        for item in block["items"]:
            marker = "□" if kind == "checklist" else "•"
            bullet = Table([[p(marker, BODY), p(item, BODY)]], colWidths=[15, WIDTH - 15])
            bullet.setStyle(TableStyle([("VALIGN", (0, 0), (-1, -1), "TOP"),
                                       ("LEFTPADDING", (0, 0), (-1, -1), 0),
                                       ("RIGHTPADDING", (0, 0), (-1, -1), 0),
                                       ("TOPPADDING", (0, 0), (-1, -1), 0),
                                       ("BOTTOMPADDING", (0, 0), (-1, -1), 0)]))
            out.append(bullet)
        return out + [Spacer(1, 3)]
    if kind == "sources":
        out = []
        for source in block["items"]:
            title = f'<link href="{escape(source["url"], {chr(34): "&quot;"})}" color="#176C70"><b>[{source["id"]}] {escape(source["title"])}</b></link>'
            out.append(KeepTogether([Paragraph(title, SMALL), p(source["detail"], TINY), Spacer(1, 2)]))
        return out
    raise ValueError(kind)


story = []
for index, page in enumerate(DATA["pages"]):
    if index:
        story.append(PageBreak())
    story.append(p(f'{page["number"]} / {DATA["revision"]}', ParagraphStyle("Kicker", parent=SMALL, textColor=TEAL, spaceAfter=9)))
    if index == 0:
        story.append(p(DATA["title"], TITLE))
        story.append(p(DATA["subtitle"], SMALL))
        story.append(Spacer(1, 10))
        story.append(p(page["title"], H2))
    else:
        story.append(p(page["title"], TITLE))
    story.append(p(page["intro"], INTRO))
    story.append(Spacer(1, 3))
    for block in page["blocks"]:
        story.extend(pdf_blocks(block))

doc = BaseDocTemplate(str(PDF), pagesize=A4, leftMargin=LEFT, rightMargin=RIGHT,
                      topMargin=TOP, bottomMargin=BOTTOM,
                      title="화물 정보 플랫폼 법적 검토와 첫 운영 준비",
                      author="Ssalddel 프로젝트 검토 자료")
doc.addPageTemplates(PageTemplate(id="review", frames=[Frame(LEFT, BOTTOM, WIDTH,
                                                            PAGE_H - TOP - BOTTOM,
                                                            leftPadding=0, rightPadding=0,
                                                            topPadding=0, bottomPadding=0)], onPage=page_decoration))
doc.build(story)


def h(text):
    return html.escape(text)


def html_block(block):
    kind = block["type"]
    if kind == "heading":
        return f'<h3>{h(block["text"])}</h3>'
    if kind == "paragraph":
        return f'<p>{h(block["text"])}</p>'
    if kind == "note":
        return f'<aside class="note">{h(block["text"])}</aside>'
    if kind == "callout":
        return f'<aside class="callout"><strong>{h(block["label"])}</strong><p>{h(block["text"])}</p></aside>'
    if kind == "table":
        head = ''.join(f'<th>{h(c)}</th>' for c in block["headers"])
        rows = ''.join('<tr>' + ''.join(f'<td>{h(c)}</td>' for c in row) + '</tr>' for row in block["rows"])
        return f'<div class="table-wrap"><table><thead><tr>{head}</tr></thead><tbody>{rows}</tbody></table></div>'
    if kind in ("bullets", "checklist"):
        klass = ' class="checklist"' if kind == "checklist" else ''
        items = ''.join(f'<li>{h(x)}</li>' for x in block["items"])
        return f'<ul{klass}>{items}</ul>'
    if kind == "sources":
        return '<ol class="sources">' + ''.join(
            f'<li value="{h(s["id"])}"><a href="{h(s["url"])}" target="_blank" rel="noopener noreferrer">{h(s["title"])}</a><span>{h(s["detail"])}</span></li>'
            for s in block["items"]) + '</ol>'
    raise ValueError(kind)


nav = ''.join(f'<a href="#page-{h(page["number"])}">{h(page["number"])} {h(page["title"])}</a>' for page in DATA["pages"])
sections = []
for i, page in enumerate(DATA["pages"]):
    heading = f'<h1>{h(DATA["title"]).replace(chr(10), "<br>")}</h1><p class="subtitle">{h(DATA["subtitle"])}</p><h2>{h(page["title"])}</h2>' if i == 0 else f'<h2>{h(page["title"])}</h2>'
    sections.append(f'<section id="page-{h(page["number"])}"><div class="kicker">{h(page["number"])} / {h(DATA["revision"])}</div>{heading}<p class="intro">{h(page["intro"])}</p>' + ''.join(html_block(b) for b in page["blocks"]) + '</section>')

html_text = '''<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>화물 정보 플랫폼 법적 검토와 첫 운영 준비</title>
<style>
:root{color-scheme:light;--ink:#243947;--navy:#13344b;--teal:#176c70;--line:#d7e3e8}
*{box-sizing:border-box}body{margin:0;background:#eef2f4;color:var(--ink);font:16px/1.85 "Malgun Gothic","Apple SD Gothic Neo",sans-serif}
header,main,footer{max-width:900px;margin:auto}header{padding:30px 20px 12px}header p{margin:0;color:#536773;font-size:14px}
nav{display:flex;flex-wrap:wrap;gap:8px;margin-top:16px}nav a{color:var(--teal);background:#fff;border:1px solid var(--line);border-radius:6px;text-decoration:none;padding:5px 10px;font-size:13px}
section{background:#fff;padding:46px 54px;margin:20px 0;border:1px solid var(--line);border-radius:10px;scroll-margin-top:20px}
.kicker{font-size:13px;color:var(--teal);letter-spacing:.03em;margin-bottom:14px}h1{font-size:32px;line-height:1.45;color:var(--navy);margin:0 0 18px}h2{font-size:26px;line-height:1.5;color:var(--navy);margin:0 0 20px}section:first-child h2{font-size:21px;margin-top:26px}h3{font-size:19px;color:var(--navy);margin:26px 0 10px}p{margin:0 0 15px}.intro{font-size:17px}.subtitle,.note{color:#536773;font-size:14px}.note{border-left:3px solid var(--line);padding:10px 14px;margin-top:18px;background:#f7fafb}.callout{background:#edf5f5;border:1px solid var(--line);padding:18px 20px;margin:20px 0}.callout strong{display:block;color:var(--teal);margin-bottom:8px}.callout p{margin:0}.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;font-size:14px;margin:18px 0}th{background:var(--navy);color:#fff;text-align:left}td,th{padding:13px 14px;border-bottom:1px solid var(--line);vertical-align:top}td:first-child{min-width:118px}tbody tr:nth-child(even){background:#f7fafb}ul{padding-left:23px}li{padding-left:4px;margin-bottom:8px}.checklist{list-style-type:"□  "}.sources{padding-left:25px;font-size:14px}.sources span{display:block;color:#536773;font-size:12px}.sources a{color:var(--teal);font-weight:bold}footer{padding:20px;font-size:13px;color:#536773}
@media(max-width:640px){section{padding:28px 22px;border-radius:0;margin:12px 0}h1{font-size:27px}h2{font-size:23px}body{font-size:15px}.intro{font-size:16px}th,td{padding:10px}}
@media print{@page{size:A4;margin:16mm}body{background:white;font-size:10pt;line-height:1.6}header,nav{display:none}section{break-after:page;border:0;border-radius:0;margin:0;padding:0}section:last-child{break-after:auto}h1{font-size:22pt}h2{font-size:19pt}h3{font-size:13pt}.intro{font-size:11pt}table{font-size:9pt}th{print-color-adjust:exact;-webkit-print-color-adjust:exact}.callout,.note{break-inside:avoid}footer{display:none}}
</style></head><body><header><p>확인일 2026-10-25 · 읽기 자료 r1 · PDF와 같은 내용 · 브라우저 인쇄 가능</p><nav>''' + nav + '</nav></header><main>' + ''.join(sections) + '</main><footer>법령 확인과 사업 적용 판단, 기관 확인 및 운영 허용은 별도입니다. 원문은 위 링크에서 다시 확인할 수 있습니다.</footer></body></html>'
HTML.write_text(html_text, encoding="utf-8")

from pypdf import PdfReader
reader = PdfReader(PDF)
if len(reader.pages) != len(DATA["pages"]):
    raise RuntimeError(f"Expected {len(DATA['pages'])} pages, generated {len(reader.pages)}")
page_text = [page.extract_text() or "" for page in reader.pages]
for index, (text, page) in enumerate(zip(page_text, DATA["pages"]), start=1):
    if page["title"] not in text:
        raise RuntimeError(f"Page {index} heading missing")
if any("\u25a0" in text or "\ufffd" in text for text in page_text):
    raise RuntimeError("Unexpected missing glyph marker")
links = sum(1 for page in reader.pages for annot in page.get("/Annots", [])
            if annot.get_object().get("/Subtype") == "/Link")
validation = {"pages": len(reader.pages), "pageTextLengths": [len(t) for t in page_text],
              "pdfLinks": links, "pdf": str(PDF), "html": str(HTML),
              "sameContentSource": str(HERE / "review-content.json"),
              "visualReview": "PendingAllPagePngInspection"}
(ARTIFACTS / "text-validation.json").write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(validation, ensure_ascii=False, indent=2))

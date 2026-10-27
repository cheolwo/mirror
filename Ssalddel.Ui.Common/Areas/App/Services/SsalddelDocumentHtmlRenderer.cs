using static Ssalddel.Ui.Common.Areas.App.Services.SsalddelDocumentIdentifierResolver;
using System.Net;
using System.Text;
using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Common.Identifiers;
using static Ssalddel.Ui.Common.Areas.App.Services.SsalddelPrintLayoutResolver;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>운송장·품목표·감열 전표의 HTML과 식별코드 표시만 구성합니다.</summary>
internal sealed class SsalddelDocumentHtmlRenderer(ISsalddelIdentifierCodeGenerator identifierCodeGenerator)
{
    internal static string BuildWaybillHtml(SsalddelWaybillDocumentDraft draft, string title)
    {
        var receiptText = draft.ReceiptRequired ? "인수증 필요" : "인수증 없음";
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"ko\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{E(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Arial,'Malgun Gothic',sans-serif;margin:0;color:#111827;background:#fff;}");
        html.AppendLine(".sheet{width:190mm;min-height:267mm;margin:0 auto;padding:14mm;box-sizing:border-box;}");
        html.AppendLine(".head{display:flex;justify-content:space-between;gap:16px;border-bottom:2px solid #111827;padding-bottom:10px;}");
        html.AppendLine(".title{font-size:26px;font-weight:800;}.docno{font-size:13px;color:#4b5563;text-align:right;}");
        html.AppendLine(".grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:16px;}");
        html.AppendLine(".box{border:1px solid #d1d5db;border-radius:6px;padding:12px;break-inside:avoid;}");
        html.AppendLine(".label{font-size:12px;color:#6b7280;font-weight:700;margin-bottom:6px;}.value{font-size:16px;font-weight:700;}");
        html.AppendLine(".meta{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:12px;}");
        html.AppendLine(".memo{white-space:pre-wrap;line-height:1.5;}.money{font-size:18px;font-weight:800;}");
        html.AppendLine("@media print{body{background:#fff}.sheet{width:auto;min-height:auto;margin:0;padding:0}.no-print{display:none!important}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main class=\"sheet\">");
        html.AppendLine("<section class=\"head\">");
        html.AppendLine("<div><div class=\"title\">살뜰 운송장</div><div>운송 접수 및 배차 확인용</div></div>");
        html.AppendLine($"<div class=\"docno\"><strong>{E(draft.DocumentNo)}</strong><br>{draft.CreatedAt:yyyy-MM-dd HH:mm}</div>");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"grid\">");
        AppendBox(html, "화물", draft.CargoName, $"{draft.TransportMode} · {draft.VehicleCondition}");
        AppendBox(html, "정산", draft.PaymentMethod, $"{receiptText} · 예상비용 {draft.ExpectedCost:N0}원");
        AppendBox(html, "상차", draft.PickupPlace, $"{draft.PickupAddress}{Environment.NewLine}{draft.PickupTime}");
        AppendBox(html, "하차", draft.DropoffPlace, $"{draft.DropoffAddress}{Environment.NewLine}{draft.DropoffTime}");
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"meta\">");
        AppendBox(html, "예상 운임", $"{draft.ExpectedFare:N0}원", string.Empty, "money");
        AppendBox(html, "인수증", receiptText, string.Empty);
        AppendBox(html, "출력일", draft.CreatedAt.ToString("yyyy-MM-dd"), string.Empty);
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"box\" style=\"margin-top:12px\">");
        html.AppendLine("<div class=\"label\">메모</div>");
        html.AppendLine($"<div class=\"memo\">{E(draft.Memo)}</div>");
        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    internal string BuildExpectedItemsHtml(SsalddelExpectedItemDocumentDraft draft, string fallbackTitle)
    {
        var layout = ResolvePrintLayout(draft.PrintLayout);
        var documentPayload = draft.DocumentBarcodePayload
            ?? SsalddelIdentifierCodePayloads.Create(SsalddelIdentifierKindCode.Unknown, draft.DocumentNo);

        if (layout.UseReceiptSlipLayout)
        {
            return BuildExpectedItemsReceiptSlipHtml(draft, fallbackTitle, documentPayload, layout);
        }

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"ko\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{E(draft.Title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine($"@page{{size:{layout.PageSizeCss};margin:{layout.PageMarginCss};}}");
        html.AppendLine($":root{{--sheet-width:{layout.SheetWidthCss};--sheet-min-height:{layout.SheetMinHeightCss};--sheet-padding:{layout.SheetPaddingCss};--gap:{layout.GapCss};--head-gap:{layout.HeadGapCss};--title-font:{layout.TitleFontCss};--subtitle-font:{layout.SubtitleFontCss};--meta-columns:{layout.MetaColumns};--box-padding:{layout.BoxPaddingCss};--value-font:{layout.ValueFontCss};--table-font:{layout.TableFontCss};--table-head-font:{layout.TableHeadFontCss};--cell-padding:{layout.CellPaddingCss};--qty-font:{layout.QuantityFontCss};--doc-code-width:{layout.DocumentCodeWidthCss};--doc-qr-column:{layout.DocumentQrColumnCss};--barcode-cell-width:{layout.BarcodeCellWidthCss};--line-code-width:{layout.LineCodeWidthCss};--foot-font:{layout.FootFontCss};}}");
        html.AppendLine("body{font-family:Arial,'Malgun Gothic',sans-serif;margin:0;color:#111827;background:#f3f4f6;}");
        html.AppendLine(".sheet{width:var(--sheet-width);min-height:var(--sheet-min-height);margin:0 auto;background:#fff;padding:var(--sheet-padding);box-sizing:border-box;}");
        html.AppendLine(".head{display:grid;grid-template-columns:1fr var(--doc-code-width);gap:var(--head-gap);border-bottom:2px solid #111827;padding-bottom:var(--gap);}");
        html.AppendLine(".title{font-size:var(--title-font);font-weight:800;margin-bottom:4px;}.subtitle{font-size:var(--subtitle-font);color:#4b5563;line-height:1.45;}");
        html.AppendLine(".doc-code{border:1px solid #d1d5db;border-radius:6px;padding:var(--box-padding);display:grid;grid-template-columns:var(--doc-qr-column) 1fr;gap:var(--gap);align-items:center;}.doc-code.no-qr{grid-template-columns:1fr;}");
        html.AppendLine(".doc-code svg{max-width:100%;height:auto;display:block;}.raw{font-family:Consolas,'Courier New',monospace;font-size:11px;overflow-wrap:anywhere;}");
        html.AppendLine(".meta{display:grid;grid-template-columns:repeat(var(--meta-columns),1fr);gap:var(--gap);margin-top:var(--gap);}.box{border:1px solid #d1d5db;border-radius:6px;padding:var(--box-padding);break-inside:avoid;}");
        html.AppendLine(".label{font-size:var(--table-head-font);color:#6b7280;font-weight:700;margin-bottom:3px;}.value{font-size:var(--value-font);font-weight:700;line-height:1.3;white-space:pre-wrap;}");
        html.AppendLine("table{width:100%;border-collapse:collapse;margin-top:var(--gap);font-size:var(--table-font);}th{background:#f3f4f6;text-align:left;color:#374151;font-size:var(--table-head-font);}th,td{border:1px solid #d1d5db;padding:var(--cell-padding);vertical-align:top;}");
        html.AppendLine(".barcode-cell{width:var(--barcode-cell-width);}.line-code{display:block;max-width:var(--line-code-width);overflow:hidden;}.line-code svg{max-width:var(--line-code-width);height:auto;display:block;}.qty{font-size:var(--qty-font);font-weight:800;}");
        html.AppendLine(".memo{margin-top:var(--gap);white-space:pre-wrap;line-height:1.4;}.foot{display:flex;justify-content:space-between;gap:var(--gap);margin-top:var(--gap);color:#6b7280;font-size:var(--foot-font);}");
        html.AppendLine("@media screen and (max-width: 900px){.sheet{width:100%;min-height:auto}.head{grid-template-columns:1fr}.meta{grid-template-columns:repeat(2,1fr)}}");
        html.AppendLine("@media print{body{background:#fff}.sheet{width:auto;min-height:auto;margin:0;padding:0}.box,.doc-code{break-inside:avoid}thead{display:table-header-group}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine($"<main class=\"sheet\" data-paper=\"{E(layout.PaperSize)}\" data-density=\"{E(layout.Density)}\" data-orientation=\"{E(layout.Orientation)}\">");
        html.AppendLine("<section class=\"head\">");
        html.AppendLine("<div>");
        html.AppendLine($"<div class=\"title\">{E(string.IsNullOrWhiteSpace(draft.Title) ? fallbackTitle : draft.Title)}</div>");
        html.AppendLine($"<div class=\"subtitle\">문서번호 {E(draft.DocumentNo)} · 상태 {E(draft.Status)} · 출력 {draft.CreatedAt:yyyy-MM-dd HH:mm}</div>");
        html.AppendLine("</div>");
        AppendDocumentCode(html, documentPayload, layout);
        html.AppendLine("</section>");
        html.AppendLine("<section class=\"meta\">");
        AppendBox(html, "창고", draft.WarehouseName, string.Empty);
        AppendBox(html, "소유/화주", draft.OwnerName, string.Empty);
        AppendBox(html, "상대/공급·수령", draft.CounterpartyName, string.Empty);
        AppendBox(html, "예정일", draft.ExpectedDateText, string.Empty);
        AppendBox(html, "주문 번호", draft.OrderNo, string.Empty);
        AppendBox(html, "원장 번호", draft.LedgerNo, string.Empty);
        AppendBox(html, "품목 수", $"{draft.Lines.Count:N0}건", string.Empty);
        AppendBox(html, "총 예정 수량", $"{draft.Lines.Sum(x => Math.Max(0, x.Quantity)):N0}", string.Empty);
        html.AppendLine("</section>");
        AppendExpectedItemTable(html, draft.Lines, layout);
        if (!string.IsNullOrWhiteSpace(draft.WorkMemo))
        {
            html.AppendLine("<section class=\"box memo\">");
            html.AppendLine("<div class=\"label\">작업 메모</div>");
            html.AppendLine($"<div>{E(draft.WorkMemo)}</div>");
            html.AppendLine("</section>");
        }

        html.AppendLine("<section class=\"foot\">");
        html.AppendLine("<div>바코드는 현장 스캔, 검수, 피킹, 묶음 확인을 위한 보조 식별자입니다.</div>");
        html.AppendLine("<div>Ssalddel WebAssembly document output</div>");
        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private string BuildExpectedItemsReceiptSlipHtml(
        SsalddelExpectedItemDocumentDraft draft,
        string fallbackTitle,
        SsalddelIdentifierCodePayload documentPayload,
        SsalddelPrintLayoutProfile layout)
    {
        var title = string.IsNullOrWhiteSpace(draft.Title) ? fallbackTitle : draft.Title;
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"ko\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine($"<title>{E(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine($"@page{{size:{layout.PageSizeCss};margin:{layout.PageMarginCss};}}");
        html.AppendLine($":root{{--slip-width:{layout.SheetWidthCss};--slip-padding:{layout.SheetPaddingCss};--gap:{layout.GapCss};--title-font:{layout.TitleFontCss};--body-font:{layout.TableFontCss};--small-font:{layout.TableHeadFontCss};--qty-font:{layout.QuantityFontCss};--line-code-width:{layout.LineCodeWidthCss};}}");
        html.AppendLine("body{font-family:Arial,'Malgun Gothic',sans-serif;margin:0;color:#111827;background:#fff;}");
        html.AppendLine(".receipt-slip{width:var(--slip-width);margin:0 auto;padding:var(--slip-padding);box-sizing:border-box;background:#fff;font-size:var(--body-font);}");
        html.AppendLine(".center{text-align:center}.slip-title{font-size:var(--title-font);font-weight:800;line-height:1.25}.slip-subtitle{font-size:var(--small-font);color:#4b5563;line-height:1.35;margin-top:2px;}");
        html.AppendLine(".divider{border-top:1px dashed #111827;margin:var(--gap) 0}.row{display:flex;justify-content:space-between;gap:6px;line-height:1.35}.row span:first-child{color:#4b5563}.row strong{font-weight:800;text-align:right;}");
        html.AppendLine(".doc-code{margin-top:var(--gap)}.doc-code svg{display:block;max-width:100%;height:auto;margin:2px auto}.raw{font-family:Consolas,'Courier New',monospace;font-size:var(--small-font);overflow-wrap:anywhere;line-height:1.25;}");
        html.AppendLine(".item{padding:var(--gap) 0;break-inside:avoid}.item-head{display:flex;justify-content:space-between;gap:6px;align-items:flex-start}.item-name{font-size:var(--body-font);font-weight:800;line-height:1.25}.qty{font-size:var(--qty-font);font-weight:900;white-space:nowrap;text-align:right}.item-meta{font-size:var(--small-font);color:#374151;line-height:1.35;margin-top:2px;}");
        html.AppendLine(".line-code{display:block;max-width:var(--line-code-width);margin-top:2px;overflow:hidden}.line-code svg{display:block;max-width:var(--line-code-width);height:auto}.total{font-weight:900;font-size:var(--qty-font)}.memo{white-space:pre-wrap;line-height:1.35}.foot{font-size:var(--small-font);color:#6b7280;line-height:1.35;margin-top:var(--gap)}");
        html.AppendLine("@media print{body{background:#fff}.receipt-slip{width:auto;margin:0;padding:0}.item,.doc-code{break-inside:avoid}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine($"<main class=\"receipt-slip\" data-paper=\"{E(layout.PaperSize)}\" data-density=\"{E(layout.Density)}\" data-print-style=\"{E(layout.PrintStyle)}\">");
        html.AppendLine("<section class=\"center\">");
        html.AppendLine($"<div class=\"slip-title\">{E(title)}</div>");
        html.AppendLine($"<div class=\"slip-subtitle\">{E(draft.WarehouseName)}</div>");
        html.AppendLine($"<div class=\"slip-subtitle\">{draft.CreatedAt:yyyy-MM-dd HH:mm}</div>");
        html.AppendLine("</section>");
        html.AppendLine("<div class=\"divider\"></div>");
        AppendSlipRow(html, "전표번호", draft.DocumentNo);
        AppendSlipRow(html, "상태", draft.Status);
        AppendSlipRow(html, "예정", draft.ExpectedDateText);
        AppendSlipRow(html, "주문", draft.OrderNo);
        AppendSlipRow(html, "원장", draft.LedgerNo);
        AppendSlipRow(html, "담당", draft.OwnerName);
        AppendSlipRow(html, "상대", draft.CounterpartyName);
        AppendReceiptDocumentCode(html, documentPayload, layout);
        html.AppendLine("<div class=\"divider\"></div>");
        foreach (var line in draft.Lines)
        {
            AppendReceiptItemLine(html, line, layout);
        }

        if (draft.Lines.Count == 0)
        {
            html.AppendLine("<div class=\"item\">예정 품목이 없습니다.</div>");
        }

        html.AppendLine("<div class=\"divider\"></div>");
        AppendSlipRow(html, "품목 수", $"{draft.Lines.Count:N0}건");
        AppendSlipRow(html, "총 수량", $"{draft.Lines.Sum(x => Math.Max(0, x.Quantity)):N0}");
        if (!string.IsNullOrWhiteSpace(draft.WorkMemo))
        {
            html.AppendLine("<div class=\"divider\"></div>");
            html.AppendLine("<div class=\"memo\">");
            html.AppendLine(E(draft.WorkMemo));
            html.AppendLine("</div>");
        }

        html.AppendLine("<div class=\"divider\"></div>");
        html.AppendLine("<section class=\"center foot\">");
        html.AppendLine("<div>스캔 후 피킹/포장/인계 상태를 확인하세요.</div>");
        html.AppendLine("<div>Ssalddel order slip</div>");
        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private void AppendReceiptDocumentCode(StringBuilder html, SsalddelIdentifierCodePayload payload, SsalddelPrintLayoutProfile layout)
    {
        html.AppendLine("<section class=\"doc-code center\">");
        html.AppendLine($"<div class=\"raw\">{E(payload.RawCode)}</div>");
        if (layout.IncludeDocumentQrCode)
        {
            var qr = identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
                payload,
                SsalddelMachineReadableCodeFormatCode.QrCode,
                layout.DocumentQrSizePx,
                layout.DocumentQrSizePx,
                1));
            html.AppendLine(qr.SvgMarkup);
        }

        var barcode = identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
            payload,
            SsalddelMachineReadableCodeFormatCode.Code128,
            layout.DocumentBarcodeWidthPx,
            layout.DocumentBarcodeHeightPx,
            1));
        html.AppendLine(barcode.SvgMarkup);
        html.AppendLine("</section>");
    }

    private void AppendReceiptItemLine(StringBuilder html, SsalddelExpectedItemDocumentLine line, SsalddelPrintLayoutProfile layout)
    {
        var payload = ResolveLineBarcodePayload(line);
        html.AppendLine("<section class=\"item\">");
        html.AppendLine("<div class=\"item-head\">");
        html.AppendLine("<div>");
        html.AppendLine($"<div class=\"item-name\">{E(line.ProductName)}</div>");
        html.AppendLine($"<div class=\"item-meta\">{E(line.Sku)} · {E(line.LocationCode)} · {E(line.StorageCondition)}</div>");
        html.AppendLine("</div>");
        html.AppendLine($"<div class=\"qty\">{Math.Max(0, line.Quantity):N0}{E(line.Unit)}</div>");
        html.AppendLine("</div>");
        html.AppendLine($"<div class=\"item-meta\">주문 {E(line.RelatedOrderNo)} / 묶음 {E(line.BundleBarcode)}</div>");
        if (!string.IsNullOrWhiteSpace(line.Note))
        {
            html.AppendLine($"<div class=\"item-meta\">{E(line.Note)}</div>");
        }

        html.AppendLine($"<div class=\"raw\">{E(payload.RawCode)}</div>");
        if (layout.IncludeLineBarcodes)
        {
            var barcode = identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
                payload,
                SsalddelMachineReadableCodeFormatCode.Code128,
                layout.LineBarcodeWidthPx,
                layout.LineBarcodeHeightPx,
                1));
            html.AppendLine("<span class=\"line-code\">");
            html.AppendLine(barcode.SvgMarkup);
            html.AppendLine("</span>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendSlipRow(StringBuilder html, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        html.AppendLine("<div class=\"row\">");
        html.AppendLine($"<span>{E(label)}</span>");
        html.AppendLine($"<strong>{E(value)}</strong>");
        html.AppendLine("</div>");
    }

    private void AppendDocumentCode(StringBuilder html, SsalddelIdentifierCodePayload payload, SsalddelPrintLayoutProfile layout)
    {
        SsalddelIdentifierCodeImage? qr = null;
        if (layout.IncludeDocumentQrCode)
        {
            qr = identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
                payload,
                SsalddelMachineReadableCodeFormatCode.QrCode,
                layout.DocumentQrSizePx,
                layout.DocumentQrSizePx,
                1));
        }

        var barcode = identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
            payload,
            SsalddelMachineReadableCodeFormatCode.Code128,
            layout.DocumentBarcodeWidthPx,
            layout.DocumentBarcodeHeightPx,
            1));

        html.AppendLine(layout.IncludeDocumentQrCode ? "<div class=\"doc-code\">" : "<div class=\"doc-code no-qr\">");
        if (qr is not null)
        {
            html.AppendLine(qr.SvgMarkup);
        }

        html.AppendLine("<div>");
        html.AppendLine("<div class=\"label\">문서 바코드</div>");
        html.AppendLine($"<div class=\"raw\">{E(payload.RawCode)}</div>");
        html.AppendLine(barcode.SvgMarkup);
        html.AppendLine("</div>");
        html.AppendLine("</div>");
    }

    private void AppendExpectedItemTable(StringBuilder html, IReadOnlyList<SsalddelExpectedItemDocumentLine> lines, SsalddelPrintLayoutProfile layout)
    {
        html.AppendLine("<table>");
        html.AppendLine("<thead><tr><th>품목</th><th>예정 수량</th><th>위치/보관</th><th>관련 번호</th><th class=\"barcode-cell\">품목 바코드</th></tr></thead>");
        html.AppendLine("<tbody>");
        foreach (var line in lines)
        {
            var payload = ResolveLineBarcodePayload(line);
            var barcode = layout.IncludeLineBarcodes
                ? identifierCodeGenerator.Generate(new SsalddelIdentifierCodeImageRequest(
                    payload,
                    SsalddelMachineReadableCodeFormatCode.Code128,
                    layout.LineBarcodeWidthPx,
                    layout.LineBarcodeHeightPx,
                    1))
                : null;

            html.AppendLine("<tr>");
            html.AppendLine("<td>");
            html.AppendLine($"<div class=\"value\">{E(line.ProductName)}</div>");
            html.AppendLine($"<div>{E(line.Sku)}</div>");
            html.AppendLine($"<div>{E(line.LineNo)}</div>");
            html.AppendLine("</td>");
            html.AppendLine($"<td><div class=\"qty\">{Math.Max(0, line.Quantity):N0}</div><div>{E(line.Unit)}</div></td>");
            html.AppendLine("<td>");
            html.AppendLine($"<div>{E(line.LocationCode)}</div>");
            html.AppendLine($"<div>{E(line.StorageCondition)}</div>");
            html.AppendLine("</td>");
            html.AppendLine("<td>");
            html.AppendLine($"<div>주문 {E(line.RelatedOrderNo)}</div>");
            html.AppendLine($"<div>묶음 {E(line.BundleBarcode)}</div>");
            if (!string.IsNullOrWhiteSpace(line.Note))
            {
                html.AppendLine($"<div>{E(line.Note)}</div>");
            }

            html.AppendLine("</td>");
            html.AppendLine("<td class=\"barcode-cell\">");
            html.AppendLine($"<div class=\"raw\">{E(payload.RawCode)}</div>");
            if (barcode is not null)
            {
                html.AppendLine("<span class=\"line-code\">");
                html.AppendLine(barcode.SvgMarkup);
                html.AppendLine("</span>");
            }

            html.AppendLine("</td>");
            html.AppendLine("</tr>");
        }

        if (lines.Count == 0)
        {
            html.AppendLine("<tr><td colspan=\"5\">예정 품목이 없습니다.</td></tr>");
        }

        html.AppendLine("</tbody>");
        html.AppendLine("</table>");
    }

    private static void AppendBox(StringBuilder html, string label, string value, string subText, string valueClass = "value")
    {
        html.AppendLine("<section class=\"box\">");
        html.AppendLine($"<div class=\"label\">{E(label)}</div>");
        html.AppendLine($"<div class=\"{valueClass}\">{E(value)}</div>");
        if (!string.IsNullOrWhiteSpace(subText))
        {
            html.AppendLine($"<div style=\"margin-top:6px;color:#4b5563;white-space:pre-wrap\">{E(subText)}</div>");
        }

        html.AppendLine("</section>");
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);

}

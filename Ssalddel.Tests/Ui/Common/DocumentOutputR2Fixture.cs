using System.Globalization;
using System.Net;
using System.Text.Json;
using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

internal static class DocumentOutputR2Fixture
{
    internal static Dictionary<string, SsalddelDocumentOutput> Create()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var result = new Dictionary<string, SsalddelDocumentOutput>(StringComparer.Ordinal);
            var profiles = new[]
            {
                ("default", "a4", "compact", "portrait", "sheet", true, true),
                ("a5", "a5", "comfortable", "landscape", "sheet", true, false),
                ("a6", "a6", "dense", "portrait", "sheet", false, true),
                ("thermal80", "thermal-80", "normal", "landscape", "sheet", true, true),
                ("thermal58", "thermal-58", "dense", "portrait", "sheet", false, true),
                ("alias80", "80mm", "comfortable", "landscape", "receipt-slip", true, false),
                ("alias58", "receipt-58", "dense", "portrait", "receipt-slip", false, false),
                ("unknown", "unknown", "unknown", "unknown", "unknown", true, true),
                ("hidden-codes", "a4", "dense", "portrait", "sheet", false, false),
                ("null-layout", "a4", "compact", "portrait", "sheet", true, true)
            };
            foreach (var (name, paper, density, orientation, style, qr, lineCode) in profiles)
            {
                foreach (var inbound in new[] { true, false })
                {
                    var service = new SsalddelDocumentOutputService(new RecordingCodeGenerator());
                    var draft = new SsalddelExpectedItemDocumentDraft
                    {
                        DocumentNo = "DOC-TEST-01", Title = "", Status = "검수 <대기>",
                        WarehouseName = "창고 & A", OwnerName = "소유자", CounterpartyName = "상대",
                        OrderNo = "ORDER-01", LedgerNo = "LEDGER-01", ExpectedDateText = "오늘",
                        WorkMemo = "첫 줄\n다음 <줄>", CreatedAt = new(2026, 10, 20, 10, 30, 0, TimeSpan.FromHours(9)),
                        PrintLayout = name == "null-layout" ? null! : new()
                        {
                            PaperSize = paper, Density = density, Orientation = orientation,
                            PrintStyle = style, IncludeDocumentQrCode = qr, IncludeLineBarcodes = lineCode
                        },
                        Lines =
                        [
                            new() { LineNo = "1", Sku = "SKU-A", ProductName = "품목 <A>", Quantity = 3, Unit = "개",
                                ProductBarcode = "SKU:A", BundleBarcode = "BND:A", LocationCode = "A-01", StorageCondition = "상온",
                                RelatedOrderNo = "ORDER-01", Note = "확인 & 포장" },
                            new() { LineNo = "2", ProductName = "코드 없는 품목", Quantity = 1, Note = "" }
                        ]
                    };
                    result.Add(name + (inbound ? ":in" : ":out"), inbound
                        ? service.CreateInboundExpectedItems(draft) : service.CreateOutboundExpectedItems(draft));
                }
            }
            foreach (var receipt in new[] { true, false })
            {
                var service = new SsalddelDocumentOutputService(new RecordingCodeGenerator());
                result.Add("waybill:" + receipt, service.CreateWaybill(new(
                    "WAY-01", "화물 & 품목", "직송", "회전 불가", "상차지", "상차 <주소>", "10:00",
                    "하차지", "하차 주소", "11:00", "직접 지급", receipt, 12345.67m, 2345.67m,
                    "메모\n둘째 줄", new(2026, 10, 20, 10, 30, 0, TimeSpan.FromHours(9)))));
            }
            return result;
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    internal static string NormalizedJson(SsalddelDocumentOutput value) => JsonSerializer.Serialize(value with
    {
        Html = value.Html.Replace("\r\n", "\n", StringComparison.Ordinal),
        PlainText = value.PlainText.Replace("\r\n", "\n", StringComparison.Ordinal)
    });

    private sealed class RecordingCodeGenerator : ISsalddelIdentifierCodeGenerator
    {
        private int count;
        public SsalddelIdentifierCodeImage Generate(SsalddelIdentifierCodeImageRequest request)
            => new(request.Format, request.Payload.RawCode,
                $"<svg data-call='{++count}' data-format='{WebUtility.HtmlEncode(request.Format)}' data-code='{WebUtility.HtmlEncode(request.Payload.RawCode)}' width='{request.Width}' height='{request.Height}' margin='{request.Margin}' />", "fixture");
    }
}

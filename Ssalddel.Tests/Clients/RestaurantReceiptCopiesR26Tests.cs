using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Common.Identifiers;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>기기 설정 core와 실제 HTML 생성 계약입니다. MAUI Preferences나 물리 인쇄 실행 증거가 아닙니다.</summary>
public sealed class RestaurantReceiptCopiesR26Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void 설정이없거나지원하지않는저장값이면기본두장을읽고_저장소를임의로덮어쓰지않는다(int? stored)
    {
        var store = new Store { Value = stored };
        var service = new 음식점전표출력설정Service(store);

        Assert.Equal(2, service.현재조회().출력장수);
        Assert.Equal(음식점전표출력설정Service.DefaultCopies, service.현재조회().출력장수);
        Assert.Equal(stored, store.Value);
        Assert.Equal(2, store.Reads);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task 저장한한장또는두장을새서비스에서도읽고_기존인스턴스도캐시없이현재값을읽는다(int copies)
    {
        var store = new Store();
        var first = new 음식점전표출력설정Service(store);
        var before = first.현재조회();

        await first.저장Async(copies);
        var restarted = new 음식점전표출력설정Service(store);
        Assert.Equal(copies, restarted.현재조회().출력장수);
        store.Value = copies == 1 ? 2 : 1;

        Assert.Equal(store.Value, first.현재조회().출력장수);
        Assert.Equal(2, before.출력장수);
        Assert.Equal(1, store.Writes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public async Task 지원하지않는입력은저장하지않고기존장수를보존한다(int invalid)
    {
        var store = new Store { Value = 1 };
        var service = new 음식점전표출력설정Service(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.저장Async(invalid));

        Assert.Equal(1, service.현재조회().출력장수);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task 저장소읽기와쓰기실패를성공이나기본값으로숨기지않고_실패후기존값을다시읽는다()
    {
        var readFailure = new IOException("controlled read failure");
        var writeFailure = new IOException("controlled write failure");
        var store = new Store { Value = 1, ReadFailure = readFailure };
        var service = new 음식점전표출력설정Service(store);

        Assert.Same(readFailure, Assert.Throws<IOException>(() => service.현재조회()));
        store.ReadFailure = null;
        store.WriteFailure = writeFailure;
        Assert.Same(writeFailure, await Assert.ThrowsAsync<IOException>(() => service.저장Async(2)));
        Assert.Equal(1, store.Value);
        Assert.Equal(1, service.현재조회().출력장수);
        store.WriteFailure = null;
        await service.저장Async(2);
        Assert.Equal(2, new 음식점전표출력설정Service(store).현재조회().출력장수);
    }

    [Fact]
    public async Task 취소된저장요청은기기설정을쓰지않는다()
    {
        var store = new Store { Value = 1 };
        var service = new 음식점전표출력설정Service(store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.저장Async(2, cancellation.Token));

        Assert.Equal(0, store.Writes);
        Assert.Equal(1, store.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 전표구성은원본과기존레이아웃을바꾸지않고_모든주문필드와코드와수량을보존한다(int copies)
    {
        var original = Draft(new()
        {
            PaperSize = SsalddelDocumentPaperSizeCode.Thermal58,
            Density = SsalddelDocumentDensityCode.Comfortable,
            Orientation = SsalddelDocumentOrientationCode.Landscape,
            PrintStyle = SsalddelDocumentPrintStyleCode.ReceiptSlip,
            IncludeDocumentQrCode = false, IncludeLineBarcodes = false,
            ReceiptCopies = 1, ReceiptCopyLabels = ["원본 표제"]
        });
        var originalJson = JsonSerializer.Serialize(original);

        var configured = 음식점전표출력구성.Apply(original, copies);

        Assert.NotSame(original, configured);
        Assert.NotSame(original.PrintLayout, configured.PrintLayout);
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
        foreach (var field in typeof(SsalddelExpectedItemDocumentDraft).GetProperties().Where(x => x.Name != nameof(original.PrintLayout)))
            Assert.Equal(field.GetValue(original), field.GetValue(configured));
        foreach (var field in typeof(SsalddelDocumentPrintLayoutOptions).GetProperties()
                     .Where(x => x.Name is not (nameof(original.PrintLayout.ReceiptCopies) or nameof(original.PrintLayout.ReceiptCopyLabels))))
            Assert.Equal(field.GetValue(original.PrintLayout), field.GetValue(configured.PrintLayout));
        Assert.Equal(copies, configured.PrintLayout.ReceiptCopies);
        Assert.Equal(copies == 1 ? new[] { "조리·포장용" } : new[] { "조리용", "포장용" }, configured.PrintLayout.ReceiptCopyLabels);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void 전표구성도지원하지않는장수를거절한다(int copies)
        => Assert.Throws<ArgumentOutOfRangeException>(() => 음식점전표출력구성.Apply(Draft(), copies));

    [Fact]
    public void 전표원본이없으면출력용문서를만들지않는다()
        => Assert.Throws<ArgumentNullException>(() => 음식점전표출력구성.Apply(null!, 2));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 일반문서에는빌지장수를적용하지않고원래레이아웃을보존한다(int copies)
    {
        var draft = Draft(new() { PaperSize = SsalddelDocumentPaperSizeCode.A4, PrintStyle = SsalddelDocumentPrintStyleCode.Sheet });
        var before = JsonSerializer.Serialize(draft);

        Assert.Throws<ArgumentException>(() => 음식점전표출력구성.Apply(draft, copies));

        Assert.Equal(before, JsonSerializer.Serialize(draft));
        Assert.Equal(1, draft.PrintLayout.ReceiptCopies);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 실제문서서비스는동일메뉴와수량과바코드를한개HTML의선택한장수만큼구성한다(int copies)
    {
        var service = new SsalddelDocumentOutputService(new ZxingSsalddelIdentifierCodeGenerator());
        var output = service.CreateOutboundExpectedItems(음식점전표출력구성.Apply(Draft(), copies));
        var slips = Regex.Matches(output.Html, "<main class=\"receipt-slip\"[^>]*>[\\s\\S]*?</main>", RegexOptions.CultureInvariant);

        Assert.Equal(copies, slips.Count);
        Assert.Equal(1, Regex.Matches(output.Html, "<!doctype html>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count);
        Assert.Equal(1, Regex.Matches(output.Html, "<html\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count);
        Assert.All(slips.Cast<Match>(), slip =>
        {
            Assert.Contains("수제버거 &amp; 감자", slip.Value, StringComparison.Ordinal);
            Assert.Contains("<div class=\"qty\">3개</div>", slip.Value, StringComparison.Ordinal);
            Assert.Contains("ORDER-R26", slip.Value, StringComparison.Ordinal);
            Assert.Contains("SKU:FOOD-R26", slip.Value, StringComparison.Ordinal);
            Assert.Contains("<svg", slip.Value, StringComparison.OrdinalIgnoreCase);
        });
        if (copies == 1) Assert.Contains(WebUtility.HtmlEncode("조리·포장용"), output.Html, StringComparison.Ordinal);
        else
        {
            Assert.Contains("조리용", slips[0].Value, StringComparison.Ordinal);
            Assert.Contains("포장용", slips[1].Value, StringComparison.Ordinal);
        }
        Assert.Contains("SKU:FOOD-R26", output.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void 공유문서는음식점설정을적용하지않으면기본한장을유지하고_표제도HTML인코딩한다()
    {
        var service = new SsalddelDocumentOutputService(new ZxingSsalddelIdentifierCodeGenerator());
        var defaults = new SsalddelDocumentPrintLayoutOptions();
        Assert.Equal(1, defaults.ReceiptCopies);
        Assert.Empty(defaults.ReceiptCopyLabels);
        var single = service.CreateOutboundExpectedItems(Draft());
        Assert.Equal(1, Regex.Matches(single.Html, "<main class=\"receipt-slip\"", RegexOptions.CultureInvariant).Count);
        const string label = "<script>alert('r26')</script> & 포장";
        var encoded = service.CreateOutboundExpectedItems(Draft(new()
        {
            PaperSize = SsalddelDocumentPaperSizeCode.Thermal80,
            PrintStyle = SsalddelDocumentPrintStyleCode.ReceiptSlip,
            ReceiptCopyLabels = [label]
        }));

        Assert.Contains(WebUtility.HtmlEncode(label), encoded.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(label, encoded.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", encoded.Html, StringComparison.OrdinalIgnoreCase);
    }

    private static SsalddelExpectedItemDocumentDraft Draft(SsalddelDocumentPrintLayoutOptions? layout = null) => new()
    {
        DocumentNo = "ORDER-R26", DocumentKind = SsalddelExpectedItemDocumentKindCode.OutboundExpectedItems,
        Title = "주문 빌지 R26", Status = "조리대기", WarehouseName = "음식점 R26", OwnerName = "주방/포장",
        CounterpartyName = "담당자 R26", OrderNo = "ORDER-R26", LedgerNo = "LEDGER-R26",
        ExpectedDateText = "오늘 12:30", WorkMemo = "요청: 소스 별도", CreatedAt = new(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(9)),
        DocumentBarcodePayload = SsalddelIdentifierCodePayloads.Create(SsalddelIdentifierKindCode.Order, "ORDER-R26"),
        PrintLayout = layout ?? new()
        {
            PaperSize = SsalddelDocumentPaperSizeCode.Thermal80, Density = SsalddelDocumentDensityCode.Dense,
            PrintStyle = SsalddelDocumentPrintStyleCode.ReceiptSlip
        },
        Lines = [new()
        {
            LineNo = "01", Sku = "MENU-R26", ProductName = "수제버거 & 감자", Quantity = 3, Unit = "개",
            ProductBarcode = "SKU:FOOD-R26", BundleBarcode = "BND-R26", LocationCode = "KITCHEN",
            StorageCondition = "조리대기", RelatedOrderNo = "ORDER-R26", Note = "12,000원",
            BarcodePayload = SsalddelIdentifierCodePayloads.Create(SsalddelIdentifierKindCode.Product, "FOOD-R26", rawCode: "SKU:FOOD-R26")
        }]
    };

    private sealed class Store : I음식점전표출력설정Store
    {
        public int? Value { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Exception? ReadFailure { get; set; }
        public Exception? WriteFailure { get; set; }
        public int? Read()
        {
            Reads++;
            if (ReadFailure is { } failure) throw failure;
            return Value;
        }
        public void Write(int copies)
        {
            Writes++;
            if (WriteFailure is { } failure) throw failure;
            Value = copies;
        }
    }
}

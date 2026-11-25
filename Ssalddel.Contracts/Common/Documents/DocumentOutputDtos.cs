using Ssalddel.Contracts.Common.Identifiers;

namespace Ssalddel.Contracts.Common.Documents;

public sealed record SsalddelWaybillDocumentDraft(
    string DocumentNo,
    string CargoName,
    string TransportMode,
    string VehicleCondition,
    string PickupPlace,
    string PickupAddress,
    string PickupTime,
    string DropoffPlace,
    string DropoffAddress,
    string DropoffTime,
    string PaymentMethod,
    bool ReceiptRequired,
    decimal ExpectedFare,
    decimal ExpectedCost,
    string Memo,
    DateTimeOffset CreatedAt);

public static class SsalddelExpectedItemDocumentKindCode
{
    public const string InboundExpectedItems = "inbound-expected-items";
    public const string OutboundExpectedItems = "outbound-expected-items";
}

public static class SsalddelDocumentPaperSizeCode
{
    public const string A4 = "a4";
    public const string A5 = "a5";
    public const string A6 = "a6";
    public const string Thermal80 = "thermal-80";
    public const string Thermal58 = "thermal-58";
}

public static class SsalddelDocumentDensityCode
{
    public const string Comfortable = "comfortable";
    public const string Compact = "compact";
    public const string Dense = "dense";
}

public static class SsalddelDocumentOrientationCode
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";
}

public static class SsalddelDocumentPrintStyleCode
{
    public const string Sheet = "sheet";
    public const string ReceiptSlip = "receipt-slip";
}

public sealed class SsalddelDocumentPrintLayoutOptions
{
    public string PaperSize { get; init; } = SsalddelDocumentPaperSizeCode.A4;

    public string Density { get; init; } = SsalddelDocumentDensityCode.Compact;

    public string Orientation { get; init; } = SsalddelDocumentOrientationCode.Portrait;

    public string PrintStyle { get; init; } = SsalddelDocumentPrintStyleCode.Sheet;

    public bool IncludeDocumentQrCode { get; init; } = true;

    public bool IncludeLineBarcodes { get; init; } = true;

    // Logical slips in one print job; other document layouts keep the legacy single-copy default.
    public int ReceiptCopies { get; init; } = 1;

    public IReadOnlyList<string> ReceiptCopyLabels { get; init; } = [];
}

public sealed class SsalddelExpectedItemDocumentDraft
{
    public string DocumentNo { get; init; } = string.Empty;

    public string DocumentKind { get; init; } = SsalddelExpectedItemDocumentKindCode.InboundExpectedItems;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = "예정";

    public string WarehouseName { get; init; } = string.Empty;

    public string OwnerName { get; init; } = string.Empty;

    public string CounterpartyName { get; init; } = string.Empty;

    public string OrderNo { get; init; } = string.Empty;

    public string LedgerNo { get; init; } = string.Empty;

    public string ExpectedDateText { get; init; } = string.Empty;

    public string WorkMemo { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public SsalddelDocumentPrintLayoutOptions PrintLayout { get; init; } = new();

    public SsalddelIdentifierCodePayload? DocumentBarcodePayload { get; init; }

    public IReadOnlyList<SsalddelExpectedItemDocumentLine> Lines { get; init; } = [];
}

public sealed class SsalddelExpectedItemDocumentLine
{
    public string LineNo { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string Unit { get; init; } = "개";

    public string ProductBarcode { get; init; } = string.Empty;

    public string BundleBarcode { get; init; } = string.Empty;

    public string LocationCode { get; init; } = string.Empty;

    public string StorageCondition { get; init; } = string.Empty;

    public string RelatedOrderNo { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public SsalddelIdentifierCodePayload? BarcodePayload { get; init; }
}

public sealed record SsalddelDocumentOutput(
    string FileName,
    string Title,
    string ContentType,
    string Html,
    string PlainText);

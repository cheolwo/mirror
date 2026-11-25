using Ssalddel.Contracts.Common.Documents;

namespace RestaurantDeskApp.Services;

/// <summary>동일 최신 주문을 단일 인쇄 문서의 조리·포장 전표로 구성합니다. 원본은 변경하지 않습니다.</summary>
public static class 음식점전표출력구성
{
    public static SsalddelExpectedItemDocumentDraft Apply(SsalddelExpectedItemDocumentDraft draft, int copies)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (copies is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(copies), copies, "빌지 장수는 1장 또는 2장으로 선택해 주세요.");
        var layout = draft.PrintLayout ?? new();
        if (layout.PrintStyle != SsalddelDocumentPrintStyleCode.ReceiptSlip)
            throw new ArgumentException("빌지 형식의 주문 전표에만 장수 설정을 적용할 수 있습니다.", nameof(draft));
        return new()
        {
            DocumentNo = draft.DocumentNo,
            DocumentKind = draft.DocumentKind,
            Title = draft.Title,
            Status = draft.Status,
            WarehouseName = draft.WarehouseName,
            OwnerName = draft.OwnerName,
            CounterpartyName = draft.CounterpartyName,
            OrderNo = draft.OrderNo,
            LedgerNo = draft.LedgerNo,
            ExpectedDateText = draft.ExpectedDateText,
            WorkMemo = draft.WorkMemo,
            CreatedAt = draft.CreatedAt,
            DocumentBarcodePayload = draft.DocumentBarcodePayload,
            Lines = draft.Lines,
            PrintLayout = new()
            {
                PaperSize = layout.PaperSize,
                Density = layout.Density,
                Orientation = layout.Orientation,
                PrintStyle = layout.PrintStyle,
                IncludeDocumentQrCode = layout.IncludeDocumentQrCode,
                IncludeLineBarcodes = layout.IncludeLineBarcodes,
                ReceiptCopies = copies,
                ReceiptCopyLabels = Array.AsReadOnly(copies == 1 ? new[] { "조리·포장용" } : new[] { "조리용", "포장용" })
            }
        };
    }
}

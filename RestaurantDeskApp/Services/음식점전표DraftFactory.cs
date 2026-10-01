using Ssalddel.Contracts.Common.Documents;
using Ssalddel.Contracts.Common.Identifiers;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

public sealed class 음식점전표DraftFactory
{
    public SsalddelExpectedItemDocumentDraft Create주문전표Draft(음식주문응답 order)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(order.주문번호);

        var createdAt = ToLocalOffset(order.CreatedAt);
        var ledgerNo = $"FOOD-LEDGER-{order.음식점Id}-{createdAt:yyyyMMdd}";

        return new SsalddelExpectedItemDocumentDraft
        {
            DocumentNo = order.주문번호,
            DocumentKind = SsalddelExpectedItemDocumentKindCode.OutboundExpectedItems,
            Title = $"주문 전표 {order.주문번호}",
            Status = order.상태,
            WarehouseName = $"음식점 {order.음식점Id}",
            OwnerName = "주방/포장",
            CounterpartyName = $"{order.수령인정보.수령인명} / {order.수령인정보.연락처}",
            OrderNo = order.주문번호,
            LedgerNo = ledgerNo,
            ExpectedDateText = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
            WorkMemo = BuildMemo(order),
            CreatedAt = DateTimeOffset.Now,
            PrintLayout = new SsalddelDocumentPrintLayoutOptions
            {
                PaperSize = SsalddelDocumentPaperSizeCode.Thermal80,
                Density = SsalddelDocumentDensityCode.Dense,
                Orientation = SsalddelDocumentOrientationCode.Portrait,
                PrintStyle = SsalddelDocumentPrintStyleCode.ReceiptSlip,
                IncludeDocumentQrCode = false,
                IncludeLineBarcodes = true
            },
            DocumentBarcodePayload = SsalddelIdentifierCodePayloads.Create(
                SsalddelIdentifierKindCode.Order,
                order.주문번호),
            Lines = order.상품목록.Select((item, index) => CreateLine(order, item, index + 1)).ToArray()
        };
    }

    private static SsalddelExpectedItemDocumentLine CreateLine(음식주문응답 order, 음식주문상품Dto item, int lineNumber)
    {
        var productBarcode = $"MENU-{order.음식점Id}-{lineNumber:00}";
        var bundleBarcode = $"BND-{order.주문번호}-{lineNumber:00}";

        return new SsalddelExpectedItemDocumentLine
        {
            LineNo = lineNumber.ToString("00"),
            Sku = productBarcode,
            ProductName = item.상품명,
            Quantity = item.수량,
            Unit = "개",
            ProductBarcode = productBarcode,
            BundleBarcode = bundleBarcode,
            LocationCode = "KITCHEN",
            StorageCondition = order.픽업준비시각Utc.HasValue ? "픽업준비"
                : order.조리시작시각Utc.HasValue || order.상태 == 음식주문상태코드.조리중 ? "조리중" : "조리대기",
            RelatedOrderNo = order.주문번호,
            Note = $"{item.단가:N0}원",
            BarcodePayload = SsalddelIdentifierCodePayloads.Create(
                SsalddelIdentifierKindCode.Product,
                productBarcode,
                item.상품명)
        };
    }

    private static string BuildMemo(음식주문응답 order)
    {
        var address = string.IsNullOrWhiteSpace(order.수령인정보.상세주소)
            ? order.수령인정보.주소
            : $"{order.수령인정보.주소} {order.수령인정보.상세주소}";
        var request = string.IsNullOrWhiteSpace(order.수령인정보.요청사항)
            ? "요청사항 없음"
            : order.수령인정보.요청사항;

        var preparation = order.조리시작시각Utc.HasValue || order.상태 == 음식주문상태코드.조리중
            ? "조리 시작 기록 완료"
            : order.픽업준비시각Utc.HasValue ? "기존 준비 완료 음식" : "기사 배정 확정 후 앱에서 조리 시작을 눌러 주세요.";
        return $"조리: {preparation}\n결제: {order.결제수단 ?? "미지정"} / 금액: {order.총주문금액:N0}원\n주소: {address}\n요청: {request}";
    }

    private static DateTimeOffset ToLocalOffset(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return new DateTimeOffset(value).ToLocalTime();
        }

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local));
    }
}

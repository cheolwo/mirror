using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

internal static class 입고검수Policy
{
    internal static string 입력정규화(입고검수요청 request)
    {
        if (request.검수수량 <= 0)
        {
            throw new InvalidOperationException("검수수량은 1 이상이어야 합니다.");
        }

        if (request.불량수량 < 0 || request.불량수량 > request.검수수량)
        {
            throw new InvalidOperationException("불량수량은 검수수량보다 클 수 없습니다.");
        }

        if (request.검수수량 > 100_000)
        {
            throw new InvalidOperationException("검수수량은 100,000개 이하여야 합니다.");
        }

        var inspectionMemo = (request.검수메모 ?? string.Empty).Trim();
        if (inspectionMemo.Length > 400)
        {
            throw new InvalidOperationException("검수 메모는 400자 이하로 입력해 주세요.");
        }

        return inspectionMemo;
    }

    internal static bool 동일재시도여부(
        int receivedQuantity,
        int defectiveQuantity,
        bool hasInspectionHistory,
        string? existingHistoryMemo,
        int inspectedQuantity,
        int requestedDefectiveQuantity,
        string requestedHistoryMemo)
        => receivedQuantity == inspectedQuantity
            && defectiveQuantity == requestedDefectiveQuantity
            && hasInspectionHistory
            && string.Equals(existingHistoryMemo, requestedHistoryMemo, StringComparison.Ordinal);

    internal static 입고검수Plan 신규검수계획(
        string inventoryStatus,
        int reservedQuantity,
        int inspectedQuantity,
        int defectiveQuantity)
    {
        if (!string.Equals(inventoryStatus, "보관중", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("보관중 상태의 입고상품만 검수할 수 있습니다.");
        }

        var goodQuantity = inspectedQuantity - defectiveQuantity;
        if (goodQuantity < reservedQuantity)
        {
            throw new InvalidOperationException(
                "검수 후 정상 수량은 이미 예약된 수량보다 적을 수 없습니다.");
        }

        return new 입고검수Plan(
            inspectedQuantity,
            defectiveQuantity,
            goodQuantity - reservedQuantity,
            defectiveQuantity > 0 ? "검수완료-불량포함" : "검수완료");
    }
}

internal readonly record struct 입고검수Plan(
    int 입고수량,
    int 불량수량,
    int 가용수량,
    string 상태);

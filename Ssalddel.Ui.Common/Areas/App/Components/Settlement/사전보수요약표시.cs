using System.Globalization;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Ui.Common.Areas.App.Components.Settlement;

/// <summary>기존 약정의 본인 수령 항목만 표시한다. 합산·발생·공제·지급을 계산하지 않는다.</summary>
public static class 사전보수요약표시
{
    public static IReadOnlyList<NeighborhoodSettlementTermLine> 본인수령항목(
        NeighborhoodSettlementAgreementDto agreement, string? currentUserId, bool includedInDelivery = false)
        => string.IsNullOrWhiteSpace(currentUserId) ? [] : agreement.Terms.Lines
            .Where(line => 기본보수인가(line.Kind)
                && string.Equals(line.PayeeUserId, currentUserId, StringComparison.Ordinal)
                && !string.Equals(line.PayerUserId, currentUserId, StringComparison.Ordinal)
                && line.IncludedInDelivery == includedInDelivery)
            .ToArray();

    public static bool 기본보수인가(string kind)
        => 활동정산표시.IsBasic(kind) || kind == NeighborhoodSettlementCodes.Packing;

    public static string 항목명(NeighborhoodSettlementTermLine line)
        => line.Kind == NeighborhoodSettlementCodes.Packing ? "포장 기본보수" : 활동정산표시.Kind(line.Kind);

    public static string 금액(NeighborhoodSettlementTermLine line) => line.Billing switch
    {
        NeighborhoodSettlementCodes.Fixed or NeighborhoodSettlementCodes.StoragePeriod => 활동정산표시.Money(line.AmountKrw),
        NeighborhoodSettlementCodes.PerUnit => $"{활동정산표시.Money(line.UnitPriceKrw)} × {수량(line.Quantity)}",
        NeighborhoodSettlementCodes.Free => line.AmountKrw == 0 ? "0원 · 무료 합의" : "무료 조건 확인 필요",
        _ => "산정 방식 확인 필요"
    };

    public static string 수량(decimal? quantity)
        => quantity?.ToString("0.############################", CultureInfo.GetCultureInfo("ko-KR")) ?? "수량 미확정";
}

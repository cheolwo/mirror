using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Ui.Common.Areas.App.Components.Settlement;

/// <summary>이미 허용된 약정의 관계만 설명합니다. 법적 책임·수락·지급 권한을 판정하지 않습니다.</summary>
public static class 약정책임확인표시
{
    public sealed record 동의기록요약(int 필요인원, int 동의인원, bool 당사자확인필요, bool 동일판본기록충족);

    public static bool 상품업무인가(NeighborhoodSettlementAgreementDto agreement)
        => agreement.Terms.ActivityKind == NeighborhoodSettlementCodes.StoredGoods
            || agreement.SourceKind is NeighborhoodSettlementCodes.MartOrder
                or NeighborhoodSettlementCodes.DistributedOrder or NeighborhoodSettlementCodes.DistributedLot;

    public static string 담당연결(string? partyId, string? currentUserId)
        => string.IsNullOrWhiteSpace(partyId) ? "연결 정보 확인 필요"
            : !string.IsNullOrWhiteSpace(currentUserId) && string.Equals(partyId, currentUserId, StringComparison.Ordinal)
                ? "본인으로 연결됨" : "담당 연결 기록 있음";

    public static string 수금방식(string? route) => route switch
    {
        NeighborhoodSettlementCodes.OwnerDirect => "상품 소유자 직접 수금 약정",
        NeighborhoodSettlementCodes.SellerCollect => "판매 담당자 수금·소유자 전달 약정",
        _ => "상품대금 수금 방식 확인 필요"
    };

    public static 동의기록요약 현재판본동의(NeighborhoodSettlementAgreementDto agreement)
    {
        var declared = agreement.RequiredConsentUserIds ?? [];
        var required = declared.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        var visibleParties = agreement.Terms.Lines.SelectMany(line => new[] { line.PayerUserId, line.PayeeUserId }
            .Concat(line.DeliveryCostShares.Select(share => share.PayerUserId))).ToArray();
        var unresolved = agreement.TermsVersion <= 0 || required.Length == 0
            || declared.Any(string.IsNullOrWhiteSpace) || visibleParties.Any(string.IsNullOrWhiteSpace)
            || visibleParties.Any(id => !required.Contains(id, StringComparer.Ordinal));
        var count = 0;
        if (agreement.TermsVersion > 0)
        {
            foreach (var id in required)
            {
                var records = (agreement.Consents ?? []).Where(consent => consent.TermsVersion == agreement.TermsVersion
                    && string.Equals(consent.UserId, id, StringComparison.Ordinal)).ToArray();
                if (records.Length == 0) continue;
                var latestAt = records.Max(consent => consent.RecordedAtUtc);
                // 같은 시각의 상반된 기록도 동의 완료로 임의 선택하지 않습니다.
                if (records.Where(consent => consent.RecordedAtUtc == latestAt).All(consent => consent.Granted)) count++;
            }
        }
        return new(required.Length, count, unresolved, !unresolved && count == required.Length);
    }

    public static IReadOnlyList<NeighborhoodSettlementTermLine> 본인항목(
        NeighborhoodSettlementAgreementDto agreement, string? currentUserId)
        => string.IsNullOrWhiteSpace(currentUserId) ? []
            : agreement.Terms.Lines.Where(line => string.Equals(line.PayerUserId, currentUserId, StringComparison.Ordinal)
                || string.Equals(line.PayeeUserId, currentUserId, StringComparison.Ordinal)).ToArray();

    public static string 지급관계(NeighborhoodSettlementTermLine line, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(line.PayerUserId) || string.IsNullOrWhiteSpace(line.PayeeUserId))
            return "지급·수령 담당 확인 필요";
        if (string.IsNullOrWhiteSpace(currentUserId)) return "본인 관계 확인 필요";
        var payer = string.Equals(line.PayerUserId, currentUserId, StringComparison.Ordinal);
        var payee = string.Equals(line.PayeeUserId, currentUserId, StringComparison.Ordinal);
        return payer && payee ? "본인 내부 항목 · 외부 지급과 구분"
            : payer ? "내가 지급할 약정" : payee ? "내가 받을 약정" : "본인 지급·수령 항목 아님";
    }

    public static string? 미확정조건(NeighborhoodSettlementTermLine line) => line.Billing switch
    {
        NeighborhoodSettlementCodes.PerUnit when line.UnitPriceKrw is null && line.Quantity is null => "단가·수량 미확정",
        NeighborhoodSettlementCodes.PerUnit when line.UnitPriceKrw is null => "단가 미확정",
        NeighborhoodSettlementCodes.PerUnit when line.Quantity is null => "수량 미확정",
        NeighborhoodSettlementCodes.PerUnit => null,
        NeighborhoodSettlementCodes.Fixed or NeighborhoodSettlementCodes.StoragePeriod => line.AmountKrw is null ? "금액 미확정" : null,
        NeighborhoodSettlementCodes.Free => line.AmountKrw == 0 ? null : "무료 조건 확인 필요",
        _ => "산정 방식 확인 필요"
    };
}

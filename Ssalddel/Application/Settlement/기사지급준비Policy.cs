using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Settlement;

/// <summary>기사·운영자·주간 정산이 공유하는 지급 준비 판정입니다. 승인·송금이나 저장은 수행하지 않습니다.</summary>
internal static class 기사지급준비Policy
{
    internal static 기사지급준비판정 판정(
        화주운송의뢰? request,
        decimal? expectedPayoutAmount,
        bool hasSettlementAccount,
        bool settlementAccountVerified,
        bool hasOpenAbnormalTransportIncident = false)
    {
        if (request is null)
        {
            return new(
                기사지급준비상태코드.원천의뢰없음,
                "운송 원천 의뢰를 확인할 수 없어 지급 준비를 진행할 수 없습니다.",
                false);
        }

        if (hasOpenAbnormalTransportIncident)
        {
            return new(
                기사지급준비상태코드.비정상운송검토보류,
                "비정상 운송 사건의 운영 검토가 끝날 때까지 기사 지급 준비를 보류합니다.",
                false);
        }
        if (request.모의처리처수행)
            return new("SimulationFulfillmentExcluded", "모의 처리처 수행 기록은 실제 기사 지급·정산 대상에서 제외합니다.", false);

        if (expectedPayoutAmount is not > 0)
        {
            return new(
                기사지급준비상태코드.지급예정운임없음,
                "기사 지급 예정 운임이 기록되지 않아 금액 검토가 필요합니다.",
                false);
        }

        if (string.Equals(request.정산시점, 정산시점.현장지급.ToString(), StringComparison.Ordinal))
        {
            return request.현장수금확인일시.HasValue
                ? new(
                    기사지급준비상태코드.현장수금확인,
                    "기사의 현장 수금 확인이 기록되었습니다. 플랫폼 지급 대상이 아닙니다.",
                    false)
                : new(
                    기사지급준비상태코드.현장수금대기,
                    "현장 수금 확인이 아직 기록되지 않았습니다.",
                    false);
        }

        var shipperCollectionConfirmed =
            string.Equals(request.결제상태, 상태값.결제상태.결제완료, StringComparison.Ordinal)
            || string.Equals(request.정산상태, 운임정산상태.입금확인완료.ToString(), StringComparison.Ordinal)
            || string.Equals(request.정산상태, 운임정산상태.정산완료.ToString(), StringComparison.Ordinal);
        if (!shipperCollectionConfirmed)
        {
            return new(
                기사지급준비상태코드.화주수납대기,
                "화주의 결제 또는 입금 확인이 완료되지 않았습니다.",
                false);
        }

        if (!hasSettlementAccount)
        {
            return new(
                기사지급준비상태코드.정산계좌없음,
                "기사 정산계좌를 등록해야 지급 준비를 진행할 수 있습니다.",
                false);
        }

        if (!settlementAccountVerified)
        {
            return new(
                기사지급준비상태코드.정산계좌미확인,
                "기사 정산계좌의 본인 확인이 완료되지 않았습니다.",
                false);
        }

        return new(
            기사지급준비상태코드.지급준비가능,
            "화주 수납과 정산계좌 확인이 완료되어 지급 준비가 가능합니다.",
            true);
    }

}

internal sealed record 기사지급준비판정(string Code, string Message, bool IsReady);

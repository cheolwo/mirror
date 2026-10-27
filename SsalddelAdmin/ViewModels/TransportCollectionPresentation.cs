using SsalddelAdmin.Services;

namespace SsalddelAdmin.ViewModels;

/// <summary>화주 수납 근거만 표현합니다. 기사 지급 상태를 판정하지 않습니다.</summary>
public sealed record TransportCollectionPresentation(bool Confirmed = false)
{
    public string Status => Confirmed ? "수납 확인" : "수납 미확인";

    public static TransportCollectionPresentation Create(화주운송의뢰응답? request, 결제목록응답? payment)
    {
        var state = payment?.결제상태 ?? request?.결제상태;
        if (state is not null && new[] { "취소", "환불", "실패", "미수" }
            .Any(value => state.Contains(value, StringComparison.Ordinal))) return new();
        return new(state is "결제완료" or "입금확인완료" or "입금확인" or "승인완료"
            || request?.정산상태 is "입금확인완료" or "정산완료");
    }
}

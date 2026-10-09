using Ssalddel.Contracts.Common.Fulfillment;
namespace Ssalddel.Ui.Common.Areas.App.ViewModels;
public static class AutoFulfillmentPresentation
{
    public static string State(string? state) => state switch
    {
        AutoFulfillmentStates.AwaitingSource => "음식점 주문 확인 대기",
        AutoFulfillmentStates.AwaitingRouting => "처리처 선정 대기",
        AutoFulfillmentStates.NeedsReview => "조건 재확인 필요",
        AutoFulfillmentStates.Submitting => "접수 중",
        AutoFulfillmentStates.ResultUnknown => "접수 결과 확인 필요",
        AutoFulfillmentStates.Accepted => "처리처 접수됨 · 기사 배정 확인 대기",
        AutoFulfillmentStates.DriverAssigned => "기사 배정 확인됨",
        AutoFulfillmentStates.InTransit => "배송 중",
        AutoFulfillmentStates.Completed => "배송 완료",
        AutoFulfillmentStates.CancelPending => "취소 결과 확인 필요",
        AutoFulfillmentStates.Cancelled => "현재 수행 취소됨",
        AutoFulfillmentStates.Failed => "현재 수행 실패 확인됨",
        _ => "진행 상태 확인 필요"
    };
    public static bool CanRenew(AutoFulfillmentSnapshot? value) => value is { CanRenewConsent: true, ConsentRenewalLockedReasonCode: null, SourceClosed: false, ExecutionPaused: false }
        && value.StateCode is not (AutoFulfillmentStates.Submitting or AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending or AutoFulfillmentStates.Completed);
    public static string? ConsentRenewalLockedReason(string? code) => code switch
    {
        null => null,
        "CookingAlreadyStarted" => "조리가 시작되어 자동 접수 조건과 처리처를 바꿀 수 없습니다. 필요한 취소·변경은 협의를 요청해 주세요.",
        "FulfillmentSourceClosed" => "종료된 주문은 새 자동 접수 조건을 변경할 수 없습니다.",
        "FulfillmentCompleted" => "현재 수행이 완료되어 새 자동 접수 조건을 변경할 수 없습니다.",
        "FulfillmentActive" => "진행 중인 수행의 결과를 먼저 확인해 주세요. 동의 철회는 새 접수를 멈추며 현재 주문이나 수행을 취소하지 않습니다.",
        "FulfillmentRoutingDisabled" => "자동 배송 처리가 중지되어 조건을 변경할 수 없습니다.",
        _ => "현재 수행 상태가 확인되지 않아 자동 접수 조건을 변경할 수 없습니다. 진행 상태를 다시 확인해 주세요."
    };
    public static string Money(decimal? value) => value is >= 0 ? $"{value:N0}원" : "확인 필요";
    public static string Time(DateTime? value) => value is { } time && time != default ? time.ToLocalTime().ToString("yyyy.MM.dd HH:mm") : "확인 필요";
}

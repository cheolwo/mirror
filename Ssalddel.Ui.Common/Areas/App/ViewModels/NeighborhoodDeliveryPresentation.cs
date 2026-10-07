using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public static class NeighborhoodDeliveryPresentation
{
    public static string Stage(NeighborhoodDeliveryResponse value) => value.DispatchStatusCode == "인수완료" ? "전달 완료" : value.ProposalStateCode switch
    {
        NeighborhoodDeliveryProposalStates.RegistrationPending => "배차 접수 확인 필요",
        NeighborhoodDeliveryProposalStates.Queued => "기사 제안 대기",
        NeighborhoodDeliveryProposalStates.AwaitingActivation => "접수 완료 · 배차 준비 중",
        NeighborhoodDeliveryProposalStates.Proposed => "기사에게 제안 중",
        NeighborhoodDeliveryProposalStates.Assigned => "기사 배차 확정",
        NeighborhoodDeliveryProposalStates.Closed => "배송 의뢰 종료",
        _ => "진행 상태 확인 필요"
    };
    public static string? DisclosureField(string field) => field switch
    {
        "PickupAddress" => "픽업 주소", "DropoffAddress" => "전달 주소", "LocationCoordinate" => "픽업·전달 위치",
        "ContactName" => "담당자 이름", "PhoneNumber" => "담당자 전화번호", "DeliveryInstructions" => "배송 요청 사항", _ => null
    };
    public static string Money(decimal? amount) => amount is { } value ? $"{value:N0}원" : "금액 확인 필요";
    public static string Distance(decimal? amount) => amount is { } value ? $"{value:0.###}km" : "거리 확인 필요";
    public static string Collection(NeighborhoodDeliveryResponse value)
        => value.SettlementStatusCode switch
        {
            "현장수금완료" or "정산완료" or "입금확인완료" => "수금 확인 기록 있음",
            "현장수금예정" => "기사에게 직접 현장 지급 예정",
            "정산취소" => "지급 조건 취소",
            _ => "지급 상태 확인 필요"
        };
    public static string? 입금확인안내(NeighborhoodDeliveryResponse value)
        => value.SettlementStatusCode is "현장수금완료" or "정산완료" or "입금확인완료" or "결제완료" or "정산취소"
            || value.PaymentStatusCode is "결제완료" or "입금확인완료"
            ? null : "플랫폼 입금 미확인";

    public static string 다음행동안내(NeighborhoodDeliveryResponse value, bool 원배차선택확인필요 = false)
    {
        if (원배차선택확인필요) return "이전에 선택한 배차 방식의 처리 결과를 먼저 확인해 주세요.";
        if (value.DispatchStatusCode == "인수완료") return "물건 전달이 완료되었습니다. 지급 여부는 지급 기록에서 따로 확인하세요.";
        if (value.ProposalStateCode == NeighborhoodDeliveryProposalStates.Closed || value.DispatchStatusCode is "취소됨" or "취소")
            return "종료된 배송 기록입니다. 확인이 필요한 일이 있으면 아래 문제 접수를 이용하세요.";
        if (value.ProposalStateCode == NeighborhoodDeliveryProposalStates.RegistrationPending)
            return "의뢰는 저장되었습니다. 진행을 새로고침해 배차 접수 결과를 확인해 주세요.";
        if (value.DriverDisclosure is { CanRecordConsent: true, Consented: false } disclosure
            && !string.IsNullOrWhiteSpace(disclosure.ConfirmedDriverId))
            return "선정 기사와 제공할 정보를 확인하고 아래 정보 제공 동의를 진행해 주세요.";
        return value.ProposalStateCode switch
        {
            NeighborhoodDeliveryProposalStates.Proposed => "기사에게 추천한 상태입니다. 기사 수락 전이며, 진행을 새로고침해 결과를 확인할 수 있습니다.",
            NeighborhoodDeliveryProposalStates.Queued => "아직 기사가 확정되지 않았습니다. 기다린 뒤 진행을 새로고침해 주세요.",
            NeighborhoodDeliveryProposalStates.AwaitingActivation => "배차 준비 중입니다. 진행을 새로고침해 현재 상태를 확인해 주세요.",
            NeighborhoodDeliveryProposalStates.Assigned => "담당 기사의 배송 진행을 확인하세요. 진행을 새로고침하면 최신 상태를 볼 수 있습니다.",
            _ => "진행을 새로고침해 현재 배송 상태를 확인해 주세요."
        };
    }

    public static string 배차선택안내(string? mode) => mode switch
    {
        NeighborhoodDispatchModes.Automatic => "조건에 맞는 기사에게 배송을 추천합니다. 기사 수락 전에는 배정되지 않습니다.",
        NeighborhoodDispatchModes.PublicCall => "공개 콜을 확인한 기사가 직접 수락합니다. 접수와 기사 수락은 별도입니다.",
        NeighborhoodDispatchModes.Hybrid => "자동 추천과 공개 콜을 함께 이용합니다. 조건을 확인하고 수락한 기사 한 명에게 배정합니다.",
        _ => "기사의 수락 후 배정됩니다. 기사를 찾는 방식을 확인해 주세요."
    };

    public static string? 자동추천준비안내(string? mode, bool automaticDispatchEnabled)
        => automaticDispatchEnabled || mode == NeighborhoodDispatchModes.PublicCall
            || mode is not null && !NeighborhoodDispatchModes.IsKnown(mode) ? null
            : mode == NeighborhoodDispatchModes.Hybrid
                ? "자동 추천은 현재 준비 중입니다. 공개 콜의 접수·수락 상태는 별도로 확인해 주세요."
                : "자동 추천은 현재 준비 중입니다. 접수와 기사 수락 상태를 확인해 주세요.";
    public static string Progress(string? status) => status switch
    {
        "미시작" or "매칭중" or "생성됨" or "대기" or "운송대기" or "계획대기" => "배송 시작 전",
        "배차됨" or "배차완료" or "배차확정" => "기사 배차 확정",
        "상차중" or "상차지이동중" or "픽업지이동중" => "픽업지로 이동 중",
        "상차완료" or "픽업완료" => "물건 픽업 완료",
        "운송중" or "배송중" or "하차지이동중" => "전달지로 이동 중",
        "인수완료" or "하차완료" or "운송완료" or "배송완료" or "완료" => "전달 완료",
        "취소됨" or "취소" => "의뢰 취소",
        _ => "배송 진행 확인 대기"
    };
    public static string Error(Exception exception, bool writing = false)
        => exception switch
        {
            SsalddelApiException { StatusCode: 401 } => "로그인이 만료됐습니다. 다시 로그인해 주세요.",
            SsalddelApiException { StatusCode: 403 } => "현재 계정이나 서비스 설정에서는 이 배송 기능을 이용할 수 없습니다.",
            SsalddelApiException { StatusCode: 404 } => "배송 의뢰를 찾을 수 없거나 아직 이 기능이 준비되지 않았습니다.",
            SsalddelApiException { ErrorCode: "QuoteChanged" } => "배송 요금이 바뀌었습니다. 새 견적을 확인한 뒤 다시 요청해 주세요.",
            SsalddelApiException { ErrorCode: "GeocodingFailed" or "AddressNotResolved" } => "주소 위치를 확인하지 못했습니다. 도로명 주소를 확인해 주세요.",
            SsalddelApiException { StatusCode: 409 } => "현재 의뢰 상태나 이전 입력과 맞지 않습니다. 내 배송 의뢰를 먼저 확인해 주세요.",
            SsalddelApiException { StatusCode: 400 or 422 } => "입력 내용이나 동의 상태를 확인하지 못했습니다. 주소·연락처·시간과 견적을 다시 확인해 주세요.",
            _ when writing => "요청 결과를 확인하지 못했습니다. 자동으로 다시 제출하지 않았습니다. 내 배송 의뢰를 먼저 확인해 주세요.",
            _ => "배송 정보를 불러오지 못했습니다. 잠시 후 다시 확인해 주세요."
        };
}

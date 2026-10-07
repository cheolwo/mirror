using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public static class NeighborhoodCollaborationPresentation
{
    public static string AgreedCost(decimal? value) => value is { } amount ? $"{amount:N0}원" : "미정";
    public static bool HasDriverDelivery(NeighborhoodCollaborationTerms? value) => value?.TransferMethod == NeighborhoodTransferMethods.DriverDelivery;
    public static string NextStep(NeighborhoodCollaborationResponse value)
    {
        if (value.DeliveryRegistrationPending) return "배송 접수 결과를 먼저 확인해 주세요.";
        if (value.StorageReservationPending) return "보관 예약 결과를 먼저 확인해 주세요.";
        if (value.StatusCode == NeighborhoodCollaborationStates.Completed) return "약속이 완료되었습니다. 지급은 별도로 확인해 주세요.";
        if (NeighborhoodCollaborationStates.IsTerminal(value.StatusCode)) return "현재 약속은 종료되었습니다.";
        if (value.LinkedDeliveryRequestId is not null) return "배송 진행을 확인해 주세요.";
        if (value.Terms?.StorageSpaceId is not null) return "보관공간에서 인계·반환 상태를 확인해 주세요.";
        if (value.CanRequestDelivery || value.AllowedActions.Contains(NeighborhoodCollaborationActions.LinkDelivery)) return "배송 조건을 확인하고 요청해 주세요.";
        return value.StatusCode switch
        {
            NeighborhoodCollaborationStates.Requested => "양측의 조건 확인과 동의를 기다리고 있습니다.",
            NeighborhoodCollaborationStates.CompletionProposed => value.CompletionProposedByMe ? "상대방의 완료 확인을 기다리고 있습니다." : "당사자의 완료 확인을 기다리고 있습니다.",
            _ => "현재 상태와 아래 약속 내용을 확인해 주세요."
        };
    }
    public static string Kind(string? code) => code switch { "goods" => "물품 교류", "food" => "음식 교류", "transport" => "배송 도움", "storage" => "물품 보관", _ => "협업" };
    public static string Stage(string? code) => code switch
    {
        "requested" => "조건 확인 중", "agreed" => "양쪽 동의 완료", "in-progress" => "진행 중", "completion-proposed" => "완료 확인 대기",
        "completed" => "완료", "cancelled" => "취소", "rejected" => "거절", "expired" => "기간 종료", _ => "상태 확인 필요"
    };
    public static string Action(string code) => code switch
    {
        "withdraw-handover-consent" => "인계 정보 동의 철회",
        "handover-info-consent" => "인계 정보 확인에 동의",
        "agree" => "이 조건에 동의하기", "start" => "협업 시작하기", "propose-completion" => "완료 확인 요청", "confirm-completion" => "완료 확인하기",
        "cancel" => "협업 취소", "reject" => "신청 거절", "accept-participation" => "참여 수락", "reject-participation" => "참여 거절",
        "withdraw-participation" => "참여 철회", "request-participation" => "참여 신청", _ => "확인"
    };
    public static string? Primary(NeighborhoodCollaborationResponse? value)
        => value is null ? null : new[] { NeighborhoodCollaborationActions.HandoverInfoConsent, NeighborhoodCollaborationActions.Agree, NeighborhoodCollaborationActions.Start,
            NeighborhoodCollaborationActions.ConfirmCompletion, NeighborhoodCollaborationActions.ProposeCompletion,
            NeighborhoodCollaborationActions.RequestParticipation }.FirstOrDefault(value.AllowedActions.Contains);
    public static string SpaceStage(string? code) => code switch { "published" => "제공 중", "paused" => "제공 일시중단", "closed" => "제공 종료", "draft" => "공개 전", _ => "상태 확인 필요" };
    public static string ReservationStage(string? code) => code switch { "reserved" => "인계 확인 대기", "in-custody" => "보관 중", "returned" => "반환 확인 완료", "cancelled" => "예약 취소", _ => "상태 확인 필요" };
    public static string HandoverAction(string code) => code switch { "confirm-intake" => "물품 인계 확인", "confirm-return" => "물품 반환 확인", "cancel" => "예약 취소", _ => "확인" };
}

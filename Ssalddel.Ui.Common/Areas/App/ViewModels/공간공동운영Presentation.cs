using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public static class 공간공동운영Presentation
{
    public static string Role(string? code) => code switch
    {
        공간공동운영역할Codes.SpaceProvider => "공간 제공",
        공간공동운영역할Codes.Supplier => "상품 공급",
        공간공동운영역할Codes.Driver => "배송",
        공간공동운영역할Codes.Resident => "이용 주민",
        공간공동운영역할Codes.Picker => "피킹",
        공간공동운영역할Codes.Coordinator => "운영 조율",
        공간공동운영역할Codes.InboundInspector => "입고 검수",
        _ => "역할 확인 필요"
    };
    public static string Stage(string? code) => code switch
    {
        "gathering" => "함께할 사람 모집 중", "ready" => "운영 준비", "running" => "운영 중",
        "prepared" => "회차 준비", "active" => "회차 진행 중", "completed" => "회차 마침",
        "paused" => "잠시 멈춤", "closed" => "모집 종료", _ => "진행 확인"
    };
    public static string Participation(string? code) => code switch
    { "accepted" => "역할 수락", "interested" => "관심 참여", "withdrawn" => "참여 철회", _ => "참여 확인" };
    public static string Action(string? code) => code switch
    {
        공간공동운영명령Codes.AcceptOperator => "운영 담당 맡기",
        공간공동운영명령Codes.ReleaseOperator => "운영 담당 인계 준비",
        공간공동운영명령Codes.ProposeOperatorHandover => "운영 담당 인계 제안",
        공간공동운영명령Codes.AcceptOperatorHandover => "운영 담당 인계 수락",
        공간공동운영명령Codes.AgreeTerms => "현재 운영 조건에 동의",
        공간공동운영명령Codes.CreateCycle => "다음 운영 회차 준비",
        공간공동운영명령Codes.StartCycle => "운영 회차 시작",
        공간공동운영명령Codes.CompleteCycle => "결과 확인 후 회차 마치기",
        공간공동운영명령Codes.Pause => "새 업무 잠시 멈추기",
        공간공동운영명령Codes.Resume => "새 업무 다시 준비하기",
        공간공동운영명령Codes.Close => "모집 종료하기",
        _ => "확인하기"
    };
    public static string DetailRoute(string ledgerId) => 공간공동운영Routes.Home + "/" + Uri.EscapeDataString(ledgerId);
    public static string? SafeWorkHref(string? href)
        => href is { Length: > 0 and <= 1200 } && href.StartsWith('/') && !href.StartsWith("//")
           && !href.Contains('\\') && !href.Any(char.IsControl) && !href.Contains("://", StringComparison.Ordinal)
            ? href : null;
}

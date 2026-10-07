using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>서버의 사건·보류 사실을 의뢰자에게 표시하며 운송/지급 상태를 변경하지 않습니다.</summary>
public static class CargoIncidentPresentation
{
    public static RoleWorkspaceField? Notice(화주운송의뢰응답 request)
    {
        var incidents = request.비정상운송사건목록.Where(item => item.상태Code != "Closed"
            && item.업무통제상태Code is not ("Resumed" or "Closed")).ToArray();
        var full = incidents.Any(item => item.보류범위Code == "EntireTransport" || item.업무통제상태Code == "FullyHeld");
        var partial = incidents.Any(item => item.보류범위Code == "AffectedQuantity" || item.업무통제상태Code == "PartiallyHeld");
        var settlement = incidents.Any(item => item.정산보류적용여부);
        if (!request.비정상운송검토보류중 && !full && !partial && !settlement) return null;
        var scope = full ? "전체 운송이 보류되어 있습니다." : partial ? "영향 수량만 보류 중입니다. 전체 운송 중단과는 다릅니다."
            : "보류 범위는 운송 내역에서 확인해 주세요.";
        return new("운영 검토", "운영 검토 중 · " + scope + (settlement ? " 정산도 보류 중입니다." : "")
            + " 운송 내역에서 진행 상황을 확인해 주세요.");
    }

    public static string Type(string code) => code switch
    { "QuantityMismatch" => "수량 불일치", "CargoDamage" => "화물 훼손", "DropoffRecipientUnavailable" => "하차지 부재", _ => "운송 예외" };
    public static string State(string code) => code switch
    { "OperationsReviewPending" => "운영 검토 대기", "ActionDecided" => "조치 결정", "Closed" => "검토 종료", _ => "상태 확인 필요" };
    public static string Actor(string code) => code == "PlatformOperationsReview" ? "운영자" : "담당 확인 필요";
    public static string Scope(string code) => code switch
    { "EntireTransport" => "전체 운송", "AffectedQuantity" => "영향 수량", "None" => "운송 보류 없음", _ => "범위 확인 필요" };
    public static string Control(string code) => code switch
    { "FullyHeld" => "전체 운송 보류", "PartiallyHeld" => "영향 수량 보류", "ContinueWithCaution" => "주의하며 진행", "Resumed" => "운송 재개", "Closed" => "검토 종료", _ => "진행 조건 확인 필요" };
}

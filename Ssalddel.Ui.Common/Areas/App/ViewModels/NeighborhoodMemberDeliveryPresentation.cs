using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public static class NeighborhoodMemberDeliveryPresentation
{
    public static bool ValidLocation(RoleWorkspaceLocation? point) => point is not null
        && double.IsFinite(point.Latitude) && double.IsFinite(point.Longitude)
        && point.Latitude is >= -90 and <= 90 && point.Longitude is >= -180 and <= 180
        && point.AccuracyMeters is >= 0 and <= 100
        && DateTimeOffset.UtcNow - point.MeasuredAt <= TimeSpan.FromMinutes(5)
        && point.MeasuredAt - DateTimeOffset.UtcNow <= TimeSpan.FromSeconds(30);
    public static string State(string? value) => value switch
    {
        "Pending" => "수락 대기", "Accepted" or "Assigned" => "픽업 이동 중", "PickedUp" => "전달 이동 중",
        "Delivered" => "전달 완료 · 수령 확인 대기", "Issue" => "문제 접수 · 해결 대기", "Declined" => "거절한 제안",
        "Expired" => "기한 만료", "Cancelled" => "취소", "Completed" => "수령 확인 완료", _ => "현재 상태 확인 필요"
    };
    public static string Block(string? code) => code switch
    {
        "" or null => "", "Disabled" or "MemberDeliveryDisabled" => "배송 참여 서비스가 아직 열리지 않았습니다.",
        _ => "현재 배송 참여를 이용할 수 없습니다. 운영 안내를 확인해 주세요."
    };
}
